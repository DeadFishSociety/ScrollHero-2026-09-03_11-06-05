using UnityEngine;

// Plays the little dopamine-hit sounds as the player engages with the feed: one
// (weighted-random) sound each time a reel is swiped, another each time a reel is
// liked. Add it to the Feed object. Uses its own AudioSource.
[RequireComponent(typeof(ReelFeedController))]
public class FeedAudio : MonoBehaviour
{
    [Tooltip("Source the feed sounds play through. Auto-added if left empty.")]
    [SerializeField] private AudioSource source;

    [Tooltip("Weighted set of sounds, one plays per reel swiped.")]
    [SerializeField] private SoundBank scrollSounds;

    [Tooltip("Weighted set of sounds, one plays per like.")]
    [SerializeField] private SoundBank likeSounds;

    private ReelFeedController feed;

    private void Awake()
    {
        feed = GetComponent<ReelFeedController>();
        if (source == null)
        {
            source = GetComponent<AudioSource>();
        }
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
    }

    private void OnEnable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted += OnScroll;
        }
        ReelLike.AnyReelLiked += OnLike;
    }

    private void OnDisable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted -= OnScroll;
        }
        ReelLike.AnyReelLiked -= OnLike;
    }

    private void OnScroll()
    {
        scrollSounds.PlayOneShot(source);
    }

    private void OnLike()
    {
        likeSounds.PlayOneShot(source);
    }
}
