/**
 * [Obsolete] 메시지: 문자열 리터럴 이스케이프와 대체 API 이름 검증.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import { escapeObsoleteMessage } from '../../src/generators/csharp/utils.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

function sdkText(): string {
  return fs
    .readdirSync(SDK_DIR)
    .filter(f => /^AIT\..*\.cs$/.test(f) && !f.startsWith('AIT.Types.'))
    .map(f => fs.readFileSync(path.join(SDK_DIR, f), 'utf-8'))
    .join('\n');
}

describe('escapeObsoleteMessage', () => {
  test('큰따옴표와 역슬래시를 이스케이프하고 줄바꿈은 공백으로 합친다', () => {
    expect(escapeObsoleteMessage('say "hi"\\now\nnext   line')).toBe('say \\"hi\\"\\\\now next line');
  });

  test('역슬래시를 먼저 이스케이프해 따옴표 이스케이프가 깨지지 않는다', () => {
    // 입력 `\"` -> 출력 `\\\"` (역슬래시 이스케이프 + 따옴표 이스케이프)
    expect(escapeObsoleteMessage('\\"')).toBe('\\\\\\"');
  });
});

describe('생성된 [Obsolete] 메시지', () => {
  const text = sdkText();

  test('"Use X"가 가리키는 API는 모두 SDK에 있다', () => {
    const declared = new Set(
      [...text.matchAll(/public static [^\n(]*?\b(\w+)\(/g)].map(m => m[1]),
    );
    const targets = [...text.matchAll(/\[Obsolete\("Use (\w+)"\)\]/g)].map(m => m[1]);
    expect(targets.length).toBeGreaterThan(0);
    expect(targets.filter(name => !declared.has(name))).toEqual([]);
  });

  test('Obsolete 문자열 안에 이스케이프되지 않은 큰따옴표가 없다', () => {
    const bad = [...text.matchAll(/\[Obsolete\("((?:[^"\\\n]|\\.)*)"\)\]/g)].length;
    const all = [...text.matchAll(/\[Obsolete\(/g)].length;
    expect(bad).toBe(all);
  });
});
