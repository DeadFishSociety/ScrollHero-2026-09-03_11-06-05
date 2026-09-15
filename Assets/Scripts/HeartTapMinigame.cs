using UnityEngine;
using UnityEngine.EventSystems;

// Tap-the-heart minigame. An inner heart grows toward filling an outline: each
// tap on the heart adds to the fill, and it shrinks over time when idle. Fill
// the outline to win. Win-only (it never drops below empty, so you can't lose).
public class HeartTapMinigame : MinigameBase, IPointerDownHandler
{
    [Header("References")]
    [Tooltip("The inner heart that grows. Its scale is driven from 0 (empty) to 1 (full = fills the outline).")]
    [SerializeField] private RectTransform heartFill;

    [Tooltip("Spawns heart particles on each tap. Auto-found in children if left empty.")]
    [SerializeField] private HeartParticleSpawner particleSpawner;

    [Header("Fill rates")]
    [Tooltip("How much fill (0..1) each tap adds.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float growPerTap = 0.08f;

    [Tooltip("How much fill (0..1) drains per second when not tapping.")]
    [Range(0f, 2f)]
    [SerializeField] private float shrinkPerSecond = 0.25f;

    [Tooltip("Fill the heart starts at.")]
    [Range(0f, 1f)]
    [SerializeField] private float startFill = 0f;

    [Header("Tap juice")]
    [Tooltip("Extra scale added briefly on each tap for a pop.")]
    [SerializeField] private float punchAmount = 0.12f;

    [Tooltip("How fast the pop settles back.")]
    [SerializeField] private float punchDecay = 1.5f;

    private float fill;
    private float punch;
    private bool playing;

    public override void StartGame(MinigameContext context)
    {
        if (particleSpawner == null)
        {
            particleSpawner = GetComponentInChildren<HeartParticleSpawner>(true);
        }

        fill = Mathf.Clamp01(startFill);
        punch = 0f;
        ApplyScale();
        playing = true;
    }

    void Update()
    {
        if (!playing)
        {
            return;
        }

        // Drain over time, but never below empty (win-only, no losing).
        fill = Mathf.Clamp01(fill - shrinkPerSecond * Time.deltaTime);

        // Ease the tap pop back to zero.
        punch = Mathf.MoveTowards(punch, 0f, punchDecay * Time.deltaTime);

        ApplyScale();

        if (fill >= 1f)
        {
            playing = false;
            Win();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!playing)
        {
            return;
        }

        fill = Mathf.Clamp01(fill + growPerTap);
        punch = punchAmount;

        if (particleSpawner != null)
        {
            particleSpawner.Burst(eventData.position);
        }

        ApplyScale();
    }

    private void ApplyScale()
    {
        if (heartFill == null)
        {
            return;
        }
        float s = Mathf.Max(0f, fill + punch);
        heartFill.localScale = new Vector3(s, s, 1f);
    }
}
