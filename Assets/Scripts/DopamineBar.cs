using UnityEngine;
using UnityEngine.UI;

public class DopamineBar : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    
    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }
    
    private void SyncBarToDopamineLevel(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);

        int frameIndex = Mathf.RoundToInt(fraction * (frames.Length - 1));

        image.sprite = frames[frameIndex];
    }

    private void OnEnable()
    {
        DopamineManager.OnDopamineInitialized += SyncBarToDopamineLevel;
        DopamineManager.OnDopamineChange += SyncBarToDopamineLevel;
    }
    
    private void OnDisable()
    {
        DopamineManager.OnDopamineInitialized -= SyncBarToDopamineLevel;
        DopamineManager.OnDopamineChange -= SyncBarToDopamineLevel;
    }
}
