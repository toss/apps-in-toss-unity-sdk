#!/usr/bin/env node
/**
 * Unity .meta GUID 위생 검사
 *
 * 배포(published UPM)되는 .meta 파일의 GUID에 대해 두 가지를 검사한다:
 *
 *   1) 중복 GUID  — 배포 대상 .meta 중 같은 guid를 쓰는 파일이 2개 이상이면 실패.
 *                   복붙/머지 사고로 인한 내부 충돌을 차단한다.
 *   2) 구조적 GUID — 손으로 박은 순차/저엔트로피 GUID(예: b1c2d3e4f5a6...)를 검출.
 *                   이런 값은 다른 패키지의 placeholder/순차 GUID와 충돌하기 쉽다
 *                   (techchat #4076: Runtime/Sentry.meta ↔ com.unity.addressables).
 *                   Unity가 생성하는 진짜 랜덤 GUID만 허용한다.
 *
 * 검사 범위(shipped)는 release.yml의 제거 규칙을 따른다:
 *   - "~" 접미 디렉토리(Tests~, sdk-runtime-generator~, scripts~, *BuildConfig~ 등) 제외
 *   - .github/ 제외
 *   - 릴리즈 시 제거되는 루트 파일(.gitignore.meta, CLAUDE.md.meta, run-local-tests.sh.meta) 제외
 *
 * 사용법:  node scripts/check-meta-guids.js
 * 종료코드: 위반이 하나라도 있으면 1, 없으면 0.
 *
 * 알려진 한계: 휴리스틱은 "명백히 구조적인" 손-작성 GUID를 잡는다. 우연히 랜덤처럼
 * 보이게 손으로 친 고엔트로피 GUID는 검출하지 못하지만, 그런 값은 진짜 랜덤 GUID와
 * 충돌 확률이 동일하므로 위험하지 않다. 외부 패키지와의 충돌 방지는 본질적으로
 * "GUID를 랜덤으로 생성"해야만 보장된다.
 */

"use strict";

const { execSync } = require("child_process");
const fs = require("fs");

// ---------------------------------------------------------------------------
// 구조적 GUID 판정 임계값
//   현 저장소 배포 메타(~200개 진짜 랜덤 GUID)에 대해 오탐 0,
//   기존 손-작성 가짜(순차/바이트등차/첫nibble순차/둘째nibble순환) 전부 검출로 보정됨.
//   임의의 균일 랜덤 GUID가 걸릴 확률은 합산 ~0.04%(약 1/2200) 수준.
// ---------------------------------------------------------------------------
const NIBBLE_RUN_MIN = 7; // 연속 nibble ±1(mod16) 최장 런
const BYTE_DELTA_RUN_MIN = 5; // 일정 delta 바이트 등차 최장 런 (예: +0x11 반복)
const FIRST_NIBBLE_RUN_MIN = 6; // 바이트 첫-nibble ±1(mod16) 최장 런
const SECOND_NIBBLE_PERIOD_MAX = 6; // 바이트 둘째-nibble 수열 완전 주기 (예: a,b,c,d,e,f 순환)

function isShipped(p) {
  if (p.split("/").some((seg) => seg.endsWith("~"))) return false;
  if (p.startsWith(".github/")) return false;
  if (
    p === ".gitignore.meta" ||
    p === "CLAUDE.md.meta" ||
    p === "run-local-tests.sh.meta"
  ) {
    return false;
  }
  return true;
}

function readGuid(file) {
  const text = fs.readFileSync(file, "utf8");
  const m = text.match(/^guid:\s*([0-9a-fA-F]{32})\s*$/m);
  return m ? m[1].toLowerCase() : null;
}

function nibbles(guid) {
  return Array.from(guid, (c) => parseInt(c, 16));
}
function bytesOf(guid) {
  const b = [];
  for (let i = 0; i < 32; i += 2) b.push(parseInt(guid.slice(i, i + 2), 16));
  return b;
}
function longestPm1Run(seq, mod) {
  let best = 1;
  let cur = 1;
  for (let i = 1; i < seq.length; i++) {
    const d = (((seq[i] - seq[i - 1]) % mod) + mod) % mod;
    if (d === 1 || d === mod - 1) {
      cur += 1;
      if (cur > best) best = cur;
    } else {
      cur = 1;
    }
  }
  return best;
}
function constDeltaRun(bytes) {
  let best = 1;
  for (let s = 0; s < bytes.length - 1; s++) {
    const d = (((bytes[s + 1] - bytes[s]) % 256) + 256) % 256;
    let r = 2;
    let k = s + 1;
    while (
      k + 1 < bytes.length &&
      (((bytes[k + 1] - bytes[k]) % 256) + 256) % 256 === d
    ) {
      r += 1;
      k += 1;
    }
    if (r > best) best = r;
  }
  return best;
}
function secondNibblePeriod(bytes) {
  const sn = bytes.map((x) => x & 0xf);
  for (let p = 1; p <= 6; p++) {
    let ok = true;
    for (let i = 0; i < sn.length; i++) {
      if (sn[i] !== sn[i % p]) {
        ok = false;
        break;
      }
    }
    if (ok) return p;
  }
  return 99;
}
function structuredReason(guid) {
  const nib = nibbles(guid);
  const bytes = bytesOf(guid);
  const fn = bytes.map((x) => x >> 4);
  if (longestPm1Run(nib, 16) >= NIBBLE_RUN_MIN) return "연속 nibble 순차";
  if (constDeltaRun(bytes) >= BYTE_DELTA_RUN_MIN) return "바이트 등차 수열";
  if (longestPm1Run(fn, 16) >= FIRST_NIBBLE_RUN_MIN) return "첫-nibble 순차";
  if (secondNibblePeriod(bytes) <= SECOND_NIBBLE_PERIOD_MAX)
    return "둘째-nibble 주기 반복";
  return null;
}

function main() {
  let files;
  try {
    files = execSync("git ls-files '*.meta'", { encoding: "utf8" })
      .split("\n")
      .filter(Boolean);
  } catch (e) {
    console.error("::error::git ls-files 실행 실패 — git 저장소에서 실행하세요.");
    process.exit(1);
  }

  const shipped = files.filter(isShipped);
  const byGuid = new Map(); // guid -> [files]
  const structured = []; // { file, guid, reason }
  const invalid = []; // guid 라인 없음/형식 오류

  for (const f of shipped) {
    const guid = readGuid(f);
    if (!guid) {
      invalid.push(f);
      continue;
    }
    if (!byGuid.has(guid)) byGuid.set(guid, []);
    byGuid.get(guid).push(f);
    const reason = structuredReason(guid);
    if (reason) structured.push({ file: f, guid, reason });
  }

  const duplicates = [...byGuid.entries()].filter(([, fs2]) => fs2.length > 1);

  let failed = false;

  if (invalid.length > 0) {
    failed = true;
    console.log("::error::유효한 32자리 hex guid가 없는 .meta 파일이 있습니다:");
    for (const f of invalid) console.log(`  - ${f}`);
    console.log("");
  }

  if (duplicates.length > 0) {
    failed = true;
    console.log("::error::중복 GUID가 발견되었습니다 (배포 대상 .meta끼리 충돌):");
    for (const [guid, fs2] of duplicates) {
      console.log(`  guid ${guid}:`);
      for (const f of fs2) console.log(`    - ${f}`);
    }
    console.log("");
  }

  if (structured.length > 0) {
    failed = true;
    console.log(
      "::error::손으로 박은 듯한 구조적/순차 GUID가 발견되었습니다 (충돌 위험):"
    );
    for (const { file, guid, reason } of structured) {
      console.log(`  - ${file}`);
      console.log(`      guid ${guid} (${reason})`);
    }
    console.log("");
    console.log("해결 방법: 해당 .meta의 guid를 새 랜덤 GUID로 교체하세요. 예:");
    console.log("  node -e \"console.log(require('crypto').randomBytes(16).toString('hex'))\"");
    console.log("");
  }

  if (failed) {
    process.exit(1);
  }

  console.log(
    `✓ 배포 대상 .meta ${shipped.length}개의 GUID 위생 검사 통과 (중복 없음, 구조적 GUID 없음).`
  );
}

main();
