using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Drives a full-bleed video background for a Reel.
// Decodes a VideoClip (or URL) into a per-instance RenderTexture and shows it
// on a RawImage, cover-cropped so the video fills the rect without distortion.
[RequireComponent(typeof(VideoPlayer))]
public class ReelVideoBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RawImage targetImage;

    [Header("Source")]
    [Tooltip("Drag a clip here for bundled videos. Leave empty to use the URL below.")]
    [SerializeField] private VideoClip clip;
    [Tooltip("Used only when Clip is empty. e.g. a StreamingAssets path or an https link.")]
    [SerializeField] private string url;

    [Header("Playback")]
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool playAudio = true;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private bool isPrepared;
    private bool muted;
    private float volume = 1f;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loop;
        videoPlayer.waitForFirstFrame = true;      // avoids a black flash on the first frame
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = playAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;

        videoPlayer.prepareCompleted += OnPrepareCompleted;
        ApplySource();
    }

    void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    void OnDisable()
    {
        // Pause instead of stop so the feed can resume where it left off.
        // Skip when the player is already disabled (e.g. during Destroy).
        if (videoPlayer != null && videoPlayer.enabled && videoPlayer.isPrepared)
        {
            videoPlayer.Pause();
        }
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            // Stop decoding and detach the target before the RenderTexture is freed,
            // so the render thread never writes into a destroyed texture.
            videoPlayer.Stop();
            videoPlayer.targetTexture = null;
        }
        if (targetImage != null)
        {
            targetImage.texture = null;
        }
        ReleaseRenderTexture();
    }

    // --- Public API for a feed controller -------------------------------------

    // Mute/unmute the video's own audio track (used to silence off-screen reels).
    public void SetMuted(bool value)
    {
        muted = value;
        ApplyMute();
    }

    private void ApplyMute()
    {
        if (videoPlayer != null && videoPlayer.audioTrackCount > 0)
        {
            videoPlayer.SetDirectAudioMute(0, muted);
        }
    }

    // Sets this video's audio volume (0..1). Driven by the feed's global Video
    // Volume setting; re-applied automatically once the audio track is ready.
    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (videoPlayer != null && videoPlayer.audioTrackCount > 0)
        {
            videoPlayer.SetDirectAudioVolume(0, volume);
        }
    }

    // Swap in a new clip at runtime (e.g. when this reel is recycled in a feed).
    public void SetClip(VideoClip newClip)
    {
        clip = newClip;
        url = null;
        RestartWithNewSource();
    }

    // Swap in a new URL at runtime (StreamingAssets path or remote link).
    public void SetUrl(string newUrl)
    {
        url = newUrl;
        clip = null;
        RestartWithNewSource();
    }

    public void Play()
    {
        if (isPrepared)
        {
            videoPlayer.Play();
        }
        else
        {
            // Prepare first; playback kicks off in OnPrepareCompleted.
            videoPlayer.Prepare();
        }
    }

    public void Pause()
    {
        videoPlayer.Pause();
    }

    public void Stop()
    {
        videoPlayer.Stop();
    }

    // --- Internals ------------------------------------------------------------

    private void ApplySource()
    {
        if (clip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
        }
        else
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = url;
        }
    }

    private void RestartWithNewSource()
    {
        isPrepared = false;
        videoPlayer.Stop();
        ApplySource();
        if (isActiveAndEnabled && playOnEnable)
        {
            Play();
        }
    }

    private void OnPrepareCompleted(VideoPlayer source)
    {
        isPrepared = true;

        // Size the RenderTexture to the video's native resolution for crisp output.
        int width = (int)source.width;
        int height = (int)source.height;
        if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
        {
            ReleaseRenderTexture();
            renderTexture = new RenderTexture(width, height, 0);
            videoPlayer.targetTexture = renderTexture;
            if (targetImage != null)
            {
                targetImage.texture = renderTexture;
            }
        }

        ApplyCoverCrop(width, height);
        ApplyMute();     // re-apply now that the audio track exists
        ApplyVolume();   // ditto for the global volume
        source.Play();
    }

    // Cover-crop: adjust the RawImage uvRect so the video fills the rect
    // without stretching, cropping the overflowing axis (like CSS object-fit: cover).
    private void ApplyCoverCrop(int videoWidth, int videoHeight)
    {
        if (targetImage == null || videoWidth == 0 || videoHeight == 0)
        {
            return;
        }

        Rect rect = targetImage.rectTransform.rect;
        if (rect.width == 0f || rect.height == 0f)
        {
            return;
        }

        float videoAspect = (float)videoWidth / videoHeight;
        float rectAspect = rect.width / rect.height;

        if (rectAspect > videoAspect)
        {
            // Rect is wider than the video: fill width, crop top/bottom.
            float scale = videoAspect / rectAspect;
            targetImage.uvRect = new Rect(0f, (1f - scale) * 0.5f, 1f, scale);
        }
        else
        {
            // Rect is taller than the video: fill height, crop left/right.
            float scale = rectAspect / videoAspect;
            targetImage.uvRect = new Rect((1f - scale) * 0.5f, 0f, scale, 1f);
        }
    }

    private void ReleaseRenderTexture()
    {
        if (renderTexture != null)
        {
            if (videoPlayer != null && videoPlayer.targetTexture == renderTexture)
            {
                videoPlayer.targetTexture = null;
            }
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }
}
