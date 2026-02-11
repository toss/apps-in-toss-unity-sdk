/**
 * Firebase jslib 생성기
 *
 * Firebase API 정의에서 Unity WebGL jslib 브릿지 파일들을 생성합니다.
 * 카테고리별로 분리된 jslib 파일을 생성합니다.
 */

import type { FirebaseAPIDefinition } from '../../parser/firebase-parser.js';

/**
 * Firebase jslib 파일들 생성
 * @returns Map<파일명, 내용>
 */
export function generateFirebaseJSLib(apis: FirebaseAPIDefinition[]): Map<string, string> {
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
    const shortCategory = category.replace('Firebase.', '');
    const fileName = `Firebase-${shortCategory}.jslib`;
    const content = generateJslibFile(fileName, shortCategory, categoryApis);
    files.set(fileName, content);
  }

  return files;
}

/**
 * 카테고리별 jslib 파일 생성
 */
function generateJslibFile(
  fileName: string,
  category: string,
  apis: FirebaseAPIDefinition[]
): string {
  const functions = apis.map(api => generateJslibFunction(api));

  // 구독 해제 함수 추가 (구독 API가 있는 경우)
  const hasSubscriptions = apis.some(api => api.isSubscription);
  if (hasSubscriptions) {
    functions.push(generateUnsubscribeFunction());
  }

  return `/**
 * ${fileName}
 *
 * Firebase ${category} bridge for Unity WebGL
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
${functions.join('\n\n')}

});
`;
}

/**
 * 개별 API의 jslib 함수 생성
 */
function generateJslibFunction(api: FirebaseAPIDefinition): string {
  if (api.isSubscription) {
    return generateSubscriptionFunction(api);
  }
  if (api.isAsync) {
    return generateAsyncFunction(api);
  }
  return generateSyncFunction(api);
}

/**
 * 비동기 API jslib 함수 생성
 */
function generateAsyncFunction(api: FirebaseAPIDefinition): string {
  const paramConversions = api.parameters.map(p => {
    if (p.needsSerialization) {
      return `        var ${p.name}Val = ${p.name} ? JSON.parse(UTF8ToString(${p.name})) : null;`;
    }
    if (p.csharpType === 'string') {
      return `        var ${p.name}Val = UTF8ToString(${p.name});`;
    }
    if (p.csharpType === 'bool') {
      return `        var ${p.name}Val = ${p.name} !== 0;`;
    }
    return `        var ${p.name}Val = ${p.name};`;
  }).join('\n');

  const paramList = [...api.parameters.map(p => p.name), 'callbackId', 'typeName'].join(', ');
  const apiCallArgs = api.parameters.map(p => `${p.name}Val`).join(', ');

  return `    __Firebase_${api.name}_Internal: function(${paramList}) {
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);
${paramConversions ? '\n' + paramConversions + '\n' : ''}
        console.log('[AIT Firebase] ${api.name} called, callbackId:', callback);

        try {
            var promise = window.__AIT_Firebase.${api.name}(${apiCallArgs});

            if (!promise || typeof promise.then !== 'function') {
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promise), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promise
                .then(function(result) {
                    console.log('[AIT Firebase] ${api.name} resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT Firebase] ${api.name} rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.error('[AIT Firebase] ${api.name} error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        }
    },`;
}

/**
 * 동기 API jslib 함수 생성 (void 반환, 콜백 없음)
 */
function generateSyncFunction(api: FirebaseAPIDefinition): string {
  const paramConversions = api.parameters.map(p => {
    if (p.needsSerialization) {
      return `        var ${p.name}Val = ${p.name} ? JSON.parse(UTF8ToString(${p.name})) : null;`;
    }
    if (p.csharpType === 'string') {
      return `        var ${p.name}Val = UTF8ToString(${p.name});`;
    }
    if (p.csharpType === 'bool') {
      return `        var ${p.name}Val = ${p.name} !== 0;`;
    }
    return `        var ${p.name}Val = ${p.name};`;
  }).join('\n');

  const paramList = api.parameters.map(p => p.name).join(', ');
  const apiCallArgs = api.parameters.map(p => `${p.name}Val`).join(', ');

  return `    __Firebase_${api.name}_Internal: function(${paramList}) {
${paramConversions ? paramConversions + '\n' : ''}
        console.log('[AIT Firebase] ${api.name} called');

        try {
            window.__AIT_Firebase.${api.name}(${apiCallArgs});
        } catch (error) {
            console.error('[AIT Firebase] ${api.name} error:', error);
        }
    },`;
}

/**
 * 구독 API jslib 함수 생성
 */
function generateSubscriptionFunction(api: FirebaseAPIDefinition): string {
  return `    __Firebase_${api.name}_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT Firebase] ${api.name} subscribing, id:', subId);

        try {
            var unsubscribe = window.__AIT_Firebase.${api.name}(function(data) {
                console.log('[AIT Firebase] ${api.name} event:', data);
                var payload = JSON.stringify({
                    CallbackId: subId,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({
                        success: true,
                        data: JSON.stringify(data || {}),
                        error: ''
                    })
                });
                SendMessage('AITCore', 'OnAITEventCallback', payload);
            });

            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT Firebase] ${api.name} error:', error);
            var payload = JSON.stringify({
                CallbackId: subId,
                TypeName: typeNameStr,
                Result: JSON.stringify({
                    success: false,
                    data: '',
                    error: error.message || String(error)
                })
            });
            SendMessage('AITCore', 'OnAITEventCallback', payload);
        }
    },`;
}

/**
 * 구독 해제 함수 (기존 AIT 패턴 재사용)
 */
function generateUnsubscribeFunction(): string {
  return `    __Firebase_Unsubscribe_Internal: function(subscriptionId) {
        var subId = UTF8ToString(subscriptionId);

        if (window.__AIT_SUBSCRIPTIONS && window.__AIT_SUBSCRIPTIONS[subId]) {
            console.log('[AIT Firebase] Unsubscribing:', subId);
            var unsubscribe = window.__AIT_SUBSCRIPTIONS[subId];
            if (typeof unsubscribe === 'function') {
                unsubscribe();
            }
            delete window.__AIT_SUBSCRIPTIONS[subId];
        }
    },`;
}
