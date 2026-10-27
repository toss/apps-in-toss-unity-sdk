import { SourceFile, Signature, SyntaxKind } from 'ts-morph';
import { ParsedAPI } from '../types.js';

/**
 * web-bridge의 파일별 .d.ts는 tsc가 구조 분해·rest 파라미터를 내보내며 이름을 잃은
 * `(args_0: string, args_1: string)` 형태로 선언한다. 같은 API가 web-framework 번들
 * index.d.ts에는 실제 이름(`(key: string, value: string)`)으로 남아 있으므로, 번들에서
 * 이름만 되살린다. 타입·순서·개수는 건드리지 않는다.
 */
const GENERATED_PARAM_NAME = /^args_\d+$/;
const IDENTIFIER = /^[A-Za-z_$][A-Za-z0-9_$]*$/;

/** 파라미터 이름이 tsc가 만든 `args_N` 자리표시자인지 */
export function isGeneratedParamName(name: string): boolean {
  return GENERATED_PARAM_NAME.test(name);
}

/**
 * 호출 시그니처의 파라미터 이름 목록. 컴파일러의 시그니처 심볼은 레이블드 튜플 rest
 * (`...args: [key: string, value: string]`)를 `args_0`/`args_1`로 펼치므로, 선언 노드를
 * 직접 읽어 튜플 레이블을 꺼낸다. 레이블이 없는 요소는 자리표시자로 남긴다.
 */
function signatureParamNames(sig: Signature): string[] {
  const decl = sig.getDeclaration();
  const declParams = 'getParameters' in decl ? decl.getParameters() : undefined;
  if (!declParams) return sig.getParameters().map(p => p.getName());

  const names: string[] = [];
  for (const param of declParams) {
    const tuple = param.isRestParameter() ? param.getTypeNode()?.asKind(SyntaxKind.TupleType) : undefined;
    if (!tuple) {
      names.push(param.getName());
      continue;
    }
    tuple.getElements().forEach((el, i) => {
      const named = el.asKind(SyntaxKind.NamedTupleMember);
      names.push(named ? named.getName() : `args_${i}`);
    });
  }
  return names;
}

/**
 * 번들에서 API의 첫 호출 시그니처 파라미터 이름 목록을 찾는다. 못 찾으면 undefined.
 * 네임스페이스 API는 `declare const NS: { method: ... }`의 프로퍼티, 최상위 API는
 * 함수 선언 또는 변수 선언의 호출 시그니처를 읽는다.
 */
function findBundledParamNames(api: ParsedAPI, bundle: SourceFile): string[] | undefined {
  const exported = bundle.getExportedDeclarations();
  try {
    if (api.namespace) {
      const varDecl = exported
        .get(api.namespace)
        ?.find(d => d.getKind() === SyntaxKind.VariableDeclaration)
        ?.asKind(SyntaxKind.VariableDeclaration);
      if (!varDecl) return undefined;
      const prop = varDecl.getType().getProperty(api.originalName);
      const sig = prop?.getTypeAtLocation(varDecl).getCallSignatures()[0];
      return sig && signatureParamNames(sig);
    }

    const decl = exported.get(api.originalName)?.[0];
    if (!decl) return undefined;
    const fn = decl.asKind(SyntaxKind.FunctionDeclaration);
    if (fn) return signatureParamNames(fn.getSignature());
    const sig = decl.getType().getCallSignatures()[0];
    return sig && signatureParamNames(sig);
  } catch {
    // 재귀 타입 등으로 컴파일러가 실패하면 이름 복구만 포기한다 (파싱 결과는 그대로).
    return undefined;
  }
}

/**
 * `args_N` 파라미터를 가진 API의 이름을 번들 선언의 실제 이름으로 되돌린다.
 *
 * 개수가 다르거나, 실제 이름이 식별자가 아니거나(구조 분해 등) 여전히 `args_N`이거나
 * 서로 겹치면 해당 API는 건드리지 않는다 (잘못된 이름보다 `args_N`이 안전).
 * 복구 전 이름은 typeNameSeed에 남겨 `<Api><Param>` 타입명이 바뀌지 않게 한다.
 *
 * @returns 이름이 바뀐 API 수
 */
export function recoverParameterNames(apis: ParsedAPI[], bundle: SourceFile): number {
  let recovered = 0;
  for (const api of apis) {
    if (!api.parameters.some(p => isGeneratedParamName(p.name))) continue;

    const names = findBundledParamNames(api, bundle);
    if (!names || names.length !== api.parameters.length) continue;
    if (names.some(n => !IDENTIFIER.test(n) || isGeneratedParamName(n))) continue;
    if (new Set(names).size !== names.length) continue;

    api.parameters.forEach((param, i) => {
      if (!isGeneratedParamName(param.name)) return;
      param.typeNameSeed = param.name;
      param.name = names[i];
    });
    recovered++;
  }
  return recovered;
}
