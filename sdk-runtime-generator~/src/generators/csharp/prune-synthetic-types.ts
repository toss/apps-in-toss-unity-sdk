/**
 * 생성기가 API 시그니처를 따라가다 임시로 지은 이름(`DataType`, `...OnEventParamData` 등)의 클래스 중
 * 아무 곳에서도 쓰이지 않는 것을 지운다.
 *
 * keepNames(이전 SDK가 공개하던 이름)는 참조가 없어도 남긴다.
 */
import type { GeneratedTypeUnit } from '../../types.js';

const WORD = /[A-Za-z_][A-Za-z0-9_]*/g;

/**
 * 식별자 세기에서 주석과 문자열 리터럴은 뺀다. 카테고리 이름(`[APICategory("Environment")]`)이나
 * 문서 주석에 같은 단어가 있다고 타입이 쓰이는 것으로 보면 안 된다.
 */
function stripNoise(text: string): string {
  return text.replace(/"(?:[^"\\\n]|\\.)*"/g, '""').replace(/\/\/[^\n]*/g, '');
}

function wordCounts(text: string): Map<string, number> {
  const counts = new Map<string, number>();
  for (const word of stripNoise(text).match(WORD) ?? []) {
    counts.set(word, (counts.get(word) ?? 0) + 1);
  }
  return counts;
}

export function pruneUnreferencedSyntheticTypes(
  units: readonly GeneratedTypeUnit[],
  declaredNames: ReadonlySet<string>,
  otherTexts: readonly string[],
): { kept: GeneratedTypeUnit[]; pruned: string[] } {
  let kept = [...units];
  const pruned: string[] = [];
  const otherCounts = wordCounts(otherTexts.join('\n'));

  for (;;) {
    const counts = wordCounts(kept.map(u => u.code).join('\n'));
    const removable = kept.filter(unit => {
      if (declaredNames.has(unit.name)) return false;
      // 선언부에서 한 번 나오고, 다른 곳에서는 나오지 않는 이름
      const uses = (counts.get(unit.name) ?? 0) + (otherCounts.get(unit.name) ?? 0);
      return uses <= 1;
    });
    if (removable.length === 0) break;
    const names = new Set(removable.map(u => u.name));
    pruned.push(...names);
    kept = kept.filter(u => !names.has(u.name));
  }
  return { kept, pruned };
}
