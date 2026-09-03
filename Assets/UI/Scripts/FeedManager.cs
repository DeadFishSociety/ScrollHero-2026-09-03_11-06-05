using UnityEngine;
using TMPro;
using System.Collections;

public class FeedManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private FeedItem scrollPanelPrefab;
    [SerializeField] private FeedItem actionPanelPrefab;

    [Header("References")]
    [SerializeField] private RectTransform feedContainer;
    [SerializeField] private SwipeInput swipeInput;
    [SerializeField] private DopamineMeter dopamineMeter;
    [SerializeField] private TMP_Text scrollCountText;

    [Header("Settings")]
    [SerializeField] private int actionEveryNScrolls = 5;

    private int scrollCount;
    private FeedItem currentItem;
    private bool waitingForActionResponse;

    void Start()
    {
        swipeInput.OnSwipe += HandleSwipe;
        SpawnNext(FeedItemType.Scroll);
    }

    void OnDestroy() => swipeInput.OnSwipe -= HandleSwipe;

    private void HandleSwipe(SwipeDirection direction)
    {
        if (waitingForActionResponse)
        {
            if (direction == currentItem.RequiredSwipe)
            {
                waitingForActionResponse = false;
                dopamineMeter.OnSuccessfulAction();
                SpawnNext(FeedItemType.Scroll);
            }
            else
            {
                dopamineMeter.OnFailedAction();
            }
            return;
        }

        if (direction != SwipeDirection.Up)
            return; // only an upward swipe counts as "scrolling"

        scrollCount++;
        scrollCountText.text = $"Scrolls: {scrollCount}";
        dopamineMeter.OnScroll();

        if (scrollCount % actionEveryNScrolls == 0)
        {
            SpawnNext(FeedItemType.Action);
            waitingForActionResponse = true;
        }
        else
        {
            SpawnNext(FeedItemType.Scroll);
        }
    }

    private void SpawnNext(FeedItemType type)
    {
        if (currentItem != null)
            Destroy(currentItem.gameObject);

        FeedItem prefab = type == FeedItemType.Scroll ? scrollPanelPrefab : actionPanelPrefab;
        currentItem = Instantiate(prefab, feedContainer);

        RectTransform rt = currentItem.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        StartCoroutine(SlideIn(rt));
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