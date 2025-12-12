/**
 * TypeScript 기반 jslib 생성기
 *
 * web-framework의 TypeScript 타입을 활용하여
 * 타입 안전한 jslib 브릿지 코드를 생성합니다.
 */

import { ParsedAPI, ParsedType } from '../types.js';

/**
 * jslib 생성 결과
 */
export interface JSLibGenerationResult {
  /** 카테고리별 TypeScript 소스 코드 */
  typescriptFiles: Map<string, string>;
  /** 카테고리별 최종 jslib 코드 */
  jslibFiles: Map<string, string>;
}

/**
 * TypeScript 기반 jslib 생성기
 */
export class JSLibGenerator {
  /**
   * API 목록에서 jslib 파일들 생성
   */
  async generate(
    apis: ParsedAPI[],
    webFrameworkTag: string
  ): Promise<Map<string, string>> {
    // 카테고리별로 API 그룹화
    const apisByCategory = new Map<string, ParsedAPI[]>();
    for (const api of apis) {
      const category = api.category;
      if (!apisByCategory.has(category)) {
        apisByCategory.set(category, []);
      }
      apisByCategory.get(category)!.push(api);
    }

    // 카테고리별 jslib 파일 생성
    const jslibFiles = new Map<string, string>();

    for (const [category, categoryAPIs] of apisByCategory.entries()) {
      const functions = categoryAPIs.map(api => this.generateFunction(api));

      // 이벤트 API가 있는 카테고리에는 구독 해제 함수 추가
      const hasEventApis = categoryAPIs.some(api => api.isEventSubscription);
      if (hasEventApis) {
        functions.push(this.generateUnsubscribeFunction());
      }

      const fileName = `AppsInToss-${category}.jslib`;
      const fileContent = this.generateFileContent(fileName, webFrameworkTag, category, functions);
      jslibFiles.set(fileName, fileContent);
    }

    return jslibFiles;
  }

  /**
   * TypeScript 소스와 jslib 모두 생성 (타입 검사용)
   */
  async generateWithTypescript(
    apis: ParsedAPI[],
    webFrameworkTag: string
  ): Promise<JSLibGenerationResult> {
    // 카테고리별로 API 그룹화
    const apisByCategory = new Map<string, ParsedAPI[]>();
    for (const api of apis) {
      const category = api.category;
      if (!apisByCategory.has(category)) {
        apisByCategory.set(category, []);
      }
      apisByCategory.get(category)!.push(api);
    }

    const typescriptFiles = new Map<string, string>();
    const jslibFiles = new Map<string, string>();

    for (const [category, categoryAPIs] of apisByCategory.entries()) {
      // TypeScript 소스 생성
      const tsContent = this.generateTypescriptFile(category, categoryAPIs);
      typescriptFiles.set(`bridge-${category}.ts`, tsContent);

      // jslib 생성
      const functions = categoryAPIs.map(api => this.generateFunction(api));
      const hasEventApis = categoryAPIs.some(api => api.isEventSubscription);
      if (hasEventApis) {
        functions.push(this.generateUnsubscribeFunction());
      }

      const fileName = `AppsInToss-${category}.jslib`;
      const fileContent = this.generateFileContent(fileName, webFrameworkTag, category, functions);
      jslibFiles.set(fileName, fileContent);
    }

    return { typescriptFiles, jslibFiles };
  }

  /**
   * TypeScript 파일 내용 생성 (타입 검사용)
   */
  private generateTypescriptFile(category: string, apis: ParsedAPI[]): string {
    const imports = this.generateApiImports(apis);
    const windowType = this.generateWindowType(apis);
    const functions = apis.map(api => this.generateTypescriptFunction(api)).join('\n\n');

    return `/**
 * bridge-${category}.ts
 *
 * This file is auto-generated for type checking.
 * 이 파일은 타입 검사를 위해 자동 생성되었습니다.
 */

// Unity WebGL jslib 런타임 글로벌 함수 선언
declare function UTF8ToString(ptr: number): string;
declare function SendMessage(objectName: string, methodName: string, param: string): void;

// web-framework API 타입 import
${imports}

// window.AppsInToss 타입 선언
declare const window: {
  AppsInToss: {
${windowType}
  };
  __AIT_SUBSCRIPTIONS?: Record<string, () => void>;
};

${functions}
`;
  }

  /**
   * API별 import 문 생성
   */
  private generateApiImports(apis: ParsedAPI[]): string {
    const imports: string[] = [];
    const seenNamespaces = new Set<string>();

    for (const api of apis) {
      if (api.namespace) {
        // 네임스페이스 API (Storage, IAP 등)
        if (!seenNamespaces.has(api.namespace)) {
          imports.push(`import type { ${api.namespace} } from '@apps-in-toss/web-framework';`);
          seenNamespaces.add(api.namespace);
        }
      } else if (!api.isEventSubscription) {
        // 일반 API 함수
        imports.push(`import type { ${api.originalName} } from '@apps-in-toss/web-framework';`);
      }
    }

    // 이벤트 API의 네임스페이스들
    for (const api of apis) {
      if (api.isEventSubscription && api.namespace && !seenNamespaces.has(api.namespace)) {
        // 이벤트 네임스페이스는 별도 처리 필요할 수 있음
        seenNamespaces.add(api.namespace);
      }
    }

    return imports.join('\n');
  }

  /**
   * window.AppsInToss 타입 생성
   */
  private generateWindowType(apis: ParsedAPI[]): string {
    const members: string[] = [];
    const seenNamespaces = new Set<string>();

    for (const api of apis) {
      if (api.namespace) {
        // 네임스페이스 API
        if (!seenNamespaces.has(api.namespace)) {
          members.push(`    ${api.namespace}: typeof ${api.namespace};`);
          seenNamespaces.add(api.namespace);
        }
      } else if (!api.isEventSubscription) {
        // 일반 API 함수
        members.push(`    ${api.originalName}: typeof ${api.originalName};`);
      }
    }

    return members.join('\n');
  }

  /**
   * TypeScript import 문 생성
   */
  private generateTypescriptImports(apis: ParsedAPI[]): string {
    // web-framework에서 필요한 타입들 추출
    const typeImports = new Set<string>();

    for (const api of apis) {
      this.collectTypesFromParsedType(api.returnType, typeImports);
      for (const param of api.parameters) {
        this.collectTypesFromParsedType(param.type, typeImports);
      }
    }

    if (typeImports.size === 0) {
      return `import type WebFramework from '@apps-in-toss/web-framework';`;
    }

    const sortedTypes = Array.from(typeImports).sort();
    return `import type WebFramework from '@apps-in-toss/web-framework';
import type { ${sortedTypes.join(', ')} } from '@apps-in-toss/web-framework';`;
  }

  /**
   * ParsedType에서 import 필요한 타입 수집
   */
  private collectTypesFromParsedType(type: ParsedType, types: Set<string>): void {
    if (type.kind === 'object' && type.name !== 'object' && !type.name.startsWith('{')) {
      // 커스텀 타입은 import 필요
      types.add(type.name);
    }
    if (type.promiseType) {
      this.collectTypesFromParsedType(type.promiseType, types);
    }
    if (type.elementType) {
      this.collectTypesFromParsedType(type.elementType, types);
    }
    if (type.unionTypes) {
      for (const unionType of type.unionTypes) {
        this.collectTypesFromParsedType(unionType, types);
      }
    }
    if (type.successType) {
      this.collectTypesFromParsedType(type.successType, types);
    }
  }

  /**
   * TypeScript 함수 생성 (타입 포함)
   */
  private generateTypescriptFunction(api: ParsedAPI): string {
    if (api.isEventSubscription) {
      return this.generateTypescriptEventFunction(api);
    }
    return this.generateTypescriptRegularFunction(api);
  }

  /**
   * 일반 API TypeScript 함수 생성
   */
  private generateTypescriptRegularFunction(api: ParsedAPI): string {
    // void 타입 파라미터 필터링 (web-framework의 (args_0: void) 패턴 처리)
    const effectiveParams = api.parameters.filter(p =>
      !(p.type.kind === 'primitive' && p.type.name === 'void')
    );

    // 파라미터는 Unity에서 전달될 때 모두 number (포인터)로 전달됨
    const params = effectiveParams.map(p => `${p.name}: number`).join(', ');
    const paramList = effectiveParams.length > 0
      ? `${params}, callbackId: number, typeName: number`
      : 'callbackId: number, typeName: number';

    // API 호출 표현식 (Parameters<typeof xxx> 타입 추출에 사용)
    const apiCallExpr = api.namespace
      ? `window.AppsInToss.${api.namespace}.${api.originalName}`
      : `window.AppsInToss.${api.originalName}`;

    const jsConversions = effectiveParams.map((p, index) =>
      `  const ${p.name}Val = ${this.getJSConversionTs(p.name, p.type, index, apiCallExpr)};`
    ).join('\n');

    const apiCallParams = effectiveParams.map(p => `${p.name}Val`).join(', ');
    const apiCall = `${apiCallExpr}(${apiCallParams})`;

    if (api.isAsync) {
      return this.generateAsyncTypescriptFunction(api, paramList, jsConversions, apiCall);
    } else {
      return this.generateSyncTypescriptFunction(api, paramList, jsConversions, apiCall);
    }
  }

  /**
   * 비동기 TypeScript 함수 생성
   */
  private generateAsyncTypescriptFunction(
    api: ParsedAPI,
    paramList: string,
    jsConversions: string,
    apiCall: string
  ): string {
    const isDiscriminatedUnion = api.returnType.kind === 'promise' &&
      api.returnType.promiseType?.isDiscriminatedUnion === true;

    const resultHandling = isDiscriminatedUnion
      ? `        let resultPayload: { _type: string; _errorCode: string; _successJson: string | null };
        if (typeof result === 'string') {
          resultPayload = { _type: 'error', _errorCode: result, _successJson: null };
        } else {
          resultPayload = { _type: 'success', _successJson: JSON.stringify(result), _errorCode: '' };
        }
        const payload = JSON.stringify({
          CallbackId: callback,
          TypeName: typeNameStr,
          Result: JSON.stringify({ success: true, data: JSON.stringify(resultPayload), error: '' })
        });`
      : `        const payload = JSON.stringify({
          CallbackId: callback,
          TypeName: typeNameStr,
          Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
        });`;

    return `export const __${api.name}_Internal = (${paramList}): void => {
  const callback = UTF8ToString(callbackId);
  const typeNameStr = UTF8ToString(typeName);

  console.log('[AIT jslib] ${api.name} called, callbackId:', callback);
${jsConversions ? '\n' + jsConversions + '\n' : ''}
  try {
    const promiseResult = ${apiCall};

    if (!promiseResult || typeof promiseResult.then !== 'function') {
      console.log('[AIT jslib] ${api.originalName} did not return a Promise, sending immediate response');
      const payload = JSON.stringify({
        CallbackId: callback,
        TypeName: typeNameStr,
        Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
      });
      SendMessage('AITCore', 'OnAITCallback', payload);
      return;
    }

    promiseResult
      .then((result: unknown) => {
        console.log('[AIT jslib] ${api.originalName} resolved:', result);
${resultHandling}
        SendMessage('AITCore', 'OnAITCallback', payload);
      })
      .catch((error: Error) => {
        console.log('[AIT jslib] ${api.originalName} rejected:', error);
        const payload = JSON.stringify({
          CallbackId: callback,
          TypeName: typeNameStr,
          Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
        });
        SendMessage('AITCore', 'OnAITCallback', payload);
      });
  } catch (error: unknown) {
    console.log('[AIT jslib] ${api.originalName} sync error:', error);
    const errorMessage = error instanceof Error ? error.message : String(error);
    const payload = JSON.stringify({
      CallbackId: callback,
      TypeName: typeNameStr,
      Result: JSON.stringify({ success: false, data: '', error: errorMessage })
    });
    SendMessage('AITCore', 'OnAITCallback', payload);
  }
};`;
  }

  /**
   * 동기 TypeScript 함수 생성
   */
  private generateSyncTypescriptFunction(
    api: ParsedAPI,
    paramList: string,
    jsConversions: string,
    apiCall: string
  ): string {
    return `export const __${api.name}_Internal = (${paramList}): void => {
  const callback = UTF8ToString(callbackId);
  const typeNameStr = UTF8ToString(typeName);
${jsConversions ? '\n' + jsConversions + '\n' : ''}
  try {
    const result = ${apiCall};
    const payload = JSON.stringify({
      CallbackId: callback,
      TypeName: typeNameStr,
      Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
    });
    SendMessage('AITCore', 'OnAITCallback', payload);
  } catch (error: unknown) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    const payload = JSON.stringify({
      CallbackId: callback,
      TypeName: typeNameStr,
      Result: JSON.stringify({ success: false, data: '', error: errorMessage })
    });
    SendMessage('AITCore', 'OnAITCallback', payload);
  }
};`;
  }

  /**
   * 이벤트 구독 TypeScript 함수 생성
   */
  private generateTypescriptEventFunction(api: ParsedAPI): string {
    const hasEventData = api.eventDataType &&
      api.eventDataType.name !== 'void' &&
      api.eventDataType.name !== 'undefined';

    const onEventHandler = hasEventData
      ? `        onEvent: (data: unknown) => {
          console.log('[AIT jslib] ${api.eventName} fired:', data);
          const payload = JSON.stringify({
            CallbackId: subId,
            TypeName: typeNameStr,
            Result: JSON.stringify({
              success: true,
              data: JSON.stringify(data || {}),
              error: ''
            })
          });
          SendMessage('AITCore', 'OnAITEventCallback', payload);
        },`
      : `        onEvent: () => {
          console.log('[AIT jslib] ${api.eventName} fired (void)');
          const payload = JSON.stringify({
            CallbackId: subId,
            TypeName: typeNameStr,
            Result: JSON.stringify({
              success: true,
              data: null,
              error: ''
            })
          });
          SendMessage('AITCore', 'OnAITEventCallback', payload);
        },`;

    return `export const __${api.name}_Internal = (subscriptionId: number, typeName: number): void => {
  const subId = UTF8ToString(subscriptionId);
  const typeNameStr = UTF8ToString(typeName);

  console.log('[AIT jslib] ${api.name} subscribing, id:', subId);

  try {
    const unsubscribe = window.AppsInToss.${api.namespace}.addEventListener('${api.eventName}', {
${onEventHandler}
        onError: (error: Error) => {
          console.log('[AIT jslib] ${api.eventName} error:', error);
          const payload = JSON.stringify({
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
      });

    if (!window.__AIT_SUBSCRIPTIONS) {
      window.__AIT_SUBSCRIPTIONS = {};
    }
    window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

  } catch (error: unknown) {
    console.error('[AIT jslib] ${api.name} subscribe error:', error);
    const errorMessage = error instanceof Error ? error.message : String(error);
    const payload = JSON.stringify({
      CallbackId: subId,
      TypeName: typeNameStr,
      Result: JSON.stringify({
        success: false,
        data: '',
        error: errorMessage
      })
    });
    SendMessage('AITCore', 'OnAITEventCallback', payload);
  }
};`;
  }

  /**
   * TypeScript 타입 변환 코드 생성
   * @param paramIndex - 파라미터 인덱스 (Parameters<typeof func>[index] 타입 추출용)
   * @param apiCall - API 호출 표현식 (window.AppsInToss.xxx 또는 window.AppsInToss.Namespace.xxx)
   */
  private getJSConversionTs(paramName: string, paramType: ParsedType, paramIndex: number, apiCall: string): string {
    // 함수 시그니처에서 직접 타입을 추출 (Parameters<typeof xxx>[index])
    const paramTypeExpr = `Parameters<typeof ${apiCall}>[${paramIndex}]`;

    switch (paramType.kind) {
      case 'primitive':
        if (paramType.name === 'string') {
          return `UTF8ToString(${paramName})`;
        }
        return paramName;

      case 'object':
      case 'array':
        return `JSON.parse(UTF8ToString(${paramName})) as ${paramTypeExpr}`;

      default:
        // function, union 등 복잡한 타입도 Parameters로 추출
        if (paramType.kind === 'function' || paramType.kind === 'union' || paramType.kind === 'record') {
          return `JSON.parse(UTF8ToString(${paramName})) as ${paramTypeExpr}`;
        }
        return paramName;
    }
  }

  /**
   * TypeScript 타입명 가져오기
   */
  private getTsTypeName(type: ParsedType): string {
    switch (type.kind) {
      case 'primitive':
        return type.name;
      case 'array':
        return type.elementType ? `${this.getTsTypeName(type.elementType)}[]` : 'unknown[]';
      case 'object':
        return type.name === 'object' ? 'Record<string, unknown>' : type.name;
      default:
        return 'unknown';
    }
  }

  // ============================================================================
  // jslib 생성 (기존 방식 유지, JavaScript로 출력)
  // ============================================================================

  /**
   * 단일 API 함수 생성 (JavaScript)
   */
  private generateFunction(api: ParsedAPI): string {
    if (api.isEventSubscription) {
      return this.generateEventFunction(api);
    }
    return this.generateRegularFunction(api);
  }

  /**
   * 일반 API 함수 생성 (JavaScript)
   */
  private generateRegularFunction(api: ParsedAPI): string {
    // void 타입 파라미터 필터링
    const effectiveParams = api.parameters.filter(p =>
      !(p.type.kind === 'primitive' && p.type.name === 'void')
    );

    const params = effectiveParams.map(p => p.name);
    const paramList = [...params, 'callbackId', 'typeName'].join(', ');

    // 파라미터 로깅
    const paramLogs = effectiveParams.map(p =>
      `        console.log('[AIT jslib] ${api.name} raw param ${p.name}:', UTF8ToString(${p.name}));`
    ).join('\n');

    // 파라미터 변환을 인라인으로 처리 (중간 변수 없이)
    const apiCallParams = effectiveParams.map(p => this.getJSConversion(p.name, p.type)).join(', ');
    const apiCall = api.namespace
      ? `window.AppsInToss.${api.namespace}.${api.originalName}(${apiCallParams})`
      : `window.AppsInToss.${api.originalName}(${apiCallParams})`;

    if (api.isAsync) {
      return this.generateAsyncFunction(api, paramList, paramLogs, apiCall);
    } else {
      return this.generateSyncFunction(api, paramList, apiCall);
    }
  }

  /**
   * 비동기 함수 생성 (JavaScript)
   */
  private generateAsyncFunction(
    api: ParsedAPI,
    paramList: string,
    paramLogs: string,
    apiCall: string
  ): string {
    const isDiscriminatedUnion = api.returnType.kind === 'promise' &&
      api.returnType.promiseType?.isDiscriminatedUnion === true;

    const resultHandling = isDiscriminatedUnion
      ? `                    var resultPayload;
                    if (typeof result === 'string') {
                        resultPayload = { _type: "error", _errorCode: result, _successJson: null };
                    } else {
                        resultPayload = { _type: "success", _successJson: JSON.stringify(result), _errorCode: "" };
                    }
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(resultPayload), error: '' })
                    });`
      : `                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });`;

    return `    __${api.name}_Internal: function(${paramList}) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] ${api.name} called, callbackId:', callback);
${paramLogs ? paramLogs + '\n' : ''}
        try {
            var promiseResult = ${apiCall};
            console.log('[AIT jslib] ${api.originalName} returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                console.log('[AIT jslib] ${api.originalName} did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    console.log('[AIT jslib] ${api.originalName} resolved:', result);
${resultHandling}
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    console.log('[AIT jslib] ${api.originalName} rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                });
        } catch (error) {
            console.log('[AIT jslib] ${api.originalName} sync error:', error);
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
   * 동기 함수 생성 (JavaScript)
   */
  private generateSyncFunction(
    api: ParsedAPI,
    paramList: string,
    apiCall: string
  ): string {
    return `    __${api.name}_Internal: function(${paramList}) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = ${apiCall};
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
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
   * 이벤트 구독 함수 생성 (JavaScript)
   */
  private generateEventFunction(api: ParsedAPI): string {
    const hasEventData = api.eventDataType &&
      api.eventDataType.name !== 'void' &&
      api.eventDataType.name !== 'undefined';

    const onEventHandler = hasEventData
      ? `                onEvent: function(data) {
                    console.log('[AIT jslib] ${api.eventName} fired:', data);
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: JSON.stringify(data || {}),
                            error: ''
                        })
                    });
                    // Event callbacks go to OnAITEventCallback (persistent)
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },`
      : `                onEvent: function() {
                    console.log('[AIT jslib] ${api.eventName} fired (void)');
                    var payload = JSON.stringify({
                        CallbackId: subId,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({
                            success: true,
                            data: null,
                            error: ''
                        })
                    });
                    // Event callbacks go to OnAITEventCallback (persistent)
                    SendMessage('AITCore', 'OnAITEventCallback', payload);
                },`;

    return `    __${api.name}_Internal: function(subscriptionId, typeName) {
        var subId = UTF8ToString(subscriptionId);
        var typeNameStr = UTF8ToString(typeName);

        console.log('[AIT jslib] ${api.name} subscribing, id:', subId);

        try {
            // Subscribe to event
            var unsubscribe = window.AppsInToss.${api.namespace}.addEventListener('${api.eventName}', {
${onEventHandler}
                onError: function(error) {
                    console.log('[AIT jslib] ${api.eventName} error:', error);
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
            });

            // Store unsubscribe function for later cleanup
            if (!window.__AIT_SUBSCRIPTIONS) {
                window.__AIT_SUBSCRIPTIONS = {};
            }
            window.__AIT_SUBSCRIPTIONS[subId] = unsubscribe;

        } catch (error) {
            console.error('[AIT jslib] ${api.name} subscribe error:', error);
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
   * JavaScript 타입 변환 코드 생성
   */
  private getJSConversion(paramName: string, paramType: ParsedType): string {
    switch (paramType.kind) {
      case 'primitive':
        if (paramType.name === 'string') {
          return `UTF8ToString(${paramName})`;
        }
        return paramName;

      case 'object':
      case 'array':
        return `JSON.parse(UTF8ToString(${paramName}))`;

      default:
        return paramName;
    }
  }

  /**
   * 이벤트 구독 해제 함수 생성
   */
  private generateUnsubscribeFunction(): string {
    return `    __AITUnsubscribe_Internal: function(subscriptionId) {
        var subId = UTF8ToString(subscriptionId);

        if (window.__AIT_SUBSCRIPTIONS && window.__AIT_SUBSCRIPTIONS[subId]) {
            console.log('[AIT jslib] Unsubscribing:', subId);
            var unsubscribe = window.__AIT_SUBSCRIPTIONS[subId];
            if (typeof unsubscribe === 'function') {
                unsubscribe();
            }
            delete window.__AIT_SUBSCRIPTIONS[subId];
        } else {
            console.warn('[AIT jslib] Unknown subscription:', subId);
        }
    },`;
  }

  /**
   * jslib 파일 내용 생성
   */
  private generateFileContent(
    fileName: string,
    _webFrameworkTag: string,
    _category: string,
    functions: string[]
  ): string {
    return `/**
 * ${fileName}
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
${functions.join('\n\n')}

});
`;
  }
}
