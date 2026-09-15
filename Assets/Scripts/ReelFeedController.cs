using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Video;

// Instagram-style vertical reel feed.
// Place this on a full-screen container (a RectTransform stretched to fill the
// Canvas). It spawns reels as children, moves them 1:1 with the finger while
// dragging, snaps to the nearest reel on release, and recycles reels that
// scroll off the top so the user can scroll forever.
//
// Drag events (IDragHandler) require the container to be hit by the raycaster,
// so give this object an Image with Raycast Target on (a fully transparent one
// is fine).
[RequireComponent(typeof(RectTransform))]
public class ReelFeedController : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Spawning")]
    [Tooltip("The Reel prefab to spawn. Drag Assets/Prefabs/Reel.prefab here.")]
    [SerializeField] private GameObject reelPrefab;

    [Header("Feed content")]
    [Tooltip("Each entry becomes one reel as you scroll. Set the video, audio, username, description and audio name here - not on the prefab.")]
    [SerializeField] private ReelPost[] posts;

    [Tooltip("Sequential: play the list in order (loops). WeightedRandom: pick each reel at random using the per-post Weight.")]
    [SerializeField] private FeedOrder order = FeedOrder.Sequential;

    [Tooltip("WeightedRandom only: avoid showing the same post twice in a row (when more than one exists).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Feel")]
    [Tooltip("Fraction of a screen you must drag past for it to advance to the next reel on release.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float advanceThreshold = 0.2f;

    [Tooltip("Flick speed (canvas units/sec) that advances even on a short drag.")]
    [SerializeField] private float flickVelocity = 1200f;

    [Tooltip("How quickly a reel settles into place after you let go. Higher = snappier.")]
    [SerializeField] private float snapSpeed = 14f;

    // How many reels to keep alive: the current one plus this many below it.
    private const int BufferCount = 3;

    private RectTransform rectTransform;
    private Canvas canvas;

    // reels[0] = the one in view, reels[1] = the one below it, etc.
    private readonly List<RectTransform> reels = new List<RectTransform>();
    private int nextPostIndex;
    private int lastPostIndex = -1;

    // The reel currently allowed to play audio (always the top one).
    private RectTransform audioReel;

    // Fired whenever a different reel becomes the top (initial + each recycle).
    // Used by the minigame system to roll a trigger per reel.
    public event System.Action TopReelChanged;

    // How far the feed has been scrolled within the current reel.
    // Kept in the range [0, ViewportHeight); increases as the user swipes up.
    private float offset;
    private float targetOffset;
    private bool dragging;
    private float lastDragVelocity;

    // One reel fills the whole container; read live so it adapts to any aspect ratio.
    private float ViewportHeight => rectTransform.rect.height;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    void Start()
    {
        if (reelPrefab == null)
        {
            Debug.LogError("ReelFeedController: Reel Prefab is not assigned.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < BufferCount; i++)
        {
            SpawnReelAtEnd();
        }
        LayoutReels();
        UpdateCurrentAudio();
    }

    void Update()
    {
        if (dragging)
        {
            return;
        }

        float height = ViewportHeight;
        if (height <= 0f)
        {
            return;
        }

        // Ease toward the settle target (0 = stay on current reel, height = advance).
        offset = Mathf.Lerp(offset, targetOffset, Time.deltaTime * snapSpeed);

        if (targetOffset >= height && offset >= height - 0.5f)
        {
            // Finished advancing: drop the top reel, spawn a new one, reset.
            Recycle();
            offset -= height;
            targetOffset = 0f;
        }

        LayoutReels();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        lastDragVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float height = ViewportHeight;
        if (height <= 0f)
        {
            return;
        }

        // Convert the screen-space finger delta into canvas units for 1:1 movement.
        float scale = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
        float deltaY = eventData.delta.y / scale;

        offset += deltaY;
        lastDragVelocity = deltaY / Mathf.Max(Time.deltaTime, 0.0001f);

        // Can't scroll back past the current reel (forward-only feed).
        if (offset < 0f)
        {
            offset = 0f;
        }

        // Handle fast multi-reel swipes without releasing: recycle as we cross each screen.
        while (offset >= height)
        {
            Recycle();
            offset -= height;
        }

        LayoutReels();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;

        bool advance = offset > ViewportHeight * advanceThreshold
                       || lastDragVelocity > flickVelocity;
        targetOffset = advance ? ViewportHeight : 0f;
    }

    // Positions every active reel: reel i sits i screens below the top,
    // shifted up by the current scroll offset.
    private void LayoutReels()
    {
        float height = ViewportHeight;
        for (int i = 0; i < reels.Count; i++)
        {
            Vector2 pos = reels[i].anchoredPosition;
            pos.x = 0f;
            pos.y = offset - i * height;
            reels[i].anchoredPosition = pos;
        }
    }

    private void SpawnReelAtEnd()
    {
        GameObject go = Instantiate(reelPrefab, rectTransform);
        RectTransform rt = go.GetComponent<RectTransform>();

        // Force full-stretch so each reel fills the container exactly.
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        reels.Add(rt);
        AssignPost(go);

        // New reels always spawn below the top, so start them muted.
        ReelVideoBackground video = go.GetComponentInChildren<ReelVideoBackground>(true);
        if (video != null)
        {
            video.SetMuted(true);
        }

        LayoutReels();
    }

    private void Recycle()
    {
        if (reels.Count == 0)
        {
            return;
        }

        RectTransform top = reels[0];
        reels.RemoveAt(0);
        Destroy(top.gameObject);

        SpawnReelAtEnd();

        // The old top (which was playing) is gone; hand audio to the new top.
        UpdateCurrentAudio();
    }

    // Ensures only the current top reel plays audio. Called when the top changes.
    private void UpdateCurrentAudio()
    {
        RectTransform top = reels.Count > 0 ? reels[0] : null;
        if (top == audioReel)
        {
            return;
        }

        // Silence the previous top (skipped automatically if it was just destroyed).
        SetReelAudio(audioReel, false);

        audioReel = top;

        // Unmute + play the new top.
        SetReelAudio(top, true);

        // Notify listeners (e.g. the minigame manager) that the reel changed.
        TopReelChanged?.Invoke();
    }

    // Mute or restore the current top reel's audio (video track + custom clip).
    // Used to duck the reel behind a minigame overlay.
    public void SetTopReelAudio(bool on)
    {
        SetReelAudio(audioReel, on);
    }

    // Turns a reel's audio on or off: both the custom ReelContent clip and the
    // video's own audio track.
    private void SetReelAudio(RectTransform reel, bool on)
    {
        if (reel == null)
        {
            return;
        }

        ReelContent content = reel.GetComponentInChildren<ReelContent>(true);
        if (content != null)
        {
            if (on)
            {
                content.PlayAudio();
            }
            else
            {
                content.StopAudio();
            }
        }

        ReelVideoBackground video = reel.GetComponentInChildren<ReelVideoBackground>(true);
        if (video != null)
        {
            video.SetMuted(!on);
        }
    }

    // Hands the spawned reel the next post's content: video, audio and text.
    private void AssignPost(GameObject reel)
    {
        if (posts == null || posts.Length == 0)
        {
            return;
        }

        int index = (order == FeedOrder.WeightedRandom) ? PickWeightedIndex() : (nextPostIndex++ % posts.Length);
        lastPostIndex = index;
        ReelPost post = posts[index];

        ReelVideoBackground video = reel.GetComponentInChildren<ReelVideoBackground>(true);
        if (video != null && post.video != null)
        {
            video.SetClip(post.video);
        }

        ReelContent content = reel.GetComponentInChildren<ReelContent>(true);
        if (content != null)
        {
            content.SetContent(post.username, post.description, post.audioName, post.audio);
        }
    }

    // Picks a post index at random, biased by each post's Weight. Optionally
    // avoids repeating the last post when more than one is available.
    private int PickWeightedIndex()
    {
        // Total weight of the eligible posts (optionally skipping the last one).
        float total = 0f;
        for (int i = 0; i < posts.Length; i++)
        {
            if (avoidImmediateRepeat && i == lastPostIndex && posts.Length > 1)
            {
                continue;
            }
            total += Mathf.Max(0f, posts[i].weight);
        }

        // No usable weights: fall back to a uniform pick over all posts.
        if (total <= 0f)
        {
            return Random.Range(0, posts.Length);
        }

        float roll = Random.value * total;
        for (int i = 0; i < posts.Length; i++)
        {
            if (avoidImmediateRepeat && i == lastPostIndex && posts.Length > 1)
            {
                continue;
            }
            roll -= Mathf.Max(0f, posts[i].weight);
            if (roll <= 0f)
            {
                return i;
            }
        }

        // Floating-point safety net.
        return posts.Length - 1;
    }
}

// How the feed picks which post to show next.
public enum FeedOrder
{
    Sequential,
    WeightedRandom
}

// One post in the feed. Fill these in on the ReelFeedController's Posts array.
[System.Serializable]
public class ReelPost
{
    public VideoClip video;
    public AudioClip audio;
    public string username = "SuperCoolUser_Name";
    [TextArea(2, 5)]
    public string description;
    public string audioName = "Original audio";

    [Tooltip("Relative chance of being picked in WeightedRandom order. Higher = more often. Ignored in Sequential order.")]
    [Min(0f)]
    public float weight = 1f;
}
