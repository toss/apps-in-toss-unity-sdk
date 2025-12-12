// Unity API Stub for Testing
// Minimal implementation of UnityEngine APIs needed for SDK compilation

using System;

namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(Object obj) { }
    }

    public class GameObject : Object
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : new() => new T();
    }

    public class MonoBehaviour : Object
    {
        public GameObject gameObject { get; set; }
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => default(T);
        public static string ToJson(object obj) => "";
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
}

namespace UnityEngine.Scripting
{
    /// <summary>
    /// Prevents Unity's bytecode stripping from removing types, methods, etc.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Assembly |
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Enum |
        AttributeTargets.Constructor |
        AttributeTargets.Method |
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Event |
        AttributeTargets.Interface |
        AttributeTargets.Delegate,
        Inherited = false)]
    public class PreserveAttribute : Attribute
    {
        public PreserveAttribute() { }
    }
}
