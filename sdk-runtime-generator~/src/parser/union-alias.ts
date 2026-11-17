/**
 * 객체 멤버가 있는 union 타입의 이름 해석.
 *
 * 번들 d.ts는 같은 union을 함수 시그니처에서 인라인으로 펼쳐 쓰기도 한다
 * (예: grantPromotionRewardForGame의 반환형은 GrantPromotionRewardResult를 풀어 쓴 형태).
 * 인라인 union은 이름이 없어 C#에서 멤버별로 쪼개지거나 object로 무너지므로, 번들에 선언된
 * type alias 중 멤버 구성이 같은 것을 찾아 그 이름을 쓴다. 번들 롤업이 붙인 `$1` 같은
 * 접미사는 제거한다.
 */
import type { SourceFile } from 'ts-morph';

const aliasBySignature = new Map<string, string>();

function memberSignature(member: any): string | undefined {
  const text: string = member.getText();
  if (text === 'undefined' || text === 'null') return undefined;
  if (member.isStringLiteral?.() || member.isNumberLiteral?.() || member.isBooleanLiteral?.()) return text;
  if (member.isObject?.()) {
    const props = member.getProperties().map((symbol: any) => {
      const decl = symbol.getDeclarations()[0];
      const optional = decl?.hasQuestionToken?.() ? '?' : '';
      return `${symbol.getName()}${optional}:${decl ? decl.getType().getText() : 'unknown'}`;
    });
    return `{${props.sort().join(';')}}`;
  }
  return text;
}

/** union의 멤버 구성 서명. 객체 멤버가 없으면 undefined (문자열 리터럴 union 등은 enum 경로가 처리한다) */
function unionSignature(type: any): string | undefined {
  const members: any[] = type.getUnionTypes?.() ?? [];
  if (members.length < 2) return undefined;
  if (!members.some(m => m.isObject?.() && !m.isArray?.())) return undefined;
  const sigs = members.map(memberSignature).filter((s): s is string => s !== undefined);
  return sigs.sort().join('|');
}

/** 번들의 union type alias를 서명별로 등록한다 (TypeScriptParser 생성 시 한 번 호출) */
export function registerUnionAliases(sourceFile: SourceFile): void {
  aliasBySignature.clear();
  for (const alias of sourceFile.getTypeAliases()) {
    const signature = unionSignature(alias.getType());
    if (!signature) continue;
    const cleanName = alias.getName().replace(/\$\d+$/, '');
    const hasSuffix = alias.getName() !== cleanName;
    const existing = aliasBySignature.get(signature);
    // 접미사 없는(= 공개 export 이름인) alias를 우선한다
    if (!existing || (!hasSuffix && existing.includes('$'))) {
      aliasBySignature.set(signature, hasSuffix ? `${cleanName}$` : cleanName);
    }
  }
}

/** 인라인 union이 번들 alias와 같은 구성이면 alias 이름을 돌려준다 */
export function lookupUnionAlias(type: any): string | undefined {
  // 선언된 alias를 그대로 쓰는 union은 TS가 이미 이름을 붙여 준다 (구조가 같은 다른 alias로 바꾸지 않는다)
  if (type.getAliasSymbol?.()) return undefined;
  const signature = unionSignature(type);
  if (!signature) return undefined;
  const name = aliasBySignature.get(signature);
  return name?.replace(/\$$/, '');
}
