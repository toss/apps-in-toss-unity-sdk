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
//   버전이다. 3.x WEB deploy 서버가 이 값을 수락함을 실제 ait deploy로 확인 완료했다
//   (2026-06-20 베타 probe: beta-release.yml deploy_probe_only 디스패치 → 3.x ait
//   build가 이 패치로 0.84.0을 주입한 .ait를 ait deploy가 정상 배포 생성, deploymentId
//   019ee488-… 발급). 향후 upstream이 기대 런타임 버전을 바꿔 거부가 재발하면, deployment
//   응답에서 서버가 기대하는 값을 확인해 이 상수만 갱신하면 된다(beta-release.yml의
//   deployment GET 진단 참조).
//
// isGame:
//   - 3.x `ait build`의 setMetadata 호출에는 isGame 키도 없어, @apps-in-toss/ait-format이
//     기본값 false를 기록한다. 따라서 Unity 게임의 .ait도 전부 isGame:false가 된다.
//   - runtimeVersion과 같은 setMetadata 객체에 isGame 키를 주입하되, 값은 패치 시점에
//     고정하지 않는다. 주입되는 코드는 `ait build` 실행 시점에 process.env.AIT_IS_GAME을
//     읽는다. 이유: (1) Unity가 빌드마다 환경변수로 webViewType(game/partner)을 넘기므로
//     설정이 바뀌어도 cli.js에 stale 값이 남지 않는다(패치는 멱등이라 재주입되지 않음).
//     (2) GraniteBuildRunner의 `exec node ait-patch-cli.mjs`와 package.json의
//     `build` 스크립트 두 경로 모두 같은 env를 상속하므로 별도 인자 전달이 필요 없다.
//   - 값 규칙: "false"(대소문자·공백 무시)만 false, 미설정/그 외 값은 true(이 SDK는
//     Unity 게임용이고 webViewType 기본값이 game이다). 패치 시점에 env 값을 로그로 남긴다.
//
// 안전 규칙 — 어떤 상황에서도 빌드를 막지 않는다(항상 정상 종료):
//   (a) 멱등        : setMetadata에 이미 runtimeVersion/isGame이 있으면 해당 키는 no-op.
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
 * `key: <exprSource>,` 한 줄을 여는 `{` 직후에 주입한 새 소스를 반환한다.
 * 객체에 key가 이미 있으면(키: 값, 단축 프로퍼티) no-op.
 *
 * 삽입 지점은 항상 "여는 `{` 직후"이므로, 객체 본문의 brace 카운팅이 다소
 * 어긋나더라도 구문상 안전하다. brace 파서는 문자열/템플릿/주석 내부의 괄호를
 * 무시하므로 setMetadata 인자 안에 nested `})` 또는 `}` 문자가 있어도 오삽입하지
 * 않는다.
 *
 * @param {string} src
 * @param {string} key
 * @param {string} exprSource  주입할 JS 표현식 소스
 * @param {string} describe    성공 reason에 쓸 설명
 * @returns {{ changed: boolean, source: string, reason: string }}
 */
export function injectMetadataKey(src, key, exprSource, describe) {
  const obj = findSetMetadataObject(src);
  if (!obj) {
    return { changed: false, source: src, reason: 'setMetadata 객체 리터럴 없음 (2.x이거나 구조 변경)' };
  }
  const objText = src.slice(obj.start, obj.end + 1);
  if (new RegExp(`\\b${key}\\s*[:,}]`).test(objText)) {
    return { changed: false, source: src, reason: `${key} 이미 존재 (멱등/upstream emit)` };
  }
  // 여는 `{`(obj.start) 직후에 한 줄 삽입 (다른 필드 보존).
  const insertAt = obj.start + 1;
  const injected = `\n    ${key}: ${exprSource},`;
  const out = src.slice(0, insertAt) + injected + src.slice(insertAt);
  return { changed: true, source: out, reason: describe };
}

/** setMetadata에 runtimeVersion 상수를 주입한다. */
export function injectRuntimeVersion(src, runtimeVersion = RUNTIME_VERSION) {
  return injectMetadataKey(
    src,
    'runtimeVersion',
    JSON.stringify(runtimeVersion),
    `runtimeVersion=${JSON.stringify(runtimeVersion)} 삽입`,
  );
}

/** ait build 실행 시점에 평가되는 isGame 표현식. "false"만 false, 나머지는 true. */
export const IS_GAME_EXPR = 'String(process.env.AIT_IS_GAME ?? "").trim().toLowerCase() !== "false"';

/** 패치 시점 env 값을 같은 규칙으로 해석한다(로그용). */
export function resolveIsGame(raw) {
  return String(raw ?? '').trim().toLowerCase() !== 'false';
}

/** setMetadata에 isGame(런타임 env 평가)을 주입한다. */
export function injectIsGame(src) {
  return injectMetadataKey(src, 'isGame', IS_GAME_EXPR, 'isGame=<AIT_IS_GAME 런타임 평가> 삽입');
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

  const rawIsGame = process.env.AIT_IS_GAME;
  const known = typeof rawIsGame === 'string' && ['true', 'false'].includes(rawIsGame.trim().toLowerCase());
  console.log(
    `${TAG} AIT_IS_GAME=${rawIsGame === undefined ? '(미설정)' : JSON.stringify(rawIsGame)} → isGame=${resolveIsGame(rawIsGame)}` +
      (known ? '' : ' (미설정/유효하지 않은 값 — 기본값 true)'),
  );

  const rv = injectRuntimeVersion(src, RUNTIME_VERSION);
  const ig = injectIsGame(rv.source);
  const source = ig.source;
  const changed = rv.changed || ig.changed;
  const reason = [rv, ig].filter((r) => r.changed).map((r) => r.reason).join(', ');
  if (!changed) {
    console.log(`${TAG} 패치 불필요 — ${rv.reason}; ${ig.reason}.`);
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
