// -----------------------------------------------------------------------
// <copyright file="AIT.FetchContacts.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - FetchContacts API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - FetchContacts
    /// </summary>
    public static partial class AIT
    {
        public static Task<ContactResult> FetchContacts(FetchContactsOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<ContactResult>();
            string callbackId = AITCore.Instance.RegisterCallback<ContactResult>(result => tcs.SetResult(result));
            __fetchContacts_Internal(options, callbackId, "ContactResult");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] FetchContacts called");
            return Task.FromResult(default(ContactResult));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __fetchContacts_Internal(FetchContactsOptions options, string callbackId, string typeName);
#endif
    }
}
