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
    [SerializeField] private DopamineMeter dopamineMeter;

    [Tooltip("Shows how many reels have been scrolled (not the score).")]
    [SerializeField] private TMP_Text scrollCountText;

    [Tooltip("Optional. Shows the score — only successful actions raise it.")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Settings")]
    [SerializeField] private int actionEveryNScrolls = 5;

    [Tooltip("When an action overlay fails (e.g. times out), move on to the next reel anyway. " +
             "Untick to let the player keep trying the same action.")]
    [SerializeField] private bool advanceOnActionFail = true;

    private int scrollCount;
    private int score;
    private FeedItem currentItem;
    private FeedOverlay currentOverlay;
    private bool actionInProgress;

    void Start()
    {
        swipeInput.OnSwipe += HandleSwipe;
        UpdateScoreText();
        SpawnScroll();
    }

    void OnDestroy()
    {
        swipeInput.OnSwipe -= HandleSwipe;
        DetachOverlay();
    }

    private void HandleSwipe(SwipeDirection direction)
    {
        // While an action overlay is up it handles its own input (taps on the heart,
        // the close cross, ...). Swipes are swallowed so you can't scroll past it.
        if (actionInProgress && currentOverlay != null && currentOverlay.BlocksSwipe)
            return;

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
        if (dopamineMeter != null)
            dopamineMeter.OnSuccessfulAction();

        DetachOverlay();
        SpawnScroll();
    }

    private void OnOverlayFailed(FeedOverlay overlay)
    {
        if (dopamineMeter != null)
            dopamineMeter.OnFailedAction();

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

    private void SpawnPanel(FeedItemType type)
    {
        DetachOverlay();

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
