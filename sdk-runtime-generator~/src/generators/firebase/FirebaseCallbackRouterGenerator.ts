/**
 * Firebase 콜백 라우터 생성기
 *
 * FirebaseCallbackRouter.cs를 생성합니다.
 * AITCore의 외부 라우터 확장 포인트에 등록하여
 * Firebase 타입의 콜백을 처리합니다.
 */

import type { FirebaseAPIDefinition, FirebaseTypeDefinition } from '../../parser/firebase-parser.js';

/**
 * FirebaseCallbackRouter.cs 생성
 */
export function generateFirebaseCallbackRouter(
  apis: FirebaseAPIDefinition[],
  types: FirebaseTypeDefinition[]
): string {
  // 비동기 API에서 사용하는 고유 반환 타입 수집 (void 제외)
  const callbackTypes = new Set<string>();
  for (const api of apis) {
    if (api.isAsync && !api.returnType.isVoid) {
      callbackTypes.add(api.returnType.csharpType);
    }
  }

  // 구독 API에서 사용하는 고유 이벤트 데이터 타입 수집
  const eventDataTypes = new Set<string>();
  for (const api of apis) {
    if (api.isSubscription) {
      eventDataTypes.add(api.returnType.csharpType);
    }
  }

  const callbackCases = Array.from(callbackTypes).map(typeName =>
    generateCallbackCase(typeName)
  ).join('\n');

  const subscriptionCases = Array.from(eventDataTypes).map(typeName =>
    generateSubscriptionCase(typeName)
  ).join('\n');

  return `// -----------------------------------------------------------------------
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
${callbackCases}
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
${subscriptionCases}
                default:
                    return false;
            }
        }
    }
}
`;
}

/**
 * 비동기 콜백 case 생성
 */
function generateCallbackCase(typeName: string): string {
  return `                case "${typeName}":
                    if (apiResponse.success)
                    {
                        if (core.TryGetCallback<${typeName}>(callbackId, out var cb_${typeName}))
                        {
                            var data = JsonConvert.DeserializeObject<${typeName}>(apiResponse.data);
                            cb_${typeName}(data);
                        }
                    }
                    else
                    {
                        if (core.TryGetErrorCallback(callbackId, out var errCb_${typeName}))
                        {
                            errCb_${typeName}(new AITException("${typeName}", apiResponse.error));
                        }
                    }
                    return true;`;
}

/**
 * 구독 콜백 case 생성
 */
function generateSubscriptionCase(typeName: string): string {
  return `                case "${typeName}":
                    if (apiResponse.success)
                    {
                        if (core.TryGetSubscriptionCallback<${typeName}>(callbackId, out var subCb_${typeName}))
                        {
                            var data = JsonConvert.DeserializeObject<${typeName}>(apiResponse.data);
                            subCb_${typeName}(data);
                        }
                    }
                    else
                    {
                        if (core.TryGetSubscriptionErrorCallback(callbackId, out var subErrCb_${typeName}))
                        {
                            subErrCb_${typeName}(new AITException("${typeName}", apiResponse.error));
                        }
                    }
                    return true;`;
}
