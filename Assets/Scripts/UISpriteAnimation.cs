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

    [Tooltip("Use unscaled time so the animation keeps running if the game is paused/time-scaled.")]
    [SerializeField] private bool useUnscaledTime = true;

    private Image image;
    private Coroutine routine;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.preserveAspect = preserveAspect;
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
