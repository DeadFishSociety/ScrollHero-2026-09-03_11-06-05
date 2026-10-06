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

    // True while a temporary override clip (e.g. overdrive music) is playing in
    // place of the theme.
    private bool overriding;

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

    // Pauses the music (keeps its position). Used to silence the theme while a
    // special reel plays its own sound, so nothing overlaps.
    public void Pause()
    {
        if (source != null)
        {
            source.Pause();
        }
    }

    // Resumes the paused theme - unless an override (e.g. overdrive music) owns it.
    public void Resume()
    {
        if (source != null && !overriding)
        {
            source.UnPause();
        }
    }

    // Replaces the theme with a temporary looping clip (e.g. overdrive music).
    // Call StopOverride() to return to the theme.
    public void PlayOverride(AudioClip clip, float overrideVolume = -1f)
    {
        if (source == null || clip == null)
        {
            return;
        }
        overriding = true;
        source.clip = clip;
        source.loop = true;
        source.volume = overrideVolume >= 0f ? overrideVolume : volume;
        source.Play();
    }

    // Returns to the main theme after a PlayOverride.
    public void StopOverride()
    {
        if (source == null || !overriding)
        {
            return;
        }
        overriding = false;
        source.volume = volume;
        source.clip = theme;
        source.loop = true;
        if (theme != null)
        {
            source.Play();
        }
        else
        {
            source.Stop();
        }
    }
}
