// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Permission.cs" company="Toss">
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
    public class GetPermissionPermission
    {
        [Preserve]
        [JsonProperty("name")]
        public PermissionName Name;
        [Preserve]
        [JsonProperty("access")]
        public PermissionAccess Access;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OpenPermissionDialogPermission
    {
        [Preserve]
        [JsonProperty("name")]
        public PermissionName Name;
        [Preserve]
        [JsonProperty("access")]
        public PermissionAccess Access;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class RequestPermissionPermission
    {
        [Preserve]
        [JsonProperty("name")]
        public PermissionName Name;
        [Preserve]
        [JsonProperty("access")]
        public PermissionAccess Access;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
