using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Services;

// Shows the game-over overlay when the run ends (by default when the dopamine
// bar empties). Tracks how many reels were scrolled and how long the player
// survived, plays a sprite animation, fills in the score / reels / time labels,
// and saves + shows the high score.
//
// Put this on a persistent object in the scene (it tracks stats from the start)
// and wire the overlay pieces below. The overlay itself is a hidden child that
// this enables on game over - see the prefab notes.
public class GameOverController : MonoBehaviour
{
    [Header("Overlay")]
    [Tooltip("The overlay root, enabled on game over. Should start disabled in the scene.")]
    [SerializeField] private GameObject overlayRoot;

    [Tooltip("Retry button. Its click reloads the scene - wired automatically, no OnClick setup needed.")]
    [SerializeField] private Button retryButton;

    [Tooltip("Home button. Its click returns to the main menu - wired automatically, no OnClick setup needed.")]
    [SerializeField] private Button homeButton;

    [Tooltip("Scene loaded by the Home button.")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Sprite animation")]
    [Tooltip("Image the animation frames play on.")]
    [SerializeField] private Image animationImage;
    [Tooltip("Frames of the sprite animation, played in order.")]
    [SerializeField] private Sprite[] animationFrames;
    [Tooltip("Playback speed, frames per second.")]
    [SerializeField] private float animationFps = 12f;
    [Tooltip("Loop the animation, or play it once and hold the last frame.")]
    [SerializeField] private bool loopAnimation = true;

    [Header("Stat labels")]
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private TMP_Text reelsLabel;
    [SerializeField] private TMP_Text timeLabel;
    [SerializeField] private TMP_Text highScoreLabel;
    [Tooltip("Optional. Enabled only when this run beats the saved high score.")]
    [SerializeField] private GameObject newHighScoreBadge;

    [Header("Label formats ({0} is the value)")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [SerializeField] private string reelsFormat = "Reels: {0}";
    [SerializeField] private string timeFormat = "Time: {0}";
    [SerializeField] private string highScoreFormat = "Best: {0}";

    [Header("Sound")]
    [Tooltip("Played once when the game-over overlay appears. Layers over the music (the music keeps playing). Leave empty for none.")]
    [SerializeField] private AudioClip gameOverSound;

    [Range(0f, 1f)]
    [SerializeField] private float gameOverVolume = 1f;

    [Tooltip("Source the game-over sound plays through. Auto-added if left empty.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Behaviour")]
    [Tooltip("Freeze the game (Time.timeScale = 0) while the overlay is up.")]
    [SerializeField] private bool pauseOnGameOver = true;

    [Header("References (auto-found if empty)")]
    [SerializeField] private DopamineManager dopamineManager;
    [SerializeField] private ReelFeedController feed;

    private int reelsScrolled;
    private float startTime;
    private bool gameOver;

    private void Awake()
    {
        if (dopamineManager == null)
        {
            dopamineManager = FindFirstObjectByType<DopamineManager>();
        }
        if (feed == null)
        {
            feed = FindFirstObjectByType<ReelFeedController>();
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
        if (newHighScoreBadge != null)
        {
            newHighScoreBadge.SetActive(false);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RestartGame);
            retryButton.onClick.AddListener(RestartGame);
        }

        if (homeButton != null)
        {
            homeButton.onClick.RemoveListener(GoToMainMenu);
            homeButton.onClick.AddListener(GoToMainMenu);
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
        if (feed != null)
        {
            feed.ScrollCommitted += OnReelScrolled;
        }
        if (dopamineManager != null)
        {
            dopamineManager.Depleted += TriggerGameOver;
        }
    }

    private void OnDisable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted -= OnReelScrolled;
        }
        if (dopamineManager != null)
        {
            dopamineManager.Depleted -= TriggerGameOver;
        }
    }

    private void Start()
    {
        // Unscaled so a paused/ducked game doesn't distort the survival time.
        startTime = Time.unscaledTime;
    }

    private void OnReelScrolled()
    {
        if (!gameOver)
        {
            reelsScrolled++;
        }
    }

    // Ends the run and shows the overlay. Called by the dopamine Depleted event,
    // but public so any other fail state (e.g. losing a minigame) can call it too.
    [ContextMenu("Trigger Game Over")]
    public void TriggerGameOver()
    {
        if (gameOver)
        {
            return;
        }
        gameOver = true;

        float timeAlive = Time.unscaledTime - startTime;
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;

        int highScore = SaveDataService.Current.highscore;
        bool isNewHigh = score > highScore;
        if (isNewHigh)
        {
            highScore = score;
            SaveDataService.Update(s => s.highscore = score);
            _ = LeaderboardService.Save(score);
        }

        if (scoreLabel != null) scoreLabel.text = string.Format(scoreFormat, score);
        if (reelsLabel != null) reelsLabel.text = string.Format(reelsFormat, reelsScrolled);
        if (timeLabel != null) timeLabel.text = string.Format(timeFormat, FormatTime(timeAlive));
        if (highScoreLabel != null) highScoreLabel.text = string.Format(highScoreFormat, highScore);
        if (newHighScoreBadge != null) newHighScoreBadge.SetActive(isNewHigh);

        // Stop the feed from receiving input: Time.timeScale = 0 does NOT halt
        // pointer/drag events, so without this the player can keep scrolling the
        // paused feed (piling up dopamine and frozen scroll animations).
        if (feed != null)
        {
            feed.enabled = false;
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
        }

        // Game-over sting, layered over the music (which keeps playing).
        if (gameOverSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(gameOverSound, gameOverVolume);
        }

        if (animationImage != null && animationFrames != null && animationFrames.Length > 0)
        {
            StartCoroutine(PlayAnimation());
        }

        if (pauseOnGameOver)
        {
            Time.timeScale = 0f;
        }
    }

    // Reloads the current scene for a fresh run. Wired to retryButton, and also
    // public so you can call it from elsewhere.
    public void RestartGame()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    // Returns to the main menu, ending the current run. Wired to homeButton, and
    // also public so you can call it from elsewhere.
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // undo the game-over pause before leaving
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Plays the frames on unscaled time so it animates even while the game is
    // paused (Time.timeScale = 0).
    private IEnumerator PlayAnimation()
    {
        float frameDuration = 1f / Mathf.Max(1f, animationFps);
        do
        {
            for (int i = 0; i < animationFrames.Length; i++)
            {
                animationImage.sprite = animationFrames[i];
                yield return new WaitForSecondsRealtime(frameDuration);
            }
        }
        while (loopAnimation && gameOver);
    }

    // Seconds -> "m:ss".
    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return $"{minutes}:{secs:00}";
    }
}
