using UnityEngine;
using TMPro;
using System.Collections;

public class FeedManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private FeedItem scrollPanelPrefab;
    [SerializeField] private FeedItem actionPanelPrefab;

    [Tooltip("The pool of action overlays that can appear, and how one is chosen. " +
             "Tick/untick entries here to decide which actions are in rotation.")]
    [SerializeField] private OverlayPicker overlayPicker = new OverlayPicker();

    [Header("References")]
    [SerializeField] private RectTransform feedContainer;
    [SerializeField] private SwipeInput swipeInput;

    [Tooltip("Shows how many reels have been scrolled (not the score).")]
    [SerializeField] private TMP_Text scrollCountText;

    [Tooltip("Optional. Shows the score — only successful actions raise it.")]
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("Optional. Shows how many lives are left.")]
    [SerializeField] private TMP_Text livesText;

    [Header("Settings")]
    [SerializeField] private int actionEveryNScrolls = 5;

    [Tooltip("Lives the player starts with. Each time a dopamine timer empties " +
             "before the reel/action is cleared, one life is lost.")]
    [SerializeField, Min(1)] private int startingLives = 5;

    [Tooltip("When an action overlay fails (e.g. times out), move on to the next reel anyway. " +
             "Untick to let the player keep trying the same action.")]
    [SerializeField] private bool advanceOnActionFail = true;

    private int scrollCount;
    private int score;
    private int lives;
    private bool gameOver;
    private FeedItem currentItem;
    private FeedOverlay currentOverlay;
    private ReelTimer currentReelTimer;
    private bool actionInProgress;

    /// <summary>Fired once when lives reach zero. Hook a lose screen here later.</summary>
    public event System.Action GameOver;

    void Start()
    {
        swipeInput.OnSwipe += HandleSwipe;
        lives = startingLives;
        UpdateScoreText();
        UpdateLivesText();
        SpawnScroll();
    }

    void OnDestroy()
    {
        swipeInput.OnSwipe -= HandleSwipe;
        DetachOverlay();
        DetachReelTimer();
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

        // NOTE: scrolling reels deliberately does NOT score. Only completing an
        // action overlay does. See OnOverlayCompleted.

        if (scrollCount % actionEveryNScrolls == 0 && overlayPicker.HasAny)
            SpawnAction();
        else
            SpawnScroll();
    }

    private void SpawnScroll()
    {
        actionInProgress = false;
        SpawnPanel(FeedItemType.Scroll);

        // A scroll reel is timed: the player must swipe up before its dopamine
        // timer empties, or a life is lost. Action panels have no ReelTimer — the
        // overlay owns their timer instead.
        currentReelTimer = currentItem != null ? currentItem.GetComponentInChildren<ReelTimer>() : null;
        if (currentReelTimer != null)
        {
            currentReelTimer.Expired += OnReelExpired;
            currentReelTimer.Restart();
        }
    }

    private void OnReelExpired()
    {
        // Timed out before the player swiped away — lose a life, then move on.
        // (This wasn't a scroll, so scrollCount is left unchanged.)
        LoseLife();
        if (gameOver)
            return;

        SpawnScroll();
    }

    private void SpawnAction()
    {
        SpawnPanel(FeedItemType.Action);

        FeedOverlay prefab = overlayPicker.Pick();
        if (prefab == null)
        {
            // Nothing usable in the pool — treat it as a normal reel instead of stalling.
            actionInProgress = false;
            return;
        }

        RectTransform overlayRoot = currentItem.ResolveOverlayRoot();
        // worldPositionStays=false so the overlay keeps the size, anchors, position
        // and scale authored in its prefab instead of inheriting the panel's.
        // Intentionally NOT stretched to fill — each overlay controls its own layout
        // via its prefab RectTransform (a small centered heart, a full-screen ad, ...).
        currentOverlay = Instantiate(prefab, overlayRoot, false);

        currentOverlay.Completed += OnOverlayCompleted;
        currentOverlay.Failed += OnOverlayFailed;

        actionInProgress = true;
        currentOverlay.Begin();
    }

    private void OnOverlayCompleted(FeedOverlay overlay)
    {
        score++;
        UpdateScoreText();

        DetachOverlay();
        SpawnScroll();
    }

    private void OnOverlayFailed(FeedOverlay overlay)
    {
        // The action's dopamine timer ran out (or the player gave up) — lose a life.
        LoseLife();
        if (gameOver)
            return;

        if (advanceOnActionFail)
        {
            DetachOverlay();
            SpawnScroll();
        }
        else
        {
            // Let the player try again on the same panel.
            overlay.Begin();
        }
    }

    private void LoseLife()
    {
        if (gameOver)
            return;

        lives = Mathf.Max(0, lives - 1);
        UpdateLivesText();

        if (lives <= 0)
            EndGame();
    }

    private void EndGame()
    {
        gameOver = true;
        DetachReelTimer();
        DetachOverlay();
        Debug.Log("[FeedManager] GAME OVER — out of lives.");
        GameOver?.Invoke();
        // Feed is frozen: HandleSwipe ignores input and nothing new is spawned.
    }

    private void UpdateLivesText()
    {
        if (livesText != null)
            livesText.text = $"Lives: {lives}";
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    private void DetachOverlay()
    {
        if (currentOverlay == null)
            return;

        currentOverlay.Completed -= OnOverlayCompleted;
        currentOverlay.Failed -= OnOverlayFailed;
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

    private void SpawnPanel(FeedItemType type)
    {
        DetachOverlay();
        DetachReelTimer();

        if (currentItem != null)
            Destroy(currentItem.gameObject); // destroys the overlay child too

        FeedItem prefab = type == FeedItemType.Scroll ? scrollPanelPrefab : actionPanelPrefab;
        currentItem = Instantiate(prefab, feedContainer);

        RectTransform rt = currentItem.GetComponent<RectTransform>();
        StretchToFill(rt);
        StartCoroutine(SlideIn(rt));
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

    private IEnumerator SlideIn(RectTransform rt)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector2 startPos = new Vector2(0f, -Screen.height); // start off-screen below
        Vector2 endPos = Vector2.zero;

        rt.anchoredPosition = startPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        rt.anchoredPosition = endPos;
    }
}
