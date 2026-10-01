using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Shows the player's remaining lives as a single Image that swaps to a different
// sprite for each life count. When a life is lost it briefly flashes a damage
// sprite, then settles back onto the sprite for the new life count.
//
// Mirrors DopamineBar: put it on an Image and it drives itself off the
// DopamineManager's OnLivesChanged event - no manual wiring to the manager.
//
// Life Sprites are indexed by the current life count: element [i] is shown when
// the player has i lives. So for 3 starting lives, provide sprites for 0, 1, 2
// and 3 (four entries).
[RequireComponent(typeof(Image))]
public class LivesDisplay : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Sprite shown per life count: element [i] is used when the player has i lives.")]
    [SerializeField] private Sprite[] lifeSprites;

    [Header("Damage flash")]
    [Tooltip("Sprite shown briefly when a life is lost, before settling on the new life count. Leave empty to skip the flash.")]
    [SerializeField] private Sprite damageSprite;

    [Tooltip("How long (seconds) the damage sprite is shown.")]
    [Min(0f)]
    [SerializeField] private float damageFlashDuration = 0.3f;

    [Tooltip("Optional. Raised when a life is lost - wire any extra reaction here (sound, particles, screen shake...).")]
    [SerializeField] private UnityEvent onLifeLost;

    private Image image;

    // Latest life count, used to settle back after the damage flash.
    private int currentLives;

    private Coroutine flashRoutine;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        DopamineManager.OnLivesChanged += OnLivesChanged;
        DopamineManager.OnLifeLost += OnLifeLostSignal;
    }

    private void OnDisable()
    {
        DopamineManager.OnLivesChanged -= OnLivesChanged;
        DopamineManager.OnLifeLost -= OnLifeLostSignal;
    }

    // Count changed (loss, overdrive set/reset, or initial). Just updates the
    // sprite - the damage flash is driven by OnLifeLost instead, so the overdrive
    // reset (3 -> 2) doesn't flash.
    private void OnLivesChanged(int lives, int maxLives)
    {
        currentLives = lives;

        // If a flash is in progress, let it settle onto the new count when it ends.
        if (flashRoutine == null)
        {
            SyncSprite(currentLives);
        }
    }

    // An actual life was lost: play the damage flash.
    private void OnLifeLostSignal()
    {
        onLifeLost?.Invoke();

        if (damageSprite != null && damageFlashDuration > 0f)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }
            flashRoutine = StartCoroutine(DamageFlash());
        }
        else
        {
            SyncSprite(currentLives);
        }
    }

    private IEnumerator DamageFlash()
    {
        image.sprite = damageSprite;
        yield return new WaitForSecondsRealtime(damageFlashDuration);
        SyncSprite(currentLives);
        flashRoutine = null;
    }

    private void SyncSprite(int lives)
    {
        if (lifeSprites == null || lifeSprites.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(lives, 0, lifeSprites.Length - 1);
        image.sprite = lifeSprites[index];
    }
}
