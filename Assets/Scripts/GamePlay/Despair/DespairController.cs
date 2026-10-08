using UnityEngine;

// Handles the despair reel - the negative counterpart to the holy/overdrive reel.
// Its sound plays once while the despair reel is on screen and stops when you
// scroll away (so it never lingers). Liking the reel applies the penalty via the
// DopamineManager: lose a life, or (on the last life) lose half the dopamine.
//
// Put this on the Feed object next to the other managers.
public class DespairController : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("Played once while the despair reel is on screen; stops when you scroll away. Leave empty for none.")]
    [SerializeField] private AudioClip sound;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("Source the despair sound plays through. Auto-added if left empty.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("References (auto-found if empty)")]
    [SerializeField] private DopamineManager dopamineManager;
    [SerializeField] private ReelFeedController feed;

    private void Awake()
    {
        if (dopamineManager == null)
        {
            dopamineManager = FindFirstObjectByType<DopamineManager>();
        }
        if (feed == null)
        {
            feed = FindFirstObjectByType<ReelFeedController>();
        }
        if (sfxSource == null)
        {
            // Dedicated source - never GetComponent, or we'd grab (and Stop) the
            // MusicPlayer's AudioSource when it shares this GameObject.
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.playOnAwake = false;
        sfxSource.loop = false; // play once, never loop
    }

    private void OnEnable()
    {
        ReelLike.DespairLiked += OnDespairLiked;
        if (feed != null)
        {
            feed.TopReelChanged += OnTopReelChanged;
        }
    }

    private void OnDisable()
    {
        ReelLike.DespairLiked -= OnDespairLiked;
        if (feed != null)
        {
            feed.TopReelChanged -= OnTopReelChanged;
        }
    }

    // Play the despair sound once when the despair reel becomes the current reel;
    // stop it as soon as a different reel is shown.
    private void OnTopReelChanged()
    {
        if (sfxSource == null)
        {
            return;
        }

        if (feed != null && feed.TopReelIsDespair)
        {
            if (sound != null)
            {
                sfxSource.clip = sound;
                sfxSource.volume = volume;
                sfxSource.Play();
            }
        }
        else
        {
            sfxSource.Stop();
        }
    }

    // Liking the despair reel applies the penalty (the sound is handled above).
    private void OnDespairLiked()
    {
        if (dopamineManager != null)
        {
            dopamineManager.ApplyDespair();
        }
    }
}
