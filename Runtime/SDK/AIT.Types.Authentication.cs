// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Authentication.cs" company="Toss">
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
    public enum AppLoginResultReferrer
    {
        [EnumMember(Value = "DEFAULT")]
        DEFAULT,
        [EnumMember(Value = "SANDBOX")]
        SANDBOX
    }

    [Serializable]
    [Preserve]
    public partial class AppLoginResult
    {
        [Preserve]
        public string AuthorizationCode;
        [Preserve]
        public AppLoginResultReferrer Referrer;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
