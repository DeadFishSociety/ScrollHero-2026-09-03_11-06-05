using TMPro;
using UnityEngine;
using Services;
using DTOs;

public class MainMenuHighscoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text highscoreText;
    [SerializeField] private string format = "Personal Best: {0}";

    private void Start()
    {
        SaveData data = SaveSystem.Load();
        highscoreText.text = string.Format(format, data.highscore);
    }
}
