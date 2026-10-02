using UnityEngine;

// Shakes a target transform while overdrive mode is active, for extra juice.
// Put this on (or point it at) a wrapper that holds the gameplay UI - NOT an
// object whose position is driven by something else (e.g. a SafeAreaFitter),
// since it offsets localPosition.
public class OverdriveScreenShake : MonoBehaviour
{
    [Tooltip("Transform to shake. Defaults to this object's transform.")]
    [SerializeField] private Transform target;

    [Tooltip("Shake distance in canvas units (UI) or world units.")]
    [SerializeField] private float amplitude = 18f;

    [Tooltip("How jittery the shake is.")]
    [SerializeField] private float frequency = 22f;

    [Tooltip("Use unscaled time so it shakes even if the game is time-scaled.")]
    [SerializeField] private bool useUnscaledTime = true;

    private bool shaking;
    private Vector3 baseLocalPos;
    private float seed;

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }
        baseLocalPos = target.localPosition;
        seed = Random.value * 100f;
    }

    private void OnEnable()
    {
        OverdriveController.Started += StartShake;
        OverdriveController.Ended += StopShake;
    }

    private void OnDisable()
    {
        OverdriveController.Started -= StartShake;
        OverdriveController.Ended -= StopShake;
        if (shaking)
        {
            StopShake();
        }
    }

    private void StartShake()
    {
        if (target == null)
        {
            return;
        }
        // Capture the rest position now, so we restore to it exactly afterward.
        baseLocalPos = target.localPosition;
        shaking = true;
    }

    private void StopShake()
    {
        shaking = false;
        if (target != null)
        {
            target.localPosition = baseLocalPos;
        }
    }

    private void Update()
    {
        if (!shaking || target == null)
        {
            return;
        }

        float t = (useUnscaledTime ? Time.unscaledTime : Time.time) * frequency;
        float x = (Mathf.PerlinNoise(seed + t, 0f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(0f, seed + t) - 0.5f) * 2f;
        target.localPosition = baseLocalPos + new Vector3(x, y, 0f) * amplitude;
    }
}
