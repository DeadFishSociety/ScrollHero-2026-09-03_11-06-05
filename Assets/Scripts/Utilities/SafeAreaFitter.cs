using UnityEngine;

// Anchors this RectTransform to the device's safe area (notch / home indicator).
//
// Important: Screen.safeArea, Screen.width and Screen.height can report stale or
// transitional values during a scene load - notably on iOS and the simulator -
// so computing the anchors once in Awake is unreliable. The first scene gets lucky
// (the screen is already settled), but when you leave and come back the fresh
// fitter runs Awake mid-transition, caches a bad frame, and the UI ends up broken.
//
// This version keeps polling and re-applies whenever the safe area OR the screen
// size/orientation changes, and ignores degenerate frames, so it self-corrects
// instead of locking in a bad value.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;

    // The screen state the current anchors were computed from. Start invalid so the
    // first valid frame always applies.
    private Rect lastSafeArea = Rect.zero;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private ScreenOrientation lastOrientation;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        // Force a re-apply when (re)enabled; the values may differ from last time.
        lastScreenWidth = 0;
        lastScreenHeight = 0;
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    // Re-applies the safe area only when something it depends on actually changed.
    private void Refresh()
    {
        Rect safeArea = Screen.safeArea;
        int width = Screen.width;
        int height = Screen.height;
        ScreenOrientation orientation = Screen.orientation;

        // Skip degenerate frames (can happen mid scene-load) - wait for real values.
        if (width <= 0 || height <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
        {
            return;
        }

        if (safeArea == lastSafeArea
            && width == lastScreenWidth
            && height == lastScreenHeight
            && orientation == lastOrientation)
        {
            return;
        }

        lastSafeArea = safeArea;
        lastScreenWidth = width;
        lastScreenHeight = height;
        lastOrientation = orientation;

        ApplySafeArea(safeArea, width, height);
    }

    private void ApplySafeArea(Rect safeArea, int width, int height)
    {
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= width;
        anchorMin.y /= height;
        anchorMax.x /= width;
        anchorMax.y /= height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
