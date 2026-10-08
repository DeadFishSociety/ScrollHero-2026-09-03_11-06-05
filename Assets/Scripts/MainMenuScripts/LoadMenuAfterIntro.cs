using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Plays the intro video, then fades to the main menu when it ends. The player can
// also tap/click anywhere (or press any key) to skip straight to the menu.
[RequireComponent(typeof(VideoPlayer))]
public class LoadMenuAfterIntro : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenuScene";
    [SerializeField] private ScreenFader fader;

    [Tooltip("Let the player tap/click anywhere (or press a key) to skip the intro.")]
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

        if (SkipRequested())
        {
            LoadMenu();
        }
    }

    // Detects a tap / click / key press. This project runs on the new Input System
    // (legacy UnityEngine.Input is disabled and would throw), so read it from there;
    // the legacy branch is kept only for projects configured the old way.
    private bool SkipRequested()
    {
#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            return true;
        }
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }
        return false;
#else
        // A touch tap also raises mouse button 0, so this covers mobile and editor.
        return Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown;
#endif
    }

    private void LoadMenu()
    {
        if (loading)
        {
            return;
        }
        loading = true;

        if (fader != null)
        {
            fader.FadeOutAndLoad(menuSceneName);
        }
        else
        {
            // No fader wired - go straight to the menu so the intro is still skippable.
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
