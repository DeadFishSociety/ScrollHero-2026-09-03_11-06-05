using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Like this post" — the player has to tap the heart a number of times.
/// </summary>
public class LikeOverlay : FeedOverlay
{
    [Header("Like")]
    [SerializeField] private Button heartButton;

    [Tooltip("The thing that pops when tapped. Defaults to the heart button's own transform.")]
    [SerializeField] private RectTransform heartVisual;

    [SerializeField, Min(1)] private int requiredTaps = 8;

    [Header("Fill")]
    [Tooltip("Inner heart that grows to fill the outline as taps come in. Optional.")]
    [SerializeField] private RectTransform heartFill;

    [Tooltip("How full the inner heart is at 0 taps, as a fraction of its authored size. " +
             "0 = starts invisible, 0.2 = starts at 20%.")]
    [SerializeField, Range(0f, 1f)] private float fillStartScale = 0f;

    [Tooltip("How long the inner heart takes to grow to its new size after each tap.")]
    [SerializeField, Min(0.01f)] private float fillGrowDuration = 0.15f;

    [Header("Feedback")]
    [SerializeField] private float popScale = 1.25f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.12f;

    [Tooltip("Optional: tinted from unfilledColor to filledColor as taps come in.")]
    [SerializeField] private Graphic heartGraphic;
    [SerializeField] private Color unfilledColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color filledColor = new Color(1f, 0.2f, 0.35f, 1f);

    private int taps;
    private Coroutine popRoutine;
    private Coroutine fillRoutine;

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
        taps = 0;

        if (Visual != null)
            Visual.localScale = baseScale;

        if (fillRoutine != null)
        {
            StopCoroutine(fillRoutine);
            fillRoutine = null;
        }
        if (heartFill != null)
            heartFill.localScale = fillFullScale * fillStartScale; // snap to empty

        RefreshTint();
    }

    private void HandleTap()
    {
        if (IsFinished)
            return;

        taps++;
        RefreshTint();
        Pop();
        GrowFill();

        if (taps >= requiredTaps)
            Complete();
        else
            ReportProgress();
    }

    // Fraction (0..1) of the inner heart's full size for the current tap count.
    private float FillFraction => Mathf.Lerp(fillStartScale, 1f, (float)taps / requiredTaps);

    private void GrowFill()
    {
        if (heartFill == null)
            return;

        if (fillRoutine != null)
            StopCoroutine(fillRoutine);

        fillRoutine = StartCoroutine(GrowFillRoutine(fillFullScale * FillFraction));
    }

    private IEnumerator GrowFillRoutine(Vector3 target)
    {
        Vector3 start = heartFill.localScale;
        float elapsed = 0f;

        while (elapsed < fillGrowDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fillGrowDuration));
            heartFill.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }

        heartFill.localScale = target;
        fillRoutine = null;
    }

    private void RefreshTint()
    {
        if (heartGraphic == null)
            return;

        heartGraphic.color = Color.Lerp(unfilledColor, filledColor, (float)taps / requiredTaps);
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

    protected override string GetProgressLabel() => $"{Mathf.Min(taps, requiredTaps)} / {requiredTaps}";
}
