using System.Collections;
using UnityEngine;

// Orchestrates the overdrive bonus round. It starts when the player likes the
// special overdrive reel (ReelLike.OverdriveLiked). For `duration` seconds it:
//   - jumps the player to max lives and drains a full dopamine bar as the timer,
//   - locks the score multiplier to a fixed value,
//   - suppresses minigames,
//   - shows an overlay and swaps in overdrive music,
// then reverts everything (lives back to start, dopamine full, music/overlay off).
//
// Put this on the Feed object (next to the other managers) and wire the overlay
// and music below.
public class OverdriveController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long overdrive lasts, in seconds.")]
    [Min(0.1f)]
    [SerializeField] private float duration = 10f;

    [Header("Scoring")]
    [Tooltip("Score multiplier locked in for the whole round.")]
    [SerializeField] private float lockedMultiplier = 15f;

    [Header("Presentation")]
    [Tooltip("Full-screen overlay shown during overdrive. Hidden otherwise.")]
    [SerializeField] private GameObject overlay;

    [Tooltip("Music that replaces the theme during overdrive. Leave empty to keep the theme.")]
    [SerializeField] private AudioClip music;

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.7f;

    [Header("References (auto-found if empty)")]
    [SerializeField] private DopamineManager dopamineManager;
    [SerializeField] private MusicPlayer musicPlayer;

    // Read by FeedScorer (locked multiplier) and MinigameManager (suppression).
    public static bool IsActive { get; private set; }
    public static float Multiplier { get; private set; } = 1f;

    private Coroutine routine;

    private void Awake()
    {
        if (dopamineManager == null)
        {
            dopamineManager = FindFirstObjectByType<DopamineManager>();
        }
        if (musicPlayer == null)
        {
            musicPlayer = FindFirstObjectByType<MusicPlayer>();
        }
        if (overlay != null)
        {
            overlay.SetActive(false);
        }

        // Reset static state (it persists across scene loads otherwise).
        IsActive = false;
        Multiplier = 1f;
    }

    private void OnEnable()
    {
        ReelLike.OverdriveLiked += Activate;
    }

    private void OnDisable()
    {
        ReelLike.OverdriveLiked -= Activate;

        // If we're torn down mid-round, don't leave the static flag stuck on.
        if (IsActive)
        {
            End();
        }
    }

    // Starts overdrive (ignored if already running).
    public void Activate()
    {
        if (IsActive)
        {
            return;
        }
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        IsActive = true;
        Multiplier = lockedMultiplier;

        if (overlay != null)
        {
            overlay.SetActive(true);
        }
        if (musicPlayer != null && music != null)
        {
            musicPlayer.PlayOverride(music, musicVolume);
        }
        if (dopamineManager != null)
        {
            dopamineManager.EnterOverdrive(duration);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        routine = null;
        End();
    }

    // Reverts everything overdrive changed. Safe to call more than once.
    private void End()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        IsActive = false;
        Multiplier = 1f;

        if (dopamineManager != null)
        {
            dopamineManager.ExitOverdrive();
        }
        if (musicPlayer != null)
        {
            musicPlayer.StopOverride();
        }
        if (overlay != null)
        {
            overlay.SetActive(false);
        }
    }
}
