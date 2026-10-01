using UnityEditor.Overlays;
using System.IO;
using UnityEngine;

namespace Services
{
    public class SaveSystem
    {
        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, "savedata.json");
        
        public static void Save(SaveData saveData)
        {
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(FilePath, json);
        }

        public static SaveData Load()
        {
            if (!File.Exists(FilePath)) {
                Debug.LogWarning("Save file not found. Returning fresh data.");
                return new SaveData();
            }

            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson(json);
        }
    }
}