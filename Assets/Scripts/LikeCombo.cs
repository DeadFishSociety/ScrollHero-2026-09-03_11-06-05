using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rewards the player for liking reels on a streak. Every reel must be liked
// before it is scrolled away; do that for enough reels in a row and a combo
// activates: a text label, a spritesheet animation and a sound all fire, and the
// sound pitches up as the streak climbs. Scrolling past a reel that wasn't liked
// (including reels flicked past on a fast swipe) breaks the streak.
//
// Drives off the same two signals FeedScorer uses, so no feed changes are needed:
//   - ReelLike.AnyReelLiked      : the current reel was liked.
//   - ReelFeedController.ScrollCommitted : a reel was scrolled away (once per reel).
//
// Put this anywhere in the scene (e.g. next to the combo HUD on the Canvas) and
// wire the feed and HUD references in the Inspector.
public class LikeCombo : MonoBehaviour
{
    [Header("Feed")]
    [Tooltip("The feed to listen to. Leave empty to find the one in the scene automatically.")]
    [SerializeField] private ReelFeedController feed;

    [Header("Combo")]
    [Tooltip("How many liked-and-scrolled reels in a row are needed before the combo activates.")]
    [SerializeField] private int activationThreshold = 3;

    [Tooltip("Highest the combo can climb to. The streak stops rising once it reaches this (e.g. 10 = max 10x).")]
    [SerializeField] private int maxCombo = 10;

    [Header("Label")]
    [Tooltip("Text that shows the current combo, e.g. \"x3\". Hidden until the combo activates.")]
    [SerializeField] private TMP_Text comboLabel;

    [Tooltip("Format for the combo text. {0} is the combo count.")]
    [SerializeField] private string labelFormat = "x{0}";

    [Tooltip("How long the label takes to pop when the combo increases.")]
    [SerializeField] private float popDuration = 0.25f;

    [Tooltip("Bounciness of the label pop (higher = more overshoot).")]
    [SerializeField] private float popOvershoot = 2f;

    [Header("Spritesheet animation")]
    [Tooltip("Image the combo animation frames are played on. Hidden when idle. Optional.")]
    [SerializeField] private Image comboAnimationImage;

    [Tooltip("Frames of the combo spritesheet animation, played in order (e.g. slices of 64_32_COMBO_VISUAL).")]
    [SerializeField] private Sprite[] comboAnimationFrames;

    [Tooltip("Playback speed of the spritesheet animation, in frames per second.")]
    [SerializeField] private float comboAnimationFps = 24f;

    [Header("Sound")]
    [Tooltip("Sound played each time the combo triggers. Leave empty for none.")]
    [SerializeField] private AudioClip comboSound;

    [Tooltip("Source for the combo sound. Defaults to an AudioSource on this object (added if missing).")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Pitch of the sound at the moment the combo first activates.")]
    [SerializeField] private float basePitch = 1f;

    [Tooltip("How much the pitch rises for each combo step past activation.")]
    [SerializeField] private float pitchStep = 0.1f;

    [Tooltip("Highest pitch the sound is allowed to reach.")]
    [SerializeField] private float maxPitch = 2f;

    // Current run of consecutive liked-and-scrolled reels.
    private int streak = 0;

    // Whether the reel currently in view has been liked yet.
    private bool currentReelLiked = false;

    private Vector3 labelBaseScale = Vector3.one;
    private Coroutine popRoutine;
    private Coroutine animRoutine;

    private bool ComboActive => streak >= activationThreshold;

    // True once the streak has reached the activation threshold.
    public bool IsActive => ComboActive;

    // The current combo count (the "xN" shown on the label).
    public int Combo => streak;

    // Score multiplier the combo currently applies: the combo count while active
    // (so it matches the "xN" label), or 1 when no combo is running.
    public float ScoreMultiplier => ComboActive ? streak : 1f;

    private void Awake()
    {
        if (feed == null)
        {
            feed = FindObjectOfType<ReelFeedController>();
        }

        if (comboLabel != null)
        {
            labelBaseScale = comboLabel.transform.localScale;
            comboLabel.gameObject.SetActive(false);
        }
        if (comboAnimationImage != null)
        {
            comboAnimationImage.gameObject.SetActive(false);
        }
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
        }
    }

    private void OnEnable()
    {
        ReelLike.AnyReelLiked += OnReelLiked;
        if (feed != null)
        {
            feed.ScrollCommitted += OnScrollCommitted;
        }
    }

    private void OnDisable()
    {
        ReelLike.AnyReelLiked -= OnReelLiked;
        if (feed != null)
        {
            feed.ScrollCommitted -= OnScrollCommitted;
        }
    }

    // A reel was liked. Remember it so the next scroll extends the streak.
    private void OnReelLiked()
    {
        currentReelLiked = true;
    }

    // A reel was scrolled away. Extend the streak if it was liked, otherwise break.
    private void OnScrollCommitted()
    {
        if (currentReelLiked)
        {
            // Grow the streak, but never past the max combo.
            streak = Mathf.Min(streak + 1, maxCombo);
            if (ComboActive)
            {
                TriggerCombo();
            }
        }
        else
        {
            if (ComboActive)
            {
                HideCombo();
            }
            streak = 0;
        }

        // The newly shown reel hasn't been liked yet.
        currentReelLiked = false;
    }

    // Shows/refreshes the HUD and plays the animation + pitched sound. Called on
    // each liked reel once the streak has reached the activation threshold.
    private void TriggerCombo()
    {
        if (comboLabel != null)
        {
            comboLabel.gameObject.SetActive(true);
            comboLabel.text = string.Format(labelFormat, streak);

            if (popDuration > 0f)
            {
                if (popRoutine != null)
                {
                    StopCoroutine(popRoutine);
                }
                popRoutine = StartCoroutine(PopLabel());
            }
        }

        if (comboAnimationImage != null && comboAnimationFrames != null && comboAnimationFrames.Length > 0)
        {
            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
            }
            animRoutine = StartCoroutine(PlayAnimation());
        }

        PlaySound();
    }

    // Plays the combo sound, pitched up by how far the streak is past activation.
    private void PlaySound()
    {
        if (comboSound == null || sfxSource == null)
        {
            return;
        }

        int stepsPastActivation = streak - activationThreshold;
        float pitch = Mathf.Min(basePitch + stepsPastActivation * pitchStep, maxPitch);

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(comboSound);
    }

    // Hides the HUD when the combo breaks.
    private void HideCombo()
    {
        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
            popRoutine = null;
        }
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }

        if (comboLabel != null)
        {
            comboLabel.transform.localScale = labelBaseScale;
            comboLabel.gameObject.SetActive(false);
        }
        if (comboAnimationImage != null)
        {
            comboAnimationImage.gameObject.SetActive(false);
        }
    }

    // Scales the label up with a springy overshoot so it "pops" on each step.
    private IEnumerator PopLabel()
    {
        Transform t = comboLabel.transform;
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / popDuration);
            t.localScale = labelBaseScale * EaseOutBack(k, popOvershoot);
            yield return null;
        }
        t.localScale = labelBaseScale;
        popRoutine = null;
    }

    private IEnumerator PlayAnimation()
    {
        comboAnimationImage.gameObject.SetActive(true);

        float frameDuration = 1f / Mathf.Max(1f, comboAnimationFps);
        for (int i = 0; i < comboAnimationFrames.Length; i++)
        {
            comboAnimationImage.sprite = comboAnimationFrames[i];
            yield return new WaitForSeconds(frameDuration);
        }

        comboAnimationImage.gameObject.SetActive(false);
        animRoutine = null;
    }

    // Overshooting ease: 0 -> 1, popping slightly past 1 before settling.
    private static float EaseOutBack(float x, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot);
        float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
