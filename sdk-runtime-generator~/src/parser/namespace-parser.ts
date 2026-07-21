import { SourceFile, FunctionDeclaration, SyntaxKind } from 'ts-morph';
import { ParsedAPI, ParsedParameter, ParsedType, NestedCallback } from '../types.js';
import { toPascalCase, getNamespaceCategory, getCategoryFromPath } from './utils.js';
import { parseType } from './type-parser.js';
import { recordDomViolation } from './dom-violations.js';
import { extractJsDocForProperty, extractParamDescriptions, extractReturnsDescription, extractExamples } from './jsdoc-extractor.js';
import {
  detectEventNamespaces,
  detectGlobalFunctions,
  detectNamespaceObjects,
} from './detection.js';
import { parseEventNamespace } from './event-parser.js';

/**
 * 콜백 기반 API 패턴 감지
 *
 * 조건:
 * 1. 파라미터가 1개 (args 객체)
 * 2. args 객체에 onEvent, onError 함수 프로퍼티 존재
 * 3. 반환 타입이 () => void (cleanup/dispose 함수)
 *
 * 예: GoogleAdMob.loadAppsInTossAdMob, loadFullScreenAd 등
 */
export function detectCallbackBasedPattern(
  parameters: ParsedParameter[],
  returnType: ParsedType
): boolean {
  // 조건 1: 파라미터가 1개
  if (parameters.length !== 1) return false;

  const argsType = parameters[0].type;

  // 조건 2: args가 object 타입이고 properties가 있어야 함
  if (argsType.kind !== 'object' || !argsType.properties) return false;

  // onEvent와 onError 함수 프로퍼티 존재 확인
  const hasOnEvent = argsType.properties.some(
    (p) => p.name === 'onEvent' && p.type.kind === 'function'
  );
  const hasOnError = argsType.properties.some(
    (p) => p.name === 'onError' && p.type.kind === 'function'
  );

  if (!hasOnEvent || !hasOnError) return false;

  // 조건 3: 반환 타입이 () => void (cleanup 함수)
  // returnType.kind가 'function'이고 반환값이 void인 경우
  const isCleanupReturn =
    returnType.kind === 'function' &&
    (returnType.raw?.includes('() => void') ||
      returnType.functionReturnType?.name === 'void');

  return isCleanupReturn;
}

/**
 * 파라미터에서 중첩 콜백 감지
 *
 * 구조 기반 감지:
 * - 최상위 함수 프로퍼티 (param.onEvent, param.onError): 이벤트 콜백 시스템 사용
 * - object 타입 프로퍼티 내부의 함수 (param.options.processProductGrant): 중첩 콜백
 *
 * 중요: 중첩 콜백 렌더링(RegisterNestedCallback + `options.<콜백>` 접근 +
 * `<래퍼타입명>Options` 파라미터 타입 유도)이 지원하는 형태는
 * "단일 파라미터 + 파라미터 타입에 'options' 프로퍼티 + 콜백이 options 직속"뿐이다.
 * (예: IAP.createOneTimePurchaseOrder({ options: { ..., processProductGrant } }))
 * 그 외 형태 — 다중 파라미터(tossAds.attach(adGroupId, target, options?)),
 * 'callbacks' 등 다른 프로퍼티 아래의 콜백(tossAds.initialize) — 를 감지하면
 * 존재하지 않는 타입(stringOptions, InitializeOptionsOptions 등)을 참조하고
 * 나머지 파라미터를 유실하는 C#이 생성되므로(CS0246), 감지 대상에서 제외한다.
 * 제외된 API는 일반(async) 경로로 생성된다.
 */
export function detectNestedCallbacks(parameters: ParsedParameter[]): NestedCallback[] {
  // 지원 형태 가드: 단일 파라미터 + object 타입 + 'options' 프로퍼티 보유
  if (parameters.length !== 1) return [];
  const wrapperType = parameters[0].type;
  if (wrapperType.kind !== 'object' || !wrapperType.properties?.some(p => p.name === 'options')) {
    return [];
  }
  const nestedCallbacks: NestedCallback[] = [];
  const seenCallbacks = new Set<string>(); // 중복 방지

  // options 프로퍼티 타입의 "직속" 함수 프로퍼티만 수집한다.
  // 템플릿은 `options.<콜백>` 접근만 렌더하므로 더 깊은 중첩(options.a.b)은 지원 불가.
  const collectDirectCallbacks = (type: ParsedType, path: string[]): void => {
    // object 타입: 직속 함수 프로퍼티 수집
    if (type.kind === 'object' && type.properties) {
      for (const prop of type.properties) {
        if (prop.type.kind === 'function') {
          const callbackKey = [...path, prop.name].join('.');
          if (!seenCallbacks.has(callbackKey)) {
            seenCallbacks.add(callbackKey);
            nestedCallbacks.push({
              name: prop.name,
              path: [...path, prop.name],
              parameterType: prop.type.functionParams?.[0],
              returnType: prop.type.functionReturnType,
            });
          }
        }
      }
    }

    // union 타입: 각 멤버 확인 (인터섹션 타입이 union으로 파싱된 경우 포함 —
    // 예: Sku & { processProductGrant } 형태) — 같은 레벨로 취급
    if (type.kind === 'union' && type.unionTypes) {
      for (const unionMember of type.unionTypes) {
        collectDirectCallbacks(unionMember, path);
      }
    }
  };

  const optionsProp = wrapperType.properties.find(p => p.name === 'options');
  if (optionsProp && (optionsProp.type.kind === 'object' || optionsProp.type.kind === 'union')) {
    collectDirectCallbacks(optionsProp.type, ['options']);
  }

  return nestedCallbacks;
}

/**
 * 네임스페이스 객체의 메서드들을 파싱
 * 예: IAP.getProductItemList, Storage.getItem 등
 */
export function parseNamespaceObject(
  namespaceName: string,
  varDecl: any,
  sourceFile: SourceFile
): ParsedAPI[] {
  const apis: ParsedAPI[] = [];
  const type = varDecl.getType();
  const properties = type.getProperties?.() || [];

  // JSDoc에서 각 프로퍼티의 설명 추출을 위해 원본 텍스트도 확인
  const varDeclText = varDecl.getText?.() || '';

  for (const prop of properties) {
    const methodName = prop.getName();
    const propType = prop.getTypeAtLocation?.(varDecl) || prop.getDeclaredType?.();

    if (!propType) continue;

    // 메서드인지 확인 (함수 타입)
    const callSignatures = propType.getCallSignatures?.() || [];
    if (callSignatures.length === 0) continue;

    const signature = callSignatures[0];

    // JSDoc에서 deprecated 확인
    const jsDocComment = extractJsDocForProperty(varDeclText, methodName);
    const isDeprecated = jsDocComment.includes('@deprecated');
    const deprecatedMatch = jsDocComment.match(/@deprecated\s+(.+?)(?=\n\s*\*\s*@|\n\s*\*\/|$)/s);
    const deprecatedMessage = deprecatedMatch ? deprecatedMatch[1].trim() : undefined;

    // 설명 추출
    const descriptionMatch = jsDocComment.match(/@description\s+(.+?)(?=\n\s*\*\s*@|\n\s*\*\/|$)/s);
    const description = descriptionMatch ? descriptionMatch[1].trim() : undefined;

    // 파라미터 파싱
    const parameters = signature.getParameters?.().map((param: any, index: number) => {
      let paramName = param.getName();
      if (paramName.includes('{') || paramName.includes('}') || paramName.includes(',')) {
        paramName = `options${index > 0 ? index : ''}`;
      }
      const valueDecl = param.getValueDeclaration?.();
      const paramType = valueDecl?.getType?.() || param.getDeclaredType?.();
      // optional 확인: isOptional() 또는 hasQuestionToken() (typeof 참조 시 isOptional이 false 반환할 수 있음)
      const isOptional = param.isOptional?.() || valueDecl?.hasQuestionToken?.() || false;
      return {
        name: paramName,
        type: paramType ? parseType(paramType) : { name: 'any', kind: 'primitive' as const, raw: 'any' },
        optional: isOptional,
        description: undefined,
      };
    }) || [];

    const returnType = parseType(signature.getReturnType());

    // 네임스페이스를 카테고리로 사용
    const category = getNamespaceCategory(namespaceName);

    // C# 메서드 이름: 네임스페이스 + PascalCase 메서드명
    // 예: IAP.getProductItemList -> IAPGetProductItemList
    const pascalMethodName = toPascalCase(methodName);
    const fullName = `${namespaceName}${pascalMethodName}`;

    if (checkAndRecordDomViolationsNs(fullName, parameters, returnType, sourceFile, category)) {
      continue;
    }

    const isAsync = returnType.kind === 'promise';

    // 중첩 콜백 감지 (options 파라미터 내부의 함수 타입)
    const nestedCallbacks = detectNestedCallbacks(parameters);

    // 콜백 기반 API 감지 (onEvent/onError 패턴)
    const isCallbackBased = detectCallbackBasedPattern(parameters, returnType);

    apis.push({
      name: fullName,
      pascalName: toPascalCase(fullName),
      originalName: methodName,
      category,
      file: sourceFile.getFilePath(),
      description,
      parameters,
      returnType,
      isAsync: isCallbackBased ? false : isAsync, // 콜백 기반 API는 동기로 처리
      hasPermission: false,
      namespace: namespaceName,
      isDeprecated,
      deprecatedMessage,
      nestedCallbacks: nestedCallbacks.length > 0 ? nestedCallbacks : undefined,
      isCallbackBased,
    });
  }

  return apis;
}

/**
 * index.d.ts에서 네임스페이스 객체, 이벤트 네임스페이스, 글로벌 함수 파싱
 * 모든 항목을 동적으로 감지하여 파싱
 */
export function parseNamespaceObjects(sourceFile: SourceFile): ParsedAPI[] {
  const apis: ParsedAPI[] = [];
  const exportedDeclarations = sourceFile.getExportedDeclarations();

  // 1. 이벤트 네임스페이스 감지 (addEventListener 패턴)
  const eventNamespaces = detectEventNamespaces(sourceFile);

  // 2. 글로벌 함수 감지 (FunctionDeclaration)
  const globalFunctions = detectGlobalFunctions(sourceFile);

  // 3. 네임스페이스 객체 감지 (나머지 중 메서드만 있는 객체)
  const namespaceObjects = detectNamespaceObjects(sourceFile, eventNamespaces, globalFunctions);

  for (const [name, declarations] of exportedDeclarations) {
    // 네임스페이스 객체 파싱 (동적 감지됨)
    if (namespaceObjects.has(name)) {
      for (const declaration of declarations) {
        if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
          const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
          if (varDecl) {
            const namespaceAPIs = parseNamespaceObject(name, varDecl, sourceFile);
            apis.push(...namespaceAPIs);
          }
        }
      }
    }

    // 이벤트 네임스페이스 파싱 (동적 감지됨)
    if (eventNamespaces.has(name)) {
      const eventAPIs = parseEventNamespace(name, sourceFile);
      apis.push(...eventAPIs);
    }

    // 글로벌 함수 파싱 (동적 감지됨)
    if (globalFunctions.has(name)) {
      for (const declaration of declarations) {
        if (declaration.getKind() === SyntaxKind.FunctionDeclaration) {
          const func = declaration as FunctionDeclaration;
          const api = parseFunctionDeclarationForNamespace(func, sourceFile);
          if (api) {
            // 글로벌 함수는 Environment 카테고리로 설정
            api.category = 'Environment';
            apis.push(api);
          }
        }
        // 변수 선언 형태의 함수도 처리
        if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
          const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
          if (varDecl) {
            const type = varDecl.getType();
            const callSignatures = type.getCallSignatures();
            if (callSignatures.length > 0) {
              const api = parseVariableFunctionForNamespace(name, varDecl, sourceFile);
              if (api) {
                api.category = 'Environment';
                apis.push(api);
              }
            }
          }
        }
      }
    }
  }

  return apis;
}

/**
 * 함수 선언 파싱 (네임스페이스 파서용)
 */
function parseFunctionDeclarationForNamespace(
  func: FunctionDeclaration,
  sourceFile: SourceFile
): ParsedAPI | null {
  const name = func.getName();
  if (!name) return null;

  const jsDoc = func.getJsDocs()[0];
  const description = jsDoc?.getDescription().trim();

  // JSDoc에서 추가 정보 추출
  const paramDescriptions = extractParamDescriptions(jsDoc);
  const returnDescription = extractReturnsDescription(jsDoc);
  const examples = extractExamples(jsDoc);

  // 파라미터 파싱 (JSDoc 설명 포함)
  const parameters = func.getParameters().map((param, index) => {
    let paramName = param.getName();
    // Destructuring parameter ({ foo }) 처리: 간단한 이름으로 변경
    if (paramName.includes('{') || paramName.includes('}') || paramName.includes(',')) {
      paramName = `options${index > 0 ? index : ''}`;
    }
    // optional 확인: isOptional() 또는 hasQuestionToken()
    const isOptional = param.isOptional() || param.hasQuestionToken?.() || false;
    return {
      name: paramName,
      type: parseType(param.getType()),
      optional: isOptional,
      description: paramDescriptions.get(param.getName()), // 원본 이름으로 설명 찾기
    };
  });

  const returnType = parseType(func.getReturnType());

  if (checkAndRecordDomViolationsNs(name, parameters, returnType, sourceFile, getCategoryFromPath(sourceFile.getFilePath()))) {
    return null;
  }

  // returnType이 Promise인지 확인하여 동기/비동기 구분
  const isAsync = returnType.kind === 'promise';
  const hasPermission = checkPermissionSupport(func);

  // 콜백 기반 API 감지 (onEvent/onError 패턴)
  const isCallbackBased = detectCallbackBasedPattern(parameters, returnType);

  return {
    name,
    pascalName: toPascalCase(name),
    originalName: name,
    category: getCategoryFromPath(sourceFile.getFilePath()),
    file: sourceFile.getFilePath(),
    description,
    returnDescription,
    examples,
    parameters,
    returnType,
    isAsync: isCallbackBased ? false : isAsync,
    hasPermission,
    isCallbackBased,
  };
}

/**
 * 변수 함수 파싱 (네임스페이스 파서용)
 */
function parseVariableFunctionForNamespace(
  name: string,
  varDecl: any,
  sourceFile: SourceFile
): ParsedAPI | null {
  const type = varDecl.getType();
  const signatures = type.getCallSignatures();

  if (signatures.length === 0) return null;

  const signature = signatures[0];
  // getJsDocs()가 있으면 사용, 없으면 빈 배열
  const jsDocs = typeof varDecl.getJsDocs === 'function' ? varDecl.getJsDocs() : [];
  const jsDoc = jsDocs[0];
  const description = jsDoc?.getDescription?.()?.trim();

  // JSDoc에서 추가 정보 추출
  const paramDescriptions = extractParamDescriptions(jsDoc);
  const returnDescription = extractReturnsDescription(jsDoc);
  const examples = extractExamples(jsDoc);

  // 파라미터 파싱 (JSDoc 설명 포함)
  const parameters = signature.getParameters().map((param: any, index: number) => {
    let paramName = param.getName();
    // Destructuring parameter ({ foo }) 처리: 간단한 이름으로 변경
    if (paramName.includes('{') || paramName.includes('}') || paramName.includes(',')) {
      paramName = `options${index > 0 ? index : ''}`;
    }
    const valueDecl = param.getValueDeclaration?.();
    const paramType = valueDecl?.getType?.() || param.getDeclaredType?.();
    // optional 확인: isOptional() 또는 hasQuestionToken() (typeof 참조 시 isOptional이 false 반환할 수 있음)
    const isOptional = param.isOptional?.() || valueDecl?.hasQuestionToken?.() || false;
    return {
      name: paramName,
      type: paramType ? parseType(paramType) : { name: 'any', kind: 'primitive', raw: 'any' },
      optional: isOptional,
      description: paramDescriptions.get(param.getName()), // 원본 이름으로 설명 찾기
    };
  });

  const returnType = parseType(signature.getReturnType());

  if (checkAndRecordDomViolationsNs(name, parameters, returnType, sourceFile, getCategoryFromPath(sourceFile.getFilePath()))) {
    return null;
  }

  // returnType이 Promise인지 확인하여 동기/비동기 구분
  const isAsync = returnType.kind === 'promise';

  // 콜백 기반 API 감지 (onEvent/onError 패턴)
  const isCallbackBased = detectCallbackBasedPattern(parameters, returnType);

  return {
    name,
    pascalName: toPascalCase(name),
    originalName: name,
    category: getCategoryFromPath(sourceFile.getFilePath()),
    file: sourceFile.getFilePath(),
    description,
    returnDescription,
    examples,
    parameters,
    returnType,
    isAsync: isCallbackBased ? false : isAsync,
    hasPermission: false,
    isCallbackBased,
  };
}

/**
 * Permission 지원 여부 확인
 */
function checkPermissionSupport(func: FunctionDeclaration): boolean {
  const type = func.getReturnType();
  const properties = type.getProperties();

  return properties.some(
    prop => prop.getName() === 'getPermission' || prop.getName() === 'openPermissionDialog'
  );
}

/**
 * DOM 전용 타입 위반을 감지하고 collector에 기록 (네임스페이스 파서용).
 * 위반이 하나라도 있으면 true를 반환한다.
 */
function checkAndRecordDomViolationsNs(
  functionName: string,
  parameters: { name: string; type: ParsedType }[],
  returnType: ParsedType,
  sourceFile: SourceFile,
  category: string,
): boolean {
  const file = sourceFile.getFilePath();
  let hasViolation = false;
  for (const p of parameters) {
    if (p.type.kind === 'dom-only') {
      recordDomViolation({
        functionName,
        category,
        location: 'parameter',
        paramName: p.name,
        rawType: p.type.raw ?? p.type.name,
        file,
      });
      hasViolation = true;
    }
  }
  if (returnType.kind === 'dom-only') {
    recordDomViolation({
      functionName,
      category,
      location: 'return',
      rawType: returnType.raw ?? returnType.name,
      file,
    });
    hasViolation = true;
  }
  return hasViolation;
}
