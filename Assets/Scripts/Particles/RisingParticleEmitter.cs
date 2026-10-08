using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Emits short-lived UI particles that travel from a start point up to a destination
// (e.g. an empty placed over the dopamine bar), along an eased, arcing path, then
// fade out. Works on a Screen Space canvas like HeartParticleSpawner - a world
// ParticleSystem can't chase a UI element cleanly.
//
// One emitter can be shared by several callers (the combo and the scroll overlay),
// each passing its own destination. Put this on a full-screen stretched UI object
// (the Canvas, or a stretched child of it) so particle coordinates map 1:1 to the
// screen, then reference it from the scripts that should fire particles.
[RequireComponent(typeof(RectTransform))]
public class RisingParticleEmitter : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Default target the particles rise to (make an empty at the dopamine bar). Callers can override it. If none is set anywhere, particles just drift straight up.")]
    [SerializeField] private Transform destination;

    [Tooltip("How far straight up (canvas units) particles drift when no destination is set at all.")]
    [SerializeField] private float noDestinationRise = 300f;

    [Header("Look")]
    [Tooltip("Sprite used for each particle.")]
    [SerializeField] private Sprite particleSprite;
    [Tooltip("Fallback particle colour, used when no per-life colour applies.")]
    [SerializeField] private Color color = Color.white;

    [Tooltip("Particle colour per life count: element [i] is used when the player has i lives (1 = despair, 2 = normal, 3 = overdrive). Leave the array empty to always use the colour above.")]
    [SerializeField] private Color[] lifeColors;
    [Tooltip("Base size of a particle, in canvas units.")]
    [SerializeField] private Vector2 startSize = new Vector2(48f, 48f);
    [Tooltip("Each particle's size is multiplied by a random value in this range (x = min, y = max).")]
    [SerializeField] private Vector2 sizeMultiplierRange = new Vector2(0.8f, 1.2f);

    [Header("Emission")]
    [Tooltip("How many particles per emit call.")]
    [Min(1)]
    [SerializeField] private int countPerEmit = 6;
    [Tooltip("Random radius (canvas units) around the start point that particles spawn within.")]
    [SerializeField] private float spawnScatter = 40f;
    [Tooltip("Delay between particles in one burst (seconds), so they stream out instead of moving as one blob.")]
    [Min(0f)]
    [SerializeField] private float emitStagger = 0.04f;

    [Header("Travel")]
    [Tooltip("Seconds a particle takes to reach the destination.")]
    [Min(0.01f)]
    [SerializeField] private float travelDuration = 0.7f;
    [Tooltip("Random +/- variation on travel duration so particles don't arrive in lockstep.")]
    [Min(0f)]
    [SerializeField] private float travelDurationJitter = 0.15f;
    [Tooltip("Eased progress along the path (0..1 over the travel). Ease-in-out reads as a nice accelerate-then-settle.")]
    [SerializeField] private AnimationCurve travelEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Sideways arc height (canvas units) at the midpoint, so the path bows into a curve instead of a straight line.")]
    [SerializeField] private float arcHeight = 120f;
    [Tooltip("Random +/- variation on the arc height per particle (0 = identical arcs).")]
    [SerializeField] private float arcJitter = 60f;

    [Header("Scale / fade")]
    [Tooltip("Scale multiplier over the particle's life (time 0..1).")]
    [SerializeField] private AnimationCurve scaleOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);
    [Tooltip("Fraction of the travel after which the particle starts fading out (1 = fade only at the very end).")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeStart = 0.7f;
    [Tooltip("Use unscaled time so particles keep moving while the game is paused / time-scaled.")]
    [SerializeField] private bool useUnscaledTime = true;

    // The first active emitter in the scene, for callers that are spawned at runtime
    // (like reels) and so can't reference a scene emitter in their prefab.
    public static RisingParticleEmitter Primary { get; private set; }

    // The emitter's own default destination, so runtime callers can reuse it.
    public Transform Destination => destination;

    private RectTransform rectTransform;
    private Canvas canvas;
    private Camera uiCamera;

    // Current life count, for picking the per-life colour. -1 until the manager reports.
    private int lives = -1;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;
    }

    private void OnEnable()
    {
        if (Primary == null)
        {
            Primary = this;
        }
        DopamineManager.OnLivesChanged += OnLivesChanged;
    }

    private void OnDisable()
    {
        if (Primary == this)
        {
            Primary = null;
        }
        DopamineManager.OnLivesChanged -= OnLivesChanged;
    }

    private void OnLivesChanged(int newLives, int maxLives)
    {
        lives = newLives;
    }

    // The colour to spawn with: the per-life colour for the current life count, or
    // the fallback colour when no per-life colours are set.
    private Color CurrentColor()
    {
        if (lifeColors != null && lifeColors.Length > 0 && lives >= 0)
        {
            int index = Mathf.Clamp(lives, 0, lifeColors.Length - 1);
            return lifeColors[index];
        }
        return color;
    }

    // Emit a burst originating at a screen position (e.g. where the combo/overlay
    // played). overrideDestination, if given, wins over the serialized destination.
    public void EmitFromScreen(Vector2 screenPosition, Transform overrideDestination = null)
    {
        if (particleSprite == null || !isActiveAndEnabled || rectTransform == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, uiCamera, out Vector2 startLocal))
        {
            return;
        }

        Transform dest = overrideDestination != null ? overrideDestination : destination;
        Vector2 endLocal = DestinationLocal(dest, startLocal);

        StartCoroutine(EmitBurst(startLocal, endLocal));
    }

    // Convenience: emit from a UI element's current position (e.g. the combo image).
    public void EmitFrom(RectTransform source, Transform overrideDestination = null)
    {
        if (source == null)
        {
            return;
        }
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, source.position);
        EmitFromScreen(screen, overrideDestination);
    }

    // Resolves the destination to a local point in this rect. Falls back to a point
    // straight above the start when there's no destination.
    private Vector2 DestinationLocal(Transform dest, Vector2 fallbackFromStart)
    {
        if (dest == null)
        {
            return fallbackFromStart + Vector2.up * noDestinationRise;
        }

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, dest.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out Vector2 local);
        return local;
    }

    private IEnumerator EmitBurst(Vector2 startLocal, Vector2 endLocal)
    {
        for (int i = 0; i < countPerEmit; i++)
        {
            SpawnOne(startLocal, endLocal);
            if (emitStagger > 0f)
            {
                yield return WaitFor(emitStagger);
            }
        }
    }

    private void SpawnOne(Vector2 startLocal, Vector2 endLocal)
    {
        GameObject go = new GameObject("RisingParticle", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(rectTransform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        float sizeMul = Random.Range(
            Mathf.Min(sizeMultiplierRange.x, sizeMultiplierRange.y),
            Mathf.Max(sizeMultiplierRange.x, sizeMultiplierRange.y));
        rt.sizeDelta = startSize * sizeMul;

        Vector2 start = startLocal + Random.insideUnitCircle * spawnScatter;
        rt.anchoredPosition = start;

        Image img = go.GetComponent<Image>();
        img.sprite = particleSprite;
        img.color = CurrentColor();
        img.raycastTarget = false;
        img.preserveAspect = true;

        float duration = Mathf.Max(0.01f, travelDuration + Random.Range(-travelDurationJitter, travelDurationJitter));
        float arc = arcHeight + Random.Range(-arcJitter, arcJitter);
        float arcDir = Random.value < 0.5f ? -1f : 1f; // bow left or right

        StartCoroutine(Travel(rt, img, start, endLocal, duration, arc, arcDir));
    }

    private IEnumerator Travel(RectTransform rt, Image img, Vector2 start, Vector2 end, float duration, float arc, float arcDir)
    {
        Color baseColor = img.color;
        Vector3 baseScale = rt.localScale;

        // Perpendicular to the straight path, used to bow the trajectory sideways.
        Vector2 dir = end - start;
        Vector2 perp = dir.sqrMagnitude > 0.0001f ? new Vector2(-dir.y, dir.x).normalized : Vector2.right;

        float t = 0f;
        while (t < duration)
        {
            t += DeltaTime();
            float k = Mathf.Clamp01(t / duration);
            float eased = travelEase.Evaluate(k);

            // Eased straight-line interpolation + a sine arc that peaks mid-flight.
            Vector2 pos = Vector2.LerpUnclamped(start, end, eased);
            pos += perp * (arcDir * arc * Mathf.Sin(eased * Mathf.PI));
            rt.anchoredPosition = pos;

            rt.localScale = baseScale * Mathf.Max(0f, scaleOverLife.Evaluate(k));

            float alpha = baseColor.a;
            if (fadeStart < 1f && k > fadeStart)
            {
                alpha = Mathf.Lerp(baseColor.a, 0f, (k - fadeStart) / (1f - fadeStart));
            }
            Color c = baseColor;
            c.a = alpha;
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private object WaitFor(float seconds)
    {
        if (useUnscaledTime)
        {
            return new WaitForSecondsRealtime(seconds);
        }
        return new WaitForSeconds(seconds);
    }
}
