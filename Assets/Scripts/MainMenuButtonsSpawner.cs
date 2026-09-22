using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuButtonsSpawner : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab;
    
    [System.Serializable]
    public struct MenuButton
    {
        public string buttonText;
        public string sceneName;   // must match a scene name in Build Settings
    }
    [SerializeField] MenuButton[] buttons;

    void Start()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var config = buttons[i];
            
            GameObject button = Instantiate(buttonPrefab, transform, false);
            button.GetComponentInChildren<TMP_Text>().text = config.buttonText;
            
            string scene = config.sceneName;
            button.GetComponent<Button>().onClick.AddListener(() => SceneManager.LoadScene(scene));
        }
    }
}
