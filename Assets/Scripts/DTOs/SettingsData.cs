using System;

namespace DTOs
{
    [Serializable]
    public class SettingsData
    {
        private static readonly Random Rng = new Random();

        public string username = GenerateUsername();

        // Whether username has been claimed on the leaderboard server.
        public bool registered;

        public static string GenerateUsername() => $"Player#{Rng.Next(0, 100000000):D8}";
    }
}