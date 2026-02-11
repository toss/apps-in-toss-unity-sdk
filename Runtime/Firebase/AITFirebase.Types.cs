// -----------------------------------------------------------------------
// <copyright file="AITFirebase.Types.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
    /// <summary>Firebase 인증 사용자 정보</summary>
    [Serializable]
    [Preserve]
    public class FirebaseUser
    {
        [Preserve]
        public FirebaseUser() { }

        /// <summary>사용자 고유 ID</summary>
        [Preserve]
        [JsonProperty("uid")]
        public string Uid { get; set; }

        /// <summary>이메일 주소</summary>
        [Preserve]
        [JsonProperty("email")]
        public string Email { get; set; }

        /// <summary>표시 이름</summary>
        [Preserve]
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        /// <summary>프로필 사진 URL</summary>
        [Preserve]
        [JsonProperty("photoURL")]
        public string PhotoURL { get; set; }

        /// <summary>전화번호</summary>
        [Preserve]
        [JsonProperty("phoneNumber")]
        public string PhoneNumber { get; set; }

        /// <summary>익명 사용자 여부</summary>
        [Preserve]
        [JsonProperty("isAnonymous")]
        public bool IsAnonymous { get; set; }

        /// <summary>이메일 인증 여부</summary>
        [Preserve]
        [JsonProperty("emailVerified")]
        public bool EmailVerified { get; set; }
    }
}
