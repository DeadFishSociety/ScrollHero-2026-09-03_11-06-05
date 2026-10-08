using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Spawns short-lived heart Images that fly up, shrink and fade out. Works on a
// Screen Space - Overlay canvas (unlike a world Particle System). Call Burst()
// with a screen position (e.g. from a tap).
[RequireComponent(typeof(RectTransform))]
public class HeartParticleSpawner : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Sprite used for each heart particle (e.g. heart.png).")]
    [SerializeField] private Sprite particleSprite;
    [SerializeField] private Color color = Color.white;
    [Tooltip("Base size of a heart particle, in canvas units.")]
    [SerializeField] private Vector2 startSize = new Vector2(48f, 48f);

    [Tooltip("Each heart's size is multiplied by a random value in this range (x = min, y = max). Set both to 1 for uniform size.")]
    [SerializeField] private Vector2 sizeMultiplierRange = new Vector2(0.8f, 1.3f);

    [Header("Burst")]
    [Tooltip("How many hearts spawn per tap.")]
    [SerializeField] private int countPerTap = 5;
    [SerializeField] private float lifetime = 0.6f;

    [Header("Motion (canvas units/sec)")]
    [Tooltip("Upward speed.")]
    [SerializeField] private float riseSpeed = 350f;
    [Tooltip("Random horizontal spread of speed.")]
    [SerializeField] private float horizontalSpread = 180f;
    [Tooltip("Downward pull applied over the lifetime.")]
    [SerializeField] private float gravity = 400f;
    [Tooltip("Random rotation speed range (deg/sec).")]
    [SerializeField] private float spinRange = 180f;

    [Header("Rise to dopamine bar (optional)")]
    [Tooltip("When on, hearts travel up to a destination (e.g. the dopamine bar) along an eased arc instead of flying off ballistically. Turn on for the like hearts; leave off for minigame bursts.")]
    [SerializeField] private bool riseToDestination = false;

    [Tooltip("Where the hearts rise to. Leave empty to use the scene RisingParticleEmitter's Destination automatically.")]
    [SerializeField] private Transform destination;

    [Tooltip("Seconds a heart takes to reach the destination.")]
    [Min(0.01f)]
    [SerializeField] private float travelDuration = 0.6f;

    [Tooltip("Random +/- variation on travel duration, so hearts don't arrive in lockstep.")]
    [Min(0f)]
    [SerializeField] private float travelDurationJitter = 0.12f;

    [Tooltip("Eased progress along the path (0..1 over the travel). Ease-in-out reads nicely.")]
    [SerializeField] private AnimationCurve travelEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Sideways arc height (canvas units) at the midpoint, so the path bows into a curve.")]
    [SerializeField] private float arcHeight = 90f;

    [Tooltip("Random +/- variation on the arc height per heart (0 = identical arcs).")]
    [SerializeField] private float arcJitter = 50f;

    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    // Emit a burst of hearts at the given screen position.
    public void Burst(Vector2 screenPosition)
    {
        if (particleSprite == null)
        {
            return;
        }

        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, cam, out localPoint))
        {
            return;
        }

        // Decide whether the hearts rise to a destination this burst.
        bool travel = false;
        Vector2 endLocal = Vector2.zero;
        if (riseToDestination)
        {
            Transform dest = ResolveDestination();
            if (dest != null)
            {
                Vector2 destScreen = RectTransformUtility.WorldToScreenPoint(cam, dest.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, destScreen, cam, out endLocal))
                {
                    travel = true;
                }
            }
        }

        for (int i = 0; i < countPerTap; i++)
        {
            SpawnOne(localPoint, travel, endLocal);
        }
    }

    // The destination to rise to: the explicit one, else the scene emitter's.
    private Transform ResolveDestination()
    {
        if (destination != null)
        {
            return destination;
        }
        return RisingParticleEmitter.Primary != null ? RisingParticleEmitter.Primary.Destination : null;
    }

    private void SpawnOne(Vector2 localPoint, bool travel, Vector2 endLocal)
    {
        GameObject go = new GameObject("HeartParticle", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(rectTransform, false);
        float sizeMul = Random.Range(
            Mathf.Min(sizeMultiplierRange.x, sizeMultiplierRange.y),
            Mathf.Max(sizeMultiplierRange.x, sizeMultiplierRange.y));
        rt.sizeDelta = startSize * sizeMul;
        rt.anchoredPosition = localPoint;

        Image img = go.GetComponent<Image>();
        img.sprite = particleSprite;
        img.color = color;
        img.raycastTarget = false;

        if (travel)
        {
            float duration = Mathf.Max(0.01f, travelDuration + Random.Range(-travelDurationJitter, travelDurationJitter));
            float arc = arcHeight + Random.Range(-arcJitter, arcJitter);
            float arcDir = Random.value < 0.5f ? -1f : 1f;
            StartCoroutine(TravelTo(rt, img, localPoint, endLocal, duration, arc, arcDir));
        }
        else
        {
            Vector2 velocity = new Vector2(Random.Range(-horizontalSpread, horizontalSpread), riseSpeed);
            float spin = Random.Range(-spinRange, spinRange);
            StartCoroutine(Animate(rt, img, velocity, spin));
        }
    }

    // Flies a heart up to the destination along an eased, arcing path, shrinking and
    // fading as it arrives.
    private IEnumerator TravelTo(RectTransform rt, Image img, Vector2 start, Vector2 end, float duration, float arc, float arcDir)
    {
        Color baseColor = img.color;
        Vector3 baseScale = rt.localScale;

        Vector2 dir = end - start;
        Vector2 perp = dir.sqrMagnitude > 0.0001f ? new Vector2(-dir.y, dir.x).normalized : Vector2.right;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = travelEase.Evaluate(k);

            Vector2 pos = Vector2.LerpUnclamped(start, end, eased);
            pos += perp * (arcDir * arc * Mathf.Sin(eased * Mathf.PI));
            rt.anchoredPosition = pos;

            // Hold size most of the way, then shrink + fade into the bar at the end.
            float tail = Mathf.InverseLerp(0.7f, 1f, k);
            rt.localScale = baseScale * Mathf.Lerp(1f, 0.4f, tail);
            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, tail);
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }

    private IEnumerator Animate(RectTransform rt, Image img, Vector2 velocity, float spin)
    {
        float t = 0f;
        Color baseColor = img.color;

        while (t < lifetime)
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = t / lifetime; // 0..1

            velocity.y -= gravity * dt;
            rt.anchoredPosition += velocity * dt;
            rt.Rotate(0f, 0f, spin * dt);

            float scale = Mathf.Lerp(1f, 0f, k);
            rt.localScale = new Vector3(scale, scale, 1f);

            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, k);
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }
}
