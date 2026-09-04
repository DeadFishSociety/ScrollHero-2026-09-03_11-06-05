using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Close the ad" — the player has to tap the cross, but the cross jumps to a new
/// spot every time it is hit. It only closes after it has been caught N times.
/// </summary>
public class AdCloseOverlay : FeedOverlay
{
    [Header("Ad")]
    [SerializeField] private Button closeButton;

    [Tooltip("Area the cross is allowed to jump around in. Defaults to this overlay's own rect.")]
    [SerializeField] private RectTransform moveArea;

    [Tooltip("How many taps it takes to actually close the ad. Every tap but the last makes it dodge.")]
    [SerializeField, Min(1)] private int requiredTaps = 3;

    [Header("Dodge")]
    [Tooltip("Keeps the cross away from the edges of the move area (pixels).")]
    [SerializeField] private Vector2 padding = new Vector2(40f, 40f);

    [Tooltip("The cross will not hop to a spot closer than this to where it was (pixels).")]
    [SerializeField, Min(0f)] private float minMoveDistance = 250f;

    [Tooltip("How long the hop takes. 0 = teleport instantly.")]
    [SerializeField, Min(0f)] private float moveDuration = 0.12f;

    private int taps;
    private Vector2 startAnchoredPosition;
    private Coroutine moveRoutine;
    private bool startPositionCaptured;

    private RectTransform ButtonRect => closeButton != null ? closeButton.transform as RectTransform : null;
    private RectTransform Area => moveArea != null ? moveArea : transform as RectTransform;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HandleTap);

        CaptureStartPosition();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleTap);
    }

    private void CaptureStartPosition()
    {
        if (startPositionCaptured)
            return;

        RectTransform rect = ButtonRect;
        if (rect == null)
            return;

        startAnchoredPosition = rect.anchoredPosition;
        startPositionCaptured = true;
    }

    protected override void OnBegin()
    {
        taps = 0;
        CaptureStartPosition();

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        RectTransform rect = ButtonRect;
        if (rect != null)
            rect.anchoredPosition = startAnchoredPosition;

        if (closeButton != null)
            closeButton.interactable = true;
    }

    private void HandleTap()
    {
        if (IsFinished)
            return;

        taps++;

        if (taps >= requiredTaps)
        {
            Complete();
            return;
        }

        Dodge();
        ReportProgress();
    }

    private void Dodge()
    {
        RectTransform rect = ButtonRect;
        if (rect == null)
            return;

        Vector2 target = PickPosition(rect.anchoredPosition, rect.rect.size);

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        if (moveDuration <= 0f)
        {
            rect.anchoredPosition = target;
            return;
        }

        moveRoutine = StartCoroutine(MoveRoutine(rect, target));
    }

    private Vector2 PickPosition(Vector2 current, Vector2 buttonSize)
    {
        Vector2 areaSize = Area.rect.size;

        // How far from the centre of the area the button's centre may sit.
        Vector2 range = (areaSize - buttonSize) * 0.5f - padding;
        range.x = Mathf.Max(0f, range.x);
        range.y = Mathf.Max(0f, range.y);

        Vector2 best = current;
        float bestDistance = -1f;

        // Try a handful of spots and keep the first one that is far enough away;
        // if the area is too small for that, fall back to the furthest we found.
        for (int i = 0; i < 20; i++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(-range.x, range.x),
                Random.Range(-range.y, range.y));

            float distance = Vector2.Distance(candidate, current);
            if (distance >= minMoveDistance)
                return candidate;

            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private IEnumerator MoveRoutine(RectTransform rect, Vector2 target)
    {
        Vector2 start = rect.anchoredPosition;
        float elapsed = 0f;

        // Don't let the player land a second tap mid-flight.
        if (closeButton != null)
            closeButton.interactable = false;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
            rect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        rect.anchoredPosition = target;

        if (closeButton != null)
            closeButton.interactable = true;

        moveRoutine = null;
    }

    protected override string GetProgressLabel() => $"{Mathf.Min(taps, requiredTaps)} / {requiredTaps}";
}
