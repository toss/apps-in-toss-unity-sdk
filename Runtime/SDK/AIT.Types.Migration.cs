// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Migration.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    public enum OriginStorageDumpErrorStorage
    {
        [EnumMember(Value = "localStorage")]
        LocalStorage,
        [EnumMember(Value = "indexedDB")]
        IndexedDB,
        [EnumMember(Value = "opfs")]
        Opfs
    }

    [Serializable]
    [Preserve]
    public class MigrationGetOriginStorageResult
    {
        [Preserve]
        [JsonProperty("previous")]
        public OriginStorageDump Previous;
        [Preserve]
        [JsonProperty("current")]
        public OriginStorageDump Current;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDump
    {
        [Preserve]
        [JsonProperty("origin")]
        public string Origin;
        [Preserve]
        [JsonProperty("localStorage")]
        public Dictionary<string, object> LocalStorage;
        [Preserve]
        [JsonProperty("indexedDB")]
        public OriginStorageDumpIndexedDB[] IndexedDB;
        [Preserve]
        [JsonProperty("opfs")]
        public OriginStorageDumpOpfs Opfs;
        [Preserve]
        [JsonProperty("errors")]
        public OriginStorageDumpError[] Errors;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDB
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        [Preserve]
        [JsonProperty("version")]
        public double Version;
        [Preserve]
        [JsonProperty("objectStores")]
        public OriginStorageDumpIndexedDBObjectStore[] ObjectStores;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDBObjectStore
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        /// <summary>저장소의 keyPath예요. 문자열, 문자열 배열(JArray), null 중 하나로 와요.</summary>
        [Preserve]
        [JsonProperty("keyPath")]
        public object KeyPath;
        [Preserve]
        [JsonProperty("autoIncrement")]
        public bool AutoIncrement;
        [Preserve]
        [JsonProperty("indexes")]
        public OriginStorageDumpIndexedDBObjectStoreIndex[] Indexes;
        [Preserve]
        [JsonProperty("records")]
        public OriginStorageDumpIndexedDBObjectStoreRecord[] Records;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDBObjectStoreIndex
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        /// <summary>인덱스의 keyPath예요. 문자열 또는 문자열 배열(JArray)로 와요.</summary>
        [Preserve]
        [JsonProperty("keyPath")]
        public object KeyPath;
        [Preserve]
        [JsonProperty("multiEntry")]
        public bool MultiEntry;
        [Preserve]
        [JsonProperty("unique")]
        public bool Unique;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpIndexedDBObjectStoreRecord
    {
        [Preserve]
        [JsonProperty("key")]
        public object Key;
        [Preserve]
        [JsonProperty("primaryKey")]
        public object PrimaryKey;
        [Preserve]
        [JsonProperty("value")]
        public object Value;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpOpfs
    {
        [Preserve]
        [JsonProperty("directories")]
        public string[] Directories;
        [Preserve]
        [JsonProperty("files")]
        public OriginStorageDumpOpfsFile[] Files;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpOpfsFile
    {
        [Preserve]
        [JsonProperty("path")]
        public string Path;
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("lastModified")]
        public double LastModified;
        [Preserve]
        [JsonProperty("data")]
        public byte[] Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OriginStorageDumpError
    {
        [Preserve]
        [JsonProperty("storage")]
        public OriginStorageDumpErrorStorage Storage;
        [Preserve]
        [JsonProperty("message")]
        public string Message;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
