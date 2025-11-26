// -----------------------------------------------------------------------
// <copyright file="AIT.AppLogin.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - AppLogin API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - AppLogin
    /// </summary>
    public static partial class AIT
    {
        public static Task<AppLoginResult> AppLogin()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AppLoginResult>();
            string callbackId = AITCore.Instance.RegisterCallback<AppLoginResult>(result => tcs.SetResult(result));
            __appLogin_Internal(callbackId, "AppLoginResult");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] AppLogin called");
            return Task.FromResult(default(AppLoginResult));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __appLogin_Internal(string callbackId, string typeName);
#endif
    }
}
