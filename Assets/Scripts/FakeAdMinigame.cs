using UnityEngine;
using UnityEngine.UI;

// Fake-ad minigame: shows a random ad image and a close button that jumps to a
// new random spot each time it's tapped. After it's been tapped a set number of
// times, the ad closes (win). Overlay, win-only.
public class FakeAdMinigame : MinigameBase
{
    [Header("Ad background")]
    [Tooltip("The Image that displays the ad. A random sprite from Ad Images is assigned on start.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Pool of ad images to pick from at random.")]
    [SerializeField] private Sprite[] adImages;

    [Header("Close button")]
    [SerializeField] private Button closeButton;

    [Tooltip("Area the button is allowed to move within. Defaults to the button's parent.")]
    [SerializeField] private RectTransform moveArea;

    [Tooltip("How many taps on the close button are needed before the ad closes (at difficulty 0).")]
    [Min(1)]
    [SerializeField] private int requiredClicks = 5;

    [Tooltip("Required taps at difficulty 1. Set this LOWER than Required Clicks so a high-difficulty ad needs fewer taps and stays beatable when you enter it on low dopamine. The count scales between Required Clicks and this by the minigame's difficulty.")]
    [Min(1)]
    [SerializeField] private int requiredClicksAtMaxDifficulty = 3;

    [Tooltip("How long (seconds) the button takes to glide to its new spot (at difficulty 0).")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.25f;

    [Tooltip("Glide time at difficulty 1 (a snappier, harder-to-catch button). Scales between this and Move Duration by difficulty.")]
    [Min(0f)]
    [SerializeField] private float moveDurationAtMaxDifficulty = 0.12f;

    [Header("Dopamine drain (while this ad is up)")]
    [Tooltip("Dopamine drained per second while the ad is up, at difficulty 0.")]
    [Min(0f)]
    [SerializeField] private float drainPerSecond = 6f;

    [Tooltip("Dopamine drain per second at difficulty 1. Set this LOWER than Drain Per Second so a high-difficulty ad drains less, keeping it beatable. Scales between Drain Per Second and this by difficulty.")]
    [Min(0f)]
    [SerializeField] private float drainPerSecondAtMaxDifficulty = 3f;

    [Tooltip("The DopamineManager this ad drives its drain on. Auto-found if left empty.")]
    [SerializeField] private DopamineManager dopamineManager;

    [Header("Feedback")]
    [Tooltip("Optional. Bursts particles at the close button every time it's tapped. Leave empty for none.")]
    [SerializeField] private HeartParticleSpawner closeParticles;

    [Header("Sound")]
    [Tooltip("Source the click sounds play through. Auto-added if left empty.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Sounds for a click that doesn't close the ad yet (the button runs away).")]
    [SerializeField] private SoundBank missedClickSounds;
    [Tooltip("Sounds for the final click that closes the ad.")]
    [SerializeField] private SoundBank successClickSounds;

    private RectTransform buttonRect;
    private int clicks;

    // Difficulty-scaled values, resolved in StartGame.
    private int activeRequiredClicks;
    private float activeMoveDuration;

    // Smooth-move state.
    private Vector2 moveStart;
    private Vector2 moveTarget;
    private float moveT;
    private bool moving;

    public override void StartGame(MinigameContext context)
    {
        // Pick a random ad image.
        if (backgroundImage != null && adImages != null && adImages.Length > 0)
        {
            backgroundImage.sprite = adImages[Random.Range(0, adImages.Length)];
        }

        clicks = 0;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        // Difficulty scales the tap count, button speed and drain (each between its
        // own difficulty-0 and difficulty-1 setting).
        float d = Mathf.Clamp01(context.difficulty);
        activeRequiredClicks = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(requiredClicks, requiredClicksAtMaxDifficulty, d)));
        activeMoveDuration = Mathf.Max(0f, Mathf.Lerp(moveDuration, moveDurationAtMaxDifficulty, d));

        // Push this ad's own dopamine drain for the current difficulty. The
        // DopamineManager clears it again when the minigame finishes.
        if (dopamineManager == null)
        {
            dopamineManager = FindFirstObjectByType<DopamineManager>();
        }
        if (dopamineManager != null)
        {
            dopamineManager.SetMinigameDrainOverride(Mathf.Lerp(drainPerSecond, drainPerSecondAtMaxDifficulty, d));
        }

        if (closeButton != null)
        {
            buttonRect = closeButton.transform as RectTransform;
            if (moveArea == null && buttonRect != null)
            {
                moveArea = buttonRect.parent as RectTransform;
            }

            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void OnCloseClicked()
    {
        clicks++;

        // Fire particles at the button's current spot on every tap (including the
        // final, winning one).
        if (closeParticles != null && closeButton != null)
        {
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, closeButton.transform.position);
            closeParticles.Burst(screenPos);
        }

        if (clicks >= activeRequiredClicks)
        {
            successClickSounds.PlayOneShot(audioSource);
            closeButton.onClick.RemoveListener(OnCloseClicked);
            Win();
            return;
        }

        missedClickSounds.PlayOneShot(audioSource);
        MoveButton();
    }

    void Update()
    {
        if (!moving || buttonRect == null)
        {
            return;
        }

        moveT += Time.deltaTime / Mathf.Max(0.0001f, activeMoveDuration);
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveT));
        buttonRect.anchoredPosition = Vector2.LerpUnclamped(moveStart, moveTarget, k);

        if (moveT >= 1f)
        {
            moving = false;
        }
    }

    // Picks a new random spot inside the move area (keeping the button fully on
    // screen) and starts gliding toward it. Assumes the button is centre-anchored.
    private void MoveButton()
    {
        if (buttonRect == null || moveArea == null)
        {
            return;
        }

        Vector2 area = moveArea.rect.size;
        Vector2 btn = buttonRect.rect.size;
        float halfX = Mathf.Max(0f, (area.x - btn.x) * 0.5f);
        float halfY = Mathf.Max(0f, (area.y - btn.y) * 0.5f);

        Vector2 target = new Vector2(Random.Range(-halfX, halfX), Random.Range(-halfY, halfY));

        if (activeMoveDuration <= 0f)
        {
            buttonRect.anchoredPosition = target;
            moving = false;
            return;
        }

        moveStart = buttonRect.anchoredPosition;
        moveTarget = target;
        moveT = 0f;
        moving = true;
    }
}
