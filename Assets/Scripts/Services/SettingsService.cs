using System;
using DTOs;

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
    }
}