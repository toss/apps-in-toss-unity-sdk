// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Authentication.cs" company="Toss">
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
    public enum AppLoginResultReferrer
    {
        [EnumMember(Value = "DEFAULT")]
        DEFAULT,
        [EnumMember(Value = "SANDBOX")]
        SANDBOX
    }

    [Serializable]
    [Preserve]
    public class AppLoginResult
    {
        [Preserve]
        [JsonProperty("authorizationCode")]
        public string AuthorizationCode;
        [Preserve]
        [JsonProperty("referrer")]
        public AppLoginResultReferrer Referrer;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
