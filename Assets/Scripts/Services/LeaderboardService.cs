using System;
using System.Collections.Generic;
using DTOs;
using UnityEngine;
using UnityEngine.Networking;

namespace Services
{
    public enum ClaimResult { Success, Taken, Failed }

    // All requests fail silently: nothing is thrown or logged when the server can't be reached.
    public static class LeaderboardService
    {
        private const string BaseURL = "https://larsverschoor.nl";

        // Runs on every game start: claims the username once, then uploads the local highscore.
        // A taken generated name is replaced by a new one; a failed request just stops until next launch.
        [RuntimeInitializeOnLoadMethod]
        private static async void SyncOnStartup()
        {
            while (!SettingsService.Current.registered)
            {
                ClaimResult result = await Register(SettingsService.Current.username);
                if (result == ClaimResult.Failed)
                {
                    return;
                }

                if (result == ClaimResult.Success)
                {
                    SettingsService.Update(s => s.registered = true);
                }
                else
                {
                    SettingsService.Update(s => s.username = SettingsData.GenerateUsername());
                }
            }

            await Save(SaveDataService.Current.highscore);
        }

        public static Awaitable<ClaimResult> Register(string username) =>
            PostClaim("/register", new RegisterRequest { username = username });

        public static Awaitable<ClaimResult> UpdateUsername(string newUsername) =>
            PostClaim("/update-username", new UpdateUsernameRequest { username = SettingsService.Current.username, new_username = newUsername });

        // Sorted from highest to lowest score; empty when the request fails.
        public static async Awaitable<List<LeaderboardEntry>> Load()
        {
            using UnityWebRequest request = UnityWebRequest.Get(BaseURL + "/leaderboard");
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                return new List<LeaderboardEntry>();
            }

            List<LeaderboardEntry> entries = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text)?.entries ?? new List<LeaderboardEntry>();
            entries.Sort((a, b) => b.score.CompareTo(a.score));
            return entries;
        }

        public static async Awaitable Save(int score)
        {
            using UnityWebRequest request = Post("/highscore", new LeaderboardEntry { username = SettingsService.Current.username, score = score });
            await request.SendWebRequest();
        }

        private static async Awaitable<ClaimResult> PostClaim(string path, object body)
        {
            using UnityWebRequest request = Post(path, body);
            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return ClaimResult.Success;
            }
            return request.responseCode == 409 ? ClaimResult.Taken : ClaimResult.Failed;
        }

        private static UnityWebRequest Post(string path, object body) =>
            UnityWebRequest.Post(BaseURL + path, JsonUtility.ToJson(body), "application/json");

        [Serializable]
        private class RegisterRequest
        {
            public string username;
        }

        [Serializable]
        private class UpdateUsernameRequest
        {
            public string username;
            public string new_username;
        }

        [Serializable]
        private class LeaderboardResponse
        {
            public List<LeaderboardEntry> entries;
        }
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public string username;
        public int score;
    }
}
