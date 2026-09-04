using UnityEngine;
using UnityEngine.UI;

public class DopamineMeter : MonoBehaviour
{
    [SerializeField] private Slider slider; // range 0-1
    [SerializeField] private float decayPerSecond = 0.05f;
    [SerializeField] private float actionSuccessBoost = 0.15f;
    [SerializeField] private float actionFailPenalty = 0.1f;

    private float value = 1f;
    private float drainMultiplier = 1f;

    void Start() => slider.value = value;

    void Update()
    {
        value = Mathf.Clamp01(value - decayPerSecond * drainMultiplier * Time.deltaTime);
        slider.value = value;
    }

    // Scrolling reels deliberately no longer boosts dopamine — only actions do.
    public void OnSuccessfulAction() => value = Mathf.Clamp01(value + actionSuccessBoost);
    public void OnFailedAction() => value = Mathf.Clamp01(value - actionFailPenalty);

    /// <summary>Scales decay speed, e.g. 2x while a notification is on screen. 1 = normal.</summary>
    public void SetDrainMultiplier(float multiplier) => drainMultiplier = Mathf.Max(0f, multiplier);
}