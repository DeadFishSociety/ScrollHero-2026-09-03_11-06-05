using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lets the player "like" the reel this sits on. A like is one-way (no un-like) and can be
/// triggered two independent ways, each toggled by its own bool:
///   • a double-tap on the reel — FeedManager routes double-taps here, and
///   • a dedicated heart button on the reel UI.
/// Turn either off to disable that method. On the first like it swaps the outline heart for
/// a red one (which pops in), plays an optional points-gained animation placed next to the
/// heart, and raises <see cref="Liked"/> so FeedManager can award the bonus.
///
/// FeedManager finds this component on the spawned reel and subscribes to <see cref="Liked"/>.
/// </summary>
public class ReelLike : MonoBehaviour
{
    [Header("Input methods")]
    [Tooltip("Allow double-tapping the reel to like it. FeedManager routes double-taps here.")]
    [SerializeField] private bool allowDoubleTap = true;

    [Tooltip("Allow a dedicated heart button to like it.")]
    [SerializeField] private bool allowButton = true;

    [Tooltip("The heart button (used when Allow Button is on). It's hidden when Allow Button is off.")]
    [SerializeField] private Button likeButton;

    [Header("Feedback")]
    [Tooltip("The outline heart shown before liking. Hidden once liked, so only the red heart remains.")]
    [SerializeField] private GameObject unlikedIcon;

    [Tooltip("The red heart shown once liked. It pops in with a little scale animation.")]
    [SerializeField] private GameObject likedIndicator;

    [Tooltip("Optional points-gained animation played on a like. Place its object to the LEFT " +
             "of the heart in the prefab so it plays there. If empty, FeedManager falls back " +
             "to the shared ScrollAnimationOverlay.")]
    [SerializeField] private ReelSpriteAnimation likeAnimation;

    [Header("Pop-in")]
    [Tooltip("How long the red heart's pop-in scale animation lasts.")]
    [SerializeField, Min(0f)] private float popInDuration = 0.25f;

    /// <summary>True once this reel has been liked. Likes are one-way.</summary>
    public bool IsLiked { get; private set; }

    /// <summary>Whether double-tap liking is enabled on this reel.</summary>
    public bool DoubleTapEnabled => allowDoubleTap;

    /// <summary>True if this reel plays its own like animation (so FeedManager skips the shared one).</summary>
    public bool HasLikeAnimation => likeAnimation != null;

    /// <summary>Raised once, the first time this reel is liked.</summary>
    public event System.Action<ReelLike> Liked;

    private Coroutine popRoutine;

    private void Awake()
    {
        if (likeButton != null)
        {
            likeButton.gameObject.SetActive(allowButton);
            likeButton.onClick.AddListener(OnButtonClicked);
        }
        if (unlikedIcon != null)
            unlikedIcon.SetActive(true);
        if (likedIndicator != null)
            likedIndicator.SetActive(false);
    }

    private void OnDestroy()
    {
        if (likeButton != null)
            likeButton.onClick.RemoveListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (allowButton)
            TryLike();
    }

    /// <summary>Called by FeedManager when a double-tap lands on this reel.</summary>
    public void OnDoubleTap()
    {
        if (allowDoubleTap)
            TryLike();
    }

    private void TryLike()
    {
        if (IsLiked)
            return; // one-way — no un-like

        IsLiked = true;

        // Swap the outline heart for the red one, popping it in.
        if (unlikedIcon != null)
            unlikedIcon.SetActive(false);
        if (likedIndicator != null)
        {
            likedIndicator.SetActive(true);
            if (popRoutine != null)
                StopCoroutine(popRoutine);
            if (isActiveAndEnabled && popInDuration > 0f)
                popRoutine = StartCoroutine(PopIn(likedIndicator.transform));
        }

        if (likeAnimation != null)
            likeAnimation.PlayFromStart(); // plays next to the heart, wherever it's placed

        Liked?.Invoke(this);
    }

    /// <summary>Scale the red heart from nothing up to its resting size with a little overshoot.</summary>
    private IEnumerator PopIn(Transform target)
    {
        Vector3 restScale = target.localScale; // the size authored in the prefab
        float elapsed = 0f;

        while (elapsed < popInDuration)
        {
            elapsed += Time.deltaTime;
            target.localScale = restScale * EaseOutBack(Mathf.Clamp01(elapsed / popInDuration));
            yield return null;
        }

        target.localScale = restScale;
        popRoutine = null;
    }

    /// <summary>Eases 0 -> 1 but overshoots just past 1 before settling, for a springy pop.</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
