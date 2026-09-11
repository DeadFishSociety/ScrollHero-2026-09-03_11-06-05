using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Like this post" — the player taps the heart to fill it up. The fill drains
/// over time, so they have to keep tapping to top it off before it completes.
/// </summary>
public class LikeOverlay : FeedOverlay
{
    [Header("Like")]
    [SerializeField] private Button heartButton;

    [Tooltip("The thing that pops when tapped. Defaults to the heart button's own transform.")]
    [SerializeField] private RectTransform heartVisual;

    [Tooltip("Taps needed to fill the heart from empty, ignoring decay. " +
             "Each tap adds 1 / this to the fill.")]
    [SerializeField, Min(1)] private int requiredTaps = 8;

    [Header("Fill")]
    [Tooltip("Inner heart that grows to fill the outline as taps come in. Optional.")]
    [SerializeField] private RectTransform heartFill;

    [Tooltip("How full the inner heart is at 0% fill, as a fraction of its authored size. " +
             "0 = starts invisible, 0.2 = starts at 20%.")]
    [SerializeField, Range(0f, 1f)] private float fillStartScale = 0f;

    [Tooltip("How fast the fill drains per second while the player isn't tapping. " +
             "0 = never shrinks.")]
    [SerializeField, Min(0f)] private float fillDecayPerSecond = 0.25f;

    [Tooltip("Smoothing time for the heart following the fill value. Smaller = snappier.")]
    [SerializeField, Min(0.001f)] private float fillSmoothing = 0.12f;

    [Header("Feedback")]
    [SerializeField] private float popScale = 1.25f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.12f;

    [Tooltip("Optional: tinted from unfilledColor to filledColor as the heart fills.")]
    [SerializeField] private Graphic heartGraphic;
    [SerializeField] private Color unfilledColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color filledColor = new Color(1f, 0.2f, 0.35f, 1f);

    [Header("Particles")]
    [Tooltip("Little sprites that burst out of the heart on each tap. 0 = no particles.\n" +
             "Sizes and distances below are fractions of the heart's CURRENT radius, so " +
             "they scale automatically as the heart grows or if it's sized bigger on screen.")]
    [SerializeField, Min(0)] private int particleCount = 10;

    [Tooltip("Sprite for each particle. Leave empty to reuse the heart's own sprite.")]
    [SerializeField] private Sprite particleSprite;

    [SerializeField] private Color particleColor = new Color(1f, 0.2f, 0.35f, 1f);

    [Tooltip("Particle diameter as a fraction of the heart's current radius.")]
    [SerializeField, Min(0f)] private float particleSizeFraction = 0.22f;

    [Tooltip("Where particles spawn, as a fraction of the heart's radius out from its centre. " +
             "1 = right at the edge — keeps them from being hidden behind the heart.")]
    [SerializeField, Range(0f, 2f)] private float particleStartRadius = 0.9f;

    [Tooltip("How fast particles fly out, in heart-radii per second, picked randomly in this range.")]
    [SerializeField] private Vector2 particleSpeedRange = new Vector2(1.2f, 2.6f);

    [Tooltip("Downward pull on particles, in heart-radii per second². 0 = they fly straight out.")]
    [SerializeField] private float particleGravity = 4f;

    [Tooltip("Seconds a particle lives before it has fully shrunk and faded.")]
    [SerializeField, Min(0.05f)] private float particleLifetime = 0.6f;

    [Header("Sound")]
    [Tooltip("Sound played on each tap. Add clip variations — weighted or not depending on the " +
             "mode chosen. Pitch is driven by how full the heart is (see below), so leave this " +
             "slot's own Pitch Range at 1.")]
    [SerializeField] private SoundEffect tapSound = new SoundEffect();

    [Tooltip("Tap-sound pitch when the heart is empty (0% complete).")]
    [SerializeField, Min(0.01f)] private float pitchAtEmpty = 0.8f;

    [Tooltip("Tap-sound pitch when the heart is full (100% complete).")]
    [SerializeField, Min(0.01f)] private float pitchAtFull = 1.6f;

    [Tooltip("How completeness (0..1) maps to pitch between empty and full. Linear by default; " +
             "curve it for an ease or an accelerating rise.")]
    [SerializeField] private AnimationCurve pitchOverProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    // Continuous progress, 0..1. A tap adds 1/requiredTaps; decay chips away at it.
    private float fill;
    private Coroutine popRoutine;

    // The scale the heart was authored at in the prefab. The pop animation is
    // relative to this, so a heart scaled down in the Inspector stays that size.
    private Vector3 baseScale = Vector3.one;

    // The full size of the inner fill heart, captured from the prefab.
    private Vector3 fillFullScale = Vector3.one;

    private RectTransform Visual => heartVisual != null
        ? heartVisual
        : (heartButton != null ? heartButton.transform as RectTransform : null);

    private void Awake()
    {
        if (Visual != null)
            baseScale = Visual.localScale;

        if (heartFill != null)
            fillFullScale = heartFill.localScale;

        if (heartButton != null)
            heartButton.onClick.AddListener(HandleTap);
    }

    private void OnDestroy()
    {
        if (heartButton != null)
            heartButton.onClick.RemoveListener(HandleTap);
    }

    /// <summary>Set how fast the heart's fill drains (higher = must tap faster to keep up).
    /// Adaptive difficulty; apply before Begin().</summary>
    public void SetFillDecay(float decayPerSecond) => fillDecayPerSecond = Mathf.Max(0f, decayPerSecond);

    protected override void OnBegin()
    {
        fill = 0f;

        if (Visual != null)
            Visual.localScale = baseScale;

        ApplyFillScale(snap: true); // start empty immediately
        RefreshTint();
    }

    protected override void OnTick(float deltaTime)
    {
        // Drain while idle. Tapping outpaces this; stop tapping and it shrinks back.
        if (fillDecayPerSecond > 0f && fill > 0f)
        {
            fill = Mathf.Clamp01(fill - fillDecayPerSecond * deltaTime);
            RefreshTint();
            ReportProgress();
        }

        ApplyFillScale(snap: false); // smoothly follow the fill value every frame
    }

    private void HandleTap()
    {
        if (IsFinished)
            return;

        fill = Mathf.Clamp01(fill + 1f / requiredTaps);
        RefreshTint();
        PlayTapSound(); // pitched by the new (higher) completeness
        Pop();
        EmitBurst();

        if (fill >= 1f)
            Complete();
        else
            ReportProgress();
    }

    // Play the tap sound at a pitch that rises with how full the heart is.
    private void PlayTapSound()
    {
        float curved = Mathf.Clamp01(pitchOverProgress.Evaluate(fill));
        tapSound.Play(Mathf.Lerp(pitchAtEmpty, pitchAtFull, curved));
    }

    // Target scale of the inner heart for the current fill value.
    private Vector3 FillTarget => fillFullScale * Mathf.Lerp(fillStartScale, 1f, fill);

    private void ApplyFillScale(bool snap)
    {
        if (heartFill == null)
            return;

        if (snap)
        {
            heartFill.localScale = FillTarget;
            return;
        }

        // Exponential smoothing toward the target; frame-rate independent.
        float t = 1f - Mathf.Exp(-Time.deltaTime / fillSmoothing);
        heartFill.localScale = Vector3.Lerp(heartFill.localScale, FillTarget, t);
    }

    private void RefreshTint()
    {
        if (heartGraphic == null)
            return;

        heartGraphic.color = Color.Lerp(unfilledColor, filledColor, fill);
    }

    private void Pop()
    {
        RectTransform visual = Visual;
        if (visual == null)
            return;

        if (popRoutine != null)
            StopCoroutine(popRoutine);

        popRoutine = StartCoroutine(PopRoutine(visual));
    }

    private IEnumerator PopRoutine(RectTransform visual)
    {
        float elapsed = 0f;
        visual.localScale = baseScale * popScale;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            visual.localScale = baseScale * Mathf.Lerp(popScale, 1f, t);
            yield return null;
        }

        visual.localScale = baseScale;
        popRoutine = null;
    }

    // Spray a handful of small heart sprites out from the heart on each tap. These
    // are plain UI Images so they render on the same canvas as the overlay, no
    // ParticleSystem or material setup required.
    //
    // Sizes and distances are all measured in "heart radii" — a fraction of the
    // heart's current on-screen radius — so the burst automatically scales with the
    // heart as it grows with taps or if it's sized bigger on screen, and particles
    // always start at the edge instead of hiding behind the heart.
    private void EmitBurst()
    {
        RectTransform origin = Visual;
        RectTransform parent = transform as RectTransform;
        if (particleCount <= 0 || origin == null || parent == null)
            return;

        Sprite sprite = particleSprite != null ? particleSprite : ResolveHeartSprite();
        if (sprite == null)
            return;

        float radius = CurrentHeartRadius(parent);
        if (radius <= 0f)
            return;

        // The heart is a direct child of this overlay root, so its anchored position
        // is exactly where the burst should start in the particles' parent space.
        Vector2 start = origin.anchoredPosition;

        for (int i = 0; i < particleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 spawn = start + dir * (radius * particleStartRadius);
            Vector2 velocity = dir * (Random.Range(particleSpeedRange.x, particleSpeedRange.y) * radius);
            StartCoroutine(ParticleRoutine(parent, sprite, spawn, velocity, radius));
        }
    }

    // The heart's current radius in the particles' parent space, folding in the fill
    // growth, the tap pop, and however big the heart is authored on screen.
    private float CurrentHeartRadius(RectTransform parent)
    {
        RectTransform heart = heartFill != null ? heartFill : Visual;
        if (heart == null)
            return 0f;

        var corners = new Vector3[4]; // bottom-left, top-left, top-right, bottom-right
        heart.GetWorldCorners(corners);
        float worldWidth = Vector3.Distance(corners[0], corners[3]);
        float worldHeight = Vector3.Distance(corners[0], corners[1]);
        float worldRadius = 0.5f * Mathf.Min(worldWidth, worldHeight);

        float parentScale = parent.lossyScale.x;
        return Mathf.Approximately(parentScale, 0f) ? worldRadius : worldRadius / parentScale;
    }

    private Sprite ResolveHeartSprite()
    {
        if (heartButton != null && heartButton.image != null && heartButton.image.sprite != null)
            return heartButton.image.sprite;
        return heartGraphic is Image graphicImage ? graphicImage.sprite : null;
    }

    private IEnumerator ParticleRoutine(RectTransform parent, Sprite sprite, Vector2 startPos, Vector2 velocity, float radius)
    {
        var go = new GameObject("HeartParticle", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        float size = radius * particleSizeFraction;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = startPos;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        float gravity = particleGravity * radius; // radii/s² → parent units/s²
        float elapsed = 0f;
        while (elapsed < particleLifetime)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            velocity += Vector2.down * (gravity * dt);
            rect.anchoredPosition += velocity * dt;

            float t = elapsed / particleLifetime;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t);

            Color c = particleColor;
            c.a = particleColor.a * (1f - t);
            image.color = c;

            yield return null;
        }

        Destroy(go);
    }

    protected override string GetProgressLabel() => $"{Mathf.RoundToInt(fill * 100)}%";
}
