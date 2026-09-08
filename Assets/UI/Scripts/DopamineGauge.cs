using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a 0..1 "time remaining" value as a single frame of the dopamine sprite
/// sheet. Pure view: something else (a ReelTimer or a FeedOverlay) owns the clock
/// and calls <see cref="SetFraction"/> each frame.
///
/// Drop in ONLY the frames you want to use, ordered FULL -> EMPTY. You do not need
/// all 42 sliced frames — e.g. a 12-frame drain is fine.
/// </summary>
public class DopamineGauge : MonoBehaviour
{
    [Tooltip("The Image this gauge swaps sprites on.")]
    [SerializeField] private Image image;

    [Tooltip("Dopamine sprite-sheet frames in FULL -> EMPTY order. " +
             "Only the ones you drop here are used.")]
    [SerializeField] private Sprite[] frames;

    /// <summary>fraction: 1 = full (first frame), 0 = empty (last frame).</summary>
    public void SetFraction(float fraction)
    {
        if (image == null || frames == null || frames.Length == 0)
            return;

        fraction = Mathf.Clamp01(fraction);
        // 1 -> first frame (full), 0 -> last frame (empty).
        int index = Mathf.RoundToInt((1f - fraction) * (frames.Length - 1));
        index = Mathf.Clamp(index, 0, frames.Length - 1);
        image.sprite = frames[index];
    }
}
