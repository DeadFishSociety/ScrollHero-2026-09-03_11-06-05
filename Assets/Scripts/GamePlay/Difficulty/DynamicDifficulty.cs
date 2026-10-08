using System;
using UnityEngine;

// Scales the game's difficulty up as the player's score climbs. Add this to the
// Feed object (next to ReelFeedController / MinigameManager). It watches the
// ScoreManager, turns the score into a 0..1 progress value, and pushes that into
// the other systems:
//
//   - Minigames spawn more often.
//   - The dopamine bar drains faster, and swipe/like top-ups are worth less.
//   - Each minigame plays harder (per-minigame difficulty range below).
//
// Everything is inspector-tunable and only takes effect while this component is
// enabled - disabling it returns the game to its base (score-0) difficulty.
public class DynamicDifficulty : MonoBehaviour
{
    // Per-minigame difficulty ramp. Its difficulty goes from Difficulty At Min
    // Score (at score 0) to Difficulty At Max Score (at Score For Max Difficulty).
    [Serializable]
    public class MinigameDifficulty
    {
        [Tooltip("The minigame prefab - the same one listed on the MinigameManager.")]
        public GameObject prefab;

        [Range(0f, 1f)] public float difficultyAtMinScore = 0f;
        [Range(0f, 1f)] public float difficultyAtMaxScore = 1f;
    }

    [Header("References (optional - auto-found if empty)")]
    [SerializeField] private MinigameManager minigameManager;
    [SerializeField] private DopamineManager dopamineManager;

    [Header("Score -> difficulty")]
    [Tooltip("Score at which difficulty reaches its maximum. Below this it ramps up; above it stays maxed.")]
    [SerializeField] private int scoreForMaxDifficulty = 500;

    [Tooltip("Shape of the ramp from score 0 (left) to Score For Max Difficulty (right). Linear by default.")]
    [SerializeField] private AnimationCurve difficultyOverScore = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Minigame spawn frequency")]
    [Tooltip("Extra spawn chance per reel added at max difficulty (on top of the MinigameManager's base chance).")]
    [Range(0f, 1f)]
    [SerializeField] private float extraSpawnChanceAtMax = 0.4f;

    [Header("Dopamine drain")]
    [Tooltip("Drain rate multiplier at max difficulty (1 = unchanged). The bar empties faster as score climbs.")]
    [Min(0f)]
    [SerializeField] private float drainMultiplierAtMax = 2.5f;

    [Header("Dopamine gains")]
    [Tooltip("Swipe/like top-up multiplier at max difficulty (1 = unchanged, <1 = smaller gains).")]
    [Min(0f)]
    [SerializeField] private float gainMultiplierAtMax = 0.5f;

    [Header("Per-minigame difficulty")]
    [Tooltip("Difficulty ramp per minigame. Listed minigames use their own min->max range.")]
    [SerializeField] private MinigameDifficulty[] minigames;

    [Tooltip("On: minigames not listed above ramp uniformly with score (0->1). Off: they use their own authored Difficulty.")]
    [SerializeField] private bool scaleUnlistedMinigames = true;

    // Current 0..1 difficulty progress, recomputed whenever the score changes.
    private float progress;

    // --- Read-only accessors (used by the debug overlay) ---------------------

    // Current 0..1 difficulty progress.
    public float Progress => progress;

    // Score at which difficulty maxes out.
    public int ScoreForMaxDifficulty => scoreForMaxDifficulty;

    // The live values being pushed into the other systems.
    public float CurrentExtraSpawnChance => extraSpawnChanceAtMax * progress;
    public float CurrentDrainMultiplier => Mathf.Lerp(1f, drainMultiplierAtMax, progress);
    public float CurrentGainMultiplier => Mathf.Lerp(1f, gainMultiplierAtMax, progress);

    // The configured per-minigame ramps, for display.
    public MinigameDifficulty[] Minigames => minigames;
    public bool ScaleUnlistedMinigames => scaleUnlistedMinigames;

    // The difficulty a listed minigame is currently at (for the debug list).
    public float CurrentDifficultyOf(MinigameDifficulty entry)
    {
        return entry != null
            ? Mathf.Lerp(entry.difficultyAtMinScore, entry.difficultyAtMaxScore, progress)
            : 0f;
    }

    private void Awake()
    {
        if (minigameManager == null)
        {
            minigameManager = FindFirstObjectByType<MinigameManager>();
        }
        if (dopamineManager == null)
        {
            dopamineManager = FindFirstObjectByType<DopamineManager>();
        }

        // Own the minigame difficulty hook while we're around.
        if (minigameManager != null)
        {
            minigameManager.DifficultyOverride = ResolveMinigameDifficulty;
        }
    }

    private void OnEnable()
    {
        ScoreManager.OnScoreChanged += OnScoreChanged;
        // Apply immediately for the current score (the event may have already fired).
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
        OnScoreChanged(score);
    }

    private void OnDisable()
    {
        ScoreManager.OnScoreChanged -= OnScoreChanged;

        // Hand everything back to its neutral, base-difficulty state so a disabled
        // component leaves no lingering scaling behind.
        if (minigameManager != null)
        {
            minigameManager.ExtraChance = 0f;
            if (minigameManager.DifficultyOverride == ResolveMinigameDifficulty)
            {
                minigameManager.DifficultyOverride = null;
            }
        }
        if (dopamineManager != null)
        {
            dopamineManager.DrainMultiplier = 1f;
            dopamineManager.GainMultiplier = 1f;
        }
    }

    private void OnScoreChanged(int score)
    {
        progress = ComputeProgress(score);

        if (minigameManager != null)
        {
            minigameManager.ExtraChance = extraSpawnChanceAtMax * progress;
        }
        if (dopamineManager != null)
        {
            dopamineManager.DrainMultiplier = Mathf.Lerp(1f, drainMultiplierAtMax, progress);
            dopamineManager.GainMultiplier = Mathf.Lerp(1f, gainMultiplierAtMax, progress);
        }
        // Per-minigame difficulty is pulled on demand via ResolveMinigameDifficulty
        // when a minigame spawns, using the latest progress.
    }

    // Turns a raw score into a 0..1 difficulty via the ramp curve.
    private float ComputeProgress(int score)
    {
        float normalized = Mathf.Clamp01(score / (float)Mathf.Max(1, scoreForMaxDifficulty));
        return Mathf.Clamp01(difficultyOverScore.Evaluate(normalized));
    }

    // MinigameManager's DifficultyOverride hook: the difficulty a given prefab
    // should play at right now. A listed minigame uses its own min->max range.
    // An unlisted one ramps uniformly with score, or (if Scale Unlisted Minigames
    // is off) returns -1 so the manager falls back to its authored Difficulty.
    private float ResolveMinigameDifficulty(GameObject prefab)
    {
        if (minigames != null)
        {
            for (int i = 0; i < minigames.Length; i++)
            {
                MinigameDifficulty entry = minigames[i];
                if (entry != null && entry.prefab == prefab)
                {
                    return Mathf.Lerp(entry.difficultyAtMinScore, entry.difficultyAtMaxScore, progress);
                }
            }
        }
        return scaleUnlistedMinigames ? progress : -1f;
    }
}
