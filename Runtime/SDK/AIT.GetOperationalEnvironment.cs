// -----------------------------------------------------------------------
// <copyright file="AIT.GetOperationalEnvironment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetOperationalEnvironment API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetOperationalEnvironment
    /// </summary>
    public static partial class AIT
    {
        /// <returns>현재 운영 환경을 나타내는 문자열이에요. 'toss': 토스 앱에서 실행 중이에요. 'sandbox': 샌드박스 환경에서 실행 중이에요.</returns>
        public static Task GetOperationalEnvironment()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __getOperationalEnvironment_Internal(callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetOperationalEnvironment called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getOperationalEnvironment_Internal(string callbackId, string typeName);
#endif
    }
}
