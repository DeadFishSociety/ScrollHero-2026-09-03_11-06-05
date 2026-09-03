using UnityEngine;
using UnityEngine.UI;

public enum FeedItemType { Scroll, Action }

public class FeedItem : MonoBehaviour
{
    public FeedItemType Type;

    [Tooltip("Later: swap this for a RawImage + VideoPlayer to show real video.")]
    public Image DisplayImage;

    [Tooltip("Action panels only — which swipe correctly dismisses this panel.")]
    public SwipeDirection RequiredSwipe = SwipeDirection.Left;
}