using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Spawns short-lived heart Images that fly up, shrink and fade out. Works on a
// Screen Space - Overlay canvas (unlike a world Particle System). Call Burst()
// with a screen position (e.g. from a tap).
[RequireComponent(typeof(RectTransform))]
public class HeartParticleSpawner : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Sprite used for each heart particle (e.g. heart.png).")]
    [SerializeField] private Sprite particleSprite;
    [SerializeField] private Color color = Color.white;
    [Tooltip("Base size of a heart particle, in canvas units.")]
    [SerializeField] private Vector2 startSize = new Vector2(48f, 48f);

    [Tooltip("Each heart's size is multiplied by a random value in this range (x = min, y = max). Set both to 1 for uniform size.")]
    [SerializeField] private Vector2 sizeMultiplierRange = new Vector2(0.8f, 1.3f);

    [Header("Burst")]
    [Tooltip("How many hearts spawn per tap.")]
    [SerializeField] private int countPerTap = 5;
    [SerializeField] private float lifetime = 0.6f;

    [Header("Motion (canvas units/sec)")]
    [Tooltip("Upward speed.")]
    [SerializeField] private float riseSpeed = 350f;
    [Tooltip("Random horizontal spread of speed.")]
    [SerializeField] private float horizontalSpread = 180f;
    [Tooltip("Downward pull applied over the lifetime.")]
    [SerializeField] private float gravity = 400f;
    [Tooltip("Random rotation speed range (deg/sec).")]
    [SerializeField] private float spinRange = 180f;

    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    // Emit a burst of hearts at the given screen position.
    public void Burst(Vector2 screenPosition)
    {
        if (particleSprite == null)
        {
            return;
        }

        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, cam, out localPoint))
        {
            return;
        }

        for (int i = 0; i < countPerTap; i++)
        {
            SpawnOne(localPoint);
        }
    }

    private void SpawnOne(Vector2 localPoint)
    {
        GameObject go = new GameObject("HeartParticle", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(rectTransform, false);
        float sizeMul = Random.Range(
            Mathf.Min(sizeMultiplierRange.x, sizeMultiplierRange.y),
            Mathf.Max(sizeMultiplierRange.x, sizeMultiplierRange.y));
        rt.sizeDelta = startSize * sizeMul;
        rt.anchoredPosition = localPoint;

        Image img = go.GetComponent<Image>();
        img.sprite = particleSprite;
        img.color = color;
        img.raycastTarget = false;

        Vector2 velocity = new Vector2(Random.Range(-horizontalSpread, horizontalSpread), riseSpeed);
        float spin = Random.Range(-spinRange, spinRange);

        StartCoroutine(Animate(rt, img, velocity, spin));
    }

    private IEnumerator Animate(RectTransform rt, Image img, Vector2 velocity, float spin)
    {
        float t = 0f;
        Color baseColor = img.color;

        while (t < lifetime)
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = t / lifetime; // 0..1

            velocity.y -= gravity * dt;
            rt.anchoredPosition += velocity * dt;
            rt.Rotate(0f, 0f, spin * dt);

            float scale = Mathf.Lerp(1f, 0f, k);
            rt.localScale = new Vector3(scale, scale, 1f);

            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, k);
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }
}
