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
        if (videoPlayer != null)
        {
            videoPlayer.Pause();
        }
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
        }
        ReleaseRenderTexture();
    }

    // --- Public API for a feed controller -------------------------------------

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
