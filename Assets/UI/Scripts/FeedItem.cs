using UnityEngine;
using UnityEngine.UI;

public enum FeedItemType { Scroll, Action }

public class FeedItem : MonoBehaviour
{
    public FeedItemType Type;

    [Tooltip("Later: swap this for a RawImage + VideoPlayer to show real video.")]
    public Image DisplayImage;

    [Tooltip("Action panels only — where the chosen overlay prefab is spawned. " +
             "Leave empty to spawn it on this panel's own RectTransform.")]
    public RectTransform OverlayRoot;

    /// <summary>Where an overlay should be parented — the explicit root, or this panel itself.</summary>
    public RectTransform ResolveOverlayRoot()
        => OverlayRoot != null ? OverlayRoot : transform as RectTransform;
}
