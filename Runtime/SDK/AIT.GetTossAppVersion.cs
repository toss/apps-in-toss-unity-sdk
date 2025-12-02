// -----------------------------------------------------------------------
// <copyright file="AIT.GetTossAppVersion.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetTossAppVersion API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetTossAppVersion
    /// </summary>
    public static partial class AIT
    {
        /// <returns>토스 앱 버전</returns>
        public static Task GetTossAppVersion()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __getTossAppVersion_Internal(callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetTossAppVersion called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getTossAppVersion_Internal(string callbackId, string typeName);
#endif
    }
}
