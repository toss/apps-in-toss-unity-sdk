// Apps in Toss Unity SDK — 빌드 단계 폰트 subset 러너 (SDK 내장 Node.js 로 실행).
//
// AITFontSubsetProcessor 가 빌드 직전, 대상 .ttf/.otf 소스를 지정한 유니코드 범위만 남기도록
// subset 하여 Unity 가 .data 에 굽는 폰트 데이터를 급감시킨다. harfbuzz(hb-subset, wasm) 를
// 래핑한 `subset-font` 패키지를 사용한다(Google Fonts 와 동일 코덱).
//
// 사용:
//   node subset-font-runner.mjs <inputFont> <outputFont> <unicodeRanges>
//   unicodeRanges: 콤마 구분, fontTools 와 동일 표기. 예) "U+0020-007E,U+AC00-D7A3,U+1100-11FF"
//
// 종료코드: 0=성공, 2=잘못된 인자, 3=subset 실패. 결과(JSON 1줄)를 stdout 에 출력한다.

import { readFile, writeFile } from 'node:fs/promises';

function fail(code, msg) {
  process.stdout.write(JSON.stringify({ ok: false, error: msg }) + '\n');
  process.exit(code);
}

const [, , inPath, outPath, rangeSpec] = process.argv;
if (!inPath || !outPath || !rangeSpec) {
  fail(2, 'usage: node subset-font-runner.mjs <in> <out> <unicodeRanges>');
}

// "U+0020-007E,U+AC00-D7A3,..." → 보존할 코드포인트 배열.
function expandRanges(spec) {
  const cps = [];
  for (const raw of spec.split(',')) {
    const part = raw.trim().replace(/^u\+/i, '');
    if (!part) continue;
    if (part.includes('-')) {
      const [a, b] = part.split('-');
      const lo = parseInt(a, 16);
      const hi = parseInt(b, 16);
      if (Number.isNaN(lo) || Number.isNaN(hi) || hi < lo) continue;
      for (let c = lo; c <= hi; c++) cps.push(c);
    } else {
      const c = parseInt(part, 16);
      if (!Number.isNaN(c)) cps.push(c);
    }
  }
  return cps;
}

(async () => {
  let subsetFont;
  try {
    subsetFont = (await import('subset-font')).default;
  } catch (e) {
    fail(3, 'subset-font 모듈 로드 실패: ' + (e && e.message ? e.message : String(e)));
  }

  const cps = expandRanges(rangeSpec);
  if (cps.length === 0) {
    fail(2, '유효한 유니코드 범위가 없습니다: ' + rangeSpec);
  }

  // 코드포인트 → 보존 문자열(subset-font 의 text 인자). 대용량(수만 글자) 대비 청크 join.
  const parts = [];
  let buf = [];
  for (const c of cps) {
    buf.push(String.fromCodePoint(c));
    if (buf.length >= 4096) { parts.push(buf.join('')); buf = []; }
  }
  if (buf.length) parts.push(buf.join(''));
  const text = parts.join('');

  try {
    const input = await readFile(inPath);
    const out = await subsetFont(input, text, { targetFormat: 'truetype' });
    await writeFile(outPath, out);
    process.stdout.write(JSON.stringify({
      ok: true,
      codepoints: cps.length,
      inBytes: input.length,
      outBytes: out.length,
    }) + '\n');
    process.exit(0);
  } catch (e) {
    fail(3, 'subset 실패: ' + (e && e.message ? e.message : String(e)));
  }
})();
