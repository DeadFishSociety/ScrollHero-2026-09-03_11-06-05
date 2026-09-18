using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Handles "liking" a reel. Tapping the LikeButton, OR double-tapping the reel,
// likes it: the button is hidden, the filled Heart icon is revealed, a burst of
// heart particles plays, an optional spritesheet animation plays near the heart,
// and an optional sound plays. Liking is one-way.
//
// Put this on the Reel root. Taps on the reel body bubble up to here (the button
// consumes its own taps), and scroll drags still bubble past to the feed.
public class ReelLike : MonoBehaviour, IPointerClickHandler
{
    // Fired whenever any reel is liked (button tap or double tap). Reels are
    // spawned dynamically, so a feed-level listener (e.g. FeedScorer) subscribes
    // here rather than wiring every reel. Follows DopamineManager's static-event
    // style.
    public static event Action AnyReelLiked;

    [Header("Like button / heart")]
    [Tooltip("The LikeButton. Its click likes the reel; it is hidden once liked.")]
    [SerializeField] private Button likeButton;

    [Tooltip("The filled Heart icon shown once liked. Hidden until then.")]
    [SerializeField] private GameObject heartIcon;

    [Tooltip("How long the heart takes to pop into view when liked.")]
    [SerializeField] private float heartPopDuration = 0.35f;

    [Tooltip("Bounciness of the heart pop (higher = more overshoot).")]
    [SerializeField] private float heartPopOvershoot = 1.7f;

    [Header("Double tap")]
    [Tooltip("Max seconds between two taps on the reel to count as a double tap (which also likes).")]
    [SerializeField] private float doubleTapInterval = 0.3f;

    [Header("Heart particles")]
    [Tooltip("Spawns the burst of heart particles on like.")]
    [SerializeField] private HeartParticleSpawner heartParticles;

    [Header("Spritesheet animation (near the heart)")]
    [Tooltip("Image the like animation frames are played on. Sits near the heart icon; hidden when idle.")]
    [SerializeField] private Image likeAnimationImage;

    [Tooltip("Frames of the like spritesheet animation, played in order.")]
    [SerializeField] private Sprite[] likeAnimationFrames;

    [Tooltip("Playback speed of the spritesheet animation, in frames per second.")]
    [SerializeField] private float likeAnimationFps = 24f;

    [Header("Sound")]
    [Tooltip("Sound played on like. Leave empty for none.")]
    [SerializeField] private AudioClip likeSound;

    [Tooltip("Source for the like sound. Defaults to the reel's own AudioSource (played via PlayOneShot, so it layers over the music).")]
    [SerializeField] private AudioSource sfxSource;

    private bool liked;
    private float lastTapTime = -1f;
    private Vector3 heartBaseScale = Vector3.one;

    void Awake()
    {
        // The heart starts hidden; the button is what's visible until liked.
        if (heartIcon != null)
        {
            heartBaseScale = heartIcon.transform.localScale;
            heartIcon.SetActive(false);
        }
        if (likeAnimationImage != null)
        {
            likeAnimationImage.gameObject.SetActive(false);
        }
        if (likeButton != null)
        {
            likeButton.onClick.RemoveListener(OnLikeButtonClicked);
            likeButton.onClick.AddListener(OnLikeButtonClicked);
        }
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }
    }

    // Single tap on the LikeButton itself.
    private void OnLikeButtonClicked()
    {
        Vector2 screenPos = likeButton != null
            ? RectTransformUtility.WorldToScreenPoint(null, likeButton.transform.position)
            : (Vector2)Input.mousePosition;
        Like(screenPos);
    }

    // Taps that reach the reel body (not the button): a double tap also likes.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (liked)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (lastTapTime > 0f && now - lastTapTime <= doubleTapInterval)
        {
            lastTapTime = -1f;
            Like(eventData.position);
        }
        else
        {
            lastTapTime = now;
        }
    }

    private void Like(Vector2 screenPosition)
    {
        if (liked)
        {
            return;
        }
        liked = true;

        if (likeButton != null)
        {
            likeButton.gameObject.SetActive(false);
        }
        if (heartIcon != null)
        {
            heartIcon.SetActive(true);
            if (heartPopDuration > 0f)
            {
                heartIcon.transform.localScale = Vector3.zero;
                StartCoroutine(PopHeart());
            }
        }

        AnyReelLiked?.Invoke();

        BurstParticles(screenPosition);

        if (likeAnimationImage != null && likeAnimationFrames != null && likeAnimationFrames.Length > 0)
        {
            StartCoroutine(PlayAnimation());
        }
        PlaySound();
    }

    // Emit hearts from the heart icon, and also from where the user tapped (the
    // two coincide for a button press, so that case bursts only once).
    private void BurstParticles(Vector2 screenPosition)
    {
        if (heartParticles == null)
        {
            return;
        }

        Vector2 heartPos = HeartScreenPosition();
        heartParticles.Burst(heartPos);

        if ((screenPosition - heartPos).sqrMagnitude > 1f)
        {
            heartParticles.Burst(screenPosition);
        }
    }

    private Vector2 HeartScreenPosition()
    {
        Transform t = heartIcon != null
            ? heartIcon.transform
            : (likeButton != null ? likeButton.transform : transform);
        return RectTransformUtility.WorldToScreenPoint(null, t.position);
    }

    // Scales the heart from nothing up to its normal size with a springy
    // overshoot so it "pops" into view.
    private IEnumerator PopHeart()
    {
        Transform t = heartIcon.transform;
        float elapsed = 0f;
        while (elapsed < heartPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / heartPopDuration);
            t.localScale = heartBaseScale * EaseOutBack(k, heartPopOvershoot);
            yield return null;
        }
        t.localScale = heartBaseScale;
    }

    // Overshooting ease: 0 -> 1, popping slightly past 1 before settling.
    private static float EaseOutBack(float x, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot);
        float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private IEnumerator PlayAnimation()
    {
        likeAnimationImage.gameObject.SetActive(true);

        float frameDuration = 1f / Mathf.Max(1f, likeAnimationFps);
        for (int i = 0; i < likeAnimationFrames.Length; i++)
        {
            likeAnimationImage.sprite = likeAnimationFrames[i];
            yield return new WaitForSeconds(frameDuration);
        }

        likeAnimationImage.gameObject.SetActive(false);
    }

    private void PlaySound()
    {
        if (likeSound == null)
        {
            return;
        }
        AudioSource source = sfxSource != null ? sfxSource : GetComponent<AudioSource>();
        if (source != null)
        {
            source.PlayOneShot(likeSound);
        }
    }
}
