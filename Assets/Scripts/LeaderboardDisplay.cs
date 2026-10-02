using System.Collections.Generic;
using Services;
using UnityEngine;

// Fills the container with a card per entry for the top scores on the leaderboard.
public class LeaderboardDisplay : MonoBehaviour
{
    [SerializeField] private HighscoreCard cardPrefab;
    [SerializeField] private Transform container;
    [SerializeField] private int count = 10;

    private async void Start()
    {
        List<LeaderboardEntry> entries = await LeaderboardService.Load();

        // The scene may have been left while the request was running.
        if (this == null)
        {
            return;
        }

        int yourIndex = entries.FindIndex(e => e.username == SettingsService.Current.username);

        for (int i = 0; i < Mathf.Min(count, entries.Count); i++)
        {
            AddCard(entries, i, yourIndex);
        }

        // Outside the top: show your own entry at the bottom with your real rank.
        if (yourIndex >= count)
        {
            AddCard(entries, yourIndex, yourIndex);
        }
    }

    private void AddCard(List<LeaderboardEntry> entries, int index, int yourIndex)
    {
        Instantiate(cardPrefab, container).Set(index + 1, entries[index], index == yourIndex);
    }
}
