using System.Collections.Generic;
using UnityEngine;

public enum SoundSelectionMode
{
    /// <summary>Pick at random, respecting each variation's Weight.</summary>
    RandomWeighted,
    /// <summary>Pick at random, ignoring weights (every variation equally likely).</summary>
    Random,
    /// <summary>Walk down the enabled variations in order, then start over.</summary>
    Sequential
}

/// <summary>One clip variation in a <see cref="SoundEffect"/> slot.</summary>
[System.Serializable]
public class SoundVariation
{
    [Tooltip("The audio clip for this variation.")]
    public AudioClip clip;

    [Tooltip("Relative chance of being picked. 2 is twice as likely as 1. RandomWeighted mode only.")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("Untick to keep this variation in the list but out of rotation.")]
    public bool enabled = true;

    public bool IsUsable => enabled && clip != null;
}

/// <summary>
/// A reusable, inspector-friendly sound "slot": a pool of clip variations plus the rule for
/// choosing between them (weighted random, plain random, or sequential). Drop one on any
/// component that needs a sound, add clips, and call <see cref="Play"/>.
///
/// Clips play through the assigned AudioSource (as 2D UI SFX, set its Spatial Blend to 0). If
/// no source is assigned it falls back to a shared one created on demand, so the slot still
/// works with zero wiring.
/// </summary>
[System.Serializable]
public class SoundEffect
{
    [Tooltip("The clip variations that can play. Untick a variation to disable it without deleting it.")]
    [SerializeField] private List<SoundVariation> variations = new List<SoundVariation>();

    [SerializeField] private SoundSelectionMode mode = SoundSelectionMode.RandomWeighted;

    [Tooltip("Random modes: never play the same clip twice in a row (unless it is the only one enabled).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Tooltip("Playback volume.")]
    [Range(0f, 1f)][SerializeField] private float volume = 1f;

    [Tooltip("Random pitch range per play, for variety. Leave both at 1 for no pitch change.")]
    [SerializeField] private Vector2 pitchRange = Vector2.one;

    [Tooltip("The AudioSource these clips play through (PlayOneShot). Leave empty to use a " +
             "shared 2D source created automatically.")]
    [SerializeField] private AudioSource source;

    private readonly List<SoundVariation> usable = new List<SoundVariation>();
    private AudioClip lastPlayed;
    private int nextIndex;

    private static AudioSource sharedSource;

    /// <summary>Pick a clip according to the mode and play it once, using the random Pitch Range.</summary>
    public void Play()
    {
        float pitch = Mathf.Approximately(pitchRange.x, pitchRange.y)
            ? pitchRange.x
            : Random.Range(pitchRange.x, pitchRange.y);
        PlayInternal(pitch);
    }

    /// <summary>
    /// Pick a clip and play it once at an explicit pitch — overrides the random Pitch Range.
    /// Use this to drive pitch from game state (e.g. minigame progress).
    /// </summary>
    public void Play(float pitch) => PlayInternal(pitch);

    private void PlayInternal(float pitch)
    {
        AudioClip clip = Pick();
        if (clip == null)
            return;

        AudioSource output = source != null ? source : SharedSource;
        if (output == null)
            return;

        output.pitch = pitch;
        output.PlayOneShot(clip, volume);
    }

    private AudioClip Pick()
    {
        usable.Clear();
        for (int i = 0; i < variations.Count; i++)
            if (variations[i] != null && variations[i].IsUsable)
                usable.Add(variations[i]);

        if (usable.Count == 0)
            return null;

        AudioClip picked = mode == SoundSelectionMode.Sequential
            ? PickSequential()
            : PickRandom(mode == SoundSelectionMode.RandomWeighted);

        lastPlayed = picked;
        return picked;
    }

    private AudioClip PickSequential()
    {
        if (nextIndex >= usable.Count)
            nextIndex = 0;

        AudioClip clip = usable[nextIndex].clip;
        nextIndex = (nextIndex + 1) % usable.Count;
        return clip;
    }

    private AudioClip PickRandom(bool weighted)
    {
        // Drop the previous pick so the same clip doesn't repeat back to back.
        if (avoidImmediateRepeat && lastPlayed != null && usable.Count > 1)
            usable.RemoveAll(v => v.clip == lastPlayed);

        if (usable.Count == 0)
            return lastPlayed;

        if (!weighted)
            return usable[Random.Range(0, usable.Count)].clip;

        float total = 0f;
        for (int i = 0; i < usable.Count; i++)
            total += Mathf.Max(0f, usable[i].weight);

        // All weights zero (or negative) — treat them as equally likely.
        if (total <= 0f)
            return usable[Random.Range(0, usable.Count)].clip;

        float roll = Random.Range(0f, total);
        for (int i = 0; i < usable.Count; i++)
        {
            roll -= Mathf.Max(0f, usable[i].weight);
            if (roll <= 0f)
                return usable[i].clip;
        }

        return usable[usable.Count - 1].clip;
    }

    /// <summary>A shared 2D AudioSource, created on first use, for slots with no source assigned.</summary>
    private static AudioSource SharedSource
    {
        get
        {
            if (sharedSource == null)
            {
                var go = new GameObject("SharedSfxSource");
                Object.DontDestroyOnLoad(go);
                sharedSource = go.AddComponent<AudioSource>();
                sharedSource.playOnAwake = false;
                sharedSource.spatialBlend = 0f; // 2D
            }
            return sharedSource;
        }
    }
}
