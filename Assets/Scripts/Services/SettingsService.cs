using System;
using DTOs;
using UnityEngine;

namespace Services
{
    public static class SettingsService
    {
        private const string File = "settings.json";
        public static SettingsData Current { get; private set; }
        public static event Action<SettingsData> Changed;

        // Runs before anything else on every Play, so the settings are re-read from disk
        // even when Unity enters Play mode without a domain reload (statics survive then).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Load()
        {
            Current = SaveSystem.Load<SettingsData>(File);
            Changed = null;
        }

        public static void Update(Action<SettingsData> mutate)
        {
            mutate(Current);
            SaveSystem.Save(Current, File);
            Changed?.Invoke(Current);
        }

        // Claims the new name on the leaderboard first (a rename, or a registration if the
        // current name was never registered) and only saves it locally when that succeeds.
        public static async Awaitable<ClaimResult> ChangeUsername(string newUsername)
        {
            if (newUsername == Current.username)
            {
                return ClaimResult.Success;
            }

            ClaimResult result = Current.registered
                ? await LeaderboardService.UpdateUsername(newUsername)
                : await LeaderboardService.Register(newUsername);

            if (result == ClaimResult.Success)
            {
                Update(s =>
                {
                    s.username = newUsername;
                    s.registered = true;
                });
            }
            return result;
        }
    }
}
