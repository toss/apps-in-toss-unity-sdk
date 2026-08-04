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
//
// ── 대규모 CFF 서브셋 조용한 외곽선 드롭 방어 ──
//   subset-font(harfbuzz wasm) 는 대규모 서브셋(글리프 ~40k 이상, CFF 출력 ~11MB 추정 — wasm 메모리
//   상한 부근)에서 CFF 테이블을 조용히 드롭하고도 성공을 반환하는 버그가 있다(설치본/최신본 모두
//   재현, 업그레이드로 해결 불가). 결과물은 sfnt 버전만 바뀌고 외곽선 테이블(glyf/CFF/CFF2)이 전혀
//   없는 깨진 폰트가 되어 Unity FreeType 임포트가 실패하거나 조용히 tofu 가 된다. 이를 막기 위해
//   1차 서브셋 결과의 sfnt 테이블 디렉토리를 직접 파싱해 외곽선 테이블 존재를 검증하고, 없으면
//   `noLayoutClosure: true`(GSUB/GPOS closure 생략)로 1회 재시도한다 — 실측상 이 옵션이 wasm 메모리
//   사용량을 낮춰 동일 대규모 서브셋도 정상 산출한다. 재시도까지 실패하면 exit 3 으로 명확히 실패시켜
//   깨진 폰트가 조용히 빌드에 섞이지 않게 한다.

import { readFile, writeFile } from 'node:fs/promises';

// sfnt 테이블 디렉토리에서 지정 태그 존재 여부를 확인한다. 헤더 12바이트(sfntVersion u32 +
// numTables u16 + searchRange/entrySelector/rangeShift 각 u16) 이후 레코드가 16바이트씩
// (tag 4바이트 + checkSum/offset/length 각 u32) 이어진다. 잘리거나 쓰레기 입력이면 false(예외 없음).
function sfntHasTable(buf, tag) {
  if (!buf || buf.length < 12) return false;
  const numTables = buf.readUInt16BE(4);
  const tagBytes = Buffer.from(tag, 'ascii');
  for (let i = 0; i < numTables; i++) {
    const recPos = 12 + i * 16;
    if (recPos + 16 > buf.length) return false;
    if (buf.compare(tagBytes, 0, 4, recPos, recPos + 4) === 0) {
      return true;
    }
  }
  return false;
}

// glyf(TrueType) 또는 CFF/CFF2(OpenType CFF) 중 하나라도 있으면 외곽선 보유로 판정.
function sfntHasOutline(buf) {
  return sfntHasTable(buf, 'glyf') || sfntHasTable(buf, 'CFF ') || sfntHasTable(buf, 'CFF2');
}

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
    let out = await subsetFont(input, text, { targetFormat: 'truetype' });
    let retriedNoLayoutClosure = false;

    if (!sfntHasOutline(out)) {
      // 1차 결과에 외곽선 테이블이 없음 → 대규모 CFF 서브셋 드롭 의심(상단 주석 참조). noLayoutClosure
      // 로 1회 재시도.
      retriedNoLayoutClosure = true;
      out = await subsetFont(input, text, { targetFormat: 'truetype', noLayoutClosure: true });

      if (!sfntHasOutline(out)) {
        fail(3, 'subset 산출물에 외곽선 테이블(glyf/CFF/CFF2)이 없습니다(noLayoutClosure 재시도 후에도) — ' +
          'harfbuzz wasm 대규모 서브셋 조용한 드롭 의심(코드포인트 수: ' + cps.length + ')');
      }
    }

    await writeFile(outPath, out);
    process.stdout.write(JSON.stringify({
      ok: true,
      codepoints: cps.length,
      inBytes: input.length,
      outBytes: out.length,
      retriedNoLayoutClosure,
    }) + '\n');
    process.exit(0);
  } catch (e) {
    fail(3, 'subset 실패: ' + (e && e.message ? e.message : String(e)));
  }
})();
