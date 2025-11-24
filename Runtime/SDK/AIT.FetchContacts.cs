// -----------------------------------------------------------------------
// <copyright file="AIT.FetchContacts.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - FetchContacts API
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - FetchContacts
    /// </summary>
    public static partial class AIT
    {
        public static void FetchContacts(FetchContactsOptions options, Action<ContactResult> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string callbackId = AITCore.Instance.RegisterCallback(callback);
            __fetchContacts_Internal(options, callbackId, "ContactResult");
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchContacts called");
            var mockResult = default(ContactResult);
            callback?.Invoke(mockResult);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchContacts_Internal(FetchContactsOptions options, string callbackId, string typeName);
#endif
    }
}
