// -----------------------------------------------------------------------
// <copyright file="AIT.GetIsTossLoginIntegratedService.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetIsTossLoginIntegratedService API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetIsTossLoginIntegratedService
    /// </summary>
    public static partial class AIT
    {
        /// <returns>토스 로그인이 연동된 유저인지 여부를 반환해요. true: 토스 로그인이 연동된 유저에요. false: 토스 로그인이 연동되지 않은 유저에요. undefined: 앱 버전이 최소 지원 버전보다 낮아요.</returns>
        public static Task<bool> GetIsTossLoginIntegratedService()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(result => tcs.SetResult(result));
            __getIsTossLoginIntegratedService_Internal(callbackId, "bool");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetIsTossLoginIntegratedService called");
            return Task.FromResult(false);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getIsTossLoginIntegratedService_Internal(string callbackId, string typeName);
#endif
    }
}
