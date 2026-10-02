using System;
using DTOs;
using UnityEngine;

namespace Services
{
    public static class SettingsService
    {
        private const string File = "settings.json";
        public static SettingsData Current { get; private set; } = SaveSystem.Load<SettingsData>(File);
        public static event Action<SettingsData> Changed;

        public static void Update(Action<SettingsData> mutate)
        {
            mutate(Current);
            SaveSystem.Save(Current, File);
            Changed?.Invoke(Current);
        }

        // Renames on the leaderboard first, and only applies it locally when that succeeds.
        // Before registration it's applied locally only; the startup sync registers it later.
        public static async Awaitable<ClaimResult> ChangeUsername(string newUsername)
        {
            if (newUsername == Current.username)
            {
                return ClaimResult.Success;
            }

            if (Current.registered)
            {
                ClaimResult result = await LeaderboardService.UpdateUsername(newUsername);
                if (result != ClaimResult.Success)
                {
                    return result;
                }
            }

            Update(s => s.username = newUsername);
            return ClaimResult.Success;
        }
    }
}
