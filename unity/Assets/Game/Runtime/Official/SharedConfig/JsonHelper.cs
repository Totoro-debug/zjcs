using System;
using System.IO;
using Newtonsoft.Json;

namespace GConfig.Helper
{
    public static class JsonHelper
    {
        public static IArrayPool<char> ArrayPool;
        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T), null);
        public static T FromJson<T>(string json, JsonSerializerSettings settings) => (T)FromJson(json, typeof(T), settings);
        public static object FromJson(string json, Type type) => FromJson(json, type, null);
        // Original 0x9923270 uses CreateDefault and disposes the JSON reader.
        public static object FromJson(string json, Type type, JsonSerializerSettings settings)
        {
            var serializer = JsonSerializer.CreateDefault(settings);
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                if (ArrayPool != null) reader.ArrayPool = ArrayPool;
                return serializer.Deserialize(reader, type);
            }
        }
        public static T FromJsonFast<T>(string json) => UnityEngine.JsonUtility.FromJson<T>(json);
        public static object FromJsonFast(string json, Type type) => UnityEngine.JsonUtility.FromJson(json, type);
    }
}
