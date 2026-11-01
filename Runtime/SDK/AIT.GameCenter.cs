// -----------------------------------------------------------------------
// <copyright file="AIT.GameCenter.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GameCenter APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Scripting;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GameCenter
    /// </summary>
    public static partial class AIT
    {
        /// <summary>
        /// 게임센터 리더보드 웹뷰를 열어요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable GameOpenLeaderboard(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "GameOpenLeaderboard"
            );
            __GameOpenLeaderboard_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GameOpenLeaderboard called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task GameOpenLeaderboard(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GameOpenLeaderboard"
            );
            __GameOpenLeaderboard_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameOpenLeaderboard called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameOpenLeaderboard_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Game.openLeaderboard`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GameOpenLeaderboardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GameOpenLeaderboardIsSupported"
            );
            __GameOpenLeaderboardIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GameOpenLeaderboardIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GameOpenLeaderboardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GameOpenLeaderboardIsSupported"
            );
            __GameOpenLeaderboardIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameOpenLeaderboardIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameOpenLeaderboardIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 사용자의 게임 점수를 토스게임센터 리더보드에 제출해요.
        /// `score`는 실수형 숫자의 문자열이어야 해요. 예: `"123.45"`
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>결과 값 또는 null</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SubmitGameCenterLeaderBoardScoreResponse> GameSetLeaderboardScore(GameSetLeaderboardScoreParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SubmitGameCenterLeaderBoardScoreResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<SubmitGameCenterLeaderBoardScoreResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GameSetLeaderboardScore"
            );
            __GameSetLeaderboardScore_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GameSetLeaderboardScore called");
            await Awaitable.NextFrameAsync();
            return default(SubmitGameCenterLeaderBoardScoreResponse);
#endif
        }
#else
        public static async Task<SubmitGameCenterLeaderBoardScoreResponse> GameSetLeaderboardScore(GameSetLeaderboardScoreParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SubmitGameCenterLeaderBoardScoreResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<SubmitGameCenterLeaderBoardScoreResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GameSetLeaderboardScore"
            );
            __GameSetLeaderboardScore_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameSetLeaderboardScore called");
            await Task.CompletedTask;
            return default(SubmitGameCenterLeaderBoardScoreResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameSetLeaderboardScore_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Game.setLeaderboardScore`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GameSetLeaderboardScoreIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GameSetLeaderboardScoreIsSupported"
            );
            __GameSetLeaderboardScoreIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GameSetLeaderboardScoreIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GameSetLeaderboardScoreIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GameSetLeaderboardScoreIsSupported"
            );
            __GameSetLeaderboardScoreIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameSetLeaderboardScoreIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameSetLeaderboardScoreIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 토스게임센터 프로필 정보를 가져와요.
        /// 프로필이 없으면 `statusCode: 'PROFILE_NOT_FOUND'`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>결과 값 또는 null</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GameCenterGameProfileResponse> GameGetUserProfile(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GameGetUserProfile"
            );
            __GameGetUserProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GameGetUserProfile called");
            await Awaitable.NextFrameAsync();
            return default(GameCenterGameProfileResponse);
#endif
        }
#else
        public static async Task<GameCenterGameProfileResponse> GameGetUserProfile(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GameGetUserProfile"
            );
            __GameGetUserProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameGetUserProfile called");
            await Task.CompletedTask;
            return default(GameCenterGameProfileResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameGetUserProfile_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `Game.getUserProfile`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GameGetUserProfileIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GameGetUserProfileIsSupported"
            );
            __GameGetUserProfileIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GameGetUserProfileIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GameGetUserProfileIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GameGetUserProfileIsSupported"
            );
            __GameGetUserProfileIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameGetUserProfileIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameGetUserProfileIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 게임 정보 오버레이가 완전히 사라진 시점을 구독해요.
        /// 반환되는 구독 해제 함수는 더 이상 이벤트가 필요하지 않을 때 호출하세요.
        /// </summary>
        /// <param name="onEvent">Event callback when the event is triggered</param>
        /// <param name="onError">Error callback (optional)</param>
        /// <returns>Action to call for unsubscribing from the event</returns>
        [Preserve]
        [APICategory("GameCenter")]
        public static Action GameSubscribeInfoOverlayHidden(
            Action onEvent,
            Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterVoidSubscriptionCallback(
                onEvent,
                onError
            );
            __GameSubscribeInfoOverlayHidden_Internal(subscriptionId, "void");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GameSubscribeInfoOverlayHidden subscribed");
            return () => UnityEngine.Debug.Log($"[AIT Mock] GameSubscribeInfoOverlayHidden unsubscribed");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __GameSubscribeInfoOverlayHidden_Internal(string subscriptionId, string typeName);
#endif
        /// <summary>
        /// 토스게임센터 프로필 정보를 가져와요.
        /// 프로필이 없으면 `statusCode: 'PROFILE_NOT_FOUND'`를 반환해요.
        /// 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>프로필 정보 또는 undefined를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use GameGetUserProfile")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GameCenterGameProfileResponse> GetGameCenterGameProfile(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetGameCenterGameProfile"
            );
            __getGameCenterGameProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfile called");
            await Awaitable.NextFrameAsync();
            return default(GameCenterGameProfileResponse);
#endif
        }
#else
        public static async Task<GameCenterGameProfileResponse> GetGameCenterGameProfile(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GameCenterGameProfileResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<GameCenterGameProfileResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetGameCenterGameProfile"
            );
            __getGameCenterGameProfile_Internal(callbackId, "GameCenterGameProfileResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfile called");
            await Task.CompletedTask;
            return default(GameCenterGameProfileResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGameCenterGameProfile_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `getGameCenterGameProfile`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use GameGetUserProfileIsSupported")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GetGameCenterGameProfileIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetGameCenterGameProfileIsSupported"
            );
            __getGameCenterGameProfileIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfileIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GetGameCenterGameProfileIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetGameCenterGameProfileIsSupported"
            );
            __getGameCenterGameProfileIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetGameCenterGameProfileIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getGameCenterGameProfileIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// v2 호환용 별칭이에요. GetAnonymousKey 또는 UserGetAnonymousKey를 사용해주세요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>결과 값 또는 null</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetAnonymousKey")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GetUserKeyForGameResult> GetUserKeyForGameTyped(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GetUserKeyForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyForGameResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetUserKeyForGameTyped"
            );
            __getUserKeyForGame_Internal(callbackId, "GetUserKeyForGameResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            await Awaitable.NextFrameAsync();
            return default(GetUserKeyForGameResult);
#endif
        }
#else
        public static async Task<GetUserKeyForGameResult> GetUserKeyForGameTyped(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GetUserKeyForGameResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GetUserKeyForGameResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetUserKeyForGameTyped"
            );
            __getUserKeyForGame_Internal(callbackId, "GetUserKeyForGameResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            await Task.CompletedTask;
            return default(GetUserKeyForGameResult);
#endif
        }
#endif

        /// <summary>
        /// 이전 SDK의 반환 타입(<c>string</c>)을 그대로 돌려주는 옛 시그니처다.
        /// 이전 SDK와 같은 경로(typeName "string")로 값을 받으므로 문자열이 같다.
        /// 새 코드는 <see cref="GetUserKeyForGameTyped"/>를 쓴다.
        /// </summary>
        [Obsolete("Use GetUserKeyForGameTyped")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<string> GetUserKeyForGame(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetUserKeyForGame"
            );
            __getUserKeyForGame_Internal(callbackId, "string");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            await Awaitable.NextFrameAsync();
            return "";
#endif
        }
#else
        public static async Task<string> GetUserKeyForGame(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetUserKeyForGame"
            );
            __getUserKeyForGame_Internal(callbackId, "string");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGame called");
            await Task.CompletedTask;
            return "";
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getUserKeyForGame_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `getUserKeyForGame`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use UserGetAnonymousKeyIsSupported")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GetUserKeyForGameIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GetUserKeyForGameIsSupported"
            );
            __getUserKeyForGameIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGameIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GetUserKeyForGameIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GetUserKeyForGameIsSupported"
            );
            __getUserKeyForGameIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetUserKeyForGameIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getUserKeyForGameIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <param name="paramsParam">포인트를 지급하기 위해 필요한 정보예요.</param>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. "40000": 게임이 아닌 미니앱에서 호출했을 때 "4100": 프로모션 정보를 찾을 수 없을 때 "4104": 프로모션이 중지되었을 때 "4105": 프로모션이 종료되었을 때 "4108": 프로모션이 승인되지 않았을 때 "4109": 프로모션이 실행중이 아닐 때 "4110": 리워드를 지급/회수할 수 없을 때 "4112": 프로모션 머니가 부족할 때 "4113": 이미 지급/회수된 내역일 때 "4114": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use PromotionGrantReward")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<GrantPromotionRewardResult> GrantPromotionRewardForGame(GrantPromotionRewardParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<GrantPromotionRewardResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResult>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GrantPromotionRewardForGame"
            );
            __grantPromotionRewardForGame_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResult");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGame called");
            await Awaitable.NextFrameAsync();
            return default(GrantPromotionRewardResult);
#endif
        }
#else
        public static async Task<GrantPromotionRewardResult> GrantPromotionRewardForGame(GrantPromotionRewardParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<GrantPromotionRewardResult>();
            string callbackId = AITCore.Instance.RegisterCallback<GrantPromotionRewardResult>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GrantPromotionRewardForGame"
            );
            __grantPromotionRewardForGame_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "GrantPromotionRewardResult");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGame called");
            await Task.CompletedTask;
            return default(GrantPromotionRewardResult);
#endif
        }
#endif

        /// <summary>
        /// 이전 SDK의 매개변수 타입(<see cref="GrantPromotionRewardForGameParams"/>)을 받는 옛 시그니처다.
        /// 새 코드는 <see cref="GrantPromotionRewardParams"/>를 넘긴다.
        /// </summary>
        [Obsolete("GrantPromotionRewardParams를 사용하세요.")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static Awaitable<GrantPromotionRewardResult> GrantPromotionRewardForGame(GrantPromotionRewardForGameParams paramsParam, int timeoutMs = 0)
            => GrantPromotionRewardForGame(paramsParam == null ? null : new GrantPromotionRewardParams { Params = paramsParam.Params ?? ((GrantPromotionRewardParams)paramsParam).Params, _extensionData = paramsParam._extensionData }, timeoutMs);
#else
        public static Task<GrantPromotionRewardResult> GrantPromotionRewardForGame(GrantPromotionRewardForGameParams paramsParam, int timeoutMs = 0)
            => GrantPromotionRewardForGame(paramsParam == null ? null : new GrantPromotionRewardParams { Params = paramsParam.Params ?? ((GrantPromotionRewardParams)paramsParam).Params, _extensionData = paramsParam._extensionData }, timeoutMs);
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionRewardForGame_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `grantPromotionRewardForGame`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use PromotionGrantRewardIsSupported")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> GrantPromotionRewardForGameIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "GrantPromotionRewardForGameIsSupported"
            );
            __grantPromotionRewardForGameIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGameIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> GrantPromotionRewardForGameIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "GrantPromotionRewardForGameIsSupported"
            );
            __grantPromotionRewardForGameIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GrantPromotionRewardForGameIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __grantPromotionRewardForGameIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 게임센터 리더보드 웹뷰를 열어요.
        /// 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>리더보드 웹뷰를 호출해요. 앱 버전이 낮으면 아무 동작도 하지 않고 undefined를 반환해요.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use GameOpenLeaderboard")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable OpenGameCenterLeaderboard(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => acs.SetResult(),
                error => acs.SetException(error),
                timeoutMs,
                "OpenGameCenterLeaderboard"
            );
            __openGameCenterLeaderboard_Internal(callbackId, "void");
            await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboard called");
            await Awaitable.NextFrameAsync();
            // void return - nothing to return
#endif
        }
#else
        public static async Task OpenGameCenterLeaderboard(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(
                result => tcs.TrySetResult(null),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenGameCenterLeaderboard"
            );
            __openGameCenterLeaderboard_Internal(callbackId, "void");
            await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboard called");
            await Task.CompletedTask;
            // void return - nothing to return
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openGameCenterLeaderboard_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `openGameCenterLeaderboard`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use GameOpenLeaderboardIsSupported")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> OpenGameCenterLeaderboardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "OpenGameCenterLeaderboardIsSupported"
            );
            __openGameCenterLeaderboardIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboardIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> OpenGameCenterLeaderboardIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "OpenGameCenterLeaderboardIsSupported"
            );
            __openGameCenterLeaderboardIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OpenGameCenterLeaderboardIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __openGameCenterLeaderboardIsSupported_Internal(string callbackId, string typeName);
#endif
        /// <summary>
        /// 사용자의 게임 점수를 토스게임센터 리더보드에 제출해요.
        /// `score`는 실수형 숫자의 문자열이어야 해요. 예: `"123.45"`
        /// 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <returns>점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 undefined를 반환해요. 값이 없으면 null을 반환합니다.</returns>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use GameSetLeaderboardScore")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<SubmitGameCenterLeaderBoardScoreResponse> SubmitGameCenterLeaderBoardScore(SubmitGameCenterLeaderBoardScoreParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<SubmitGameCenterLeaderBoardScoreResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<SubmitGameCenterLeaderBoardScoreResponse>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SubmitGameCenterLeaderBoardScore"
            );
            __submitGameCenterLeaderBoardScore_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScore called");
            await Awaitable.NextFrameAsync();
            return default(SubmitGameCenterLeaderBoardScoreResponse);
#endif
        }
#else
        public static async Task<SubmitGameCenterLeaderBoardScoreResponse> SubmitGameCenterLeaderBoardScore(SubmitGameCenterLeaderBoardScoreParams paramsParam, int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SubmitGameCenterLeaderBoardScoreResponse>();
            string callbackId = AITCore.Instance.RegisterCallback<SubmitGameCenterLeaderBoardScoreResponse>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SubmitGameCenterLeaderBoardScore"
            );
            __submitGameCenterLeaderBoardScore_Internal(AITJsonSettings.Serialize(paramsParam), callbackId, "SubmitGameCenterLeaderBoardScoreResponse");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScore called");
            await Task.CompletedTask;
            return default(SubmitGameCenterLeaderBoardScoreResponse);
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __submitGameCenterLeaderBoardScore_Internal(string paramsParam, string callbackId, string typeName);
#endif
        /// <summary>
        /// 현재 앱 버전과 환경에서 `submitGameCenterLeaderBoardScore`를 사용할 수 있는지 확인해요.
        /// </summary>
        /// <param name="timeoutMs">Optional client-side timeout in milliseconds. 0 (the default) waits indefinitely; a positive value throws <see cref="AITClientTimeoutException"/> if no response arrives before the deadline. The underlying platform work is not cancelled.</param>
        /// <exception cref="AITException">Thrown when the API call fails</exception>
        /// <exception cref="AITClientTimeoutException">Thrown when the timeoutMs deadline elapses before a response arrives</exception>
        [Obsolete("Use GameSetLeaderboardScoreIsSupported")]
        [Preserve]
        [APICategory("GameCenter")]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> SubmitGameCenterLeaderBoardScoreIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => acs.SetResult(result),
                error => acs.SetException(error),
                timeoutMs,
                "SubmitGameCenterLeaderBoardScoreIsSupported"
            );
            __submitGameCenterLeaderBoardScoreIsSupported_Internal(callbackId, "bool");
            return await acs.Awaitable;
#else
            // Unity Editor mock implementation (Unity 6+)
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScoreIsSupported called");
            await Awaitable.NextFrameAsync();
            return false;
#endif
        }
#else
        public static async Task<bool> SubmitGameCenterLeaderBoardScoreIsSupported(int timeoutMs = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<bool>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error),
                timeoutMs,
                "SubmitGameCenterLeaderBoardScoreIsSupported"
            );
            __submitGameCenterLeaderBoardScoreIsSupported_Internal(callbackId, "bool");
            return await tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SubmitGameCenterLeaderBoardScoreIsSupported called");
            await Task.CompletedTask;
            return false;
#endif
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __submitGameCenterLeaderBoardScoreIsSupported_Internal(string callbackId, string typeName);
#endif
    }
}
