import { SourceFile, FunctionDeclaration, SyntaxKind } from 'ts-morph';
import { ParsedAPI, ParsedParameter } from '../types.js';
import { toPascalCase, getCategoryFromPath } from './utils.js';
import { parseType } from './type-parser.js';
import { extractParamDescriptions, extractReturnsDescription, extractExamples } from './jsdoc-extractor.js';
import { detectCallbackBasedPattern } from './namespace-parser.js';

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
 * 함수 선언 파싱
 */
export function parseFunctionDeclaration(
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

  // returnType이 Promise인지 확인하여 동기/비동기 구분
  const isAsync = returnType.kind === 'promise';
  const hasPermission = checkPermissionSupport(func);

  // 콜백 기반 API 감지 (onEvent/onError 패턴)
  const isCallbackBased = detectCallbackBasedPattern(parameters, returnType);

  // 항상 .d.ts 파일명에서 카테고리 추출 (@category JSDoc 태그 무시)
  // Emscripten이 한글 파일명을 처리하지 못하므로, 영어 파일명 기반 카테고리 사용
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
    isAsync: isCallbackBased ? false : isAsync, // 콜백 기반 API는 동기로 처리
    hasPermission,
    isCallbackBased,
  };
}

/**
 * 변수 함수 파싱 (const fn = () => {})
 */
export function parseVariableFunction(
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
  // returnType이 Promise인지 확인하여 동기/비동기 구분
  const isAsync = returnType.kind === 'promise';
  const hasPermission = false; // TODO: 검증 로직 추가

  // 콜백 기반 API 감지 (onEvent/onError 패턴)
  const isCallbackBased = detectCallbackBasedPattern(parameters, returnType);

  // 항상 .d.ts 파일명에서 카테고리 추출 (@category JSDoc 태그 무시)
  // Emscripten이 한글 파일명을 처리하지 못하므로, 영어 파일명 기반 카테고리 사용
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
    isAsync: isCallbackBased ? false : isAsync, // 콜백 기반 API는 동기로 처리
    hasPermission,
    isCallbackBased,
  };
}

/**
 * 단일 소스 파일 파싱
 */
export function parseSourceFile(sourceFile: SourceFile): ParsedAPI[] {
  const apis: ParsedAPI[] = [];

  // export된 함수 찾기
  const exportedDeclarations = sourceFile.getExportedDeclarations();

  for (const [name, declarations] of exportedDeclarations) {
    for (const declaration of declarations) {
      // 함수 선언
      if (declaration.getKind() === SyntaxKind.FunctionDeclaration) {
        const func = declaration as FunctionDeclaration;
        const api = parseFunctionDeclaration(func, sourceFile);
        if (api) {
          apis.push(api);
        }
      }

      // 변수 선언 (const openCamera = ..., const getClipboardText: PermissionFunctionWithDialog<...>)
      if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
        const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
        if (varDecl) {
          const initializer = varDecl.getInitializer();
          // Arrow function (const fn = () => {})
          if (initializer && initializer.getKind() === SyntaxKind.ArrowFunction) {
            const api = parseVariableFunction(name, varDecl, sourceFile);
            if (api) {
              apis.push(api);
            }
          }
          // Const with function type (const fn: FnType = ...)
          // Arrow function이 없어도 타입이 함수면 파싱
          else {
            const type = varDecl.getType();
            const callSignatures = type.getCallSignatures();
            if (callSignatures.length > 0) {
              const api = parseVariableFunction(name, varDecl, sourceFile);
              if (api) {
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
