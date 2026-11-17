/**
 * 공개 C# 타입 이름 고정 검증.
 *
 * 3.x d.ts가 이름을 바꾼 타입도 SDK에 공개돼 있던 이름을 유지해야 한다 (type-name-pins.ts).
 * 생성된 Runtime/SDK 전체를 기준으로 확인한다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  TYPE_NAME_PINS,
  REMOVED_PUBLIC_TYPE_NAMES,
  DEPRECATED_TYPE_SHIMS,
  buildDeprecatedShimUnits,
  applyTypeNamePins,
  applyTypeNamePinsToUnits,
} from '../../src/generators/csharp/type-name-pins.js';
import { PUBLIC_TYPE_NAMES } from '../../src/generators/csharp/public-type-names.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

function generatedTypeNames(): Set<string> {
  const names = new Set<string>();
  for (const file of fs.readdirSync(SDK_DIR)) {
    if (!file.endsWith('.cs')) continue;
    const text = fs.readFileSync(path.join(SDK_DIR, file), 'utf-8');
    for (const m of text.matchAll(/public (?:sealed |static |partial |abstract )*(?:class|enum|struct|interface) (\w+)/g)) {
      names.add(m[1]);
    }
  }
  return names;
}

describe('공개 타입 이름 pin', () => {
  const generated = generatedTypeNames();

  test('기존 SDK의 공개 타입 이름은 모두 생성 결과에 있거나 제거 목록에 사유와 함께 있다', () => {
    const removed = new Set(REMOVED_PUBLIC_TYPE_NAMES);
    const missing = PUBLIC_TYPE_NAMES.filter(name => !generated.has(name) && !removed.has(name));
    expect(missing).toEqual([]);
  });

  test('제거 목록의 이름은 생성 결과에 없다', () => {
    const resurrected = REMOVED_PUBLIC_TYPE_NAMES.filter(name => generated.has(name));
    expect(resurrected).toEqual([]);
  });

  test('pin의 결과 이름은 모두 기존 공개 이름이다', () => {
    const publicNames = new Set(PUBLIC_TYPE_NAMES);
    const unknown = Object.values(TYPE_NAME_PINS).filter(name => !publicNames.has(name));
    expect(unknown).toEqual([]);
  });

  test('pin의 원래 이름은 생성 결과에 남지 않는다', () => {
    const leaked = Object.keys(TYPE_NAME_PINS).filter(name => generated.has(name));
    expect(leaked).toEqual([]);
  });

  test('두 pin이 같은 이름으로 모이지 않는다', () => {
    const values = Object.values(TYPE_NAME_PINS);
    expect(new Set(values).size).toBe(values.length);
  });

  test('식별자 단위로만 바꾼다', () => {
    const [from, to] = Object.entries(TYPE_NAME_PINS)[0];
    expect(applyTypeNamePins(`public ${from} X; ${from}Extra ${from}`)).toBe(`public ${to} X; ${from}Extra ${to}`);
  });

  test('pin 결과 이름이 이미 있는 유닛은 본문이 같으면 하나로 합친다', () => {
    const units = [
      { name: 'LoadAppsInTossAdmobResult', code: '/// a\npublic class LoadAppsInTossAdmobResult { public string X; }' },
      { name: 'AdMobLoadResult', code: '/// b\npublic class AdMobLoadResult { public string X; }' },
    ];
    const result = applyTypeNamePinsToUnits(units);
    expect(result.map(u => u.name)).toEqual(['AdMobLoadResult']);
  });

  test('pin 결과 이름이 이미 있는 유닛의 본문이 다르면 실패한다', () => {
    const units = [
      { name: 'LoadAppsInTossAdmobResult', code: 'public class LoadAppsInTossAdmobResult { public string X; }' },
      { name: 'AdMobLoadResult', code: 'public class AdMobLoadResult { public string Y; }' },
    ];
    expect(() => applyTypeNamePinsToUnits(units)).toThrow(/pin 충돌/);
  });

  test('옛 이름 하위 클래스는 모두 공개 이름이고, 기반 타입이 생성되며, Obsolete로 표시된다', () => {
    const publicNames = new Set(PUBLIC_TYPE_NAMES);
    const text = fs
      .readdirSync(SDK_DIR)
      .filter(f => f.endsWith('.cs'))
      .map(f => fs.readFileSync(path.join(SDK_DIR, f), 'utf-8'))
      .join('\n');
    for (const shim of DEPRECATED_TYPE_SHIMS) {
      expect(publicNames.has(shim.name), `${shim.name}은 기존 공개 이름이어야 한다`).toBe(true);
      expect(generated.has(shim.base), `${shim.name}의 기반 ${shim.base}가 생성돼야 한다`).toBe(true);
      expect(text).toMatch(new RegExp(`\\[Obsolete\\("[^"]*"\\)\\]\\s+public class ${shim.name} : ${shim.base}\\b`));
    }
  });

  test('옛 이름 하위 클래스는 제거 목록과 겹치지 않는다', () => {
    const removed = new Set(REMOVED_PUBLIC_TYPE_NAMES);
    expect(DEPRECATED_TYPE_SHIMS.filter(shim => removed.has(shim.name))).toEqual([]);
  });

  test('기반 타입이 없으면 하위 클래스 생성이 중단된다', () => {
    expect(() => buildDeprecatedShimUnits([{ name: 'Other', code: 'class Other {}', category: 'Other' }]))
      .toThrow(/기반 타입/);
  });

  test('공개 타입 이름은 System, UnityEngine, System.Collections.Generic의 타입과 겹치지 않는다', () => {
    const reserved = new Set([
      'Environment', 'Action', 'Func', 'Object', 'String', 'Type', 'Random', 'Exception', 'Delegate', 'Math',
      'Version', 'Uri', 'Task', 'Attribute', 'Buffer', 'Convert', 'Array', 'Enum', 'Guid', 'Nullable',
      'Application', 'Debug', 'Screen', 'Input', 'Time', 'Camera', 'Location', 'Handheld', 'Resources',
      'List', 'Dictionary', 'HashSet', 'Queue', 'Stack',
    ]);
    // Location 은 3.x 가 선언한 이름이고 AppsInToss 네임스페이스 안에서만 쓰여 UnityEngine 의 LocationService 와 다르다.
    const allowed = new Set(['Location']);
    const collisions = [...generated].filter(name => reserved.has(name) && !allowed.has(name));
    expect(collisions).toEqual([]);
  });

  test('생성된 공개 타입은 어디선가 쓰이거나 기존 공개 이름이다', () => {
    const publicNames = new Set(PUBLIC_TYPE_NAMES);
    const text = fs
      .readdirSync(SDK_DIR)
      .filter(f => f.endsWith('.cs'))
      .map(f => fs.readFileSync(path.join(SDK_DIR, f), 'utf-8'))
      // 선언 줄과 주석, 문자열은 사용으로 세지 않는다
      .join('\n')
      .replace(/\/\/.*$/gm, '')
      .replace(/"(?:[^"\\]|\\.)*"/g, '""');
    const unused = [...generated].filter(name => {
      if (publicNames.has(name)) return false;
      const uses = text.match(new RegExp(`\\b${name}\\b`, 'g'))?.length ?? 0;
      return uses <= 1; // 선언 1회뿐
    });
    // API 시그니처가 직접 쓰는 최상위 타입(Awaitable<...> 반환 등)은 위 계산에 걸리지 않는다.
    expect(unused).toEqual([]);
  });
});
