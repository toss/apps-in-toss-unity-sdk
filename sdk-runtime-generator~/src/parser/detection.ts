import { SourceFile, SyntaxKind } from 'ts-morph';

/**
 * VariableDeclaration에서 타입 주석 텍스트만 추출 (JSDoc 제외)
 * getType()을 사용하지 않으므로 스택 오버플로우 위험 없음
 */
export function getTypeAnnotationText(varDecl: any): string {
  try {
    // 타입 노드가 있으면 직접 텍스트 추출 (JSDoc 제외)
    const typeNode = varDecl.getTypeNode?.();
    if (typeNode) {
      return typeNode.getText();
    }
    // 타입 노드가 없으면 빈 문자열 반환 (JSDoc 포함 방지)
    return '';
  } catch {
    return '';
  }
}

/**
 * 선언이 현재 소스 파일에 정의되어 있는지 확인 (re-export 제외)
 */
export function isDefinedInFile(decl: any, sourceFile: SourceFile): boolean {
  try {
    const declFile = decl.getSourceFile?.();
    if (!declFile) return false;
    return declFile.getFilePath() === sourceFile.getFilePath();
  } catch {
    return false;
  }
}

/**
 * 선언이 deprecated인지 확인 (JSDoc @deprecated 태그)
 */
export function isDeprecatedDeclaration(decl: any): boolean {
  try {
    const jsDocs = decl.getJsDocs?.() || [];
    for (const jsDoc of jsDocs) {
      const tags = jsDoc.getTags?.() || [];
      if (tags.some((tag: any) => tag.getTagName?.() === 'deprecated')) {
        return true;
      }
    }
    return false;
  } catch {
    return false;
  }
}

/**
 * 이벤트 네임스페이스 감지 (타입에 addEventListener 속성 포함)
 * 패턴: const xxxEvent: { addEventListener: <K extends keyof ...> ... }
 */
export function detectEventNamespaces(sourceFile: SourceFile): Set<string> {
  const eventNamespaces = new Set<string>();
  const exportedDeclarations = sourceFile.getExportedDeclarations();

  for (const [name, declarations] of exportedDeclarations) {
    for (const decl of declarations) {
      if (decl.getKind() !== SyntaxKind.VariableDeclaration) continue;
      // 이 파일에 정의된 선언만 처리 (re-export 제외)
      if (!isDefinedInFile(decl, sourceFile)) continue;

      const typeText = getTypeAnnotationText(decl);
      // addEventListener가 속성으로 정의되어 있는지 확인
      // 패턴: addEventListener: 또는 addEventListener< (JSDoc 내 언급 제외)
      if (/addEventListener\s*[:<]/.test(typeText)) {
        eventNamespaces.add(name);
      }
    }
  }

  return eventNamespaces;
}

/**
 * 글로벌 함수 감지 (이 파일에 정의된 FunctionDeclaration 또는 단순 화살표 함수)
 * 패턴: declare function NAME(...) 또는 declare const NAME: () => ...
 * deprecated 함수는 제외
 */
export function detectGlobalFunctions(sourceFile: SourceFile): Set<string> {
  const globalFunctions = new Set<string>();
  const exportedDeclarations = sourceFile.getExportedDeclarations();

  for (const [name, declarations] of exportedDeclarations) {
    for (const decl of declarations) {
      // 이 파일에 정의된 선언만 처리 (re-export 제외)
      if (!isDefinedInFile(decl, sourceFile)) continue;

      // deprecated 선언은 제외
      if (isDeprecatedDeclaration(decl)) continue;

      // Case 1: function declaration (declare function isMinVersionSupported(...))
      if (decl.getKind() === SyntaxKind.FunctionDeclaration) {
        globalFunctions.add(name);
        continue;
      }

      // Case 2: const with simple arrow function type (const getAppsInTossGlobals: () => ...)
      if (decl.getKind() === SyntaxKind.VariableDeclaration) {
        const typeText = getTypeAnnotationText(decl);
        // 단순 화살표 함수 타입: () => ... 형태이면서 객체 리터럴이 아닌 경우
        if (/^\s*\(\s*\)\s*=>\s*\w/.test(typeText) && !typeText.includes('{')) {
          globalFunctions.add(name);
        }
      }
    }
  }

  return globalFunctions;
}

/**
 * 네임스페이스 객체 감지 (메서드들의 모음인 순수 객체)
 * 패턴: declare const NAME: { method1: (...) => ..., ... } 또는 { method1: typeof fn, ... }
 * 호출 가능한 객체(callable)는 제외 (예: startUpdateLocation)
 */
export function detectNamespaceObjects(
  sourceFile: SourceFile,
  eventNamespaces: Set<string>,
  globalFunctions: Set<string>
): Set<string> {
  const namespaceObjects = new Set<string>();
  const exportedDeclarations = sourceFile.getExportedDeclarations();

  for (const [name, declarations] of exportedDeclarations) {
    // 이미 이벤트 네임스페이스거나 글로벌 함수면 스킵
    if (eventNamespaces.has(name) || globalFunctions.has(name)) continue;

    for (const decl of declarations) {
      if (decl.getKind() !== SyntaxKind.VariableDeclaration) continue;
      // 이 파일에 정의된 선언만 처리 (re-export 제외)
      if (!isDefinedInFile(decl, sourceFile)) continue;

      const typeText = getTypeAnnotationText(decl);

      // 객체 리터럴 타입인지 확인 ({ ... } 형태)
      if (!typeText.startsWith('{')) continue;

      // 호출 가능한 객체는 제외 (타입이 (...)로 시작하면 callable)
      // 예: { (params: Foo): Bar; getPermission(): ... }
      if (/^\{\s*\(/.test(typeText)) continue;

      // 메서드가 있는지 확인:
      // 패턴 1: => (화살표 함수)
      // 패턴 2: typeof (함수 참조)
      const hasArrowMethods = typeText.includes('=>');
      const hasTypeofMethods = typeText.includes('typeof ');

      if (!hasArrowMethods && !hasTypeofMethods) continue;

      namespaceObjects.add(name);
    }
  }

  return namespaceObjects;
}
