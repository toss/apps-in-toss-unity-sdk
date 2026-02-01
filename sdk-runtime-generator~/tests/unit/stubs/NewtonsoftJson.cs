// Newtonsoft.Json API Stub for Testing
// Minimal implementation of Newtonsoft.Json APIs needed for SDK compilation

using System;
using System.IO;

// System.Runtime.Serialization types needed for JSON serialization
namespace System.Runtime.Serialization
{
    /// <summary>
    /// Specifies that the field is an enumeration member and should be serialized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class EnumMemberAttribute : Attribute
    {
        public EnumMemberAttribute() { }
        public string Value { get; set; }
    }

    /// <summary>
    /// Specifies that the class can be serialized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class DataContractAttribute : Attribute
    {
        public DataContractAttribute() { }
        public string Name { get; set; }
        public string Namespace { get; set; }
    }

    /// <summary>
    /// Specifies that the member is part of a data contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class DataMemberAttribute : Attribute
    {
        public DataMemberAttribute() { }
        public string Name { get; set; }
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public bool EmitDefaultValue { get; set; } = true;
    }
}

namespace Newtonsoft.Json
{
    /// <summary>
    /// Provides methods for serializing and deserializing JSON.
    /// </summary>
    public static class JsonConvert
    {
        public static string SerializeObject(object value) => "{}";
        public static string SerializeObject(object value, JsonSerializerSettings settings) => "{}";
        public static string SerializeObject(object value, Formatting formatting) => "{}";
        public static string SerializeObject(object value, Formatting formatting, JsonSerializerSettings settings) => "{}";

        public static T DeserializeObject<T>(string value) => default(T);
        public static T DeserializeObject<T>(string value, JsonSerializerSettings settings) => default(T);
        public static object DeserializeObject(string value, Type type) => null;
    }

    /// <summary>
    /// Specifies formatting options for JSON.
    /// </summary>
    public enum Formatting
    {
        None = 0,
        Indented = 1
    }

    /// <summary>
    /// Specifies the settings on a JsonSerializer.
    /// </summary>
    public class JsonSerializerSettings
    {
        public JsonSerializerSettings() { }
        public System.Collections.Generic.IList<JsonConverter> Converters { get; set; } = new System.Collections.Generic.List<JsonConverter>();
        public NullValueHandling NullValueHandling { get; set; }
        public MissingMemberHandling MissingMemberHandling { get; set; }
        public ReferenceLoopHandling ReferenceLoopHandling { get; set; }
        public TypeNameHandling TypeNameHandling { get; set; }
    }

    public enum NullValueHandling { Include = 0, Ignore = 1 }
    public enum MissingMemberHandling { Ignore = 0, Error = 1 }
    public enum ReferenceLoopHandling { Error = 0, Ignore = 1, Serialize = 2 }
    public enum TypeNameHandling { None = 0, Objects = 1, Arrays = 2, All = 3, Auto = 4 }

    /// <summary>
    /// Instructs the JsonSerializer how to serialize the object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class JsonPropertyAttribute : Attribute
    {
        public JsonPropertyAttribute() { }
        public JsonPropertyAttribute(string propertyName) { PropertyName = propertyName; }

        public string PropertyName { get; set; }
        public NullValueHandling NullValueHandling { get; set; }
        public bool Required { get; set; }
        public int Order { get; set; }
    }

    /// <summary>
    /// Instructs the JsonSerializer not to serialize the public field or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class JsonIgnoreAttribute : Attribute
    {
        public JsonIgnoreAttribute() { }
    }

    /// <summary>
    /// Instructs the JsonSerializer to use the specified JsonConverter when serializing the object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class JsonConverterAttribute : Attribute
    {
        public JsonConverterAttribute(Type converterType) { ConverterType = converterType; }
        public Type ConverterType { get; }
    }

    /// <summary>
    /// Converts an object to and from JSON.
    /// </summary>
    public abstract class JsonConverter
    {
        public abstract bool CanConvert(Type objectType);
        public abstract void WriteJson(JsonWriter writer, object value, JsonSerializer serializer);
        public abstract object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer);
        public virtual bool CanRead => true;
        public virtual bool CanWrite => true;
    }

    /// <summary>
    /// Serializes and deserializes objects into and from the JSON format.
    /// </summary>
    public class JsonSerializer
    {
        public JsonSerializerSettings Settings { get; set; }
    }

    /// <summary>
    /// Represents a writer that provides a fast, non-cached, forward-only way of generating JSON data.
    /// </summary>
    public abstract class JsonWriter : IDisposable
    {
        public abstract void WriteValue(string value);
        public abstract void WriteValue(int value);
        public abstract void WriteValue(long value);
        public abstract void WriteValue(double value);
        public abstract void WriteValue(bool value);
        public abstract void WriteNull();
        public abstract void WriteStartObject();
        public abstract void WriteEndObject();
        public abstract void WriteStartArray();
        public abstract void WriteEndArray();
        public abstract void WritePropertyName(string name);
        public virtual void Dispose() { }
    }

    /// <summary>
    /// Represents a reader that provides fast, non-cached, forward-only access to JSON data.
    /// </summary>
    public abstract class JsonReader : IDisposable
    {
        public abstract bool Read();
        public virtual object Value { get; }
        public virtual Type ValueType { get; }
        public virtual JsonToken TokenType { get; }
        public virtual void Dispose() { }
    }

    /// <summary>
    /// Specifies the type of JSON token.
    /// </summary>
    public enum JsonToken
    {
        None = 0,
        StartObject = 1,
        StartArray = 2,
        PropertyName = 4,
        String = 9,
        Integer = 7,
        Float = 8,
        Boolean = 11,
        Null = 12,
        EndObject = 13,
        EndArray = 14
    }
}

namespace Newtonsoft.Json.Converters
{
    /// <summary>
    /// Converts an Enum to and from its name string value.
    /// </summary>
    public class StringEnumConverter : JsonConverter
    {
        public StringEnumConverter() { }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsEnum;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            writer.WriteValue(value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null) return null;
            return Enum.Parse(objectType, reader.Value.ToString());
        }
    }
}
