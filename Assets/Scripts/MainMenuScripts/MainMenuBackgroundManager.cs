using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class MainMenuBackgroundManager : MonoBehaviour
{
    [SerializeField] private Sprite[] backgrounds;

    void Awake()
    {
        if (backgrounds == null || backgrounds.Length == 0) return;

        GetComponent<Image>().sprite = backgrounds[Random.Range(0, backgrounds.Length)];
    }
}
