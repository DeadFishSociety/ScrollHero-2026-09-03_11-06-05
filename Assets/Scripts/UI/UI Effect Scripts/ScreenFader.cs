using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private bool fadeInOnStart = true;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = fadeInOnStart ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;
    }

    void Start()
    {
        if (fadeInOnStart)
            StartCoroutine(Fade(1f, 0f));
    }

    public void FadeOutAndLoad(string sceneName)
    {
        StartCoroutine(FadeOutAndLoadRoutine(sceneName));
    }

    private IEnumerator FadeOutAndLoadRoutine(string sceneName)
    {
        canvasGroup.blocksRaycasts = true;
        yield return Fade(0f, 1f);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}