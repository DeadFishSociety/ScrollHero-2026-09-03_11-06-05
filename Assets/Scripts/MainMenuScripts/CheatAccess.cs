using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Hidden entry point to the cheat menu. Put this on a small (ideally transparent)
// UI element with a raycast-target Graphic, tucked in a corner. Tapping it several
// times quickly opens the cheat scene. Gated to the editor / development builds so
// it does nothing in a shipping release.
[RequireComponent(typeof(Graphic))]
public class CheatAccess : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Scene loaded once the secret tap count is reached.")]
    [SerializeField] private string cheatSceneName = "CheatMenuScene";

    [Tooltip("How many quick taps open the cheat menu.")]
    [Min(1)]
    [SerializeField] private int tapsToOpen = 5;

    [Tooltip("The taps must all happen within this many seconds.")]
    [SerializeField] private float withinSeconds = 2f;

    [Tooltip("On: only works in the editor or development builds, never in a shipped release.")]
    [SerializeField] private bool devBuildsOnly = true;

    private int taps;
    private float firstTapTime;

    private void Awake()
    {
        // Make sure the (likely transparent) graphic still receives clicks.
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {

        float now = Time.unscaledTime;
        if (taps == 0 || now - firstTapTime > withinSeconds)
        {
            taps = 0;
            firstTapTime = now;
        }

        taps++;
        if (taps >= tapsToOpen)
        {
            taps = 0;
            SceneManager.LoadScene(cheatSceneName);
        }
    }
}
