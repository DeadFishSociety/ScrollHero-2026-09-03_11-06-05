using UnityEngine;
using UnityEngine.Video;

// Plays the intro video, then fades to the main menu when it ends. The player can
// also tap/click anywhere to skip straight to the menu.
[RequireComponent(typeof(VideoPlayer))]
public class LoadMenuAfterIntro : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenuScene";
    [SerializeField] private ScreenFader fader;

    [Tooltip("Let the player tap/click anywhere to skip the intro.")]
    [SerializeField] private bool allowTapToSkip = true;

    // Set once the menu load has been triggered, so the video ending and a tap
    // can't both fire the transition.
    private bool loading;

    void Start()
    {
        GetComponent<VideoPlayer>().loopPointReached += _ => LoadMenu();
    }

    void Update()
    {
        if (!allowTapToSkip || loading)
        {
            return;
        }

        // A touch tap also raises mouse button 0, so this covers mobile and editor.
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            LoadMenu();
        }
    }

    private void LoadMenu()
    {
        if (loading)
        {
            return;
        }
        loading = true;
        fader.FadeOutAndLoad(menuSceneName);
    }
}
