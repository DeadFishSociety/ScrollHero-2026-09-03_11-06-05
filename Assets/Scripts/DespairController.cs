using UnityEngine;

// Handles the despair reel - the negative counterpart to the holy/overdrive reel.
// When the player likes a despair reel (ReelLike.DespairLiked) it plays a sound and
// applies the penalty via the DopamineManager: lose a life, or (on the last life)
// lose half the dopamine instead.
//
// Put this on the Feed object next to the other managers.
public class DespairController : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("Played when a despair reel is liked. Leave empty for none.")]
    [SerializeField] private AudioClip sound;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("Source the despair sound plays through. Auto-added if left empty.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("References (auto-found if empty)")]
    [SerializeField] private DopamineManager dopamineManager;

    private void Awake()
    {
        if (dopamineManager == null)
        {
            dopamineManager = FindFirstObjectByType<DopamineManager>();
        }
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
        }
    }

    private void OnEnable()
    {
        ReelLike.DespairLiked += OnDespairLiked;
    }

    private void OnDisable()
    {
        ReelLike.DespairLiked -= OnDespairLiked;
    }

    private void OnDespairLiked()
    {
        if (sound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(sound, volume);
        }
        if (dopamineManager != null)
        {
            dopamineManager.ApplyDespair();
        }
    }
}
