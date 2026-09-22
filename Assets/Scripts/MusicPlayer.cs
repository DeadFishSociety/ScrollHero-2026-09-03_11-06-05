using UnityEngine;

// Plays a looping main theme for the whole session. Add it to a persistent
// GameObject (it brings its own AudioSource) and assign the theme clip.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Tooltip("The looping main theme.")]
    [SerializeField] private AudioClip theme;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    [Tooltip("Start playing automatically on load.")]
    [SerializeField] private bool playOnStart = true;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = theme;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (source == null || theme == null)
        {
            return;
        }
        source.clip = theme;
        source.loop = true;
        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    public void Stop()
    {
        if (source != null)
        {
            source.Stop();
        }
    }
}
