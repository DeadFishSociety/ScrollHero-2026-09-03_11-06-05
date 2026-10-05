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

    [Header("Audio")]
    [Tooltip("Global volume for reel video audio (0 = silent, 1 = full). Applies to every reel.")]
    [Range(0f, 1f)]
    [SerializeField] private float videoVolume = 1f;

    [Header("Feed content")]
    [Tooltip("Each entry becomes one reel as you scroll. Set the video, audio, username, description and audio name here - not on the prefab.")]
    [SerializeField] private ReelPost[] posts;

    [Tooltip("Sequential: play the list in order (loops). WeightedRandom: pick each reel at random using the per-post Weight. Shuffled: play every reel once in a random order, then reshuffle - so nothing repeats until all have been shown.")]
    [SerializeField] private FeedOrder order = FeedOrder.Sequential;

    [Tooltip("WeightedRandom / Shuffled: avoid showing the same post twice in a row (when more than one exists).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Overdrive reel")]
    [Tooltip("The special overdrive reel's content (video + text). Leave its video empty to disable overdrive insertion.")]
    [SerializeField] private ReelPost overdrivePost;

    [Tooltip("Chance for each newly spawned reel to be the overdrive reel.")]
    [Range(0f, 1f)]
    [SerializeField] private float overdriveChancePerReel = 0.04f;

    [Tooltip("No overdrive reel among the first N spawned reels.")]
    [SerializeField] private int overdriveGracePeriodReels = 4;

    [Tooltip("Minimum reels between two overdrive reels.")]
    [SerializeField] private int overdriveMinReelsBetween = 8;

    [Tooltip("Overlay prefab (e.g. an Image with UISpriteAnimation) spawned as a child of the golden reel, so it rides the reel and is visible as it scrolls in. Leave empty for no marker.")]
    [SerializeField] private GameObject overdriveReelOverlayPrefab;

    [Tooltip("Centered \"LIKE NOW\" prompt shown while the golden reel is the current reel and not yet liked. Leave empty for none.")]
    [SerializeField] private GameObject likeNowPrompt;

    [Header("Despair reel")]
    [Tooltip("The despair reel's content (video + text). Leave its video empty to disable despair insertion.")]
    [SerializeField] private ReelPost despairPost;

    [Tooltip("Chance for each newly spawned reel to be the despair reel.")]
    [Range(0f, 1f)]
    [SerializeField] private float despairChancePerReel = 0.04f;

    [Tooltip("No despair reel among the first N spawned reels.")]
    [SerializeField] private int despairGracePeriodReels = 4;

    [Tooltip("Minimum reels between two despair reels.")]
    [SerializeField] private int despairMinReelsBetween = 8;

    [Tooltip("Overlay prefab (Image + UISpriteAnimation) shown full-screen while the despair reel is current. Leave empty for no marker.")]
    [SerializeField] private GameObject despairReelOverlayPrefab;

    [Tooltip("Centered prompt (e.g. \"DOPAMINE DESPAIR\") shown while the despair reel is the current reel. Leave empty for none.")]
    [SerializeField] private GameObject despairPrompt;

    [Header("Last life")]
    [Tooltip("Overdrive (golden) reel spawn chance per reel while on the last life - usually higher than normal, to give a comeback chance.")]
    [Range(0f, 1f)]
    [SerializeField] private float lastLifeOverdriveChancePerReel = 0.35f;

    [Tooltip("Score multiplier applied while on the last life (0 = earn no points). Read by FeedScorer.")]
    [Min(0f)]
    [SerializeField] private float lastLifeScoreMultiplier = 0f;

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

    // Shuffled order: the remaining posts to draw before the bag is refilled.
    private readonly List<int> shuffleBag = new List<int>();

    // Used to check whether the player is on the last life (last-life settings).
    private DopamineManager dopamineManager;

    // The single full-screen special overlays (children of the feed), toggled on/off.
    private GameObject overdriveOverlayInstance;
    private GameObject despairOverlayInstance;

    // Special-reel insertion bookkeeping.
    private int reelsSpawned;
    private int reelsSinceOverdrive = 100000; // large so the first eligible reel can trigger
    private int reelsSinceDespair = 100000;

    // Whether the reel just scrolled away was special (holy/despair); read by
    // LikeCombo so passing a special reel doesn't break the like streak.
    private bool lastScrolledReelWasSpecial;
    public bool LastScrolledReelWasSpecial => lastScrolledReelWasSpecial;

    // The reel currently allowed to play audio (always the top one).
    private RectTransform audioReel;

    // Fired whenever a different reel becomes the top (initial + each recycle).
    // Used by the minigame system to roll a trigger per reel.
    public event System.Action TopReelChanged;

    // Fired the instant a swipe is committed to advancing - on release (before the
    // snap-settle finishes) and on each reel crossed during a fast flick. Use this
    // for scroll-reactive effects that should feel immediate, rather than
    // TopReelChanged which fires only once the new reel has settled into place.
    public event System.Action ScrollCommitted;

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
        dopamineManager = FindFirstObjectByType<DopamineManager>();
    }

    // Score multiplier to apply while on the last life (read by FeedScorer).
    public float LastLifeScoreMultiplier => lastLifeScoreMultiplier;

    void OnEnable()
    {
        OverdriveController.Started += OnOverdriveChanged;
        OverdriveController.Ended += OnOverdriveChanged;
        // Liking a despair reel auto-advances past it, so the despair is over.
        ReelLike.DespairLiked += AdvanceToNextReel;
    }

    void OnDisable()
    {
        OverdriveController.Started -= OnOverdriveChanged;
        OverdriveController.Ended -= OnOverdriveChanged;
        ReelLike.DespairLiked -= AdvanceToNextReel;
    }

#if UNITY_EDITOR
    // Lets the Video Volume slider be tuned live in Play mode: push the new value
    // to every active reel.
    void OnValidate()
    {
        for (int i = 0; i < reels.Count; i++)
        {
            if (reels[i] == null)
            {
                continue;
            }
            ReelVideoBackground video = reels[i].GetComponentInChildren<ReelVideoBackground>(true);
            if (video != null)
            {
                video.SetVolume(videoVolume);
            }
        }
    }
#endif

    void Start()
    {
        if (reelPrefab == null)
        {
            Debug.LogError("ReelFeedController: Reel Prefab is not assigned.", this);
            enabled = false;
            return;
        }

        overdriveOverlayInstance = CreateSpecialOverlay(overdriveReelOverlayPrefab);
        despairOverlayInstance = CreateSpecialOverlay(despairReelOverlayPrefab);

        for (int i = 0; i < BufferCount; i++)
        {
            SpawnReelAtEnd();
        }
        LayoutReels();
        UpdateCurrentAudio();
    }

    // Spawns one full-screen special overlay as a child of the Feed (so it fills the
    // whole screen, not the safe area), starting hidden.
    private GameObject CreateSpecialOverlay(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, rectTransform);
        RectTransform ort = instance.transform as RectTransform;
        if (ort != null)
        {
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.sizeDelta = Vector2.zero;
            ort.anchoredPosition = Vector2.zero;
            ort.pivot = new Vector2(0.5f, 0.5f);
            ort.SetAsLastSibling(); // draw over the reels
        }
        instance.SetActive(false);
        return instance;
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
            // Capture the reel being passed BEFORE it's recycled.
            bool passedWasSpecial = ReelIsSpecial(reels.Count > 0 ? reels[0] : null);
            Recycle();
            offset -= height;
            CommitScroll(passedWasSpecial);
        }

        LayoutReels();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;

        bool advance = offset > ViewportHeight * advanceThreshold
                       || lastDragVelocity > flickVelocity;
        targetOffset = advance ? ViewportHeight : 0f;

        // Notify immediately on commit, so scroll-reactive effects don't wait for
        // the snap-settle to finish. The reel being passed is still the current top.
        if (advance)
        {
            CommitScroll(ReelIsSpecial(reels.Count > 0 ? reels[0] : null));
        }
    }

    // Records whether the passed reel was special, then raises ScrollCommitted.
    private void CommitScroll(bool passedWasSpecial)
    {
        lastScrolledReelWasSpecial = passedWasSpecial;
        ScrollCommitted?.Invoke();
    }

    // Programmatically advance to the next reel (e.g. after liking a despair reel).
    // The Update snap then recycles the current reel, so its overlay/prompt clear.
    public void AdvanceToNextReel()
    {
        float height = ViewportHeight;
        if (height <= 0f || dragging)
        {
            return;
        }

        bool passedWasSpecial = ReelIsSpecial(reels.Count > 0 ? reels[0] : null);
        targetOffset = height;
        CommitScroll(passedWasSpecial);
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
            video.SetVolume(videoVolume);
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

        // Show/hide the special overlays + prompts for the current reel.
        RefreshOverdriveOverlay(top);
        RefreshDespairOverlay(top);
        RefreshGoldenPrompt(top);
        RefreshDespairPrompt(top);

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

    // Hands the spawned reel its content: usually the next normal post, but
    // occasionally the special overdrive reel.
    private void AssignPost(GameObject reel)
    {
        reelsSpawned++;
        if (reelsSinceOverdrive < 1000000)
        {
            reelsSinceOverdrive++;
        }
        if (reelsSinceDespair < 1000000)
        {
            reelsSinceDespair++;
        }

        ReelSpecial special = ReelSpecial.None;
        ReelPost post;

        if (ShouldSpawnOverdrive())
        {
            special = ReelSpecial.Overdrive;
            post = overdrivePost;
            reelsSinceOverdrive = 0;
        }
        else if (ShouldSpawnDespair())
        {
            special = ReelSpecial.Despair;
            post = despairPost;
            reelsSinceDespair = 0;
        }
        else
        {
            if (posts == null || posts.Length == 0)
            {
                MarkReelSpecial(reel, ReelSpecial.None);
                return;
            }
            int index;
            switch (order)
            {
                case FeedOrder.WeightedRandom:
                    index = PickWeightedIndex();
                    break;
                case FeedOrder.Shuffled:
                    index = PickShuffledIndex();
                    break;
                default:
                    index = nextPostIndex++ % posts.Length;
                    break;
            }
            lastPostIndex = index;
            post = posts[index];
        }

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

        MarkReelSpecial(reel, special);
    }

    // Decides whether this freshly spawned reel should be the overdrive reel:
    // needs a configured overdrive post, not already in overdrive, and past the
    // grace period and cooldown.
    private bool ShouldSpawnOverdrive()
    {
        if (overdrivePost == null || overdrivePost.video == null)
        {
            return false;
        }
        if (OverdriveController.IsActive)
        {
            return false;
        }
        if (reelsSpawned <= overdriveGracePeriodReels || reelsSinceOverdrive < overdriveMinReelsBetween)
        {
            return false;
        }

        // On the last life the golden reel is more likely (a comeback chance).
        bool lastLife = dopamineManager != null && dopamineManager.IsLastLife;
        float chance = lastLife ? lastLifeOverdriveChancePerReel : overdriveChancePerReel;
        return Random.value <= chance;
    }

    // Same as ShouldSpawnOverdrive, for the despair reel.
    private bool ShouldSpawnDespair()
    {
        if (despairPost == null || despairPost.video == null)
        {
            return false;
        }
        if (OverdriveController.IsActive)
        {
            return false;
        }
        if (reelsSpawned <= despairGracePeriodReels || reelsSinceDespair < despairMinReelsBetween)
        {
            return false;
        }
        return Random.value <= despairChancePerReel;
    }

    // Flags the reel's ReelLike with its special kind so liking it fires the right
    // event. The visual markers are the full-screen overlays on the feed.
    private void MarkReelSpecial(GameObject reel, ReelSpecial special)
    {
        ReelLike like = reel.GetComponentInChildren<ReelLike>(true);
        if (like != null)
        {
            like.Special = special;
        }
    }

    // Whether the current top reel is the overdrive (golden) reel.
    public bool TopReelIsOverdrive => SpecialOf(reels.Count > 0 ? reels[0] : null) == ReelSpecial.Overdrive;

    // Whether the current top reel is any special reel (holy or despair).
    public bool TopReelIsSpecial => SpecialOf(reels.Count > 0 ? reels[0] : null) != ReelSpecial.None;

    private static ReelSpecial SpecialOf(RectTransform reel)
    {
        if (reel == null)
        {
            return ReelSpecial.None;
        }
        ReelLike like = reel.GetComponentInChildren<ReelLike>(true);
        return like != null ? like.Special : ReelSpecial.None;
    }

    private static bool ReelIsOverdrive(RectTransform reel) => SpecialOf(reel) == ReelSpecial.Overdrive;
    private static bool ReelIsDespair(RectTransform reel) => SpecialOf(reel) == ReelSpecial.Despair;
    private static bool ReelIsSpecial(RectTransform reel) => SpecialOf(reel) != ReelSpecial.None;

    // Shows the full-screen overdrive overlay while the golden reel is the current
    // reel OR overdrive mode is running (so it keeps playing for the whole round).
    private void RefreshOverdriveOverlay(RectTransform top)
    {
        if (overdriveOverlayInstance == null)
        {
            return;
        }

        bool show = ReelIsOverdrive(top) || OverdriveController.IsActive;
        if (show)
        {
            // Reels are recycled as later siblings, so re-assert the overlay on top.
            overdriveOverlayInstance.transform.SetAsLastSibling();
        }
        if (overdriveOverlayInstance.activeSelf != show)
        {
            overdriveOverlayInstance.SetActive(show);
        }
    }

    // Shows the full-screen despair overlay while the despair reel is the current reel.
    private void RefreshDespairOverlay(RectTransform top)
    {
        if (despairOverlayInstance == null)
        {
            return;
        }

        bool show = ReelIsDespair(top);
        if (show)
        {
            despairOverlayInstance.transform.SetAsLastSibling();
        }
        if (despairOverlayInstance.activeSelf != show)
        {
            despairOverlayInstance.SetActive(show);
        }
    }

    // Shows the centered "LIKE NOW" prompt only while the golden reel is the current
    // reel and overdrive hasn't started yet.
    private void RefreshGoldenPrompt(RectTransform top)
    {
        if (likeNowPrompt == null)
        {
            return;
        }

        bool show = ReelIsOverdrive(top) && !OverdriveController.IsActive;
        if (likeNowPrompt.activeSelf != show)
        {
            likeNowPrompt.SetActive(show);
        }
    }

    // Shows the centered "DOPAMINE DESPAIR" prompt while the despair reel is current.
    private void RefreshDespairPrompt(RectTransform top)
    {
        if (despairPrompt == null)
        {
            return;
        }

        bool show = ReelIsDespair(top);
        if (despairPrompt.activeSelf != show)
        {
            despairPrompt.SetActive(show);
        }
    }

    // Re-evaluate the overlay + prompt when overdrive begins/ends (not only on reel change).
    private void OnOverdriveChanged()
    {
        RectTransform top = reels.Count > 0 ? reels[0] : null;
        RefreshOverdriveOverlay(top);
        RefreshGoldenPrompt(top);
    }

    // Draws the next post from a shuffled bag that contains every post index once.
    // When the bag empties it is refilled and reshuffled, so no post repeats until
    // all of them have been shown.
    private int PickShuffledIndex()
    {
        if (shuffleBag.Count == 0)
        {
            RefillShuffleBag();
        }

        // Draw from the end (cheap removal).
        int last = shuffleBag.Count - 1;
        int index = shuffleBag[last];
        shuffleBag.RemoveAt(last);
        return index;
    }

    // Fills the bag with every post index and Fisher-Yates shuffles it. Optionally
    // prevents the first draw of the new bag from repeating the post just shown
    // (the only place a repeat could sneak in - across the seam between bags).
    private void RefillShuffleBag()
    {
        shuffleBag.Clear();
        for (int i = 0; i < posts.Length; i++)
        {
            shuffleBag.Add(i);
        }
        for (int i = shuffleBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffleBag[i], shuffleBag[j]) = (shuffleBag[j], shuffleBag[i]);
        }

        // The next draw is the last element; if it matches the last post shown,
        // swap it to the front so the seam doesn't repeat.
        int lastSlot = shuffleBag.Count - 1;
        if (avoidImmediateRepeat && posts.Length > 1 && shuffleBag[lastSlot] == lastPostIndex)
        {
            (shuffleBag[lastSlot], shuffleBag[0]) = (shuffleBag[0], shuffleBag[lastSlot]);
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
    WeightedRandom,
    // Shuffled "bag": plays every post once in a random order, then reshuffles and
    // repeats - so no post repeats until all of them have been shown.
    Shuffled
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
