using UnityEngine;

// Caps the frame rate to save battery on phones. Runs automatically at startup
// (no GameObject needed). VSync is turned off first because, when it's on, it
// overrides Application.targetFrameRate and the cap wouldn't take effect.
public static class FrameRateLimiter
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
