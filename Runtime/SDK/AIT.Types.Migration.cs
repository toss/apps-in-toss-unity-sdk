// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Migration.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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
    public partial class MigrationGetOriginStorageResult
    {
        [Preserve]
        public OriginStorageDump Previous;
        [Preserve]
        public OriginStorageDump Current;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDump
    {
        [Preserve]
        public string Origin;
        [Preserve]
        public Dictionary<string, object> LocalStorage;
        [Preserve]
        public OriginStorageDumpIndexedDB[] IndexedDB;
        [Preserve]
        public OriginStorageDumpOpfs Opfs;
        [Preserve]
        public OriginStorageDumpError[] Errors;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDB
    {
        [Preserve]
        public string Name;
        [Preserve]
        public double Version;
        [Preserve]
        public OriginStorageDumpIndexedDBObjectStore[] ObjectStores;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDBObjectStore
    {
        [Preserve]
        public string Name;
        /// <summary>저장소의 keyPath예요. 문자열, 문자열 배열(JArray), null 중 하나로 와요.</summary>
        [Preserve]
        public object KeyPath;
        [Preserve]
        public bool AutoIncrement;
        [Preserve]
        public OriginStorageDumpIndexedDBObjectStoreIndex[] Indexes;
        [Preserve]
        public OriginStorageDumpIndexedDBObjectStoreRecord[] Records;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDBObjectStoreIndex
    {
        [Preserve]
        public string Name;
        /// <summary>인덱스의 keyPath예요. 문자열 또는 문자열 배열(JArray)로 와요.</summary>
        [Preserve]
        public object KeyPath;
        [Preserve]
        public bool MultiEntry;
        [Preserve]
        public bool Unique;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpIndexedDBObjectStoreRecord
    {
        [Preserve]
        public object Key;
        [Preserve]
        public object PrimaryKey;
        [Preserve]
        public object Value;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpOpfs
    {
        [Preserve]
        public string[] Directories;
        [Preserve]
        public OriginStorageDumpOpfsFile[] Files;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpOpfsFile
    {
        [Preserve]
        public string Path;
        [Preserve]
        public string Type;
        [Preserve]
        public double LastModified;
        [Preserve]
        public byte[] Data;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OriginStorageDumpError
    {
        [Preserve]
        public OriginStorageDumpErrorStorage Storage;
        [Preserve]
        public string Message;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
