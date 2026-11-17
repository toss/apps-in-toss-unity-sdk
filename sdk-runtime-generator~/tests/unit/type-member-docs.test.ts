/**
 * 타입 멤버 설명이 3.x 전환 뒤에도 남아 있는지 확인한다.
 * tests/fixtures/documented-type-members.json 은 이전 SDK에서 설명이 있던 멤버 목록이다.
 * 3.x d.ts에 설명이 없으면 src/doc-fallbacks.ts 의 TYPE_MEMBER_DOC_FALLBACKS 에 옮겨 둔다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import { applyTypeMemberDocFallbacks, TYPE_MEMBER_DOC_FALLBACKS } from '../../src/doc-fallbacks.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');
const documented: string[] = JSON.parse(
  fs.readFileSync(path.join(__dirname, '../fixtures/documented-type-members.json'), 'utf-8'),
);

/** `Type.Member` -> 위에 summary가 있는지 */
function memberDocs(): Map<string, boolean> {
  const result = new Map<string, boolean>();
  for (const file of fs.readdirSync(SDK_DIR)) {
    if (!/^AIT\.Types\..*\.cs$/.test(file)) continue;
    const lines = fs.readFileSync(path.join(SDK_DIR, file), 'utf-8').split('\n');
    let cls = '';
    lines.forEach((line, i) => {
      const c = line.match(/^\s*public (?:class|enum) (\w+)/);
      if (c) {
        cls = c[1];
        return;
      }
      const m = line.match(/^ {8}public [\w<>[\],.? ]+ (\w+)(?: = [^;]+)?;/);
      if (!m || !cls) return;
      let has = false;
      for (let j = i - 1; j >= 0; j--) {
        const t = lines[j].trim();
        if (!t.startsWith('[') && !t.startsWith('///')) break;
        if (t.includes('<summary>')) has = true;
      }
      result.set(`${cls}.${m[1]}`, has);
    });
  }
  return result;
}

describe('타입 멤버 설명', () => {
  test('이전 SDK에서 설명이 있던 멤버는 지금도 설명이 있다', () => {
    const docs = memberDocs();
    const lost = documented.filter(key => docs.has(key) && !docs.get(key));
    expect(lost).toEqual([]);
  });

  test('폴백 키는 모두 생성된 멤버를 가리킨다', () => {
    const docs = memberDocs();
    expect(Object.keys(TYPE_MEMBER_DOC_FALLBACKS).filter(key => !docs.has(key))).toEqual([]);
  });

  test('폴백은 설명이 없는 필드에만 채우고, 이미 있으면 건드리지 않는다', () => {
    const code = `    public class LocationCoords
    {
        [Preserve]
        [JsonProperty("latitude")]
        public double Latitude;
        /// <summary>경도 원문</summary>
        [Preserve]
        [JsonProperty("longitude")]
        public double Longitude;
    }
`;
    const out = applyTypeMemberDocFallbacks(code);
    expect(out).toContain('/// <summary>위도</summary>\n        [Preserve]\n        [JsonProperty("latitude")]');
    expect(out).toContain('/// <summary>경도 원문</summary>');
    expect(out).not.toContain('<summary>경도</summary>');
    expect(applyTypeMemberDocFallbacks(out)).toBe(out);
  });
});
