using UnityEngine;
using UnityEngine.UI;

// Fades a full-screen vignette overlay in as the dopamine bar runs low, for a
// "closing in" feel near death. Put this on a full-screen UI Image whose sprite
// is a vignette (transparent centre, dark edges), with Raycast Target off, sitting
// above the feed. It listens to the same dopamine events DopamineBar uses, so no
// extra wiring is needed beyond having a DopamineManager in the scene.
//
// The Image's sprite gives the vignette its shape; this script only drives the
// overall alpha (and an optional heartbeat pulse) from the dopamine fraction.
[RequireComponent(typeof(Image))]
public class DopamineVignette : MonoBehaviour
{
    [Header("When to show (dopamine fraction, 0..1)")]
    [Tooltip("At or above this fraction the vignette is fully hidden. Below it, the vignette starts fading in.")]
    [Range(0f, 1f)]
    [SerializeField] private float startFraction = 0.4f;

    [Tooltip("At or below this fraction the vignette reaches full strength.")]
    [Range(0f, 1f)]
    [SerializeField] private float fullFraction = 0f;

    [Header("Strength")]
    [Tooltip("Overlay alpha at full strength (dopamine at/under Full Fraction).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.85f;

    [Tooltip("How quickly the overlay eases toward its target alpha. Higher = snappier.")]
    [SerializeField] private float fadeSpeed = 6f;

    [Header("Heartbeat pulse (optional)")]
    [Tooltip("Extra alpha added by a pulse, at full strength. 0 = no pulse. The pulse fades in with the vignette.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pulseAmplitude = 0.12f;

    [Tooltip("Pulse speed, in beats per second.")]
    [SerializeField] private float pulseSpeed = 2.5f;

    [Header("Colour (by lives)")]
    [Tooltip("Vignette colour at full lives. Requires a white/greyscale vignette sprite so it can be tinted.")]
    [SerializeField] private Color fullLivesColor = Color.black;

    [Tooltip("Vignette colour at 1 life. The colour lerps toward this as lives run down.")]
    [SerializeField] private Color lowLivesColor = Color.red;

    private Image image;

    // Target alpha from the latest dopamine reading, and the eased current value.
    private float targetAlpha;
    private float currentAlpha;

    // 0 at full lives, 1 at the last life: how far to tint toward lowLivesColor.
    private float redness;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false; // never block taps/swipes
        currentAlpha = 0f;
        Apply(0f);
    }

    private void OnEnable()
    {
        DopamineManager.OnDopamineInitialized += OnDopamineChanged;
        DopamineManager.OnDopamineChange += OnDopamineChanged;
        DopamineManager.OnLivesChanged += OnLivesChanged;
    }

    private void OnDisable()
    {
        DopamineManager.OnDopamineInitialized -= OnDopamineChanged;
        DopamineManager.OnDopamineChange -= OnDopamineChanged;
        DopamineManager.OnLivesChanged -= OnLivesChanged;
    }

    private void OnLivesChanged(int lives, int maxLives)
    {
        // Full lives -> 0 (base colour); one life -> 1 (fully lowLivesColor).
        redness = maxLives > 1 ? Mathf.Clamp01(Mathf.InverseLerp(maxLives, 1, lives)) : (lives <= 1 ? 1f : 0f);
    }

    private void OnDopamineChanged(float fraction)
    {
        // Map the fraction (startFraction -> fullFraction) to intensity (0 -> 1),
        // then to an alpha. InverseLerp handles startFraction > fullFraction.
        float intensity = Mathf.Clamp01(Mathf.InverseLerp(startFraction, fullFraction, fraction));
        targetAlpha = intensity * maxAlpha;
    }

    private void Update()
    {
        // Ease toward the target (framerate-independent), on unscaled time so it
        // keeps working if the game is time-scaled (e.g. paused or ducked).
        float k = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, k);

        float displayed = currentAlpha;
        if (pulseAmplitude > 0f)
        {
            float intensity = maxAlpha > 0f ? currentAlpha / maxAlpha : 0f;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
            displayed += intensity * pulseAmplitude * wave;
        }

        Apply(Mathf.Clamp01(displayed));
    }

    // Sets the overlay colour: RGB tinted toward lowLivesColor by the current
    // lives, alpha from the dopamine level.
    private void Apply(float alpha)
    {
        Color c = Color.Lerp(fullLivesColor, lowLivesColor, redness);
        c.a = alpha;
        image.color = c;
    }
}
