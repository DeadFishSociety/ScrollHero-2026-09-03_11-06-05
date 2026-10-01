using System;
using UnityEngine;
using UnityEngine.UI;

// Shows the dopamine level as a two-layer sprite animation: a Background and a
// Foreground Image that step through their frames together. The frame is chosen
// by the dopamine fraction (empty -> full), and both layers use the same index so
// they stay in sync (keep each life's background and foreground frame counts equal).
//
// Each life has its own background + foreground frame sets: 3 lives -> Life 3,
// 2 -> Life 2, 1 -> Life 1. Within a set, element 0 = empty, last = full.
//
// Put this on a parent object and wire the two child Images below. Lives come from
// DopamineManager.OnLivesChanged; the dopamine level from OnDopamineChange /
// OnDopamineInitialized.
public class DopamineBar : MonoBehaviour
{
    [Serializable]
    public class LifeBarFrames
    {
        [Tooltip("Background frames for this life (element 0 = empty, last = full).")]
        public Sprite[] background;

        [Tooltip("Foreground / contents frames for this life. Keep the same count as Background so they stay in sync.")]
        public Sprite[] foreground;
    }

    [Header("Layers")]
    [Tooltip("The background Image (e.g. the bar casing).")]
    [SerializeField] private Image background;

    [Tooltip("The foreground Image (the contents), drawn on top of the background.")]
    [SerializeField] private Image foreground;

    [Header("Frames per life")]
    [Tooltip("Shown while on the last life (1 life left).")]
    [SerializeField] private LifeBarFrames life1;

    [Tooltip("Shown while on 2 lives.")]
    [SerializeField] private LifeBarFrames life2;

    [Tooltip("Shown while on 3 lives.")]
    [SerializeField] private LifeBarFrames life3;

    // Assume full lives until the manager tells us otherwise (it fires on start).
    private int currentLives = 3;
    private float lastFraction;

    private void OnEnable()
    {
        DopamineManager.OnDopamineInitialized += SyncBarToDopamineLevel;
        DopamineManager.OnDopamineChange += SyncBarToDopamineLevel;
        DopamineManager.OnLivesChanged += OnLivesChanged;
    }

    private void OnDisable()
    {
        DopamineManager.OnDopamineInitialized -= SyncBarToDopamineLevel;
        DopamineManager.OnDopamineChange -= SyncBarToDopamineLevel;
        DopamineManager.OnLivesChanged -= OnLivesChanged;
    }

    private void OnLivesChanged(int lives, int maxLives)
    {
        currentLives = lives;
        // Swap to the new life's frame sets right away, at the current fill.
        ApplyFrame(lastFraction);
    }

    private void SyncBarToDopamineLevel(float fraction)
    {
        lastFraction = Mathf.Clamp01(fraction);
        ApplyFrame(lastFraction);
    }

    private void ApplyFrame(float fraction)
    {
        LifeBarFrames set = FramesForCurrentLife();
        if (set == null)
        {
            return;
        }

        ApplyLayer(background, set.background, fraction);
        ApplyLayer(foreground, set.foreground, fraction);
    }

    // Sets one layer's sprite to the frame matching the fraction. An empty frame
    // set (or missing Image) leaves that layer untouched, so art can be wired in
    // gradually.
    private static void ApplyLayer(Image image, Sprite[] frames, float fraction)
    {
        if (image == null || frames == null || frames.Length == 0)
        {
            return;
        }

        int index = Mathf.RoundToInt(fraction * (frames.Length - 1));
        index = Mathf.Clamp(index, 0, frames.Length - 1);
        image.sprite = frames[index];
    }

    // Picks the frame set for the current life. More than 3 lives reuses the Life 3
    // set; 1 (or fewer) uses the Life 1 set.
    private LifeBarFrames FramesForCurrentLife()
    {
        if (currentLives >= 3)
        {
            return life3;
        }
        if (currentLives == 2)
        {
            return life2;
        }
        return life1;
    }
}
