using UnityEngine;

/// <summary>
/// Locks a camera's viewport to a fixed target aspect ratio (default 1080x1920 = 9:16),
/// adding black bars around it — letterbox (top/bottom) on taller screens, pillarbox
/// (left/right) on wider ones. This keeps the game rendering at its design aspect on any
/// phone, so a Screen Space - Camera canvas scales uniformly and UI never stretches out
/// of bounds.
///
/// Setup:
///  1. Put this on the camera that renders your gameplay + UI.
///  2. Set that UI Canvas to Render Mode = Screen Space - Camera, with this camera as its
///     Render Camera, and its Canvas Scaler to Scale With Screen Size @ 1080x1920.
///  3. Add a SECOND camera behind this one (lower Depth) whose Clear Flags = Solid Color,
///     Background = black, so the bars are filled. The letterboxed camera only draws
///     inside its rect, so something else must paint the area outside it.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class AspectRatioLetterbox : MonoBehaviour
{
    [Tooltip("The design resolution the game is built for. The viewport is locked to this " +
             "aspect ratio; the rest of the screen is filled with black bars.")]
    [SerializeField] private Vector2 targetResolution = new Vector2(1080f, 1920f);

    private Camera cam;
    private int lastWidth;
    private int lastHeight;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    private void Update()
    {
        // Re-apply whenever the screen changes size — device rotation, split-screen, or
        // just resizing the Game view in the editor.
        if (Screen.width != lastWidth || Screen.height != lastHeight)
            Apply();
    }

    private void Apply()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
        if (cam == null || Screen.width == 0 || Screen.height == 0)
            return;

        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float targetAspect = targetResolution.x / targetResolution.y; // e.g. 0.5625 for 9:16
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Rect rect = new Rect(0f, 0f, 1f, 1f);

        if (scaleHeight < 1f)
        {
            // Screen is taller than the target — bars on top and bottom (letterbox).
            rect.height = scaleHeight;
            rect.y = (1f - scaleHeight) * 0.5f;
        }
        else
        {
            // Screen is wider than the target — bars on left and right (pillarbox).
            float scaleWidth = 1f / scaleHeight;
            rect.width = scaleWidth;
            rect.x = (1f - scaleWidth) * 0.5f;
        }

        cam.rect = rect;
    }
}
