using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays a one-shot spritesheet animation (with an optional sound) as a screen
// overlay every time the feed scrolls to a new reel. Each play appears at a
// random spot inside a configurable box in the middle of the screen.
//
// Add this to the Feed object (next to ReelFeedController), then assign the
// animation frames and, optionally, a sound.
[RequireComponent(typeof(ReelFeedController))]
public class ScrollOverlayAnimation : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Spritesheet frames, played in order.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Playback speed, in frames per second.")]
    [SerializeField] private float fps = 24f;

    [Tooltip("On-screen size of the animation, in canvas units.")]
    [SerializeField] private Vector2 size = new Vector2(300f, 300f);

    [SerializeField] private Color tint = Color.white;

    [Header("Sound")]
    [Tooltip("Sound played with the animation. Leave empty for none.")]
    [SerializeField] private AudioClip sound;

    [Tooltip("Source for the sound. Defaults to an AudioSource on this object (added if missing).")]
    [SerializeField] private AudioSource audioSource;

    [Header("Placement (fractions of the screen, 0..1)")]
    [Tooltip("Centre of the area the animation can appear in (0.5,0.5 = screen centre).")]
    [SerializeField] private Vector2 areaCenter = new Vector2(0.5f, 0.5f);

    [Tooltip("Size of that area as a fraction of the screen. Larger = more spread. (0 = always dead centre.)")]
    [SerializeField] private Vector2 areaSize = new Vector2(0.4f, 0.4f);

    [Header("Overlay")]
    [Tooltip("Parent for the spawned animation. Leave empty to use the canvas so it draws over everything.")]
    [SerializeField] private RectTransform overlayParent;

    [Tooltip("Also play once when the feed first loads (not only on scrolls).")]
    [SerializeField] private bool playOnStart = false;

    private ReelFeedController feed;

    void Awake()
    {
        feed = GetComponent<ReelFeedController>();

        if (overlayParent == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                overlayParent = canvas.transform as RectTransform;
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }

    void Start()
    {
        // ScrollCommitted only fires on actual scrolls, so play the initial one here.
        if (playOnStart)
        {
            Play();
        }
    }

    void OnEnable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted += Play;
        }
    }

    void OnDisable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted -= Play;
        }
    }

    [ContextMenu("Play Now")]
    public void Play()
    {
        if (overlayParent == null || frames == null || frames.Length == 0)
        {
            return;
        }

        GameObject go = new GameObject("ScrollOverlayAnim", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(overlayParent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = RandomPosition();
        rt.SetAsLastSibling(); // draw over everything

        Image img = go.GetComponent<Image>();
        img.color = tint;
        img.raycastTarget = false; // never block taps/swipes
        img.preserveAspect = true;

        if (sound != null && audioSource != null)
        {
            audioSource.PlayOneShot(sound);
        }

        StartCoroutine(Animate(go, img));
    }

    // Picks a random anchored position inside the middle box, clamped so the
    // centre stays on screen.
    private Vector2 RandomPosition()
    {
        Rect rect = overlayParent.rect;
        float halfW = Mathf.Abs(areaSize.x) * 0.5f;
        float halfH = Mathf.Abs(areaSize.y) * 0.5f;

        float fx = Mathf.Clamp01(areaCenter.x + Random.Range(-halfW, halfW));
        float fy = Mathf.Clamp01(areaCenter.y + Random.Range(-halfH, halfH));

        // 0..1 relative to the parent -> anchoredPosition measured from centre.
        return new Vector2((fx - 0.5f) * rect.width, (fy - 0.5f) * rect.height);
    }

    private IEnumerator Animate(GameObject go, Image img)
    {
        float frameDuration = 1f / Mathf.Max(1f, fps);
        for (int i = 0; i < frames.Length; i++)
        {
            img.sprite = frames[i];
            yield return new WaitForSeconds(frameDuration);
        }
        Destroy(go);
    }
}
