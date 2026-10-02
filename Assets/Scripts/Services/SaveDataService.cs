using System;
using DTOs;

namespace Services
{
    public static class SaveDataService
    {
        private const string File = "savedata.json";
        public static SaveData Current { get; private set; } = SaveSystem.Load<SaveData>(File);
        public static event Action<SaveData> Changed;

        public static void Update(Action<SaveData> mutate)
        {
            mutate(Current);
            SaveSystem.Save(Current, File);
            Changed?.Invoke(Current);
        }
    }
}