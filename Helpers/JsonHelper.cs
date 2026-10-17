using System.IO;
using Newtonsoft.Json; // 你的项目里有 Newtonsoft.Json 包

namespace WinLauncher.Helpers
{
    public static class JsonHelper
    {
        public static void SaveToFile<T>(string path, T data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public static T LoadFromFile<T>(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch { }
            return default(T);
        }
    }
}