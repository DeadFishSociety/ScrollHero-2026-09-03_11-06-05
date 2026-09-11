using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A background-music slot: one or more tracks (weighted, like <see cref="SoundEffect"/>) played
/// through an AudioSource. It can either loop a single chosen track forever, or act as a playlist
/// that picks the next track (weighted / random / sequential) each time one finishes.
///
/// It isn't a MonoBehaviour, so the owner must call <see cref="Tick"/> every frame (from Update)
/// for the playlist to advance; looping a single track needs no ticking.
/// </summary>
[System.Serializable]
public class BackgroundMusic
{
    [Tooltip("The AudioSource the music plays through. Add one to the scene (2D: Spatial Blend 0, " +
             "Play On Awake off) and assign it here.")]
    [SerializeField] private AudioSource source;

    [Tooltip("The tracks that can play. Untick a track to keep it in the list but out of rotation.")]
    [SerializeField] private List<SoundVariation> tracks = new List<SoundVariation>();

    [Tooltip("How the next track is chosen (used when picking the first track, and each next one " +
             "in playlist mode).")]
    [SerializeField] private SoundSelectionMode mode = SoundSelectionMode.RandomWeighted;

    [Tooltip("Random modes: don't play the same track twice in a row (unless it's the only one).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Tooltip("Loop the chosen track forever. Untick to advance to the next track when one ends " +
             "(a continuous playlist chosen by the mode above).")]
    [SerializeField] private bool loopSingleTrack = false;

    [Tooltip("Music volume.")]
    [Range(0f, 1f)][SerializeField] private float volume = 1f;

    private readonly List<SoundVariation> usable = new List<SoundVariation>();
    private AudioClip lastPlayed;
    private int nextIndex;
    private bool playing;

    /// <summary>Start playback from a freshly chosen track.</summary>
    public void Play()
    {
        if (source == null)
            return;

        AudioClip clip = Pick();
        if (clip == null)
            return;

        playing = true;
        StartClip(clip);
    }

    /// <summary>Stop playback and forget the playlist state.</summary>
    public void Stop()
    {
        playing = false;
        if (source != null)
            source.Stop();
    }

    /// <summary>
    /// Call every frame from the owner's Update. In playlist mode (loopSingleTrack off) it starts
    /// the next track once the current one finishes. A no-op when looping a single track or stopped.
    /// </summary>
    public void Tick()
    {
        if (!playing || loopSingleTrack || source == null)
            return;

        // The current track ended — queue up the next one.
        if (source.clip != null && !source.isPlaying)
        {
            AudioClip next = Pick();
            if (next != null)
                StartClip(next);
        }
    }

    private void StartClip(AudioClip clip)
    {
        source.clip = clip;
        source.loop = loopSingleTrack;
        source.volume = volume;
        source.Play();
    }

    private AudioClip Pick()
    {
        usable.Clear();
        for (int i = 0; i < tracks.Count; i++)
            if (tracks[i] != null && tracks[i].IsUsable)
                usable.Add(tracks[i]);

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
        // Drop the previous pick so the same track doesn't repeat back to back.
        if (avoidImmediateRepeat && lastPlayed != null && usable.Count > 1)
            usable.RemoveAll(t => t.clip == lastPlayed);

        if (usable.Count == 0)
            return lastPlayed;

        if (!weighted)
            return usable[Random.Range(0, usable.Count)].clip;

        float total = 0f;
        for (int i = 0; i < usable.Count; i++)
            total += Mathf.Max(0f, usable[i].weight);

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
}
