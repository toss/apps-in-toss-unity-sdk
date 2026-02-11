// -----------------------------------------------------------------------
// <copyright file="FirebaseCallbackRouter.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Callback Router
// </copyright>
// -----------------------------------------------------------------------

using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
    /// <summary>
    /// Firebase 콜백 라우터
    /// AITCore의 외부 라우터 확장 포인트에 등록하여 Firebase 타입의 콜백을 처리합니다.
    /// </summary>
    [Preserve]
    public static class FirebaseCallbackRouter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            AITCore.RegisterExternalRouter(RouteFirebaseCallback);
            AITCore.RegisterExternalSubscriptionRouter(RouteFirebaseSubscriptionCallback);
            Debug.Log("[AITFirebase] Callback router registered");
        }

        /// <summary>
        /// Firebase 비동기 API 콜백 라우팅
        /// </summary>
        static bool RouteFirebaseCallback(string callbackId, string typeName, string resultJson, AITCore core)
        {
            var apiResponse = JsonConvert.DeserializeObject<APIResponse>(resultJson);

            switch (typeName)
            {
                case "FirebaseUser":
                    if (apiResponse.success)
                    {
                        if (core.TryGetCallback<FirebaseUser>(callbackId, out var cb_FirebaseUser))
                        {
                            var data = JsonConvert.DeserializeObject<FirebaseUser>(apiResponse.data);
                            cb_FirebaseUser(data);
                        }
                    }
                    else
                    {
                        if (core.TryGetErrorCallback(callbackId, out var errCb_FirebaseUser))
                        {
                            errCb_FirebaseUser(new AITException("FirebaseUser", apiResponse.error));
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Firebase 구독 API 콜백 라우팅
        /// </summary>
        static bool RouteFirebaseSubscriptionCallback(string callbackId, string typeName, string resultJson, AITCore core)
        {
            var apiResponse = JsonConvert.DeserializeObject<APIResponse>(resultJson);

            switch (typeName)
            {
                case "FirebaseUser":
                    if (apiResponse.success)
                    {
                        if (core.TryGetSubscriptionCallback<FirebaseUser>(callbackId, out var subCb_FirebaseUser))
                        {
                            var data = JsonConvert.DeserializeObject<FirebaseUser>(apiResponse.data);
                            subCb_FirebaseUser(data);
                        }
                    }
                    else
                    {
                        if (core.TryGetSubscriptionErrorCallback(callbackId, out var subErrCb_FirebaseUser))
                        {
                            subErrCb_FirebaseUser(new AITException("FirebaseUser", apiResponse.error));
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }
    }
}
