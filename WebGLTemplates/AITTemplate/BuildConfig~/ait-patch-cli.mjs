#!/usr/bin/env node
// ait-patch-cli.mjs
//
// 빌드시점 CLI 패치 — @apps-in-toss/web-framework 3.x의 `ait build`가 .ait
// 메타데이터에 runtimeVersion을 emit하지 않는 upstream gap을 브릿지한다.
//
// 배경:
//   - 3.x `ait deploy`는 .ait 메타데이터의 runtimeVersion(non-empty)을 요구한다.
//   - 그러나 3.x `ait build`의 setMetadata 호출에는 runtimeVersion 키가 없어
//     항상 빈 문자열이 기록되고, deploy 게이트("ait 파일에 runtimeVersion이
//     없습니다")에 걸려 배포가 불가능하다.
//   - 사후에 .ait를 ait-format으로 재저장해 주입하면 zip 재압축/createdAtMs
//     재스탬프로 바이트가 달라져 서버가 "잘못된 앱 정보"로 거부함이 확인됐다
//     (폐기된 ait-inject-runtime-version.mjs 방식).
//   => 유일한 네이티브 경로: ait build를 호출하기 "직전"에 cwd의 web-framework
//      cli.js를 패치해 setMetadata가 runtimeVersion을 직접 emit하게 만든다.
//      빌드된 .ait 자체는 손대지 않으므로 봉인이 깨지지 않는다.
//
// runtimeVersion 값:
//   "0.84.0" — @apps-in-toss/cli@2.6.1의 RUNTIME_BUILD_DEFINITIONS[0].runtimeVersion.
//   2.6.1 경로(WEB 플랫폼 포함)가 서버에 실제로 기록·수락시키는 React Native 런타임
//   버전이다. 3.x WEB 서버가 동일 값을 수락하는지는 실제 ait deploy로 최종 확인해야
//   하며, 거부 시 deployment 응답에서 서버가 기대하는 값을 확인해 이 상수만 갱신하면
//   된다(beta-release.yml의 deployment GET 진단 참조).
//
// 안전 규칙 — 어떤 상황에서도 빌드를 막지 않는다(항상 정상 종료):
//   (a) 멱등        : setMetadata에 이미 runtimeVersion이 있으면 no-op.
//   (b) 버전 게이팅 : setMetadata 객체 리터럴을 못 찾으면(2.x 등) no-op.
//   (c) 구조 변화   : 패턴이 안 맞으면 경고만 남기고 no-op(빌드 진행).
//   (d) upstream 수정: upstream이 네이티브로 emit하면 (a)에 의해 자동 no-op.
//   (e) 스토어 보호 : pnpm 하드링크로 공유 store가 오염되지 않도록, 쓰기 전에
//                     하드링크를 끊고(unlink) 새 inode로 기록한다(Linux CI 필수).

import { readFileSync, writeFileSync, unlinkSync, realpathSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export const RUNTIME_VERSION = '0.84.0';
const TAG = '[ait-patch-cli]';
const CLI_REL_PATH = 'node_modules/@apps-in-toss/web-framework/dist/cli.js';

/**
 * web-framework cli.js 소스에서 setMetadata({...}) 객체 리터럴을 찾아
 * runtimeVersion 한 줄을 여는 `{` 직후에 주입한 새 소스를 반환한다.
 *
 * 삽입 지점은 항상 "여는 `{` 직후"이므로, 객체 본문의 brace 카운팅이 다소
 * 어긋나더라도 구문상 안전하다. brace 파서는 문자열/템플릿/주석 내부의 괄호를
 * 무시하므로 setMetadata 인자 안에 nested `})` 또는 `}` 문자가 있어도 오삽입하지
 * 않는다.
 *
 * @param {string} src
 * @param {string} runtimeVersion
 * @returns {{ changed: boolean, source: string, reason: string }}
 */
export function injectRuntimeVersion(src, runtimeVersion = RUNTIME_VERSION) {
  const obj = findSetMetadataObject(src);
  if (!obj) {
    return { changed: false, source: src, reason: 'setMetadata 객체 리터럴 없음 (2.x이거나 구조 변경)' };
  }
  const objText = src.slice(obj.start, obj.end + 1);
  if (/\bruntimeVersion\s*:/.test(objText)) {
    return { changed: false, source: src, reason: 'runtimeVersion 이미 존재 (멱등/upstream emit)' };
  }
  // 여는 `{`(obj.start) 직후에 runtimeVersion 한 줄 삽입 (다른 필드 보존).
  const insertAt = obj.start + 1;
  const injected = `\n    runtimeVersion: ${JSON.stringify(runtimeVersion)},`;
  const out = src.slice(0, insertAt) + injected + src.slice(insertAt);
  return { changed: true, source: out, reason: `runtimeVersion=${JSON.stringify(runtimeVersion)} 삽입` };
}

/**
 * `setMetadata(` 다음의 첫 객체 리터럴 `{...}`의 시작/끝 인덱스를 brace-depth로 찾는다.
 * 문자열/템플릿/주석 내부의 괄호는 무시한다.
 * @returns {{ start: number, end: number } | null}
 */
function findSetMetadataObject(src) {
  const call = /\bsetMetadata\s*\(/.exec(src);
  if (!call) return null;

  // `(` 다음 첫 비공백(주석 제외)이 `{` 인지 확인 — 객체 리터럴 인자가 아니면
  // (예: setMetadata(meta)) 안전하게 패치 불가로 처리한다.
  let i = skipTrivia(src, call.index + call[0].length);
  if (src[i] !== '{') return null;

  const start = i;
  let depth = 0;
  for (let j = start; j < src.length; j++) {
    const skipped = skipStringOrComment(src, j);
    if (skipped > j) { j = skipped - 1; continue; }
    const ch = src[j];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return { start, end: j };
    }
  }
  return null; // 괄호 불균형 — 안전 no-op
}

/** 공백 + 주석을 건너뛴 다음 인덱스를 반환. */
function skipTrivia(src, i) {
  for (;;) {
    while (i < src.length && /\s/.test(src[i])) i++;
    const next = skipComment(src, i);
    if (next === i) return i;
    i = next;
  }
}

/** i가 문자열/템플릿/주석 시작이면 그 끝 다음 인덱스를, 아니면 i를 반환. */
function skipStringOrComment(src, i) {
  const c = src[i];
  if (c === '"' || c === "'" || c === '`') return skipString(src, i);
  return skipComment(src, i);
}

function skipString(src, i) {
  const quote = src[i];
  let j = i + 1;
  while (j < src.length) {
    if (src[j] === '\\') { j += 2; continue; }
    if (src[j] === quote) return j + 1;
    j++;
  }
  return j; // 닫히지 않은 문자열 — 소스 끝까지
}

function skipComment(src, i) {
  if (src[i] === '/' && src[i + 1] === '/') {
    let j = i + 2;
    while (j < src.length && src[j] !== '\n') j++;
    return j;
  }
  if (src[i] === '/' && src[i + 1] === '*') {
    let j = i + 2;
    while (j < src.length && !(src[j] === '*' && src[j + 1] === '/')) j++;
    return Math.min(src.length, j + 2);
  }
  return i;
}

function main() {
  const cliPath = resolve(process.cwd(), CLI_REL_PATH);
  let realPath = cliPath;
  let src;
  try {
    // pnpm은 node_modules/@apps-in-toss/web-framework를 .pnpm 가상 스토어로
    // symlink하므로, 실제 파일 경로를 얻기 위해 realpath로 해석한다.
    realPath = realpathSync(cliPath);
    src = readFileSync(realPath, 'utf8');
  } catch (e) {
    // (b) cli.js 없음 — 2.x(granite) 경로이거나 미설치. no-op.
    console.warn(`${TAG} cli.js를 읽지 못함 — 패치 건너뜀(2.x이거나 미설치): ${e?.message ?? e}`);
    return;
  }

  const { changed, source, reason } = injectRuntimeVersion(src, RUNTIME_VERSION);
  if (!changed) {
    console.log(`${TAG} 패치 불필요 — ${reason}.`);
    return;
  }

  try {
    // (e) pnpm 하드링크로 연결된 공유 store 오염 방지: 하드링크를 끊고 새 inode로 기록.
    //     Linux CI에서 store(content-addressable)와 node_modules가 하드링크로 묶이므로,
    //     in-place 덮어쓰기는 다른 빌드가 공유하는 store 파일을 변조한다. unlink 후
    //     writeFile은 새 inode를 만들어 store와의 연결을 끊는다(macOS APFS는 reflink라 무관).
    try { unlinkSync(realPath); } catch { /* 링크가 없으면 그대로 덮어쓴다 */ }
    writeFileSync(realPath, source, 'utf8');
    console.log(`${TAG} ✓ cli.js setMetadata에 ${reason} 완료.`);
  } catch (e) {
    console.warn(`${TAG} cli.js 쓰기 실패 — 패치 건너뜀(빌드는 계속): ${e?.message ?? e}`);
  }
}

// 직접 실행될 때만 main()을 돌린다. import(단위 테스트) 시에는 실행하지 않는다.
const invokedPath = process.argv[1];
if (invokedPath && import.meta.url === pathToFileURL(invokedPath).href) {
  try {
    main();
  } catch (e) {
    // 최후의 안전망: 어떤 예외도 빌드를 막지 않는다.
    console.warn(`${TAG} 예기치 못한 오류 — 패치 건너뜀: ${e?.message ?? e}`);
  }
}
