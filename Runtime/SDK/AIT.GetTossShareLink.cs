// -----------------------------------------------------------------------
// <copyright file="AIT.GetTossShareLink.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetTossShareLink API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetTossShareLink
    /// </summary>
    public static partial class AIT
    {
        /// <param name="path">딥링크로 열고 싶은 경로예요. intoss://로 시작하는 문자열이어야 해요.</param>
        /// <returns>deep_link_value가 포함된 토스 공유 링크를 반환해요.</returns>
        public static Task<string> GetTossShareLink(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(result => tcs.SetResult(result));
            __getTossShareLink_Internal(path, callbackId, "string");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetTossShareLink called");
            return Task.FromResult("");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getTossShareLink_Internal(string path, string callbackId, string typeName);
#endif
    }
}
