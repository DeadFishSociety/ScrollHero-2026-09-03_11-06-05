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
        string highscore = SaveDataService.Current.highscore.ToString();
        highscoreText.text = string.Format(format, highscore);
    }
}
