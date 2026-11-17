/**
 * jslib 호출 인자 검증.
 *
 * jslib 함수의 매개변수는 Emscripten이 넘긴 정수(문자열 포인터)다. 웹 API를 호출할 때 매개변수 이름을
 * 그대로 인자로 쓰면 포인터 숫자가 웹 API로 넘어간다. 인자는 UTF8ToString/JSON.parse 등으로 변환해야 한다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PLUGINS = path.resolve(__dirname, '../../..', 'Runtime/SDK/Plugins');

/** 최상위 쉼표로 인자를 나눈다 */
function splitArgs(argText: string): string[] {
  const args: string[] = [];
  let depth = 0;
  let quote = '';
  let current = '';
  for (const ch of argText) {
    if (quote) {
      current += ch;
      if (ch === quote) quote = '';
      continue;
    }
    if (ch === '"' || ch === "'") {
      quote = ch;
      current += ch;
    } else if ('([{'.includes(ch)) {
      depth++;
      current += ch;
    } else if (')]}'.includes(ch)) {
      depth--;
      current += ch;
    } else if (ch === ',' && depth === 0) {
      args.push(current.trim());
      current = '';
    } else {
      current += ch;
    }
  }
  if (current.trim()) args.push(current.trim());
  return args;
}

/** `window.AppsInToss....(` 호출의 괄호 안 텍스트를 모두 찾는다 */
function appsInTossCallArgs(body: string): string[] {
  const results: string[] = [];
  const start = /window\.AppsInToss(?:\.\w+)+\(/g;
  let m: RegExpExecArray | null;
  while ((m = start.exec(body)) !== null) {
    let depth = 1;
    let i = start.lastIndex;
    for (; i < body.length && depth > 0; i++) {
      if (body[i] === '(') depth++;
      else if (body[i] === ')') depth--;
    }
    results.push(body.slice(start.lastIndex, i - 1));
  }
  return results;
}

describe('jslib 호출 인자', () => {
  const files = fs.readdirSync(PLUGINS).filter(f => f.endsWith('.jslib'));

  test('웹 API 호출 인자로 매개변수 이름을 그대로 넘기지 않는다', () => {
    const offenders: string[] = [];
    let checked = 0;
    for (const file of files) {
      const text = fs.readFileSync(path.join(PLUGINS, file), 'utf-8');
      const funcs = [...text.matchAll(/(__\w+_Internal)\s*:\s*function\s*\(([^)]*)\)\s*\{/g)];
      funcs.forEach((fn, idx) => {
        const params = new Set(fn[2].split(',').map(p => p.trim()).filter(Boolean));
        const end = idx + 1 < funcs.length ? funcs[idx + 1].index! : text.length;
        const body = text.slice(fn.index! + fn[0].length, end);
        for (const argText of appsInTossCallArgs(body)) {
          for (const arg of splitArgs(argText)) {
            checked++;
            if (params.has(arg)) offenders.push(`${file}:${fn[1]} -> ${arg}`);
          }
        }
      });
    }
    expect(checked).toBeGreaterThan(0);
    expect(offenders).toEqual([]);
  });

  test('splitArgs는 괄호와 문자열 안의 쉼표를 나누지 않는다', () => {
    expect(splitArgs('a, f(b, c), "x,y", {k: [1, 2]}')).toEqual(['a', 'f(b, c)', '"x,y"', '{k: [1, 2]}']);
  });
});
