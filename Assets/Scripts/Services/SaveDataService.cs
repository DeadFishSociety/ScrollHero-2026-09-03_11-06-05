using System;
using DTOs;
using UnityEngine;

namespace Services
{
    public static class SaveDataService
    {
        private const string File = "savedata.json";
        public static SaveData Current { get; private set; }
        public static event Action<SaveData> Changed;

        // Runs before anything else on every Play, so the data is re-read from disk
        // even when Unity enters Play mode without a domain reload (statics survive then).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Load()
        {
            Current = SaveSystem.Load<SaveData>(File);
            Changed = null;
        }

        public static void Update(Action<SaveData> mutate)
        {
            mutate(Current);
            SaveSystem.Save(Current, File);
            Changed?.Invoke(Current);
        }
    }
}