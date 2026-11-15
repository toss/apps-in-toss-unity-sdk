// @ts-check
// CI에서 E2E 스위트를 여러 job으로 나눠 돌리기 위한 샤드 선택.
// AIT_E2E_SHARD가 비어 있으면 전부 실행한다(로컬 ./run-local-tests.sh 동작 그대로).
//
//   core          : playerprefs를 뺀 나머지 전부 (결과 파일·벤치마크도 이 샤드만 의미 있다)
//   playerprefs-1 : 9번대 일부
//   playerprefs-2 : 9번대 나머지
//
// playerprefs 케이스는 한 번 부팅한 뒤 같은 상태를 이어받는 묶음이 있어 묶음을 쪼개면 안 된다:
//   9-1 → 9-2 (mockPage), 9-3 → 9-4 (failPage), 9-8 → 9-8b, 9-11 (pp8LegacySeed)
// 두 그룹은 2021.3 실측 시간(합 55.5분)을 기준으로 거의 반씩 나눴다(각 약 28분).

/** @type {Record<string, string[]>} */
export const PLAYERPREFS_GROUPS = {
  'playerprefs-1': ['9-1', '9-2', '9-5', '9-8', '9-8b', '9-11', '9-12'],
  'playerprefs-2': ['9-3', '9-4', '9-6', '9-7', '9-9', '9-10'],
};

const VALID = ['all', 'core', ...Object.keys(PLAYERPREFS_GROUPS)];

/** @returns {string} 'all' | 'core' | 'playerprefs-N' */
export function getShard() {
  const raw = (process.env.AIT_E2E_SHARD || '').trim();
  if (raw === '') return 'all';
  if (!VALID.includes(raw)) {
    throw new Error(`AIT_E2E_SHARD="${raw}" is invalid. Use one of: ${VALID.join(', ')} (or leave unset)`);
  }
  return raw;
}

export function shouldRunCore() {
  const s = getShard();
  return s === 'all' || s === 'core';
}

export function shouldRunPlayerPrefs() {
  const s = getShard();
  return s === 'all' || s in PLAYERPREFS_GROUPS;
}

/**
 * 제목이 '9-N.'으로 시작하는 케이스가 현재 샤드에 속하는지.
 * @param {string} title
 */
export function playerPrefsCaseSelected(title) {
  const s = getShard();
  if (s === 'all') return true;
  const group = PLAYERPREFS_GROUPS[s];
  if (!group) return false;
  const m = /^(9-\d+b?)\./.exec(title);
  if (!m) throw new Error(`PlayerPrefs case title must start with "9-N.": ${title}`);
  return group.includes(m[1]);
}
