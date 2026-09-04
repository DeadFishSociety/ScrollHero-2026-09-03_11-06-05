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

    [Header("Transition")]
    [Tooltip("How long one slide takes to scroll into place.")]
    [SerializeField, Min(0.01f)] private float slideDuration = 0.28f;

    [Tooltip("Eases the slide. Default is ease-in-out; edit the curve for a different feel.")]
    [SerializeField] private AnimationCurve slideEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private int scrollCount;
    private int score;
    private FeedItem currentItem;
    private FeedOverlay currentOverlay;
    private bool actionInProgress;
    private bool transitioning;

    void Start()
    {
        swipeInput.OnSwipe += HandleSwipe;
        UpdateScoreText();
        SpawnFirst();
    }

    void OnDestroy()
    {
        swipeInput.OnSwipe -= HandleSwipe;
        DetachOverlay();
    }

    private void HandleSwipe(SwipeDirection direction)
    {
        // Ignore input mid-transition so a fast second swipe can't spawn two slides.
        if (transitioning)
            return;

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

        bool spawnAction = scrollCount % actionEveryNScrolls == 0 && overlayPicker.HasAny;
        GoToNext(spawnAction ? FeedItemType.Action : FeedItemType.Scroll);
    }

    private void GoToNext(FeedItemType type)
    {
        if (transitioning)
            return;

        StartCoroutine(TransitionRoutine(type));
    }

    private IEnumerator TransitionRoutine(FeedItemType type)
    {
        transitioning = true;
        DetachOverlay(); // stop listening to the outgoing panel's overlay, if any

        FeedItem outgoing = currentItem;
        RectTransform outRt = outgoing != null ? outgoing.GetComponent<RectTransform>() : null;

        // Spawn the incoming panel already sitting just below the frame.
        FeedItem prefab = type == FeedItemType.Scroll ? scrollPanelPrefab : actionPanelPrefab;
        FeedItem incoming = Instantiate(prefab, feedContainer);
        RectTransform inRt = incoming.GetComponent<RectTransform>();
        StretchToFill(inRt);

        float h = feedContainer != null ? feedContainer.rect.height : Screen.height;
        if (h <= 0f)
            h = Screen.height;

        inRt.anchoredPosition = new Vector2(0f, -h);

        // Scroll both up by one frame height: outgoing exits the top, incoming enters.
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = slideEase.Evaluate(Mathf.Clamp01(elapsed / slideDuration));
            inRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(-h, 0f, t));
            if (outRt != null)
                outRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, h, t));
            yield return null;
        }

        inRt.anchoredPosition = Vector2.zero;

        if (outgoing != null)
            Destroy(outgoing.gameObject); // takes its overlay child with it

        currentItem = incoming;
        transitioning = false;

        if (type == FeedItemType.Action)
            SetupOverlay();
        else
            actionInProgress = false;
    }

    private void SpawnFirst()
    {
        FeedItem incoming = Instantiate(scrollPanelPrefab, feedContainer);
        RectTransform rt = incoming.GetComponent<RectTransform>();
        StretchToFill(rt);
        rt.anchoredPosition = Vector2.zero;
        currentItem = incoming;
        actionInProgress = false;
    }

    private void SetupOverlay()
    {
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

        GoToNext(FeedItemType.Scroll);
    }

    private void OnOverlayFailed(FeedOverlay overlay)
    {
        if (dopamineMeter != null)
            dopamineMeter.OnFailedAction();

        if (advanceOnActionFail)
            GoToNext(FeedItemType.Scroll);
        else
            overlay.Begin(); // let the player try again on the same panel
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
}
