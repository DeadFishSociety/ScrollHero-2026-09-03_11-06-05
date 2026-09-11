using UnityEngine;
using TMPro;
using System.Collections;

public class FeedManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private FeedItem scrollPanelPrefab;

    [Tooltip("The pool of minigame overlays that can appear, and how one is chosen. " +
             "Tick/untick entries here to decide which minigames are in rotation.")]
    [SerializeField] private OverlayPicker overlayPicker = new OverlayPicker();

    [Header("References")]
    [SerializeField] private RectTransform feedContainer;

    [Tooltip("Canvas-level container the minigame overlay is spawned into, on top of the " +
             "current reel. Make it a full-screen stretched RectTransform above the feed so " +
             "the overlay covers the reel below and swallows input until it's beaten.")]
    [SerializeField] private RectTransform overlayContainer;

    [SerializeField] private SwipeInput swipeInput;

    [Tooltip("A screen-level effect that plays when the player swipes past a normal reel.")]
    [SerializeField] private ReelSpriteAnimation scrollAnimation;

    [Tooltip("Shows how many reels have been scrolled (not the score).")]
    [SerializeField] private TMP_Text scrollCountText;

    [Tooltip("Optional. Shows the score — only successful actions raise it.")]
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("The single shared dopamine gauge on the Canvas. Whichever reel/minigame is " +
             "active drives it; prefabs no longer carry their own gauge.")]
    [SerializeField] private DopamineGauge dopamineGauge;

    [Tooltip("The dog health indicator — shows current health as a frame and shakes on a hit.")]
    [SerializeField] private HealthDisplay healthDisplay;

    [Tooltip("Game-over UI. Revealed after the death explosion plays.")]
    [SerializeField] private GameOverScreen gameOverScreen;

    [Header("Settings")]
    [Tooltip("Every this many scrolls, the next reel also carries a minigame overlay on top " +
             "of it that must be beaten (or time out) before the player can scroll on.")]
    [SerializeField] private int actionEveryNScrolls = 5;

    [Tooltip("Lives the player starts with. Each time a dopamine timer empties " +
             "before the reel/action is cleared, one life is lost.")]
    [SerializeField, Min(1)] private int startingLives = 5;

    [Tooltip("When a minigame overlay fails (e.g. times out), close it and let the reel below " +
             "continue as a normal timed reel. Untick to make the player keep retrying the " +
             "same minigame until they beat it.")]
    [SerializeField] private bool advanceOnActionFail = true;

    [Header("Screen time")]
    [Tooltip("The screen-time limit popup, raised on top of the current reel after a " +
             "stretch of play. Leave empty to disable the feature entirely.")]
    [SerializeField] private ScreenTimeOverlay screenTimePrefab;

    [Tooltip("Seconds of play before the screen-time popup first appears. After that it " +
             "returns whenever the time the player chose to 'extend' for runs out.")]
    [SerializeField, Min(1f)] private float firstScreenTimeDelay = 30f;
    [Header("Scoring")]
    [Tooltip("Points added each time the player scrolls past a reel.")]
    [SerializeField] private int scrollPoints = 100;

    [Tooltip("Points added when a minigame is completed successfully.")]
    [SerializeField] private int minigamePoints = 1000;

    [Tooltip("Bonus points added when the player likes a reel.")]
    [SerializeField] private int likePoints = 100;

    [Header("Audio")]
    [Tooltip("Sound played when the player scrolls to the next reel. Add clip variations — " +
             "weighted or not depending on the mode chosen.")]
    [SerializeField] private SoundEffect scrollSound = new SoundEffect();

    [Tooltip("Sound played when a minigame is completed successfully. Add clip variations — " +
             "weighted or not depending on the mode chosen.")]
    [SerializeField] private SoundEffect minigameCompleteSound = new SoundEffect();

    [Tooltip("General background music. Add one or more tracks (weighted); loop a single track " +
             "or play them as a continuous playlist.")]
    [SerializeField] private BackgroundMusic backgroundMusic = new BackgroundMusic();

    private int scrollCount;
    private int score;
    private int lives;
    private bool gameOver;
    private FeedItem currentItem;
    private FeedOverlay currentOverlay;
    private ReelTimer currentReelTimer;
    private ReelLike currentReelLike;
    private bool actionInProgress;
    private float screenTimeCountdown;

    /// <summary>Description paired with the current scroll reel's video. For later use
    /// (e.g. an overlay caption). Empty until the first video reel has spawned.</summary>
    public string CurrentReelDescription { get; private set; } = string.Empty;

    /// <summary>Fired once when lives reach zero. Hook a lose screen here later.</summary>
    public event System.Action GameOver;

    void Start()
    {
        swipeInput.OnSwipe += HandleSwipe;
        swipeInput.OnDoubleTap += HandleDoubleTap;
        lives = startingLives;
        UpdateScoreText();
        if (healthDisplay != null)
            healthDisplay.SetHealth(lives, startingLives);
        screenTimeCountdown = firstScreenTimeDelay;
        backgroundMusic.Play();
        SpawnScroll(false);
    }

    void OnDestroy()
    {
        swipeInput.OnSwipe -= HandleSwipe;
        swipeInput.OnDoubleTap -= HandleDoubleTap;
        backgroundMusic.Stop();
        DetachOverlay();
        DetachReelTimer();
        DetachReelLike();
    }

    /// <summary>
    /// Central scoring: add points, refresh the label, and play the shared points-gained
    /// animation (the ScrollAnimationOverlay). Every score source routes through here.
    /// </summary>
    private void AddScore(int amount, bool playAnimation = true)
    {
        score += amount;
        UpdateScoreText();
        if (playAnimation)
            scrollAnimation?.PlayFromStart();
    }

    /// <summary>A double-tap: like the current reel, unless a minigame is blocking input.</summary>
    private void HandleDoubleTap()
    {
        if (gameOver)
            return;

        // While a minigame is up and swallowing input, the tap belongs to it, not a like.
        if (actionInProgress && currentOverlay != null && currentOverlay.BlocksSwipe)
            return;

        if (currentReelLike != null)
            currentReelLike.OnDoubleTap();
    }

    private void OnReelLiked(ReelLike like)
    {
        // If the reel plays its own like animation (next to the heart), skip the shared
        // ScrollAnimationOverlay so it doesn't double up.
        bool playShared = like == null || !like.HasLikeAnimation;
        AddScore(likePoints, playShared);
    }

    void Update()
    {
        // Keep the background-music playlist advancing regardless of game state.
        backgroundMusic.Tick();

        // Count down toward the next screen-time popup. The clock only advances during
        // normal scrolling: it pauses while another overlay is up (a minigame, or the
        // popup itself) and stops for good once the game is over.
        if (gameOver || screenTimePrefab == null)
            return;
        if (actionInProgress || currentOverlay != null)
            return;

        screenTimeCountdown -= Time.deltaTime;
        if (screenTimeCountdown <= 0f)
            RaiseScreenTimeOverlay();
    }

    private void HandleSwipe(SwipeDirection direction)
    {
        if (gameOver)
            return; // feed is frozen once the player is out of lives

        // While an action overlay is up, hand it the swipe (swipe-based overlays like
        // the call minigame use it; tap-based ones ignore it). If it blocks swipe,
        // stop here so you can't scroll past the action.
        if (actionInProgress && currentOverlay != null)
        {
            currentOverlay.OnSwipeInput(direction);
            if (currentOverlay.BlocksSwipe)
                return;
        }

        if (direction != SwipeDirection.Up)
            return; // only an upward swipe counts as "scrolling"

        scrollCount++;
        if (scrollCountText != null)
            scrollCountText.text = $"Scrolls: {scrollCount}";

        scrollSound.Play();

        // Scrolling past a reel scores. AddScore also plays the shared points-gained
        // animation (ScrollAnimationOverlay), which lives outside the reel prefab so
        // destroying that prefab cannot cut the animation off.
        AddScore(scrollPoints);

        // Every N scrolls the next reel also carries a minigame overlay on top of it.
        bool withMinigame = scrollCount % actionEveryNScrolls == 0 && overlayPicker.HasAny;
        SpawnScroll(withMinigame);
    }

    /// <summary>
    /// Spawn the next video reel. When <paramref name="withMinigame"/> is true, a minigame
    /// overlay is dropped on top of it once it slides in: the reel's own timer stays paused
    /// and the overlay blocks scrolling until the player beats it (or it times out). A plain
    /// reel just starts its dopamine countdown once it has slid into place.
    /// </summary>
    private void SpawnScroll(bool withMinigame)
    {
        actionInProgress = false;
        SpawnPanel();

        // Remember this reel's video description so it can be used later (e.g. an
        // overlay caption). ReelVideo picks its clip in OnEnable during Instantiate,
        // so CurrentDescription is already set by the time we read it here.
        ReelVideo reelVideo = currentItem != null ? currentItem.GetComponentInChildren<ReelVideo>() : null;
        if (reelVideo != null)
            CurrentReelDescription = reelVideo.CurrentDescription;

        // Every reel is timed: the player must swipe up before its dopamine timer empties,
        // or a life is lost. On a minigame reel the timer is primed (full gauge) but left
        // paused — the overlay's own timer drives the gauge until the minigame resolves.
        currentReelTimer = currentItem != null ? currentItem.GetComponentInChildren<ReelTimer>() : null;
        ReelTimer reelTimer = currentReelTimer;
        if (reelTimer != null)
        {
            reelTimer.Expired += OnReelExpired;
            reelTimer.SetGauge(dopamineGauge); // drive the shared HUD gauge
            reelTimer.Prime(); // show a full gauge while it slides in
        }

        // The reel can be liked (double-tap and/or heart button) for bonus points.
        currentReelLike = currentItem != null ? currentItem.GetComponentInChildren<ReelLike>() : null;
        if (currentReelLike != null)
            currentReelLike.Liked += OnReelLiked;

        // Wait until the reel has finished sliding into place, then either raise the
        // minigame overlay on top of it or start the plain reel's countdown.
        FeedItem item = currentItem;
        SlidePanelIn(() =>
        {
            if (currentItem != item)
                return; // the reel was swiped away / replaced mid-slide

            if (withMinigame)
                SpawnMinigameOverlay();
            else
                BeginReelTimer();
        });
    }

    private void OnReelExpired()
    {
        // Timed out before the player swiped away — lose a life, then move on.
        // (This wasn't a scroll, so scrollCount is left unchanged.)
        LoseLife();
        if (gameOver)
            return;

        SpawnScroll(false);
    }

    /// <summary>Start the current reel's dopamine countdown (a no-op if it has no timer).</summary>
    private void BeginReelTimer()
    {
        if (currentReelTimer != null)
            currentReelTimer.Begin();
    }

    /// <summary>
    /// Raise a minigame overlay on top of the current reel, in the canvas-level overlay
    /// container. The overlay drives the shared gauge and blocks scrolling until it is
    /// beaten or times out. If nothing is usable in the pool, the reel just becomes a
    /// normal timed reel instead of stalling.
    /// </summary>
    private void SpawnMinigameOverlay()
    {
        FeedOverlay prefab = overlayPicker.Pick();
        RectTransform root = overlayContainer != null
            ? overlayContainer
            : (currentItem != null ? currentItem.ResolveOverlayRoot() : null);

        if (prefab == null || root == null)
        {
            BeginReelTimer(); // nothing to show — fall back to a plain timed reel
            return;
        }

        // worldPositionStays=false so the overlay keeps the size, anchors, position and
        // scale authored in its prefab. Intentionally NOT stretched to fill — each overlay
        // controls its own layout via its prefab RectTransform (a small centered heart, a
        // full-screen ad, ...).
        currentOverlay = Instantiate(prefab, root, false);
        currentOverlay.Completed += OnOverlayCompleted;
        currentOverlay.Failed += OnOverlayFailed;
        currentOverlay.GameOverRequested += OnOverlayGameOver;
        currentOverlay.SetGauge(dopamineGauge); // drive the shared HUD gauge

        actionInProgress = true;
        currentOverlay.Begin();      // set the minigame up + prime the gauge
        currentOverlay.StartTimer(); // the reel has already slid in, so start counting now
    }

    /// <summary>
    /// Raise the screen-time limit popup on top of the current reel. The reel's own
    /// dopamine countdown is cancelled while the popup is up; the popup drives the shared
    /// gauge with its own short timer. Closing it via "extend" scrolls on to the next
    /// reel and schedules the popup's return; "quit" ends the run.
    /// </summary>
    private void RaiseScreenTimeOverlay()
    {
        RectTransform root = overlayContainer != null
            ? overlayContainer
            : (currentItem != null ? currentItem.ResolveOverlayRoot() : null);

        if (screenTimePrefab == null || root == null)
        {
            ScheduleNextScreenTime(firstScreenTimeDelay); // can't show it now — try again later
            return;
        }

        // Cancel the current video's dopamine timer: it neither drains nor can cost a
        // life while the popup owns the screen.
        if (currentReelTimer != null)
            currentReelTimer.Stop();

        currentOverlay = Instantiate(screenTimePrefab, root, false);
        currentOverlay.Completed += OnOverlayCompleted;
        currentOverlay.Failed += OnOverlayFailed;
        currentOverlay.GameOverRequested += OnOverlayGameOver;
        currentOverlay.SetGauge(dopamineGauge);

        actionInProgress = true;
        currentOverlay.Begin();
        currentOverlay.StartTimer();
    }

    /// <summary>Set how long (seconds) until the screen-time popup next appears.</summary>
    private void ScheduleNextScreenTime(float seconds)
    {
        screenTimeCountdown = Mathf.Max(1f, seconds);
    }

    private void OnOverlayCompleted(FeedOverlay overlay)
    {
        AddScore(minigamePoints);

        // Play the win jingle only for real minigames — the screen-time popup is a choice,
        // not a challenge (ScoresOnComplete == false), so it stays silent here.
        if (overlay != null && overlay.ScoresOnComplete)
            minigameCompleteSound.Play();

        // A drag-based minigame (e.g. the phone) completes mid-gesture. Drop that
        // in-progress press so lifting the finger doesn't linger into a scroll swipe.
        if (swipeInput != null)
            swipeInput.CancelCurrentGesture();

        // The screen-time popup schedules its own return based on how long the player
        // chose to "extend" for.
        if (overlay is ScreenTimeOverlay screenTime)
            ScheduleNextScreenTime(screenTime.ChosenExtendSeconds);

        bool advance = overlay.AdvancesFeedOnComplete;
        DetachOverlay();

        if (advance)
            SpawnScroll(false); // scroll on to the next reel (screen-time popup)
        else
            BeginReelTimer();   // hand the reel below back as a normal timed reel
    }

    private void OnOverlayFailed(FeedOverlay overlay)
    {
        // Drop any in-progress press so a lingering release doesn't scroll the reel.
        if (swipeInput != null)
            swipeInput.CancelCurrentGesture();

        // The minigame's dopamine timer ran out (or the player gave up) — lose a life.
        LoseLife();
        if (gameOver)
            return;

        // A screen-time popup that timed out (no choice made) reschedules itself with
        // the default delay and always scrolls on, whatever the minigame retry setting is.
        if (overlay is ScreenTimeOverlay)
            ScheduleNextScreenTime(firstScreenTimeDelay);

        bool advance = overlay.AdvancesFeedOnComplete;

        if (advanceOnActionFail || advance)
        {
            // Close the overlay; scroll on, or let the reel below continue as normal.
            DetachOverlay();
            if (advance)
                SpawnScroll(false);
            else
                BeginReelTimer();
        }
        else
        {
            // Let the player try the same minigame again in place.
            overlay.Begin();
            overlay.StartTimer();
        }
    }

    private void OnOverlayGameOver(FeedOverlay overlay)
    {
        // Drop any in-progress press so a lingering release doesn't scroll the reel.
        if (swipeInput != null)
            swipeInput.CancelCurrentGesture();

        // The screen-time "quit" choice: end the run immediately. EndGame tears the
        // overlay down for us.
        EndGame();
    }

    private void LoseLife()
    {
        if (gameOver)
            return;

        lives = Mathf.Max(0, lives - 1);

        if (lives <= 0)
        {
            EndGame(); // the fatal hit: the dog explodes instead of a normal shake
        }
        else if (healthDisplay != null)
        {
            healthDisplay.PlayDamage(lives, startingLives); // flash + shake, settle on new frame
        }
    }

    private void EndGame()
    {
        gameOver = true;
        DetachReelTimer();
        DetachOverlay();

        // No more reels are timed once the game is over — hide the shared dopamine
        // gauge so it doesn't linger, frozen, over the game-over screen.
        if (dopamineGauge != null)
            dopamineGauge.gameObject.SetActive(false);

        Debug.Log("[FeedManager] GAME OVER — out of lives.");
        GameOver?.Invoke();
        // Feed is frozen: HandleSwipe ignores input and nothing new is spawned.

        // Fade the game-over screen into view, then explode the dog once it's fully
        // visible. If there's no game-over screen, explode straight away.
        if (gameOverScreen != null)
            gameOverScreen.Show(PlayDeathExplosion);
        else
            PlayDeathExplosion();
    }

    private void PlayDeathExplosion()
    {
        if (healthDisplay != null)
            healthDisplay.PlayExplosion(null);
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    private void DetachOverlay()
    {
        actionInProgress = false;

        if (currentOverlay == null)
            return;

        currentOverlay.Completed -= OnOverlayCompleted;
        currentOverlay.Failed -= OnOverlayFailed;
        currentOverlay.GameOverRequested -= OnOverlayGameOver;
        // The overlay lives in its own canvas container now (not as a child of the reel),
        // so it won't be destroyed with the reel — tear it down explicitly.
        Destroy(currentOverlay.gameObject);
        currentOverlay = null;
    }

    private void DetachReelTimer()
    {
        if (currentReelTimer == null)
            return;

        currentReelTimer.Expired -= OnReelExpired;
        currentReelTimer.Stop();
        currentReelTimer = null;
    }

    private void DetachReelLike()
    {
        if (currentReelLike == null)
            return;

        currentReelLike.Liked -= OnReelLiked;
        currentReelLike = null; // the component is destroyed with its reel panel
    }

    private void SpawnPanel()
    {
        DetachOverlay();
        DetachReelTimer();
        DetachReelLike();

        if (currentItem != null)
            Destroy(currentItem.gameObject);

        currentItem = Instantiate(scrollPanelPrefab, feedContainer);

        RectTransform rt = currentItem.GetComponent<RectTransform>();
        StretchToFill(rt);
        // The slide is kicked off by the caller (SpawnScroll) via
        // SlidePanelIn, so it can start that panel's timer when the slide finishes.
    }

    /// <summary>
    /// Slides the current panel up into place, then runs <paramref name="onComplete"/>.
    /// Timers are started in that callback so the countdown begins only once the panel
    /// has landed (not while it is still animating in).
    /// </summary>
    private void SlidePanelIn(System.Action onComplete)
    {
        if (currentItem == null)
        {
            onComplete?.Invoke();
            return;
        }

        RectTransform rt = currentItem.GetComponent<RectTransform>();
        StartCoroutine(SlideIn(rt, onComplete));
    }

    private static void StretchToFill(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private IEnumerator SlideIn(RectTransform rt, System.Action onComplete)
    {
        if (rt == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float duration = 0.2f;
        float elapsed = 0f;
        Vector2 startPos = new Vector2(0f, -Screen.height); // start off-screen below
        Vector2 endPos = Vector2.zero;

        rt.anchoredPosition = startPos;

        while (elapsed < duration)
        {
            if (rt == null)
                yield break; // panel was swiped away / destroyed mid-slide

            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        if (rt == null)
            yield break;

        rt.anchoredPosition = endPos;
        onComplete?.Invoke();
    }
}
