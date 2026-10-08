using Services;
using TMPro;
using UnityEngine;

public class HighscoreCard : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;

    public void Set(int rank, LeaderboardEntry entry, bool isYou)
    {
        rankText.text = rank.ToString();
        nameText.text = isYou ? $"{entry.username} (you)" : entry.username;
        scoreText.text = entry.score.ToString();
    }
}
