// Unity stubs for testing C# compilation without Unity installed
using System;

namespace UnityEngine
{
    public class MonoBehaviour
    {
        public GameObject gameObject { get; set; }
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => default(T);
        public static string ToJson(object obj) => "";
    }

    public class GameObject
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : new() => new T();
    }

    public class Object
    {
        public static void DontDestroyOnLoad(Object obj) { }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
}
