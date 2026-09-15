using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DopamineManager : MonoBehaviour
{
    public static event Action<float> OnDopamineInitialized;
    public static event Action<float> OnDopamineChange;
    
    [SerializeField] private float maximumDopamine = 100f;
    [SerializeField] private float dopamineLevel = 100f;

    private void Start()
    {
        OnDopamineInitialized?.Invoke(dopamineLevel / maximumDopamine);
    }
    
    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            float y = pointer.position.ReadValue().y;

            if (y > Screen.height * 0.5f)
                AddDopamine(10f);      // tapped top half
            else
                RemoveDopamine(10f);   // tapped bottom half
        }
    }
    
    private void UpdateDopamineLevel(float amount)
    {
        dopamineLevel =  Mathf.Clamp(dopamineLevel + amount, 0f, maximumDopamine);
        OnDopamineChange.Invoke(dopamineLevel / maximumDopamine);
    }

    public void AddDopamine(float amount)
    {
        UpdateDopamineLevel(amount);
    }

    public void RemoveDopamine(float amount)
    {
        UpdateDopamineLevel(-amount);
    }
    
    public float getDopamineLevel()
    {
        return dopamineLevel;
    }
}
