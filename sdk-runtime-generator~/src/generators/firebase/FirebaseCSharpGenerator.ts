/**
 * Firebase C# 코드 생성기
 *
 * Firebase API 정의에서 AITFirebase partial class 파일들을 생성합니다.
 * 기존 AIT SDK 패턴을 따르되, AITFirebase 네임스페이스를 사용합니다.
 */

import type { FirebaseAPIDefinition } from '../../parser/firebase-parser.js';

/**
 * API를 카테고리별로 그룹핑하여 C# partial class 파일 생성
 * @returns Map<파일명, 내용>
 */
export function generateFirebaseCSharp(apis: FirebaseAPIDefinition[]): Map<string, string> {
  const files = new Map<string, string>();

  // 카테고리별 그룹핑
  const categoryMap = new Map<string, FirebaseAPIDefinition[]>();
  for (const api of apis) {
    if (!categoryMap.has(api.category)) {
      categoryMap.set(api.category, []);
    }
    categoryMap.get(api.category)!.push(api);
  }

  for (const [category, categoryApis] of categoryMap.entries()) {
    // 카테고리 이름에서 'Firebase.' 접두사 제거하여 파일명 생성
    const shortCategory = category.replace('Firebase.', '');
    const fileName = `AITFirebase.${shortCategory}.cs`;
    const content = generateCategoryFile(category, shortCategory, categoryApis);
    files.set(fileName, content);
  }

  return files;
}

/**
 * 카테고리별 partial class 파일 생성
 */
function generateCategoryFile(
  category: string,
  shortCategory: string,
  apis: FirebaseAPIDefinition[]
): string {
  const methods = apis.map(api => generateMethod(api)).join('\n\n');

  const dllImports = apis
    .filter(api => !api.isSubscription)
    .map(api => generateDllImport(api))
    .join('\n');

  const subscriptionDllImports = apis
    .filter(api => api.isSubscription)
    .map(api => generateSubscriptionDllImport(api))
    .join('\n');

  const allDllImports = [dllImports, subscriptionDllImports].filter(s => s).join('\n');

  return `// -----------------------------------------------------------------------
// <copyright file="AITFirebase.${shortCategory}.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase ${shortCategory} APIs
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
    /// <summary>
    /// Firebase ${shortCategory} APIs
    /// </summary>
    public static partial class AITFirebase
    {
${methods}

#if UNITY_WEBGL && !UNITY_EDITOR
${allDllImports}
#endif
    }
}
`;
}

/**
 * 개별 API 메서드 생성
 */
function generateMethod(api: FirebaseAPIDefinition): string {
  if (api.isSubscription) {
    return generateSubscriptionMethod(api);
  }
  if (api.isAsync) {
    return generateAsyncMethod(api);
  }
  return generateSyncMethod(api);
}

/**
 * 비동기 API 메서드 생성 (Promise 반환)
 */
function generateAsyncMethod(api: FirebaseAPIDefinition): string {
  const paramDecls = api.parameters.map(p => {
    const defaultVal = p.optional ? ' = null' : '';
    const type = p.optional && !p.csharpType.endsWith('?') && p.csharpType === 'string'
      ? 'string' : p.csharpType;
    return `${type} ${p.name}${defaultVal}`;
  }).join(', ');

  const returnType = api.returnType.isVoid ? 'void' : api.returnType.csharpType;

  // 파라미터를 jslib에 전달하는 인자들
  const jslibArgs = api.parameters.map(p => p.name).join(', ');
  const jslibCallArgs = jslibArgs ? `${jslibArgs}, ` : '';

  const description = api.description ? `        /// <summary>${api.description}</summary>\n` : '';
  const paramDocs = api.parameters.map(p =>
    `        /// <param name="${p.name}">${p.description || ''}</param>`
  ).join('\n');
  const paramDocsSection = paramDocs ? paramDocs + '\n' : '';

  if (api.returnType.isVoid) {
    return `${description}${paramDocsSection}        [Preserve]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable ${api.csharpName}(${paramDecls})
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource();
            string callbackId = AITCore.Instance.RegisterVoidCallback(
                () => acs.SetResult(),
                error => acs.SetException(error)
            );
            __Firebase_${api.name}_Internal(${jslibCallArgs}callbackId, "void");
            await acs.Awaitable;
#else
            Debug.Log($"[AITFirebase Mock] ${api.csharpName} called");
            await Awaitable.NextFrameAsync();
#endif
        }
#else
        public static async Task ${api.csharpName}(${paramDecls})
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            string callbackId = AITCore.Instance.RegisterVoidCallback(
                () => tcs.TrySetResult(null),
                error => tcs.TrySetException(error)
            );
            __Firebase_${api.name}_Internal(${jslibCallArgs}callbackId, "void");
            await tcs.Task;
#else
            Debug.Log($"[AITFirebase Mock] ${api.csharpName} called");
            await Task.CompletedTask;
#endif
        }
#endif`;
  }

  return `${description}${paramDocsSection}        [Preserve]
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<${returnType}> ${api.csharpName}(${paramDecls})
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var acs = new AwaitableCompletionSource<${returnType}>();
            string callbackId = AITCore.Instance.RegisterCallback<${returnType}>(
                result => acs.SetResult(result),
                error => acs.SetException(error)
            );
            __Firebase_${api.name}_Internal(${jslibCallArgs}callbackId, "${returnType}");
            return await acs.Awaitable;
#else
            Debug.Log($"[AITFirebase Mock] ${api.csharpName} called");
            await Awaitable.NextFrameAsync();
            return default(${returnType});
#endif
        }
#else
        public static async Task<${returnType}> ${api.csharpName}(${paramDecls})
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<${returnType}>();
            string callbackId = AITCore.Instance.RegisterCallback<${returnType}>(
                result => tcs.TrySetResult(result),
                error => tcs.TrySetException(error)
            );
            __Firebase_${api.name}_Internal(${jslibCallArgs}callbackId, "${returnType}");
            return await tcs.Task;
#else
            Debug.Log($"[AITFirebase Mock] ${api.csharpName} called");
            await Task.CompletedTask;
            return default(${returnType});
#endif
        }
#endif`;
}

/**
 * 동기 API 메서드 생성 (void 반환 — 콜백 불필요)
 */
function generateSyncMethod(api: FirebaseAPIDefinition): string {
  const paramDecls = api.parameters.map(p => {
    const defaultVal = p.optional ? ' = null' : '';
    return `${p.csharpType} ${p.name}${defaultVal}`;
  }).join(', ');

  const jslibArgs = api.parameters.map(p => p.name).join(', ');

  const description = api.description ? `        /// <summary>${api.description}</summary>\n` : '';
  const paramDocs = api.parameters.map(p =>
    `        /// <param name="${p.name}">${p.description || ''}</param>`
  ).join('\n');
  const paramDocsSection = paramDocs ? paramDocs + '\n' : '';

  return `${description}${paramDocsSection}        [Preserve]
        public static void ${api.csharpName}(${paramDecls})
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            __Firebase_${api.name}_Internal(${jslibArgs});
#else
            Debug.Log($"[AITFirebase Mock] ${api.csharpName} called");
#endif
        }`;
}

/**
 * 구독 패턴 API 메서드 생성 (onAuthStateChanged 등)
 */
function generateSubscriptionMethod(api: FirebaseAPIDefinition): string {
  const returnType = api.returnType.csharpType;
  const description = api.description ? `        /// <summary>${api.description}</summary>\n` : '';

  return `${description}        /// <param name="onEvent">인증 상태 변경 시 호출되는 콜백</param>
        /// <param name="onError">에러 발생 시 호출되는 콜백</param>
        /// <returns>구독 해제를 위한 Action. 호출 시 구독이 취소됩니다.</returns>
        [Preserve]
        public static Action ${api.csharpName}(Action<${returnType}> onEvent, Action<AITException> onError = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string subscriptionId = AITCore.Instance.RegisterSubscriptionCallback<${returnType}>(onEvent, onError);
            __Firebase_${api.name}_Internal(subscriptionId, "${returnType}");
            return () => AITCore.Instance.Unsubscribe(subscriptionId);
#else
            Debug.Log($"[AITFirebase Mock] ${api.csharpName} subscribed");
            return () => Debug.Log($"[AITFirebase Mock] ${api.csharpName} unsubscribed");
#endif
        }`;
}

/**
 * DllImport 선언 생성 (비동기/동기 API)
 */
function generateDllImport(api: FirebaseAPIDefinition): string {
  if (!api.isAsync && api.returnType.isVoid) {
    // 동기 void 메서드 — 콜백 불필요
    const params = api.parameters.map(p => `${getDllImportType(p.csharpType)} ${p.name}`).join(', ');
    return `        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_${api.name}_Internal(${params});`;
  }

  // 비동기 메서드 — callbackId + typeName 추가
  const params = api.parameters.map(p => `${getDllImportType(p.csharpType)} ${p.name}`).join(', ');
  const allParams = params ? `${params}, string callbackId, string typeName` : 'string callbackId, string typeName';
  return `        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_${api.name}_Internal(${allParams});`;
}

/**
 * DllImport 선언 생성 (구독 API)
 */
function generateSubscriptionDllImport(api: FirebaseAPIDefinition): string {
  return `        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __Firebase_${api.name}_Internal(string subscriptionId, string typeName);`;
}

/**
 * C# 타입을 DllImport 가능한 타입으로 변환
 */
function getDllImportType(csharpType: string): string {
  switch (csharpType) {
    case 'string': return 'string';
    case 'int': return 'int';
    case 'float': return 'float';
    case 'double': return 'double';
    case 'bool': return 'bool';
    default: return 'string'; // 복합 타입은 JSON 문자열로 전달
  }
}
