// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Notification.cs" company="Toss">
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
    public class RequestNotificationAgreementOptions
    {
        [Preserve]
        [JsonProperty("options")]
        public RequestNotificationAgreementOptionsOptions Options;
        [JsonIgnore]
        public System.Action<RequestNotificationAgreementOptionsOnEventParam> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class RequestNotificationAgreementOptionsOnEventParam
    {
        [Preserve]
        [JsonProperty("type")]
        public RequestNotificationAgreementOptionsOnEventParamType Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    public enum RequestNotificationAgreementOptionsOnEventParamType
    {
        [EnumMember(Value = "newAgreement")]
        NewAgreement,
        [EnumMember(Value = "alreadyAgreed")]
        AlreadyAgreed,
        [EnumMember(Value = "agreementRejected")]
        AgreementRejected
    }

    [Serializable]
    [Preserve]
    public class RequestNotificationAgreementOptionsOptions
    {
        [Preserve]
        [JsonProperty("templateCode")]
        public string TemplateCode;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
