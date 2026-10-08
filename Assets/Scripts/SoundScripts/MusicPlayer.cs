using UnityEngine;

// Plays a looping main theme for the whole session. The source keeps playing the
// whole time; special reels (golden/despair) just fade its volume down and back up
// (see SetDucked) so nothing overlaps - the music is never stopped or paused.
// A temporary override clip (overdrive / last-life music) can replace the theme.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Tooltip("The looping main theme.")]
    [SerializeField] private AudioClip theme;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    [Tooltip("Start playing automatically on load.")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("Seconds to fade the music out / in when it's ducked for a special reel.")]
    [Min(0f)]
    [SerializeField] private float duckFadeDuration = 0.4f;

    private AudioSource source;

    // True while a temporary override clip (overdrive / last-life) is playing.
    private bool overriding;

    // Intended volume of the current clip (theme or override).
    private float baseVolume;

    // Fade multiplier: 1 = full, 0 = fully ducked (silent). Eased toward duckTarget.
    private float duck = 1f;
    private float duckTarget = 1f;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = theme;
        source.loop = true;
        source.playOnAwake = false;
        baseVolume = volume;
        ApplyVolume();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!Mathf.Approximately(duck, duckTarget))
        {
            float step = duckFadeDuration > 0f ? Time.unscaledDeltaTime / duckFadeDuration : 1f;
            duck = Mathf.MoveTowards(duck, duckTarget, step);
            ApplyVolume();
        }
    }

    public void Play()
    {
        if (source == null || theme == null)
        {
            return;
        }
        overriding = false;
        source.clip = theme;
        source.loop = true;
        baseVolume = volume;
        ApplyVolume();
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

    // Fades the music out (ducked = true) or back in (false). The source keeps
    // playing the whole time, so it's never cut off.
    public void SetDucked(bool ducked)
    {
        duckTarget = ducked ? 0f : 1f;
    }

    // Replaces the theme with a temporary looping clip (e.g. overdrive / last-life
    // music). Un-ducks immediately so the new clip is heard.
    public void PlayOverride(AudioClip clip, float overrideVolume = -1f)
    {
        if (source == null || clip == null)
        {
            return;
        }
        overriding = true;
        source.clip = clip;
        source.loop = true;
        baseVolume = overrideVolume >= 0f ? overrideVolume : volume;
        duck = 1f;
        duckTarget = 1f;
        ApplyVolume();
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
        source.clip = theme;
        source.loop = true;
        baseVolume = volume;
        duck = 1f;
        duckTarget = 1f;
        ApplyVolume();
        if (theme != null)
        {
            source.Play();
        }
        else
        {
            source.Stop();
        }
    }

    private void ApplyVolume()
    {
        if (source != null)
        {
            source.volume = baseVolume * Mathf.Clamp01(duck);
        }
    }
}
