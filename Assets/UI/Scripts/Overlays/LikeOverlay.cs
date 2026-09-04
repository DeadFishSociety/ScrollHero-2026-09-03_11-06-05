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
        Pop();

        if (fill >= 1f)
            Complete();
        else
            ReportProgress();
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

    protected override string GetProgressLabel() => $"{Mathf.RoundToInt(fill * 100)}%";
}
