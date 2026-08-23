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
 * 선언의 JSDoc 목록을 가져온다.
 *
 * ts-morph에서 `declare const NAME: Type;` 형태는 VariableDeclaration 노드에
 * getJsDocs()가 없다(JSDocableNode를 구현하지 않음) — 실제 JSDoc(예: `@deprecated`)은
 * 그 부모인 VariableStatement에 붙는다. `decl.getJsDocs?.()`만 쓰면 옵셔널 체이닝이
 * 조용히 undefined를 반환해 항상 "JSDoc 없음"으로 오판하므로(실측: getServerTime 등
 * wrapped-callable 전역 함수의 @deprecated가 매번 누락됨), VariableDeclaration이면
 * getVariableStatement()로 올라가 그쪽 JSDoc을 사용한다.
 */
function getJsDocsForDeclaration(decl: any): any[] {
  try {
    if (typeof decl.getJsDocs === 'function') {
      return decl.getJsDocs() || [];
    }
    const stmt = decl.getVariableStatement?.();
    if (stmt && typeof stmt.getJsDocs === 'function') {
      return stmt.getJsDocs() || [];
    }
    return [];
  } catch {
    return [];
  }
}

/**
 * 선언이 deprecated인지 확인 (JSDoc @deprecated 태그)
 */
export function isDeprecatedDeclaration(decl: any): boolean {
  try {
    const jsDocs = getJsDocsForDeclaration(decl);
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
 * 선언의 JSDoc @deprecated 태그 코멘트(대체 API 안내 등)를 추출.
 * 태그가 없거나 코멘트가 비어 있으면 undefined.
 */
export function getDeprecatedMessage(decl: any): string | undefined {
  try {
    const jsDocs = getJsDocsForDeclaration(decl);
    for (const jsDoc of jsDocs) {
      const tags = jsDoc.getTags?.() || [];
      for (const tag of tags) {
        if (tag.getTagName?.() === 'deprecated') {
          const text = tag.getCommentText?.();
          if (text && text.trim()) return text.trim();
        }
      }
    }
    return undefined;
  } catch {
    return undefined;
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
 * 파서 레벨에서 항상 스킵하는 글로벌 함수 이름.
 *
 * `createAsyncBridge`: web-framework 3.x dist/index.d.ts(자기완결 번들, discovery.ts의
 * self-bundle 판정 참고)에 등장하는 내부 브릿지 팩토리. 시그니처가
 * `<Args extends unknown[] = unknown[], Result = unknown>(method: string): (...args: Args) => Promise<Result>`
 * 형태의 제네릭 함수 타입인데, 이 반환 타입을 parseType()에 넘기면 ts-morph 타입
 * 해석이 순환에 빠져 `RangeError: Maximum call stack size exceeded`가 발생한다(실측,
 * --stack-size=30000에서도 재현 — 얕은 스택 한계 문제가 아니라 실제 순환).
 * EXCLUDED_APIS(categories.ts) 같은 파싱 이후 필터로는 막을 수 없다 — 크래시가
 * parseType() 호출 시점(=파싱 도중)에 발생하기 때문에, 파서가 이 이름을 애초에
 * 글로벌 함수로 감지하지 않도록 여기서 제외해야 한다.
 */
const PARSER_SKIP_GLOBAL_FUNCTIONS = new Set(['createAsyncBridge']);

/**
 * 타입 텍스트가 "callable" 화살표 함수 타입인지 판정.
 * `() => X` 같은 파라미터 없는 형태뿐 아니라 `(args: { ... }) => Y` 처럼 파라미터가
 * 객체 리터럴(중괄호 포함)인 형태도 인식한다 — 선행 `(...)`의 괄호 깊이를 직접 카운트해
 * 그 매칭되는 닫는 괄호 바로 뒤에 `=>`가 오는지만 확인하므로, 파라미터 내부에 `{`가
 * 있어도(중첩 객체/제네릭) 오탐하지 않는다.
 *
 * 객체 리터럴 타입(`{ method: ... }` 형태의 네임스페이스 객체)은 애초에 `(`로 시작하지
 * 않으므로 이 판정에서 자연히 걸러진다. `(() => X) & { isSupported: ... }` 같은
 * intersection 타입은 선행 `(...)` 뒤에 `=>`가 아니라 `&`가 오므로 여기서 제외된다
 * (이런 형태는 detectGlobalFunctions/detectNamespaceObjects 어느 쪽에도 걸리지 않는
 * 기존 동작을 그대로 유지 — FRAMEWORK_APIS 등 별도 경로로 처리됨).
 */
function isCallableArrowType(typeText: string): boolean {
  const trimmed = typeText.trim();
  if (!trimmed.startsWith('(')) return false;

  let depth = 0;
  for (let i = 0; i < trimmed.length; i++) {
    const ch = trimmed[i];
    if (ch === '(') {
      depth++;
    } else if (ch === ')') {
      depth--;
      if (depth === 0) {
        const rest = trimmed.slice(i + 1).trimStart();
        return rest.startsWith('=>');
      }
      if (depth < 0) return false;
    }
  }
  return false;
}

/**
 * "래핑된" callable 타입 판정 — isCallableArrowType으로는 못 잡는 두 형태를 추가로 인식:
 *
 * 1. intersection: `(<화살표 타입>) & { isSupported: ...; ... }` — 선행 `(...)`의
 *    매칭 닫는 괄호 바로 뒤에 `&`가 오면, 그 괄호를 벗기고 안쪽이 화살표 타입인지 재귀
 *    확인한다. (예: 3.x self-bundle의 `getServerTime: (() => Promise<number|undefined>)
 *    & { isSupported: () => boolean }`, `startUpdateLocation: ((eventParams) =>
 *    (() => void)) & { ... }`)
 * 2. 제네릭 wrapper: `Identifier<<화살표 타입>>` 형태 — 마지막 `>`까지를 제네릭
 *    인자로 보고 안쪽이 화살표 타입인지 재귀 확인한다. (예:
 *    `PermissionFunctionWithDialog<(options?: X) => Promise<Y>>`)
 *
 * ⚠️ 스코프: 이 두 형태는 2.x sibling(web-bridge)의 index.d.ts에도 동일 이름·동일
 * 형태로 존재하는 API가 있고(getServerTime, fetchAlbumPhotos 등), 2.x에서는 그 API가
 * *같은 이름의 개별 .d.ts 파일*(file-per-API, parseSourceFile 경로)로도 파싱되므로,
 * 여기서 무조건 켜면 같은 이름이 두 경로에서 중복 파싱된다(C# 생성 경로 포함 —
 * Runtime/SDK 변경으로 이어짐). 그래서 이 판정은 호출부가 명시적으로 opt-in할 때만
 * 쓰여야 한다 — detectGlobalFunctions의 includeWrappedCallables 참고.
 */
function isWrappedCallableType(typeText: string): boolean {
  const trimmed = typeText.trim();

  // intersection: 선행 (...) 뒤에 '&'
  if (trimmed.startsWith('(')) {
    let depth = 0;
    for (let i = 0; i < trimmed.length; i++) {
      const ch = trimmed[i];
      if (ch === '(') {
        depth++;
      } else if (ch === ')') {
        depth--;
        if (depth === 0) {
          const rest = trimmed.slice(i + 1).trimStart();
          if (rest.startsWith('&')) {
            return isCallableArrowType(trimmed.slice(1, i).trim());
          }
          break;
        }
        if (depth < 0) break;
      }
    }
  }

  // 제네릭 wrapper: Identifier<...>
  const genericMatch = trimmed.match(/^[A-Za-z_$][\w$.]*\s*<([\s\S]+)>$/);
  if (genericMatch) {
    return isCallableArrowType(genericMatch[1].trim());
  }

  return false;
}

/**
 * 글로벌 함수 감지 (이 파일에 정의된 FunctionDeclaration 또는 화살표 함수 타입 declare const)
 * 패턴: declare function NAME(...) 또는 declare const NAME: (...) => ...
 *
 * @param options.includeDeprecatedGlobals true면 deprecated 선언도 포함(호출부가
 *   isDeprecated 플래그를 직접 확인/보존할 책임을 진다). 기본값 false — 이 기본값은
 *   C# 생성 경로(index.ts)를 포함한 기존 모든 호출부의 동작을 그대로 보존한다.
 *   changelog 파이프라인의 self-bundle(3.x) 경로에서만 true로 호출해 deprecated
 *   최상위 함수가 "제거"가 아니라 "변경(deprecated 전환)"으로 잡히게 한다.
 * @param options.includeWrappedCallables true면 isWrappedCallableType(intersection/
 *   제네릭 wrapper 화살표 타입)도 감지 대상에 포함한다. 기본값 false — 2.x sibling에서
 *   이 형태는 같은 이름이 file-per-API 경로에서 이미 파싱되므로 여기서까지 켜면
 *   중복이 생긴다(isWrappedCallableType 문서 참고). self-bundle(3.x)에서만 opt-in.
 */
export function detectGlobalFunctions(
  sourceFile: SourceFile,
  options?: { includeDeprecatedGlobals?: boolean; includeWrappedCallables?: boolean }
): Set<string> {
  const globalFunctions = new Set<string>();
  const exportedDeclarations = sourceFile.getExportedDeclarations();
  const includeDeprecated = options?.includeDeprecatedGlobals ?? false;
  const includeWrapped = options?.includeWrappedCallables ?? false;

  for (const [name, declarations] of exportedDeclarations) {
    // 파서 크래시를 유발하는 이름은 애초에 감지하지 않음 (PARSER_SKIP_GLOBAL_FUNCTIONS 주석 참고)
    if (PARSER_SKIP_GLOBAL_FUNCTIONS.has(name)) continue;

    for (const decl of declarations) {
      // 이 파일에 정의된 선언만 처리 (re-export 제외)
      if (!isDefinedInFile(decl, sourceFile)) continue;

      // deprecated 선언은 기본적으로 제외 (includeDeprecatedGlobals일 때만 포함)
      if (!includeDeprecated && isDeprecatedDeclaration(decl)) continue;

      // Case 1: function declaration (declare function isMinVersionSupported(...))
      if (decl.getKind() === SyntaxKind.FunctionDeclaration) {
        globalFunctions.add(name);
        continue;
      }

      // Case 2: const with callable arrow function type
      // (const getAppsInTossGlobals: () => ... 또는
      //  const onVisibilityChangedByTransparentServiceWeb: (eventParams: { ... }) => (() => void))
      if (decl.getKind() === SyntaxKind.VariableDeclaration) {
        const typeText = getTypeAnnotationText(decl);
        if (isCallableArrowType(typeText)) {
          globalFunctions.add(name);
          continue;
        }
        // Case 3 (opt-in): intersection/제네릭 wrapper 화살표 타입
        // (const getServerTime: (() => Promise<number|undefined>) & { isSupported } 또는
        //  const getClipboardText: PermissionFunctionWithDialog<() => Promise<string>>)
        if (includeWrapped && isWrappedCallableType(typeText)) {
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
