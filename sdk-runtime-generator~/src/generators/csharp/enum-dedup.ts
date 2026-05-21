/**
 * Inline string-literal-union enum의 중복을 named enum으로 흡수 (또는 같은 값 셋의 다른 inline enum끼리 합치기).
 *
 * 배경: TS API 시그니처가 inline string union을 쓰면 함수마다 별도 enum이 생성된다
 *   (예: GetPermissionPermissionName, OpenPermissionDialogPermissionName, RequestPermissionPermissionName).
 *   이들은 값 셋이 모두 같지만 generator는 그 사실을 알 수 없어 중복을 emit.
 *   원본 SDK에 named `PermissionName` enum이 따로 있으면 그쪽으로 alias해 중복 제거.
 *
 * 알고리즘:
 *   1) 각 inline enum의 값 셋(set semantics)을 key로 정렬+조인.
 *   2) 같은 key의 named enum이 있으면 그 이름으로 alias.
 *   3) 그 외에는 같은 key가 이미 등장한 inline enum 이름으로 alias.
 *   4) alias 결과를 string rewrite로 모든 사용처에 반영 (word-boundary).
 */

/** 값 셋을 정렬 후 공백 조인 — 순서 무시 비교용 */
function valueSetKey(values: string[]): string {
  return [...values].sort().join(' ');
}

export interface EnumAliasResult {
  /** 결과적으로 emit해야 할 inline enum 이름과 값들 (alias된 enum은 빠짐, 등장 순서 보존) */
  emit: Array<{ name: string; values: string[] }>;
  /** aliasFrom → aliasTo (rewrite용) */
  aliases: Map<string, string>;
}

/**
 * Inline enum 목록을 named enum 값셋과 비교하여 alias 계획을 만든다.
 * @param inlineEnums 등장 순서가 보존된 inline enum의 Map (이름 → 값 배열).
 * @param parsedStringEnumValues 문자열 named enum의 Map. 값 셋이 일치하면 이쪽 이름으로 alias.
 */
export function computeEnumAliases(
  inlineEnums: Map<string, string[]>,
  parsedStringEnumValues?: Map<string, string[]>
): EnumAliasResult {
  const aliases = new Map<string, string>();
  const emit: Array<{ name: string; values: string[] }> = [];

  const namedByKey = new Map<string, string>();
  if (parsedStringEnumValues) {
    for (const [name, values] of parsedStringEnumValues) {
      if (values.length === 0) continue;
      const key = valueSetKey(values);
      // 첫 등장만 사용 (named 끼리 중복이 있어도 안정적)
      if (!namedByKey.has(key)) {
        namedByKey.set(key, name);
      }
    }
  }

  const seenInlineByKey = new Map<string, string>();
  for (const [name, values] of inlineEnums) {
    if (values.length === 0) continue;
    const key = valueSetKey(values);

    const namedMatch = namedByKey.get(key);
    if (namedMatch && namedMatch !== name) {
      aliases.set(name, namedMatch);
      continue;
    }

    const inlineMatch = seenInlineByKey.get(key);
    if (inlineMatch && inlineMatch !== name) {
      aliases.set(name, inlineMatch);
      continue;
    }
    seenInlineByKey.set(key, name);
    emit.push({ name, values });
  }

  return { emit, aliases };
}

/**
 * Alias map대로 본문 코드의 식별자를 word-boundary 치환.
 * 더 긴 이름부터 치환해 substring 충돌(예: PermissionName ⊂ GetPermissionPermissionName) 방지.
 */
export function applyEnumAliases(body: string, aliases: Map<string, string>): string {
  if (aliases.size === 0) return body;
  const ordered = Array.from(aliases.entries()).sort((a, b) => b[0].length - a[0].length);
  let out = body;
  for (const [from, to] of ordered) {
    const escaped = from.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    out = out.replace(new RegExp(`\\b${escaped}\\b`, 'g'), to);
  }
  return out;
}
