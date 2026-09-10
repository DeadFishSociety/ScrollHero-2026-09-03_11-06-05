using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small UI "particle" burst of heart sprites. On <see cref="Play"/> it emits a handful
/// of heart Images from this object's position, each drifting up and outward while shrinking
/// and fading, then cleans itself up. It lives entirely in the Canvas (no ParticleSystem),
/// so it sorts and scales with the rest of the UI.
///
/// Place this on a UI object positioned where the hearts should come from — e.g. as a child
/// of the heart icon — and drive it from ReelLike.
/// </summary>
public class HeartBurst : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Heart sprite spawned for each particle.")]
    [SerializeField] private Sprite heartSprite;

    [Tooltip("Colour tint applied to every heart.")]
    [SerializeField] private Color color = Color.white;

    [Tooltip("Size (px) of each heart at spawn.")]
    [SerializeField] private Vector2 heartSize = new Vector2(64f, 64f);

    [Header("Burst")]
    [Tooltip("How many hearts to emit per burst.")]
    [SerializeField, Min(1)] private int count = 8;

    [Tooltip("How long each heart lives before it's gone (seconds).")]
    [SerializeField, Min(0.01f)] private float lifetime = 0.8f;

    [Tooltip("Initial speed range (px per second), chosen at random per heart.")]
    [SerializeField] private Vector2 speedRange = new Vector2(250f, 500f);

    [Tooltip("Sideways spread of the launch angle, in degrees either side of straight up.")]
    [SerializeField, Range(0f, 180f)] private float spreadDegrees = 45f;

    [Tooltip("Downward pull applied over time (px per second squared). 0 = float straight up.")]
    [SerializeField] private float gravity = 400f;

    [Tooltip("Scale each heart reaches by the end of its life (1 = no change).")]
    [SerializeField, Min(0f)] private float endScale = 0.4f;

    /// <summary>Emit one burst of hearts from this object's position.</summary>
    public void Play()
    {
        if (heartSprite == null)
        {
            Debug.LogWarning("[HeartBurst] No Heart Sprite assigned — nothing to emit.", this);
            return;
        }
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[HeartBurst] Play() called while the object is inactive/disabled.", this);
            return;
        }

        Debug.Log($"[HeartBurst] Emitting {count} hearts at {transform.position}", this);

        for (int i = 0; i < count; i++)
            StartCoroutine(AnimateHeart(SpawnHeart()));
    }

    // Right-click the HeartBurst component header in the Inspector (in Play mode) -> Test Play.
    [ContextMenu("Test Play")]
    private void TestPlay() => Play();

    private RectTransform SpawnHeart()
    {
        var go = new GameObject("Heart", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = heartSize;

        var img = go.GetComponent<Image>();
        img.sprite = heartSprite;
        img.color = color;
        img.raycastTarget = false; // never eat input
        return rt;
    }

    private IEnumerator AnimateHeart(RectTransform rt)
    {
        Image img = rt.GetComponent<Image>();

        // Launch mostly upward (90 degrees), within +/- the spread.
        float angle = (90f + Random.Range(-spreadDegrees, spreadDegrees)) * Mathf.Deg2Rad;
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                           * Random.Range(speedRange.x, speedRange.y);

        Color startColor = img.color;
        Vector3 startScale = rt.localScale;
        Vector3 finalScale = startScale * endScale;

        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float p = elapsed / lifetime;

            velocity.y -= gravity * dt;
            rt.anchoredPosition += velocity * dt;
            rt.localScale = Vector3.Lerp(startScale, finalScale, p);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, p); // fade out over its life
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }
}
