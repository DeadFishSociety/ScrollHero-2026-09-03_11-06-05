using UnityEngine;
using UnityEngine.UI;

public class DopamineMeter : MonoBehaviour
{
    [SerializeField] private Slider slider; // range 0-1
    [SerializeField] private float decayPerSecond = 0.05f;
    [SerializeField] private float scrollBoost = 0.1f;
    [SerializeField] private float actionSuccessBoost = 0.15f;
    [SerializeField] private float actionFailPenalty = 0.1f;

    private float value = 1f;

    void Start() => slider.value = value;

    void Update()
    {
        value = Mathf.Clamp01(value - decayPerSecond * Time.deltaTime);
        slider.value = value;
    }

    public void OnScroll() => value = Mathf.Clamp01(value + scrollBoost);
    public void OnSuccessfulAction() => value = Mathf.Clamp01(value + actionSuccessBoost);
    public void OnFailedAction() => value = Mathf.Clamp01(value - actionFailPenalty);
}