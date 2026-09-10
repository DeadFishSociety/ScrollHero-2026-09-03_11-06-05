using UnityEngine;
using UnityEngine.InputSystem;

public enum SwipeDirection { None, Up, Down, Left, Right }

public class SwipeInput : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 50f; // in pixels

    [Header("Double-tap Settings")]
    [Tooltip("Max seconds between two taps for them to count as a double-tap.")]
    [SerializeField] private float doubleTapMaxDelay = 0.3f;

    [Tooltip("Max pixels between the two taps for them to count as a double-tap.")]
    [SerializeField] private float doubleTapMaxDistance = 120f;

    private Vector2 startPos;
    private bool isPressing;

    private float lastTapTime = -10f;
    private Vector2 lastTapPos;

    public delegate void SwipeEvent(SwipeDirection direction);
    public event SwipeEvent OnSwipe;

    /// <summary>Fired when the player taps twice quickly in roughly the same spot.</summary>
    public event System.Action OnDoubleTap;

    /// <summary>
    /// Drop the press currently in progress so its release does NOT fire a swipe.
    /// Called when a minigame is solved mid-gesture, so lifting the finger that solved
    /// it doesn't linger into scrolling the next reel. The next fresh press works normally.
    /// </summary>
    public void CancelCurrentGesture() => isPressing = false;

    void Update()
    {
        bool pressedThisFrame = false;
        bool releasedThisFrame = false;
        Vector2 currentPos = Vector2.zero;

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            currentPos = touch.position.ReadValue();
            pressedThisFrame = touch.press.wasPressedThisFrame;
            releasedThisFrame = touch.press.wasReleasedThisFrame;
        }
        else if (Mouse.current != null)
        {
            currentPos = Mouse.current.position.ReadValue();
            pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            releasedThisFrame = Mouse.current.leftButton.wasReleasedThisFrame;
        }

        if (pressedThisFrame)
        {
            startPos = currentPos;
            isPressing = true;
        }
        else if (releasedThisFrame && isPressing)
        {
            isPressing = false;
            Vector2 delta = currentPos - startPos;

            if (delta.magnitude < minSwipeDistance)
            {
                HandleTap(currentPos); // too small to be a swipe — it's a tap
                return;
            }

            OnSwipe?.Invoke(GetDirection(delta));
        }
    }

    private void HandleTap(Vector2 position)
    {
        float now = Time.time;
        bool inTime = now - lastTapTime <= doubleTapMaxDelay;
        bool inPlace = (position - lastTapPos).magnitude <= doubleTapMaxDistance;

        if (inTime && inPlace)
        {
            OnDoubleTap?.Invoke();
            lastTapTime = -10f; // consume, so a third quick tap doesn't double again
        }
        else
        {
            lastTapTime = now;
            lastTapPos = position;
        }
    }

    private SwipeDirection GetDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
    }
}