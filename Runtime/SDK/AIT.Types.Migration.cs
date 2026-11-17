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
}
