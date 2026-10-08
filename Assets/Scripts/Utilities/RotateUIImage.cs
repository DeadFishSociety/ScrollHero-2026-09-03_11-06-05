using UnityEngine;

// Continuously spins this UI element around the Z axis (like a music disc).
// Speed is in degrees per second and exposed as an inspector slider.
public class RotateUIImage : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second. Positive = counter-clockwise, negative = clockwise.")]
    [Range(-360f, 360f)]
    [SerializeField] private float speed = 90f;

    void Update()
    {
        transform.Rotate(0f, 0f, speed * Time.deltaTime);
    }

    // Hook a UI Slider's "On Value Changed (Single)" to this to change speed at runtime.
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
}
