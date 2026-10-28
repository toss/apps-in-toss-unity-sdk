/**
 * web-framework 3.x 번들 `dist/index.d.ts` 파서
 *
 * 번들이 export하는 모든 값(함수, 네임스페이스 객체, 이벤트 네임스페이스, enum, 클래스)을
 * 하나씩 분류해서 C#으로 노출할 진입점(ParsedAPI)으로 옮긴다. 노출하지 않는 export는
 * 기술적 사유와 함께 인벤토리에 남기고, 어느 쪽에도 분류되지 않은 export는 생성을 중단시킨다
 * (새 export가 조용히 누락되는 일을 막는다).
 *
 * 분류 규칙:
 * - 호출 가능한 최상위 export: 최상위 함수 API (`getClipboardText` -> `GetClipboardText`)
 * - 호출 서명 없는 객체 export: 네임스페이스. 멤버마다 API (`Storage.getItem` -> `StorageGetItem`)
 *   - 호출 가능한 멤버: 함수 API
 *   - 호출 서명 없는 멤버: 프로퍼티 읽기 API (`Device.locale` -> `DeviceLocale`)
 * - `addEventListener` 하나만 가진 객체: 이벤트 네임스페이스 (이벤트별 Subscribe API)
 * - 함수에 붙은 하위 멤버 `isSupported` / `getPermission` / `openPermissionDialog`:
 *   별도 API (`<기반 API 이름>IsSupported` 등)
 */

import { Node, SourceFile, Type, JSDoc, Symbol as TsSymbol } from 'ts-morph';
import { ParsedAPI, ParsedParameter, ParsedType } from '../types.js';
import {
  NAMESPACE_CATEGORIES,
  NOT_EXPOSED_EXPORTS,
  FLATTENED_OPTIONS_CALLBACK_APIS,
  getTopLevelCategory,
} from '../categories.js';
import { toPascalCase } from './utils.js';
import { parseType } from './type-parser.js';
import { recordDomViolation } from './dom-violations.js';
import { extractParamDescriptions, extractReturnsDescription, extractExamples } from './jsdoc-extractor.js';
import { parseEventNamespace } from './event-parser.js';
import { detectCallbackBasedPattern, detectNestedCallbacks, detectCallbackBag } from './callback-patterns.js';

/** 함수 export에 붙는 하위 멤버 중 별도 API로 노출하는 것 */
const SUB_MEMBER_NAMES = ['isSupported', 'getPermission', 'openPermissionDialog'] as const;
type SubMemberName = (typeof SUB_MEMBER_NAMES)[number];

/** 하위 멤버 중 API로 옮기지 않는 정적 메타데이터 (버전 상수) */
const METADATA_MEMBER = /^MIN_[A-Z_]+$/;

/** 번들 export 하나가 C#의 어디로 갔는지 */
export interface BundleExportRecord {
  /** 번들 export 이름 */
  exportName: string;
  kind: 'function' | 'namespace' | 'event-namespace' | 'enum' | 'class' | 'not-exposed';
  /** 이 export에서 만들어진 C# API 이름(pascalName) 목록 */
  csharpApis: string[];
  /** 노출하지 않은 이유 (kind === 'not-exposed' 또는 일부 멤버를 뺀 경우) */
  note?: string;
}

export interface BundleParseResult {
  apis: ParsedAPI[];
  inventory: BundleExportRecord[];
}

const SUB_MEMBER_DOC: Record<SubMemberName, (label: string) => string> = {
  isSupported: label => `현재 앱 버전과 환경에서 \`${label}\`를 사용할 수 있는지 확인해요.`,
  getPermission: label => `\`${label}\`에 필요한 권한의 현재 상태를 확인해요.`,
  openPermissionDialog: label => `\`${label}\`에 필요한 권한 요청 다이얼로그를 열어요.`,
};

/** JSDoc이 붙는 노드에서 JSDoc 목록을 가져온다 (`declare const x` 는 VariableStatement에 붙는다) */
function jsDocsOf(node: Node | undefined): JSDoc[] {
  if (!node) return [];
  if (Node.isVariableDeclaration(node)) {
    return node.getVariableStatement()?.getJsDocs() ?? [];
  }
  if (Node.isJSDocable(node)) {
    return node.getJsDocs();
  }
  return [];
}

interface DocInfo {
  description?: string;
  returnDescription?: string;
  examples?: string[];
  paramDescriptions: Map<string, string>;
  isDeprecated: boolean;
  deprecatedText?: string;
}

function readDocs(node: Node | undefined): DocInfo {
  const jsDocs = jsDocsOf(node);
  const jsDoc = jsDocs[jsDocs.length - 1];
  let isDeprecated = false;
  let deprecatedText: string | undefined;
  for (const doc of jsDocs) {
    for (const tag of doc.getTags()) {
      if (tag.getTagName() === 'deprecated') {
        isDeprecated = true;
        const text = tag.getCommentText()?.trim();
        if (text) deprecatedText = text;
      }
    }
  }
  const description = jsDoc?.getDescription().trim() || undefined;
  const examples = extractExamples(jsDoc);
  return {
    description,
    returnDescription: extractReturnsDescription(jsDoc),
    examples: examples.length > 0 ? examples : undefined,
    paramDescriptions: extractParamDescriptions(jsDoc),
    isDeprecated,
    deprecatedText,
  };
}

/**
 * `@deprecated` 문구에서 대체 API를 찾아 C# 이름으로 바꾼다.
 * 백틱으로 감싼 `Namespace.member` 또는 `functionName`을 찾고, 네임스페이스 형태가 있으면 우선한다
 * (`getAnonymousKey` 또는 `User.getAnonymousKey` 중 후자).
 * 백틱이 없으면 "X를/을 사용해주세요"의 X를 쓴다 (TossPay.authorize를 사용해주세요). 맨 이름은
 * 같은 네임스페이스의 형제 멤버로 본다 (TossAds.attach의 attachBanner -> TossAdsAttachBanner).
 * 예: "`Clipboard.getText`를 사용해주세요" -> "ClipboardGetText"
 */
export function findDeprecationReplacement(text: string | undefined, namespace?: string): string | undefined {
  if (!text) return undefined;
  const pascalPath = (token: string) => token.split('.').map(toPascalCase).join('');

  // 1) 백틱으로 감싼 대체 API (`Clipboard.getText`). 네임스페이스 형태(점 포함)를 우선한다.
  const quoted = Array.from(text.matchAll(/`([A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*)`/g)).map(m => m[1]);
  if (quoted.length > 0) {
    return pascalPath(quoted.find(t => t.includes('.')) ?? quoted[0]);
  }

  // 2) 백틱 없이 "X를/을 사용해주세요"로 안내한 경우 (TossPay.authorize를 사용해주세요)
  const plain = text.match(/([A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*)(?:를|을)\s*사용/);
  if (!plain) return undefined;
  const token = plain[1];
  // 같은 네임스페이스의 형제 멤버를 가리키는 맨 이름(attachBanner)은 네임스페이스를 붙인다
  if (!token.includes('.') && namespace) return pascalPath(`${namespace}.${token}`);
  return pascalPath(token);
}

/** `T | undefined`, `Promise<X> | undefined` 처럼 반환 타입에 섞인 undefined/null을 벗긴다 */
function stripNullish(type: Type): Type {
  if (!type.isUnion()) return type;
  const rest = type.getUnionTypes().filter(t => !t.isUndefined() && !t.isNull());
  if (rest.length === 1 && rest.length < type.getUnionTypes().length) return rest[0];
  return type;
}

function safeParamName(raw: string, index: number): string {
  if (raw.includes('{') || raw.includes('}') || raw.includes(',') || /^__\d+$/.test(raw)) {
    return `options${index > 0 ? index : ''}`;
  }
  return raw;
}

interface MemberContext {
  /** 타입 해석 기준 노드 */
  anchor: Node;
  file: string;
  /** 문서를 읽을 선언 */
  docNode: Node | undefined;
}

class BundleParser {
  readonly apis: ParsedAPI[] = [];
  readonly inventory: BundleExportRecord[] = [];

  constructor(private readonly sourceFile: SourceFile) {}

  parse(): BundleParseResult {
    const file = this.sourceFile.getFilePath();
    const exported = this.sourceFile.getExportedDeclarations();

    for (const [exportName, decls] of exported) {
      const decl = decls.find(
        d =>
          Node.isVariableDeclaration(d) ||
          Node.isFunctionDeclaration(d) ||
          Node.isEnumDeclaration(d) ||
          Node.isClassDeclaration(d),
      );
      if (!decl) continue; // 타입 전용 export (interface, type alias)

      const notExposed = NOT_EXPOSED_EXPORTS[exportName];
      if (notExposed) {
        this.inventory.push({ exportName, kind: 'not-exposed', csharpApis: [], note: notExposed });
        continue;
      }

      if (Node.isEnumDeclaration(decl)) {
        // enum 값은 타입 정의 경로(parseTypeDefinitions)에서 C# enum으로 생성된다.
        this.inventory.push({ exportName, kind: 'enum', csharpApis: [], note: 'C# enum으로 생성' });
        continue;
      }
      if (Node.isClassDeclaration(decl)) {
        this.inventory.push({
          exportName,
          kind: 'not-exposed',
          csharpApis: [],
          note: '오류 클래스. 생성자를 호출할 일이 없고 브릿지가 AITException으로 변환한다',
        });
        continue;
      }

      const type = decl.getType();
      const callSignatures = type.getCallSignatures();
      const properties = type.getProperties();

      if (callSignatures.length > 0) {
        const before = this.apis.length;
        this.withContext(exportName, () => this.parseCallableExport(exportName, decl, file));
        this.inventory.push({
          exportName,
          kind: 'function',
          csharpApis: this.apis.slice(before).map(a => a.pascalName),
        });
        continue;
      }

      const memberNames = properties.map(p => p.getName());
      if (memberNames.length === 1 && memberNames[0] === 'addEventListener') {
        const before = this.apis.length;
        const eventApis = parseEventNamespace(exportName, this.sourceFile);
        for (const api of eventApis) {
          api.category = NAMESPACE_CATEGORIES[exportName] ?? 'AppEvents';
          this.apis.push(api);
        }
        this.inventory.push({
          exportName,
          kind: 'event-namespace',
          csharpApis: this.apis.slice(before).map(a => a.pascalName),
          note: eventApis.length === 0 ? '등록된 이벤트가 없다 (이벤트 맵이 비어 있음)' : undefined,
        });
        continue;
      }

      if (properties.length > 0) {
        const before = this.apis.length;
        this.parseNamespace(exportName, decl, file);
        this.inventory.push({
          exportName,
          kind: 'namespace',
          csharpApis: this.apis.slice(before).map(a => a.pascalName),
        });
        continue;
      }

      throw new Error(
        `번들 export '${exportName}'을(를) 분류하지 못했습니다. 함수도 네임스페이스도 이벤트 네임스페이스도 아닙니다. ` +
          `새 export라면 categories.ts의 NOT_EXPOSED_EXPORTS에 사유와 함께 등록하거나 파서 분류 규칙을 확장하세요.`,
      );
    }

    return { apis: this.apis, inventory: this.inventory };
  }

  // ---------------------------------------------------------------------
  // 최상위 함수
  // ---------------------------------------------------------------------

  private parseCallableExport(exportName: string, decl: Node, file: string): void {
    const docs = readDocs(decl);
    const type = decl.getType();
    const category = getTopLevelCategory(exportName);

    const api = this.buildFunctionApi({
      name: exportName,
      originalName: exportName,
      namespace: undefined,
      callPath: undefined,
      category,
      type,
      ctx: { anchor: decl, file, docNode: decl },
      docs,
    });
    if (!api) return;

    if (FLATTENED_OPTIONS_CALLBACK_APIS.includes(exportName)) {
      this.flattenOptionsParameters(api);
    }
    api.isTopLevelExport = true;
    this.apis.push(api);
    this.parseSubMembers(api, type, decl, file, docs);
  }

  /**
   * `{ options: { adGroupId: string }, onEvent, onError }` 형태의 최상위 콜백 API를
   * `(adGroupId, onEvent, onError)` 형태의 평평한 C# 시그니처로 푼다.
   * 이 두 API(loadFullScreenAd/showFullScreenAd)는 처음부터 이 시그니처로 공개돼 있어 유지한다.
   */
  private flattenOptionsParameters(api: ParsedAPI): void {
    const args = api.parameters[0];
    if (!args || args.type.kind !== 'object' || !args.type.properties) return;
    const flattened: ParsedParameter[] = [];
    for (const prop of args.type.properties) {
      if (prop.name === 'onEvent' || prop.name === 'onError') {
        flattened.push({
          name: prop.name,
          type: prop.type,
          optional: prop.optional,
          description: prop.name === 'onEvent' ? '이벤트 콜백' : '에러 콜백',
        });
      } else if (prop.name === 'options' && prop.type.kind === 'object' && prop.type.properties) {
        for (const opt of prop.type.properties) {
          flattened.push({ name: opt.name, type: opt.type, optional: opt.optional, description: opt.description });
        }
      }
    }
    api.parameters = flattened;
  }

  // ---------------------------------------------------------------------
  // 네임스페이스
  // ---------------------------------------------------------------------

  private parseNamespace(namespaceName: string, decl: Node, file: string): void {
    let category = NAMESPACE_CATEGORIES[namespaceName];
    if (!category) {
      category = toPascalCase(namespaceName);
      console.warn(`⚠️  네임스페이스 '${namespaceName}'의 카테고리가 categories.ts NAMESPACE_CATEGORIES에 없어 '${category}'로 분류합니다.`);
    }
    const nsType = decl.getType();

    for (const prop of nsType.getProperties()) {
      const memberName = prop.getName();
      this.withContext(`${namespaceName}.${memberName}`, () => this.parseNamespaceMember(namespaceName, category, decl, file, prop));
    }
  }

  private parseNamespaceMember(namespaceName: string, category: string, decl: Node, file: string, prop: TsSymbol): void {
    const memberName = prop.getName();
    const propDecl = prop.getValueDeclaration() ?? prop.getDeclarations()[0];
    const memberType = prop.getTypeAtLocation(decl);
    const docs = readDocs(propDecl);
    const fullName = `${namespaceName}${toPascalCase(memberName)}`;
    const ctx: MemberContext = { anchor: decl, file, docNode: propDecl };

    if (memberType.getCallSignatures().length > 0) {
      const api = this.buildFunctionApi({
        name: fullName,
        originalName: memberName,
        namespace: namespaceName,
        callPath: undefined,
        category,
        type: memberType,
        ctx,
        docs,
      });
      if (!api) return;
      this.apis.push(api);
      this.parseSubMembers(api, memberType, propDecl ?? decl, file, docs);
      return;
    }

    // 호출 서명이 없는 멤버: 읽기 전용 프로퍼티
    const api = this.buildPropertyApi({
      name: fullName,
      originalName: memberName,
      namespace: namespaceName,
      category,
      type: memberType,
      ctx,
      docs,
    });
    if (api) this.apis.push(api);
  }

  /** 파싱 중 예외(특히 재귀 타입의 스택 오버플로우)에 어느 export인지 맥락을 붙여 다시 던진다 */
  private withContext(label: string, fn: () => void): void {
    try {
      fn();
    } catch (err) {
      const reason = err instanceof Error ? err.message : String(err);
      throw new Error(`번들 export '${label}' 파싱 실패: ${reason}`);
    }
  }

  // ---------------------------------------------------------------------
  // 하위 멤버 (isSupported / getPermission / openPermissionDialog)
  // ---------------------------------------------------------------------

  private parseSubMembers(base: ParsedAPI, type: Type, anchor: Node, file: string, baseDocs: DocInfo): void {
    const label = base.namespace ? `${base.namespace}.${base.originalName}` : base.originalName;
    for (const sub of type.getProperties()) {
      const subName = sub.getName();
      if (METADATA_MEMBER.test(subName)) continue;
      if (!(SUB_MEMBER_NAMES as readonly string[]).includes(subName)) {
        throw new Error(
          `'${label}'에 알 수 없는 하위 멤버 '${subName}'가 있습니다. 파서(bundle-parser.ts SUB_MEMBER_NAMES)에 처리 규칙을 추가하세요.`,
        );
      }
      const subType = sub.getTypeAtLocation(anchor);
      const signature = subType.getCallSignatures()[0];
      if (!signature) continue;

      const returnType = parseType(stripNullish(signature.getReturnType()));
      const isAsync = returnType.kind === 'promise';
      const name = `${base.name}${toPascalCase(subName)}`;
      const api: ParsedAPI = {
        name,
        pascalName: toPascalCase(name),
        originalName: subName,
        category: base.category,
        file,
        description: SUB_MEMBER_DOC[subName as SubMemberName](label),
        parameters: [],
        returnType,
        isAsync,
        hasPermission: false,
        namespace: base.namespace,
        callPath: `${base.namespace ? `${base.namespace}.` : ''}${base.originalName}.${subName}`,
        baseApiName: base.pascalName,
        isTopLevelExport: base.isTopLevelExport,
      };
      if (base.isDeprecated) {
        api.isDeprecated = true;
        api.deprecatedMessage = this.deprecationMessage(api, baseDocs.deprecatedText, true);
      }
      if (subName === 'getPermission' || subName === 'openPermissionDialog') {
        base.hasPermission = true;
      }
      this.apis.push(api);
    }
  }

  // ---------------------------------------------------------------------
  // ParsedAPI 조립
  // ---------------------------------------------------------------------

  private deprecationMessage(api: ParsedAPI, text: string | undefined, isSubMember = false): string | undefined {
    const replacement = findDeprecationReplacement(text, api.namespace);
    if (!replacement) return undefined;
    return isSubMember ? `Use ${replacement}${toPascalCase(api.originalName)}` : `Use ${replacement}`;
  }

  private buildPropertyApi(opts: {
    name: string;
    originalName: string;
    namespace: string;
    category: string;
    type: Type;
    ctx: MemberContext;
    docs: DocInfo;
  }): ParsedAPI | null {
    const returnType = parseType(opts.type);
    if (this.recordDomViolations(opts.name, opts.category, [], returnType, opts.ctx.file)) return null;
    return {
      name: opts.name,
      pascalName: toPascalCase(opts.name),
      originalName: opts.originalName,
      category: opts.category,
      file: opts.ctx.file,
      description: opts.docs.description,
      returnDescription: opts.docs.returnDescription,
      examples: opts.docs.examples,
      parameters: [],
      returnType,
      isAsync: false,
      hasPermission: false,
      namespace: opts.namespace,
      isProperty: true,
    };
  }

  private buildFunctionApi(opts: {
    name: string;
    originalName: string;
    namespace: string | undefined;
    callPath: string | undefined;
    category: string;
    type: Type;
    ctx: MemberContext;
    docs: DocInfo;
  }): ParsedAPI | null {
    const signature = opts.type.getCallSignatures()[0];
    const parameters: ParsedParameter[] = signature.getParameters().map((param, index) => {
      const rawName = param.getName();
      const valueDecl = param.getValueDeclaration();
      const paramType = valueDecl ? valueDecl.getType() : param.getTypeAtLocation(opts.ctx.anchor);
      const isOptional =
        param.isOptional() || (Node.isParameterDeclaration(valueDecl) && valueDecl.hasQuestionToken()) || false;
      return {
        name: safeParamName(rawName, index),
        type: parseType(paramType),
        optional: isOptional,
        description: opts.docs.paramDescriptions.get(rawName),
      };
    });

    const returnType = parseType(stripNullish(signature.getReturnType()));
    if (this.recordDomViolations(opts.name, opts.category, parameters, returnType, opts.ctx.file)) return null;

    const isCallbackBased = detectCallbackBasedPattern(parameters, returnType);
    const nestedCallbacks = detectNestedCallbacks(parameters);
    const callbackBag = isCallbackBased || nestedCallbacks.length > 0
      ? undefined
      : detectCallbackBag(parameters, returnType);
    const api: ParsedAPI = {
      name: opts.name,
      pascalName: toPascalCase(opts.name),
      originalName: opts.originalName,
      category: opts.category,
      file: opts.ctx.file,
      description: opts.docs.description,
      returnDescription: opts.docs.returnDescription,
      examples: opts.docs.examples,
      parameters,
      returnType,
      isAsync: returnType.kind === 'promise',
      hasPermission: false,
      namespace: opts.namespace,
      callPath: opts.callPath,
      isCallbackBased,
      nestedCallbacks: nestedCallbacks.length > 0 ? nestedCallbacks : undefined,
      callbackBag,
    };
    if (opts.docs.isDeprecated) {
      const message = this.deprecationMessage(api, opts.docs.deprecatedText);
      // 대체 API가 안내된 경우에만 [Obsolete]로 표시한다.
      if (message) {
        api.isDeprecated = true;
        api.deprecatedMessage = message;
      }
    }
    this.classifySubscription(api);
    return api;
  }

  /**
   * `X({ onEvent, onError? })` 형태(options 없음)의 구독 API는 직접 구독 이벤트 API로 바꾼다.
   * - SafeArea.subscribe: onEvent만 받는다
   * - Game.subscribeInfoOverlayHidden: onEvent와 onError를 받고 이벤트 데이터가 없다
   */
  private classifySubscription(api: ParsedAPI): void {
    if (api.parameters.length !== 1) return;
    const args = api.parameters[0].type;
    if (args.kind !== 'object' || !args.properties) return;
    const hasOptions = args.properties.some(p => p.name === 'options');
    const onEvent = args.properties.find(p => p.name === 'onEvent' && p.type.kind === 'function');
    const hasOnError = args.properties.some(p => p.name === 'onError' && p.type.kind === 'function');
    const returnsCleanup =
      api.returnType.kind === 'function' &&
      (api.returnType.raw?.includes('() => void') || api.returnType.functionReturnType?.name === 'void');
    if (hasOptions || !onEvent || !returnsCleanup) return;

    api.isCallbackBased = false;
    api.isEventSubscription = true;
    api.isDirectSubscribe = true;
    api.directSubscribeHasOnError = hasOnError;
    api.eventDataType = onEvent.type.functionParams?.[0];
    api.parameters = [];
    api.isAsync = false;
    api.returnType = { name: 'Action', kind: 'function', raw: '() => void' };
    api.eventName = api.originalName;
  }

  private recordDomViolations(
    functionName: string,
    category: string,
    parameters: ParsedParameter[],
    returnType: ParsedType,
    file: string,
  ): boolean {
    let violated = false;
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
        violated = true;
      }
    }
    if (returnType.kind === 'dom-only') {
      recordDomViolation({ functionName, category, location: 'return', rawType: returnType.raw ?? returnType.name, file });
      violated = true;
    }
    return violated;
  }
}

/** 번들 index.d.ts 하나를 파싱한다 */
export function parseBundle(sourceFile: SourceFile): BundleParseResult {
  const result = new BundleParser(sourceFile).parse();
  assertUniqueNames(result.apis);
  return result;
}

/** C# 이름(pascalName)이 겹치면 생성을 중단한다 (한쪽을 조용히 덮어쓰지 않는다) */
function assertUniqueNames(apis: ParsedAPI[]): void {
  const seen = new Map<string, ParsedAPI>();
  for (const api of apis) {
    const prior = seen.get(api.pascalName);
    if (prior) {
      throw new Error(
        `C# API 이름 충돌: '${api.pascalName}' (${prior.namespace ?? ''}.${prior.originalName} 와 ${api.namespace ?? ''}.${api.originalName}). ` +
          `충돌 해소 규칙을 categories.ts에 추가하세요.`,
      );
    }
    seen.set(api.pascalName, api);
  }
}
