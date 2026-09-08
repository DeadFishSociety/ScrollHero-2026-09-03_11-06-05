using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD health indicator: draws the player's health as a frame of the DopedDog
/// sheet, and on a life lost it flashes a damage sprite while shaking, then settles
/// on the new (lower) health frame.
///
/// Frame index = maxLives - livesRemaining, so the frames are ordered
/// FULL (max lives) -> DEAD (0 lives).
/// </summary>
public class HealthDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Image this draws the dog onto.")]
    [SerializeField] private Image image;

    [Header("Frames")]
    [Tooltip("DopedDog frames ordered FULL (max lives) -> DEAD (0 lives). " +
             "The frame shown is healthFrames[maxLives - livesRemaining].")]
    [SerializeField] private Sprite[] healthFrames;

    [Tooltip("Flashed during the shake to sell the hit. Reverts to the health frame after.")]
    [SerializeField] private Sprite damageSprite;

    [Header("Shake")]
    [Tooltip("How long the damage shake lasts.")]
    [SerializeField, Min(0f)] private float shakeDuration = 0.3f;

    [Tooltip("How far (pixels) the image jitters at the start of the shake.")]
    [SerializeField, Min(0f)] private float shakeMagnitude = 15f;

    [Header("Death explosion")]
    [Tooltip("DopeDogXplosion frames in play order. Played on THIS image at game over, " +
             "so the dog appears to explode in place.")]
    [SerializeField] private Sprite[] explosionFrames;

    [Tooltip("Explosion playback speed, frames per second.")]
    [SerializeField, Min(0.1f)] private float explosionFps = 24f;

    private RectTransform rt;
    private Vector2 home;
    private Coroutine shakeRoutine;
    private Coroutine explosionRoutine;

    private void Awake()
    {
        if (image != null)
            rt = image.rectTransform;
        if (rt != null)
            home = rt.anchoredPosition; // resting position to shake around
    }

    /// <summary>Show the matching health frame with no shake. Use to initialise.</summary>
    public void SetHealth(int livesRemaining, int maxLives)
    {
        ShowFrame(livesRemaining, maxLives);
    }

    /// <summary>Flash the damage sprite, shake, then settle on the new health frame.</summary>
    public void PlayDamage(int livesRemaining, int maxLives)
    {
        // A hit landing mid-shake: cancel the old shake and snap back to rest first,
        // so the jitter never drifts away from home.
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
            if (rt != null)
                rt.anchoredPosition = home;
        }

        if (isActiveAndEnabled && shakeDuration > 0f && rt != null)
            shakeRoutine = StartCoroutine(ShakeThenSettle(livesRemaining, maxLives));
        else
            ShowFrame(livesRemaining, maxLives); // can't shake — just update the frame
    }

    private IEnumerator ShakeThenSettle(int livesRemaining, int maxLives)
    {
        if (image != null && damageSprite != null)
            image.sprite = damageSprite;

        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(t / shakeDuration); // 1 -> 0, so it eases out
            rt.anchoredPosition = home + Random.insideUnitCircle * (shakeMagnitude * damper);
            yield return null;
        }

        rt.anchoredPosition = home;
        ShowFrame(livesRemaining, maxLives);
        shakeRoutine = null;
    }

    /// <summary>
    /// Take over the dog image and loop the explosion animation in place (the dog
    /// "explodes"). <paramref name="onFirstCycleComplete"/> fires once, after the first
    /// full pass through the frames — the explosion keeps looping after that.
    /// </summary>
    public void PlayExplosion(System.Action onFirstCycleComplete)
    {
        // Hand the image over to the explosion: cancel any shake and snap back to rest.
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
        if (rt != null)
            rt.anchoredPosition = home;

        if (explosionRoutine != null)
            StopCoroutine(explosionRoutine);

        // Nothing to play — don't stall the game-over flow, reveal immediately.
        if (image == null || explosionFrames == null || explosionFrames.Length == 0)
        {
            onFirstCycleComplete?.Invoke();
            return;
        }

        explosionRoutine = StartCoroutine(ExplosionLoop(onFirstCycleComplete));
    }

    private IEnumerator ExplosionLoop(System.Action onFirstCycleComplete)
    {
        float frameTime = 1f / explosionFps;
        bool firstCycleDone = false;

        while (true)
        {
            for (int i = 0; i < explosionFrames.Length; i++)
            {
                image.sprite = explosionFrames[i];
                yield return new WaitForSeconds(frameTime);
            }

            if (!firstCycleDone)
            {
                firstCycleDone = true;
                onFirstCycleComplete?.Invoke();
            }
        }
    }

    private void ShowFrame(int livesRemaining, int maxLives)
    {
        if (image == null || healthFrames == null || healthFrames.Length == 0)
            return;

        int index = Mathf.Clamp(maxLives - livesRemaining, 0, healthFrames.Length - 1);
        image.sprite = healthFrames[index];
    }
}
