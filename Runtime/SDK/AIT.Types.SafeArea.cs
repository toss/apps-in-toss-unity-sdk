// -----------------------------------------------------------------------
// <copyright file="AIT.Types.SafeArea.cs" company="Toss">
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
    public class SafeAreaInsets
    {
        [Preserve]
        [JsonProperty("top")]
        public double Top;
        [Preserve]
        [JsonProperty("left")]
        public double Left;
        [Preserve]
        [JsonProperty("right")]
        public double Right;
        [Preserve]
        [JsonProperty("bottom")]
        public double Bottom;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
