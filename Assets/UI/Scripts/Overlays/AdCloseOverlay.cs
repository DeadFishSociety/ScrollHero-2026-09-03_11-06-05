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
        RectTransform rect = ButtonRect;
        RectTransform parent = rect != null ? rect.parent as RectTransform : null;
        RectTransform area = Area;

        // If we can't resolve the parent (e.g. the button is on the Canvas root), fall back
        // to the old area-centred behaviour.
        if (rect == null || parent == null || area == null)
            return LegacyPick(current, buttonSize);

        // The move area expressed in the button's PARENT local space. This is the key fix:
        // anchoredPosition is measured in the parent's space, so if the move area is a
        // different (offset) rect than the button's parent, we must map it in — otherwise
        // only the area's size matters and the cross ignores where the area actually is.
        Rect areaLocal = RectInParentSpace(area, parent);

        // Convert a desired CENTRE (parent-local) into an anchoredPosition for any anchor /
        // pivot: anchoredPosition places the pivot at (anchor reference point) + itself, and
        // the centre sits a pivot-dependent offset from the pivot.
        Vector2 anchorMid = (rect.anchorMin + rect.anchorMax) * 0.5f;
        Vector2 anchorRef = new Vector2(
            parent.rect.xMin + anchorMid.x * parent.rect.width,
            parent.rect.yMin + anchorMid.y * parent.rect.height);
        Vector2 pivotToCentre = Vector2.Scale(new Vector2(0.5f, 0.5f) - rect.pivot, buttonSize);

        // Keep the whole button inside the area: inset by half the button plus padding.
        float halfW = buttonSize.x * 0.5f + padding.x;
        float halfH = buttonSize.y * 0.5f + padding.y;
        float minX = areaLocal.xMin + halfW;
        float maxX = areaLocal.xMax - halfW;
        float minY = areaLocal.yMin + halfH;
        float maxY = areaLocal.yMax - halfH;
        if (minX > maxX) minX = maxX = areaLocal.center.x; // area too narrow — pin to centre
        if (minY > maxY) minY = maxY = areaLocal.center.y; // area too short — pin to centre

        Vector2 best = current;
        float bestDistance = -1f;

        // Try a handful of spots and keep the first one that is far enough away;
        // if the area is too small for that, fall back to the furthest we found.
        for (int i = 0; i < 20; i++)
        {
            Vector2 desiredCentre = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            Vector2 candidate = desiredCentre - anchorRef - pivotToCentre;

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

    // Old behaviour: positions centred on the button's own parent, using only the area size.
    private Vector2 LegacyPick(Vector2 current, Vector2 buttonSize)
    {
        Vector2 range = (Area.rect.size - buttonSize) * 0.5f - padding;
        range.x = Mathf.Max(0f, range.x);
        range.y = Mathf.Max(0f, range.y);

        Vector2 best = current;
        float bestDistance = -1f;
        for (int i = 0; i < 20; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(-range.x, range.x), Random.Range(-range.y, range.y));
            float distance = Vector2.Distance(candidate, current);
            if (distance >= minMoveDistance)
                return candidate;
            if (distance > bestDistance) { bestDistance = distance; best = candidate; }
        }
        return best;
    }

    private static readonly Vector3[] cornerBuffer = new Vector3[4];

    /// <summary>The world-space rect of <paramref name="area"/> expressed in <paramref name="parent"/>'s local space.</summary>
    private static Rect RectInParentSpace(RectTransform area, RectTransform parent)
    {
        area.GetWorldCorners(cornerBuffer); // 0=BL, 1=TL, 2=TR, 3=BR (world space)
        Vector2 bottomLeft = parent.InverseTransformPoint(cornerBuffer[0]);
        Vector2 topRight = parent.InverseTransformPoint(cornerBuffer[2]);
        return new Rect(bottomLeft.x, bottomLeft.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
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
