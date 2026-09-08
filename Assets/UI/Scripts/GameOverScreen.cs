using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// The game-over UI: game-over text plus a restart button. Kept hidden until
/// <see cref="Show"/> is called (FeedManager calls it after the death explosion has
/// played its first cycle). Restart reloads the active scene for a full reset.
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

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);

        if (content != null)
            content.SetActive(false); // start hidden
    }

    private void OnDestroy()
    {
        if (restartButton != null)
            restartButton.onClick.RemoveListener(Restart);
    }

    /// <summary>Reveal the game-over text + button.</summary>
    public void Show()
    {
        if (content != null)
            content.SetActive(true);
    }

    /// <summary>Reload the current scene — resets lives, score, feed, everything.</summary>
    public void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
