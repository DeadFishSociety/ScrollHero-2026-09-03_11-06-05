using System;

namespace DTOs
{
    [Serializable]
    public class SettingsData
    {
        private static readonly Random Rng = new Random();

        public string username = $"Player#{Rng.Next(0, 1000000):D8}";
    }
}