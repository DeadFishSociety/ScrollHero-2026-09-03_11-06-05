using UnityEngine;
using UnityEngine.UI;

// Smoothly flashes a UI Graphic (TMP text or Image) between two colours while the
// object is active. Used for the golden reel's "LIKE NOW" prompt (yellow <-> white).
[RequireComponent(typeof(Graphic))]
public class UIColorFlash : MonoBehaviour
{
    [SerializeField] private Color colorA = Color.yellow;
    [SerializeField] private Color colorB = Color.white;

    [Tooltip("Full A->B->A cycles per second.")]
    [SerializeField] private float cyclesPerSecond = 2f;

    [Tooltip("Use unscaled time so it keeps flashing if the game is paused/time-scaled.")]
    [SerializeField] private bool useUnscaledTime = true;

    private Graphic graphic;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
    }

    private void Update()
    {
        if (graphic == null)
        {
            return;
        }

        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        float wave = 0.5f + 0.5f * Mathf.Sin(t * cyclesPerSecond * Mathf.PI * 2f);
        graphic.color = Color.Lerp(colorA, colorB, wave);
    }
}
