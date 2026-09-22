using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuButtonsSpawner : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab;
    
    [System.Serializable]
    public struct MenuButton
    {
        public Sprite buttonIcon;
        public string sceneName;
    }
    [SerializeField] MenuButton[] buttons;

    void Start()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var config = buttons[i];
            
            GameObject button = Instantiate(buttonPrefab, transform, false);
            button.GetComponentInChildren<Image>().sprite = config.buttonIcon;
            
            string scene = config.sceneName;
            button.GetComponent<Button>().onClick.AddListener(() => SceneManager.LoadScene(scene));
        }
    }
}
