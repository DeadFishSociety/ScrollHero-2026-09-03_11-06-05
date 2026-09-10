using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// The game-over UI: game-over text plus a restart button. Kept hidden until
/// <see cref="Show"/> is called (FeedManager calls it at game over). The panel fades
/// into view; once the fade finishes it runs the supplied callback (FeedManager uses
/// that to explode the dog only after the screen is fully visible). Restart reloads
/// the active scene for a full reset.
///
/// Put this component on an object that stays active for the whole scene, and point
/// <see cref="content"/> at the child panel that actually holds the text + button, so
/// this component's Awake runs and the button gets wired even while the panel is hidden.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Tooltip("The panel holding the game-over text + button. Hidden until Show().")]
    [SerializeField] private GameObject content;

    [Tooltip("Reloads the scene when clicked.")]
    [SerializeField] private Button restartButton;

    [Tooltip("How long the panel takes to fade into view. 0 = appear instantly.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.4f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);

        if (content != null)
        {
            // A CanvasGroup lets us fade the whole panel with one alpha. Add one if the
            // panel doesn't already have it, so the fade works without scene wiring.
            canvasGroup = content.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = content.AddComponent<CanvasGroup>();

            content.SetActive(false); // start hidden
        }
    }

    private void OnDestroy()
    {
        if (restartButton != null)
            restartButton.onClick.RemoveListener(Restart);
    }

    /// <summary>
    /// Fade the game-over text + button into view, then run <paramref name="onShown"/>
    /// once the fade completes (used to kick off the death explosion after the screen
    /// is fully visible).
    /// </summary>
    public void Show(System.Action onShown = null)
    {
        if (content == null)
        {
            onShown?.Invoke();
            return;
        }

        content.SetActive(true);

        if (canvasGroup == null || fadeDuration <= 0f)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            onShown?.Invoke();
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn(onShown));
    }

    private IEnumerator FadeIn(System.Action onShown)
    {
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeRoutine = null;
        onShown?.Invoke();
    }

    /// <summary>Reload the current scene — resets lives, score, feed, everything.</summary>
    public void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
