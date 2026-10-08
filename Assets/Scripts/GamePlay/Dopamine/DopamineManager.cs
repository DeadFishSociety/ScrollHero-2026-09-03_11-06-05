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

    // Fired with (current lives, max lives): on start, whenever a life is lost, and
    // when overdrive sets/resets the life count. Drives the lives display, bar and
    // vignette. This is a "count changed" signal, NOT necessarily damage.
    public static event Action<int, int> OnLivesChanged;

    // Fired only when a life is actually lost (dopamine emptied). Separate from
    // OnLivesChanged so the lives display can play its damage flash here without
    // also flashing on the overdrive reset (3 -> 2).
    public static event Action OnLifeLost;

    // Fired once dopamine empties with no lives left. The real game-over trigger.
    public event Action Depleted;

    [Header("Level")]
    [SerializeField] private float maximumDopamine = 100f;
    [Tooltip("Dopamine the player starts a session with. Also the amount it refills to when a life is lost.")]
    [SerializeField] private float startDopamine = 100f;

    [Header("Lives")]
    [Tooltip("Lives the player starts with. Each dopamine empty costs one; the run ends at 0.")]
    [Min(1)]
    [SerializeField] private int startLives = 2;

    [Tooltip("Maximum lives. Overdrive temporarily lifts the player to this, then resets back to Start Lives.")]
    [Min(1)]
    [SerializeField] private int maxLives = 3;

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
    private int lives;
    private bool inMinigame;
    private float activeMinigameDifficulty;
    private bool gameOver;

    // Overdrive: a timed bonus round where dopamine only drains (set by the
    // OverdriveController). While active, gains are ignored and emptying the bar
    // does NOT cost a life.
    private bool overdrive;
    private float overdriveDrainPerSecond;

    // Cheat "scroll mode": captured once on load so the run never drains dopamine.
    private bool noDrain;

    // Current, start and max lives, for late-subscribing listeners.
    public int Lives => lives;
    public int StartLives => startLives;
    public int MaxLives => maxLives;
    public bool IsOverdrive => overdrive;

    // True when the player is on their last life (the next loss ends the run).
    public bool IsLastLife => !gameOver && lives <= 1;

    // Optional per-minigame drain (per second) set by the active minigame. When
    // set it replaces the difficulty-based minigame drain for that minigame, and
    // is cleared automatically when the minigame finishes.
    private bool hasMinigameDrainOverride;
    private float minigameDrainOverride;

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
        maxLives = Mathf.Max(1, maxLives);
        lives = Mathf.Clamp(startLives, 1, maxLives);

        // Consume the cheat flag: this run honours it, but it won't leak into the
        // next (normal) run because we reset it here.
        noDrain = CheatState.NoDopamineDrain;
        CheatState.NoDopamineDrain = false;
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
        OnLivesChanged?.Invoke(lives, maxLives);
    }

    private void Update()
    {
        // Once out of lives the run is over; stop draining so the level stays at zero.
        if (gameOver)
        {
            return;
        }

        // Overdrive: drain steadily toward empty over the round (acts as the timer
        // visual). No gains, and emptying doesn't cost a life (see UpdateDopamineLevel).
        if (overdrive)
        {
            if (overdriveDrainPerSecond > 0f)
            {
                UpdateDopamineLevel(-overdriveDrainPerSecond * Time.deltaTime);
            }
            return;
        }

        // Cheat "scroll mode": never drain (gains still work, so the bar can only rise).
        if (noDrain)
        {
            return;
        }

        // Dopamine always drains: during a minigame at its own rate, otherwise the
        // steady idle rate.
        float drainPerSecond;
        if (inMinigame)
        {
            // A minigame can set its own drain (see SetMinigameDrainOverride);
            // otherwise it scales with the minigame's difficulty. The global
            // dynamic-difficulty DrainMultiplier is deliberately NOT applied here,
            // so it can't stack on top of the minigame's own (already scaled) drain
            // and make a hard minigame impossible when entered on low dopamine.
            drainPerSecond = hasMinigameDrainOverride
                ? minigameDrainOverride
                : Mathf.Lerp(minigameDrainAtEasy, minigameDrainAtHard, Mathf.Clamp01(activeMinigameDifficulty));
        }
        else
        {
            drainPerSecond = idleDrainPerSecond * Mathf.Max(0f, DrainMultiplier);
        }

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
        hasMinigameDrainOverride = false;
    }

    // --- Level plumbing ------------------------------------------------------

    private void UpdateDopamineLevel(float amount)
    {
        // No more changes once the run has ended.
        if (gameOver)
        {
            return;
        }

        dopamineLevel = Mathf.Clamp(dopamineLevel + amount, 0f, maximumDopamine);
        OnDopamineChange?.Invoke(Fraction());

        // During overdrive the bar can sit at zero without ending the round.
        if (dopamineLevel <= 0f && !overdrive)
        {
            LoseLife();
        }
    }

    // Called the moment dopamine empties. Costs a life; if any remain, the bar
    // refills and the run continues, otherwise it's game over.
    private void LoseLife()
    {
        lives = Mathf.Max(0, lives - 1);
        OnLivesChanged?.Invoke(lives, maxLives);
        OnLifeLost?.Invoke(); // damage signal (distinct from the overdrive reset)

        if (lives > 0)
        {
            // Refill and keep the run going (this is what resets the vignette).
            dopamineLevel = Mathf.Clamp(startDopamine, 0f, maximumDopamine);
            OnDopamineChange?.Invoke(Fraction());
        }
        else
        {
            gameOver = true;
            Depleted?.Invoke();
        }
    }

    // --- Overdrive -----------------------------------------------------------

    // Starts the overdrive bonus round: jump to max lives and drain a full bar over
    // `duration` seconds (as the countdown visual). Called by OverdriveController.
    public void EnterOverdrive(float duration)
    {
        if (overdrive || gameOver)
        {
            return;
        }

        overdrive = true;
        overdriveDrainPerSecond = duration > 0f ? maximumDopamine / duration : 0f;

        lives = maxLives;
        OnLivesChanged?.Invoke(lives, maxLives);

        dopamineLevel = maximumDopamine;
        OnDopamineChange?.Invoke(Fraction());
    }

    // Ends overdrive: refill to full and drop back to the starting lives.
    public void ExitOverdrive()
    {
        if (!overdrive)
        {
            return;
        }

        overdrive = false;

        lives = Mathf.Clamp(startLives, 1, maxLives);
        OnLivesChanged?.Invoke(lives, maxLives);

        dopamineLevel = maximumDopamine;
        OnDopamineChange?.Invoke(Fraction());
    }

    // Liking a despair reel: lose a life (refilling the bar, like a normal life
    // loss). On the last life, halve the current dopamine instead so it can't be an
    // instant game over. Called by DespairController.
    public void ApplyDespair()
    {
        if (gameOver || overdrive)
        {
            return;
        }

        if (lives > 1)
        {
            LoseLife(); // decrement + damage flash + refill (same as a normal loss)
        }
        else
        {
            // Last life: halve whatever's currently in the bar.
            dopamineLevel = Mathf.Clamp(dopamineLevel * 0.5f, 0f, maximumDopamine);
            OnDopamineChange?.Invoke(Fraction());
        }
    }

    public void AddDopamine(float amount)
    {
        // No top-ups during overdrive: the bar only drains.
        if (overdrive)
        {
            return;
        }
        UpdateDopamineLevel(amount);
    }

    // Lets the active minigame set its own dopamine drain (per second) for as long
    // as it is on screen, replacing the difficulty-based minigame drain. Cleared
    // automatically when the minigame finishes.
    public void SetMinigameDrainOverride(float perSecond)
    {
        minigameDrainOverride = Mathf.Max(0f, perSecond);
        hasMinigameDrainOverride = true;
    }

    public void ClearMinigameDrainOverride()
    {
        hasMinigameDrainOverride = false;
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
