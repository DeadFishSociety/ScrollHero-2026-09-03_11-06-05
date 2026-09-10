using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a pre-sliced sprite sheet on a UI Image. FeedItem starts this when a
/// normal reel is shown; action/minigame panels never call it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ReelSpriteAnimation : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("Drag the sliced sprites here in playback order.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Number of sprite-sheet frames shown per second.")]
    [SerializeField, Min(0.1f)] private float framesPerSecond = 12f;

    [Tooltip("Repeat from the first frame after reaching the end.")]
    [SerializeField] private bool loop = true;

    [Tooltip("Hide this Image after a non-looping animation completes.")]
    [SerializeField] private bool hideWhenFinished;

    [Header("Position")]
    [Tooltip("Choose a new position around this Image's authored location on every play.")]
    [SerializeField] private bool randomizePosition;

    [Tooltip("Maximum horizontal and vertical offset, in Canvas UI units.")]
    [SerializeField] private Vector2 positionJitter;

    [Header("Sound")]
    [Tooltip("Sound played each time this animation starts. Add clip variations — weighted or " +
             "not depending on the mode chosen.")]
    [SerializeField] private SoundEffect playSound = new SoundEffect();

    private Image image;
    private RectTransform rectTransform;
    private Vector2 homePosition;
    private int frameIndex;
    private float frameElapsed;
    private bool isPlaying;
    private bool reversed; // playing last -> first this run

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = transform as RectTransform;
        if (rectTransform != null)
            homePosition = rectTransform.anchoredPosition;

        if (hideWhenFinished && image != null)
            image.enabled = false;
    }

    private void OnDisable()
    {
        isPlaying = false;
    }

    /// <summary>Restart this animation from its first frame, playing forwards.</summary>
    public void PlayFromStart() => PlayFromStart(false);

    /// <summary>
    /// Restart the animation, optionally playing the frames in reverse (last -> first).
    /// Reverse is a capability for effects that need to "undo" themselves; ordinary plays
    /// pass false.
    /// </summary>
    public void PlayFromStart(bool playReversed)
    {
        if (image == null)
            image = GetComponent<Image>();

        reversed = playReversed;
        frameElapsed = 0f;
        isPlaying = HasFrames();
        frameIndex = isPlaying && playReversed ? frames.Length - 1 : 0;

        if (randomizePosition && rectTransform != null)
        {
            rectTransform.anchoredPosition = homePosition + new Vector2(
                Random.Range(-positionJitter.x, positionJitter.x),
                Random.Range(-positionJitter.y, positionJitter.y));
        }

        if (isPlaying)
        {
            image.enabled = true;
            image.sprite = frames[frameIndex];
            playSound.Play();
        }
    }

    /// <summary>Stop on the currently displayed frame.</summary>
    public void Stop() => isPlaying = false;

    private void Update()
    {
        if (!isPlaying || !HasFrames())
            return;

        frameElapsed += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, framesPerSecond);

        while (frameElapsed >= frameDuration && isPlaying)
        {
            frameElapsed -= frameDuration;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        int lastFrame = frames.Length - 1;
        bool atEnd = reversed ? frameIndex <= 0 : frameIndex >= lastFrame;

        // Let the final frame remain visible for one full frame duration before
        // hiding a one-shot effect.
        if (!loop && atEnd)
        {
            isPlaying = false;
            if (hideWhenFinished)
                image.enabled = false;
            return;
        }

        if (reversed)
            frameIndex = frameIndex <= 0 ? lastFrame : frameIndex - 1;
        else
            frameIndex = frameIndex >= lastFrame ? 0 : frameIndex + 1;

        image.sprite = frames[frameIndex];
    }

    private bool HasFrames()
    {
        return image != null && frames != null && frames.Length > 0;
    }
}
