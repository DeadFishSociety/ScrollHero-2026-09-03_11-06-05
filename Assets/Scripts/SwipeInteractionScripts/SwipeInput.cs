using UnityEngine;
using UnityEngine.InputSystem;

public enum SwipeDirection { None, Up, Down, Left, Right }

public class SwipeInput : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 50f; // in pixels

    private Vector2 startPos;
    private bool isPressing;

    public delegate void SwipeEvent(SwipeDirection direction);
    public event SwipeEvent OnSwipe;

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
                return; // too small, treat as a tap, ignore

            OnSwipe?.Invoke(GetDirection(delta));
        }
    }

    private SwipeDirection GetDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
    }
}