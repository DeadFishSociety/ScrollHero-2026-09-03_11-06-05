using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fires a short-lived burst of little UI sprites out from a point — cheap "juice"
/// that renders on the same Canvas as the caller, with no ParticleSystem or material
/// setup. Each particle flies outward, is pulled down by gravity, then shrinks and
/// fades before destroying itself.
///
/// The host MonoBehaviour runs the coroutines, so the burst stops (and its particles
/// are destroyed as children) if the host or its parent goes away.
/// </summary>
public static class UIParticleBurst
{
    /// <summary>How a burst looks and moves. All distances are in the parent's local units.</summary>
    public struct Settings
    {
        [Tooltip("Sprite for each particle. Null => a soft round dot.")]
        public Sprite sprite;
        public Color color;
        public int count;
        public float size;         // diameter
        public Vector2 speedRange; // outward speed, units/sec, picked per particle
        public float gravity;      // downward pull, units/sec²
        public float startRadius;  // how far from the origin each particle starts
        public float lifetime;     // seconds until fully faded
    }

    // A soft-edged white circle, built once and tinted per particle. Shared by every
    // round burst so we only ever generate the texture a single time.
    private static Sprite roundSprite;

    public static void Emit(MonoBehaviour host, RectTransform parent, Vector2 origin, Settings s)
    {
        if (host == null || parent == null || s.count <= 0 || s.size <= 0f)
            return;

        Sprite sprite = s.sprite != null ? s.sprite : RoundSprite();

        for (int i = 0; i < s.count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 spawn = origin + dir * s.startRadius;
            Vector2 velocity = dir * Random.Range(s.speedRange.x, s.speedRange.y);
            host.StartCoroutine(Run(parent, sprite, spawn, velocity, s));
        }
    }

    private static IEnumerator Run(RectTransform parent, Sprite sprite, Vector2 pos, Vector2 velocity, Settings s)
    {
        var go = new GameObject("BurstParticle", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(s.size, s.size);
        rect.anchoredPosition = pos;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        float elapsed = 0f;
        while (elapsed < s.lifetime)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            velocity += Vector2.down * (s.gravity * dt);
            rect.anchoredPosition += velocity * dt;

            float t = elapsed / s.lifetime;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t);

            Color c = s.color;
            c.a = s.color.a * (1f - t);
            image.color = c;

            yield return null;
        }

        Object.Destroy(go);
    }

    /// <summary>
    /// A white filled circle with a 1.5px soft edge, tintable to any colour. Generated
    /// once and shared — handy anywhere you want a round UI dot without a sprite asset.
    /// </summary>
    public static Sprite RoundSprite()
    {
        if (roundSprite != null)
            return roundSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var center = new Vector2(r, r);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01((r - d) / 1.5f); // 1 inside, soft falloff at the rim
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        roundSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return roundSprite;
    }
}
