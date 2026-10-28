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
});
