// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Environment.cs" company="Toss">
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
    public class AppsInTossGlobals
    {
        [Preserve]
        [JsonProperty("deploymentId")]
        public string DeploymentId;
        [Preserve]
        [JsonProperty("brandDisplayName")]
        public string BrandDisplayName;
        [Preserve]
        [JsonProperty("brandIcon")]
        public string BrandIcon;
        [Preserve]
        [JsonProperty("brandPrimaryColor")]
        public string BrandPrimaryColor;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IsMinVersionSupportedMinVersions
    {
        [Preserve]
        [JsonProperty("android")]
        public string Android;
        [Preserve]
        [JsonProperty("ios")]
        public string Ios;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
