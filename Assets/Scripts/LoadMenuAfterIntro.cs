using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class LoadMenuAfterIntro : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenuScene";
    [SerializeField] private ScreenFader fader;

    void Start()
    {
        GetComponent<VideoPlayer>().loopPointReached += _ => fader.FadeOutAndLoad(menuSceneName);
    }
}