using System.IO;
using UnityEngine;

namespace Services
{
    public class SaveSystem
    {
        private static string PathFor(string filename) => Path.Combine(Application.persistentDataPath, filename);
        
        public static void Save<T>(T data, string filename)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(PathFor(filename), json);
        }
        
        public static T Load<T>(string file) where T : new()
        {
            string path = PathFor(file);
            if (File.Exists(path))
                return JsonUtility.FromJson<T>(File.ReadAllText(path));

            // Save the new save file if the file did not exist yet
            T fresh = new T();
            Save(fresh, file);
            return fresh;
        }
    }
}