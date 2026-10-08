using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays a looping spritesheet animation on a UI Image while the object is active.
// Because it starts in OnEnable and stops in OnDisable, it pairs perfectly with an
// overlay the feed shows/hides: e.g. the Reel's OverdriveOverlay animates only
// while that reel is the overdrive reel.
[RequireComponent(typeof(Image))]
public class UISpriteAnimation : MonoBehaviour
{
    [Tooltip("Frames played in order.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Playback speed in frames per second.")]
    [SerializeField] private float fps = 12f;

    [Tooltip("Loop forever, or play once and hold the last frame.")]
    [SerializeField] private bool loop = true;

    [Tooltip("Keep each frame's aspect ratio instead of stretching to the Image rect.")]
    [SerializeField] private bool preserveAspect = true;

    [Tooltip("Fill the parent with no letterbox edges by stretching only the shorter axis (the other axis stays at scale 1, so nothing is cropped). The RectTransform should be stretched to its parent.")]
    [SerializeField] private bool coverParent = false;

    [Tooltip("Use unscaled time so the animation keeps running if the game is paused/time-scaled.")]
    [SerializeField] private bool useUnscaledTime = true;

    private Image image;
    private Coroutine routine;

    private void Awake()
    {
        image = GetComponent<Image>();
        // Cover needs uniform scaling, so preserve aspect is forced on in that mode.
        image.preserveAspect = preserveAspect || coverParent;
    }

    private void LateUpdate()
    {
        if (coverParent)
        {
            ApplyCover();
        }
    }

    // Scales the (stretched-to-parent) RectTransform up uniformly so the sprite
    // covers the parent rect, cropping the overflowing axis off-screen.
    private void ApplyCover()
    {
        RectTransform rt = transform as RectTransform;
        RectTransform parent = rt != null ? rt.parent as RectTransform : null;
        Sprite sprite = image != null ? image.sprite : null;
        if (rt == null || parent == null || sprite == null)
        {
            return;
        }

        float pw = parent.rect.width;
        float ph = parent.rect.height;
        float sh = sprite.rect.height;
        if (pw <= 0f || ph <= 0f || sh <= 0f)
        {
            return;
        }

        float spriteAspect = sprite.rect.width / sh;
        float parentAspect = pw / ph;

        // Fitting inside leaves a gap on one axis; stretch only that axis to fill it
        // (the other axis already fills the parent, so it stays at scale 1 - no crop).
        if (spriteAspect > parentAspect)
        {
            // Fits to width -> fill the vertical gap.
            rt.localScale = new Vector3(1f, spriteAspect / parentAspect, 1f);
        }
        else
        {
            // Fits to height -> fill the horizontal gap.
            rt.localScale = new Vector3(parentAspect / spriteAspect, 1f, 1f);
        }
    }

    private void OnEnable()
    {
        if (frames != null && frames.Length > 0)
        {
            routine = StartCoroutine(Play());
        }
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator Play()
    {
        float frameDuration = 1f / Mathf.Max(1f, fps);
        int i = 0;

        while (true)
        {
            image.sprite = frames[i];

            i++;
            if (i >= frames.Length)
            {
                if (!loop)
                {
                    break;
                }
                i = 0;
            }

            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            else
            {
                yield return new WaitForSeconds(frameDuration);
            }
        }

        routine = null;
    }
}
