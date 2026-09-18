using System;
using UnityEngine;

// Owns the dopamine level that the TopUI DopamineBar displays. The level is a
// constant tug-of-war: it always drains, and the player tops it up by engaging
// with the feed.
//
//   - Every reel swipe adds a burst.
//   - Every like adds a burst.
//   - When idle, it drains at a steady rate.
//   - While a minigame is up, it drains faster - each minigame at its own rate,
//     scaled from that minigame's authored difficulty.
//
// Drives the bar through the same fraction events as before, so DopamineBar
// needs no changes. Add this to a live GameObject in the scene (the Feed object
// or a dedicated manager both work - it auto-finds the feed and minigame
// manager if you don't wire them).
public class DopamineManager : MonoBehaviour
{
    public static event Action<float> OnDopamineInitialized;
    public static event Action<float> OnDopamineChange;

    // Fired once, the moment dopamine hits zero. The game-over trigger.
    public event Action Depleted;

    [Header("Level")]
    [SerializeField] private float maximumDopamine = 100f;
    [Tooltip("Dopamine the player starts a session with.")]
    [SerializeField] private float startDopamine = 100f;

    [Header("Gains")]
    [Tooltip("Dopamine added each time a reel is swiped away.")]
    [SerializeField] private float swipeGain = 8f;
    [Tooltip("Dopamine added each time a reel is liked.")]
    [SerializeField] private float likeGain = 12f;

    [Header("Drain (per second)")]
    [Tooltip("Steady drain while the player is just scrolling/idle.")]
    [SerializeField] private float idleDrainPerSecond = 5f;
    [Tooltip("Minigame drain for a difficulty-0 (easiest) minigame.")]
    [SerializeField] private float minigameDrainAtEasy = 6f;
    [Tooltip("Minigame drain for a difficulty-1 (hardest) minigame.")]
    [SerializeField] private float minigameDrainAtHard = 20f;

    [Header("References (optional - auto-found if empty)")]
    [SerializeField] private ReelFeedController feed;
    [SerializeField] private MinigameManager minigameManager;

    // Dynamic-difficulty hooks (1 = no change). DynamicDifficulty scales these
    // with the player's score: drain faster and top-ups worth less over time.
    public float DrainMultiplier { get; set; } = 1f;
    public float GainMultiplier { get; set; } = 1f;

    private float dopamineLevel;
    private bool inMinigame;
    private float activeMinigameDifficulty;
    private bool depleted;

    private void Awake()
    {
        // Resolve references up front so OnEnable can subscribe to them.
        if (feed == null)
        {
            feed = FindFirstObjectByType<ReelFeedController>();
        }
        if (minigameManager == null)
        {
            minigameManager = FindFirstObjectByType<MinigameManager>();
        }

        dopamineLevel = Mathf.Clamp(startDopamine, 0f, maximumDopamine);
    }

    private void OnEnable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted += OnReelSwiped;
        }
        if (minigameManager != null)
        {
            minigameManager.MinigameStarted += OnMinigameStarted;
            minigameManager.MinigameFinished += OnMinigameFinished;
        }
        ReelLike.AnyReelLiked += OnReelLiked;
    }

    private void OnDisable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted -= OnReelSwiped;
        }
        if (minigameManager != null)
        {
            minigameManager.MinigameStarted -= OnMinigameStarted;
            minigameManager.MinigameFinished -= OnMinigameFinished;
        }
        ReelLike.AnyReelLiked -= OnReelLiked;
    }

    private void Start()
    {
        OnDopamineInitialized?.Invoke(Fraction());
    }

    private void Update()
    {
        // Dopamine always drains: faster during a minigame (by its difficulty),
        // otherwise the steady idle rate.
        float drainPerSecond = inMinigame
            ? Mathf.Lerp(minigameDrainAtEasy, minigameDrainAtHard, Mathf.Clamp01(activeMinigameDifficulty))
            : idleDrainPerSecond;
        drainPerSecond *= Mathf.Max(0f, DrainMultiplier);

        if (drainPerSecond != 0f)
        {
            UpdateDopamineLevel(-drainPerSecond * Time.deltaTime);
        }
    }

    // --- Event handlers ------------------------------------------------------

    private void OnReelSwiped()
    {
        AddDopamine(swipeGain * Mathf.Max(0f, GainMultiplier));
    }

    private void OnReelLiked()
    {
        AddDopamine(likeGain * Mathf.Max(0f, GainMultiplier));
    }

    private void OnMinigameStarted(float difficulty)
    {
        inMinigame = true;
        activeMinigameDifficulty = difficulty;
    }

    private void OnMinigameFinished(GameObject prefab, MinigameOutcome outcome)
    {
        inMinigame = false;
    }

    // --- Level plumbing ------------------------------------------------------

    private void UpdateDopamineLevel(float amount)
    {
        dopamineLevel = Mathf.Clamp(dopamineLevel + amount, 0f, maximumDopamine);
        OnDopamineChange?.Invoke(Fraction());

        if (!depleted && dopamineLevel <= 0f)
        {
            depleted = true;
            Depleted?.Invoke();
        }
    }

    public void AddDopamine(float amount)
    {
        UpdateDopamineLevel(amount);
    }

    public void RemoveDopamine(float amount)
    {
        UpdateDopamineLevel(-amount);
    }

    public float getDopamineLevel()
    {
        return dopamineLevel;
    }

    private float Fraction()
    {
        return maximumDopamine > 0f ? dopamineLevel / maximumDopamine : 0f;
    }
}
