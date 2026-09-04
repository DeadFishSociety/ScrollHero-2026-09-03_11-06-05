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

    [Header("Notification")]
    [Tooltip("Prefab for the notification banner (e.g. \"New message from: Mom\"). Leave empty to disable notifications.")]
    [SerializeField] private NotificationOverlay notificationPrefab;

    [Tooltip("Independent random chance per scroll, averaging one notification every this many scrolls. " +
             "Not tied to the action-every-N-scrolls cycle, and skipped while an action overlay is showing.")]
    [SerializeField, Min(1)] private int notificationAverageEveryNScrolls = 8;

    [Tooltip("Dopamine drains this many times faster while a notification is on screen.")]
    [SerializeField] private float notificationDrainMultiplier = 2f;

    private int scrollCount;
    private int score;
    private FeedItem currentItem;
    private FeedOverlay currentOverlay;
    private bool actionInProgress;
    private NotificationOverlay activeNotification;

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
        DetachNotification();
    }

    private void HandleSwipe(SwipeDirection direction)
    {
        // A notification sits on top of whatever panel is showing and swallows every
        // swipe until it's swiped away — it doesn't share the action overlay's slot,
        // so check it before anything else.
        if (activeNotification != null)
        {
            activeNotification.OnSwipeInput(direction);
            return;
        }

        // Roll for a notification on every upward swipe attempt — even one that an
        // action overlay (e.g. the ad) would otherwise swallow below, so notifications
        // can interrupt those too, not just plain scrolling.
        if (direction == SwipeDirection.Up)
            TryTriggerNotification();

        if (activeNotification != null)
            return; // this swipe just surfaced a notification; nothing else happens this turn

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

    private void TryTriggerNotification()
    {
        if (notificationPrefab == null || currentItem == null)
            return;

        if (Random.value >= 1f / notificationAverageEveryNScrolls)
            return;

        RectTransform overlayRoot = currentItem.ResolveOverlayRoot();
        activeNotification = Instantiate(notificationPrefab, overlayRoot, false);
        activeNotification.Completed += OnNotificationDismissed;
        activeNotification.Failed += OnNotificationDismissed;

        if (dopamineMeter != null)
            dopamineMeter.SetDrainMultiplier(notificationDrainMultiplier);

        activeNotification.Begin();
    }

    // Dismissing a notification is neutral — unlike OnOverlayCompleted, it doesn't
    // score or boost dopamine. It just clears the extra drain.
    private void OnNotificationDismissed(FeedOverlay overlay)
    {
        DetachNotification();

        if (dopamineMeter != null)
            dopamineMeter.SetDrainMultiplier(1f);
    }

    private void DetachNotification()
    {
        if (activeNotification == null)
            return;

        activeNotification.Completed -= OnNotificationDismissed;
        activeNotification.Failed -= OnNotificationDismissed;
        Destroy(activeNotification.gameObject);
        activeNotification = null;
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
