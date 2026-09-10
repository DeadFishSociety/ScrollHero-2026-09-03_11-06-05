using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>One video plus the description paired with it.</summary>
[System.Serializable]
public class ReelClip
{
    [Tooltip("The video that plays in this reel's background.")]
    public VideoClip clip;

    [Tooltip("Description paired with this clip. Exposed via CurrentDescription for later " +
             "use (e.g. an overlay caption).")]
    [TextArea] public string description;
}

/// <summary>
/// Plays a random background video on a scroll reel from a pool, and exposes the
/// paired description for later use.
///
/// No black first frame: the video is Prepare()'d first and the RawImage stays hidden
/// until the first frame is decoded, so whatever sits behind it (the panel's own
/// background) shows during the brief prepare instead of a black rectangle.
/// </summary>
public class ReelVideo : MonoBehaviour
{
    [Header("Playback")]
    [Tooltip("The VideoPlayer on this reel (Render Mode: Render Texture).")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("The RawImage showing the video's RenderTexture. Hidden until the first " +
             "frame is ready, then revealed — this is what avoids the black frame.")]
    [SerializeField] private RawImage videoImage;

    [Header("Pool")]
    [Tooltip("The clips (with descriptions) this reel can pick from. One is chosen at random.")]
    [SerializeField] private List<ReelClip> clips = new List<ReelClip>();

    [Header("Panel text")]
    [Tooltip("Optional. Shows the chosen clip's paired description (the caption) on the panel.")]
    [SerializeField] private TMP_Text captionText;

    [Tooltip("Optional. Filled with a random entry from Usernames each time this reel spawns.")]
    [SerializeField] private TMP_Text usernameText;

    [Tooltip("Usernames to pick from at random for usernameText. Customize this list.")]
    [SerializeField] private string[] usernames;

    [Tooltip("Optional. Filled with a random entry from Music Tracks each time this reel spawns.")]
    [SerializeField] private TMP_Text musicText;

    [Tooltip("Music/sound labels to pick from at random for musicText. Customize this list.")]
    [SerializeField] private string[] musicTracks;

    /// <summary>Description paired with the clip chosen for this reel. Set on spawn.</summary>
    public string CurrentDescription { get; private set; }

    // Shared across reel spawns so the same clip doesn't play twice in a row.
    private static int lastPickedIndex = -1;

    private void Awake()
    {
        if (videoImage != null)
            videoImage.enabled = false; // stay hidden until the first frame is ready

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // muted
        }
    }

    private void OnEnable() => PlayRandom();

    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepared;
            videoPlayer.Stop();
        }
    }

    private void PlayRandom()
    {
        if (videoPlayer == null || clips == null || clips.Count == 0)
            return;

        ReelClip chosen = clips[PickIndex()];
        CurrentDescription = chosen != null ? chosen.description : string.Empty;

        // Fill the panel's text: the caption is paired with the chosen clip, while the
        // username and music are independent random picks from their own lists.
        if (captionText != null)
            captionText.text = CurrentDescription;
        if (usernameText != null)
            usernameText.text = PickRandom(usernames);
        if (musicText != null)
            musicText.text = PickRandom(musicTracks);

        if (chosen == null || chosen.clip == null)
            return;

        if (videoImage != null)
            videoImage.enabled = false;

        videoPlayer.clip = chosen.clip;
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare(); // decode the first frame before we show anything
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();

        if (videoImage != null)
            videoImage.enabled = true; // first frame ready — safe to reveal
    }

    /// <summary>A random entry from the pool, or empty string if it's null/empty.</summary>
    private static string PickRandom(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return string.Empty;
        return pool[Random.Range(0, pool.Length)];
    }

    private int PickIndex()
    {
        if (clips.Count == 1)
            return 0;

        int idx;
        do { idx = Random.Range(0, clips.Count); }
        while (idx == lastPickedIndex);

        lastPickedIndex = idx;
        return idx;
    }
}
