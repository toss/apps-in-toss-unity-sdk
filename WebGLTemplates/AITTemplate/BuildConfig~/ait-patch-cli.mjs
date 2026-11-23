#!/usr/bin/env node
// ait-patch-cli.mjs
//
// 빌드시점 CLI 패치 — @apps-in-toss/web-framework 3.x의 `ait build`가 .ait
// 메타데이터에 runtimeVersion/isGame을 emit하지 않는 upstream gap을 브릿지한다.
//
// 패치 대상 (3.1.0 ~ 3.9.0 전 버전에서 실측):
//   `ait` bin(web-framework/bin/ait.js)은 `import "@apps-in-toss/cli"`만 하고, 실제
//   `writer.setMetadata({...})` 호출은 @apps-in-toss/cli의 dist/index.js(buildArtifact)에 있다.
//   web-framework에는 dist/cli.js가 없다. 예전에는 web-framework/dist/cli.js만 패치 대상으로
//   삼았기 때문에, 이 파일이 사라진 뒤 패치가 "읽지 못함 — 건너뜀"으로 조용히 no-op이 되어
//   실제 번들의 isGame=false, runtimeVersion=""이 되었다.
//   => 대상 파일은 하드코딩하지 않고 프로젝트의 web-framework에서 의존 체인을 따라 찾는다
//      (resolvePatchCandidates). 대상이 없거나 앵커(setMetadata 객체 리터럴)가 없으면
//      조용히 넘어가지 않고 큰 경고를 남긴다. 빌드 후 실제 결과는 ait-verify-metadata.mjs가
//      .ait를 열어 확인한다(AIT_STRICT_METADATA=1이면 불일치 시 실패).
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
//   (c) 구조 변화   : 패턴이 안 맞으면 큰 경고를 남기고 no-op(빌드 진행).
//   (d) upstream 수정: upstream이 네이티브로 emit하면 (a)에 의해 자동 no-op.
//   (e) 스토어 보호 : pnpm 하드링크로 공유 store가 오염되지 않도록, 같은 디렉터리의 임시
//                     파일에 쓰고 rename으로 교체해 새 inode로 기록한다(하드링크가 끊긴다).
//                     쓰기가 실패해도 원본 파일은 남고, 파일 모드(실행 비트)는 보존한다.

import {
  readFileSync, writeFileSync, unlinkSync, realpathSync, renameSync, chmodSync, statSync, readdirSync,
} from 'node:fs';
import { basename, dirname, join, parse, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export const RUNTIME_VERSION = '0.84.0';
const TAG = '[ait-patch-cli]';
const WEB_FRAMEWORK = '@apps-in-toss/web-framework';
const CLI_PACKAGE = '@apps-in-toss/cli';

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
  const objs = findSetMetadataObjects(src);
  if (objs.length === 0) {
    return { changed: false, source: src, reason: 'setMetadata 객체 리터럴 없음 (2.x이거나 구조 변경)' };
  }
  // 뒤쪽 호출부부터 삽입해야 앞쪽 인덱스가 밀리지 않는다.
  let out = src;
  let count = 0;
  for (const obj of [...objs].reverse()) {
    if (hasTopLevelKey(src, obj, key)) continue;
    // 여는 `{`(obj.start) 직후에 한 줄 삽입 (다른 필드 보존).
    const insertAt = obj.start + 1;
    out = out.slice(0, insertAt) + `\n    ${key}: ${exprSource},` + out.slice(insertAt);
    count++;
  }
  if (count === 0) {
    return { changed: false, source: src, reason: `${key} 이미 존재 (멱등/upstream emit)` };
  }
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
 * 소스 안의 모든 `setMetadata({...})` 호출에서 첫 인자 객체 리터럴의 시작/끝 인덱스를
 * brace-depth로 찾는다. 문자열/템플릿/주석 내부의 괄호는 무시한다.
 * 인자가 객체 리터럴이 아닌 것(메서드 정의 `setMetadata(e){`, `setMetadata(meta)` 등)과
 * 괄호가 맞지 않는 것은 건너뛴다(안전 no-op).
 * @returns {Array<{ start: number, end: number }>}
 */
function findSetMetadataObjects(src) {
  const found = [];
  const callRe = /\bsetMetadata\s*\(/g;
  let call;
  while ((call = callRe.exec(src)) !== null) {
    // `(` 다음 첫 비공백(주석 제외)이 `{` 여야 객체 리터럴 인자다.
    const start = skipTrivia(src, call.index + call[0].length);
    if (src[start] !== '{') continue;
    const end = findMatchingBrace(src, start);
    if (end < 0) continue; // 괄호 불균형 — 안전 no-op
    found.push({ start, end });
    callRe.lastIndex = end + 1;
  }
  return found;
}

/** start의 `{`와 짝이 맞는 `}` 인덱스(없으면 -1). 문자열/주석은 건너뛴다. */
function findMatchingBrace(src, start) {
  let depth = 0;
  for (let j = start; j < src.length; j++) {
    const skipped = skipStringOrComment(src, j);
    if (skipped > j) { j = skipped - 1; continue; }
    const ch = src[j];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return j;
    }
  }
  return -1;
}

/**
 * 객체 리터럴의 "최상위" 프로퍼티에 key가 있는지 본다(`key:`, 단축 `key,`/`key}`,
 * 따옴표 키). 중첩 객체 안의 같은 이름 키는 무시한다.
 */
function hasTopLevelKey(src, obj, key) {
  let flat = '';
  let depth = 0;
  for (let j = obj.start; j <= obj.end; j++) {
    const skipped = skipStringOrComment(src, j);
    if (skipped > j) {
      // depth 1의 문자열은 `"key":` 형태의 따옴표 키일 때만 남긴다. 값 문자열은 버린다.
      if (depth === 1 && /^[\s]*:/.test(src.slice(skipped, skipped + 8)) && /^["'`]/.test(src[j])) {
        flat += src.slice(j, skipped);
      } else {
        flat += ' ';
      }
      j = skipped - 1;
      continue;
    }
    const ch = src[j];
    if (ch === '{' || ch === '[' || ch === '(') { depth++; flat += depth === 1 ? ch : ' '; continue; }
    if (ch === '}' || ch === ']' || ch === ')') { flat += depth === 1 ? ch : ' '; depth--; continue; }
    flat += depth === 1 ? ch : ' ';
  }
  return new RegExp(`(^|[^\\w$.])${key}["'\`]?\\s*[:,}]`).test(flat);
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

/**
 * 소스 하나에 runtimeVersion + isGame을 주입한다.
 * @returns {{ found: boolean, changed: boolean, source: string, reason: string }}
 *   found: setMetadata 객체 리터럴이 하나라도 있었는지(패치 앵커 존재 여부).
 */
export function patchSource(src, runtimeVersion = RUNTIME_VERSION) {
  const found = findSetMetadataObjects(src).length > 0;
  const rv = injectRuntimeVersion(src, runtimeVersion);
  const ig = injectIsGame(rv.source);
  const changed = rv.changed || ig.changed;
  const reason = changed
    ? [rv, ig].filter((r) => r.changed).map((r) => r.reason).join(', ')
    : `${rv.reason}; ${ig.reason}`;
  return { found, changed, source: ig.source, reason };
}

// ───────────────────────── 패치 대상 해석 ─────────────────────────

function tryRealpath(p) {
  try { return realpathSync(p); } catch { return null; }
}

function readJson(p) {
  try { return JSON.parse(readFileSync(p, 'utf8')); } catch { return null; }
}

/**
 * Node의 node_modules 탐색과 같은 순서로 fromFile 기준 패키지의 package.json 실제 경로를 찾는다
 * (심볼릭 링크는 realpath로 해석). require.resolve를 쓰지 않는 이유: NODE_PATH(pnpm exec가 설정한다)와
 * 전역 경로까지 뒤져 프로젝트와 무관한 다른 버전을 잡을 수 있고, exports에 ./package.json이 없는
 * 패키지는 resolve 자체가 실패한다.
 */
function findPackageJson(fromFile, name) {
  let dir = dirname(fromFile);
  for (;;) {
    const modules = basename(dir) === 'node_modules' ? dir : join(dir, 'node_modules');
    const real = tryRealpath(join(modules, name, 'package.json'));
    if (real) return real;
    const up = dirname(dir);
    if (up === dir || dir === parse(dir).root) return null;
    dir = up;
  }
}

/**
 * 패키지(package.json 실제 경로)에서 `ait` bin 엔트리와 같은 디렉터리의 .js/.mjs/.cjs 파일들을 모은다.
 * bin 필드가 없으면 dist/index.js로 가정한다. dist가 여러 chunk로 쪼개져도 setMetadata가
 * 어느 파일에 있든 찾도록 형제 파일을 함께 본다(setMetadata 리터럴이 없는 파일은 건드리지 않는다).
 */
function collectCliFiles(pkgJsonPath) {
  const pkg = readJson(pkgJsonPath) ?? {};
  const bin = typeof pkg.bin === 'string' ? pkg.bin : (pkg.bin?.ait ?? Object.values(pkg.bin ?? {})[0]);
  const entry = join(dirname(pkgJsonPath), bin ?? 'dist/index.js');
  const files = [entry];
  try {
    for (const name of readdirSync(dirname(entry))) {
      if (/\.(m|c)?js$/.test(name)) files.push(join(dirname(entry), name));
    }
  } catch { /* 디렉터리를 못 읽으면 entry만 */ }
  return files;
}

/**
 * 프로젝트 루트에서 setMetadata가 있을 수 있는 파일 후보를 순서대로 찾는다 (실제 경로, 중복 제거).
 *
 *   1. web-framework(프로젝트 직접 의존) → 그 실제 위치에서 resolve한 @apps-in-toss/cli.
 *      `ait` bin이 실제로 import하는 경로이며 pnpm(.pnpm 가상 스토어 + 형제 symlink)과
 *      npm/yarn 평탄 구조에서 모두 동작한다. pnpm은 cli를 프로젝트 node_modules에 hoist하지 않으므로
 *      프로젝트 기준 resolve만으로는 못 찾는다.
 *   2. 프로젝트 자체의 @apps-in-toss/cli (직접 의존이거나 hoist된 경우).
 *   3. 레거시: web-framework/dist/cli.js (cli가 분리되기 전 레이아웃).
 *
 * @returns {{ candidates: string[], webFrameworkDir: string | null, notes: string[] }}
 */
export function resolvePatchCandidates(root) {
  const notes = [];
  const seen = new Set();
  const candidates = [];
  const add = (p) => {
    const real = tryRealpath(p);
    if (real && !seen.has(real)) { seen.add(real); candidates.push(real); }
  };

  const projPkg = join(root, 'package.json');

  const wfPkgJson = findPackageJson(projPkg, WEB_FRAMEWORK);
  const wfDir = wfPkgJson ? dirname(wfPkgJson) : null;
  if (!wfPkgJson) notes.push(`${WEB_FRAMEWORK}를 프로젝트(${root})에서 찾지 못함`);

  // 1. web-framework → cli
  if (wfPkgJson) {
    const cliPkg = findPackageJson(wfPkgJson, CLI_PACKAGE);
    if (cliPkg) collectCliFiles(cliPkg).forEach(add);
    else notes.push(`web-framework(${wfDir})에서 ${CLI_PACKAGE}를 resolve하지 못함`);
  }

  // 2. 프로젝트 자체 cli
  const projCliPkg = findPackageJson(projPkg, CLI_PACKAGE);
  if (projCliPkg) collectCliFiles(projCliPkg).forEach(add);

  // 3. 레거시 web-framework/dist/cli.js
  if (wfDir) add(join(wfDir, 'dist', 'cli.js'));

  return { candidates, webFrameworkDir: wfDir, notes };
}

/**
 * 파일 하나를 패치한다. 새 inode로 교체하므로 pnpm 하드링크(공유 store)가 끊긴다.
 * @returns {'patched' | 'unchanged' | 'no-anchor' | 'unreadable' | 'write-failed'}
 */
function patchFile(realPath, log) {
  let src;
  try {
    src = readFileSync(realPath, 'utf8');
  } catch (e) {
    log.warn(`${TAG}   읽기 실패: ${realPath} (${e?.message ?? e})`);
    return 'unreadable';
  }
  const r = patchSource(src);
  if (!r.found) return 'no-anchor';
  if (!r.changed) {
    log.log(`${TAG}   패치 불필요: ${realPath} — ${r.reason}.`);
    return 'unchanged';
  }
  let tmp;
  try {
    // (e) 같은 디렉터리의 임시 파일에 쓰고 rename으로 교체 → 새 inode(하드링크 끊김),
    //     쓰기 도중 실패해도 원본은 그대로 남는다. 실행 비트 등 모드는 보존한다.
    const mode = statSync(realPath).mode & 0o7777;
    tmp = `${realPath}.ait-patch-${process.pid}.tmp`;
    writeFileSync(tmp, r.source, 'utf8');
    chmodSync(tmp, mode);
    renameSync(tmp, realPath);
    log.log(`${TAG}   ✓ 패치 완료: ${realPath} — ${r.reason}`);
    return 'patched';
  } catch (e) {
    if (tmp) { try { unlinkSync(tmp); } catch { /* 이미 없음 */ } }
    log.warn(`${TAG}   쓰기 실패: ${realPath} — 건너뜀(빌드는 계속): ${e?.message ?? e}`);
    return 'write-failed';
  }
}

/** GitHub Actions에서는 주석으로도 보이게 한다. */
function loudWarning(log, lines) {
  const banner = '!'.repeat(72);
  log.warn(`${TAG} ${banner}`);
  for (const l of lines) log.warn(`${TAG} WARNING: ${l}`);
  log.warn(`${TAG} ${banner}`);
  if (process.env.GITHUB_ACTIONS === 'true') {
    log.log(`::warning title=ait-patch-cli::${lines[0]}`);
  }
}

/**
 * 프로젝트 루트의 패치 대상을 해석해 모두 패치한다. 예외를 던지지 않는다.
 * @returns {{ patched: string[], unchanged: string[], noAnchor: string[], failed: string[], ok: boolean }}
 *   ok: setMetadata 앵커를 가진 파일이 하나 이상 있어 isGame/runtimeVersion이 반영될 수 있는 상태.
 */
export function patchProject(root, log = console) {
  const { candidates, notes } = resolvePatchCandidates(root);
  const result = { patched: [], unchanged: [], noAnchor: [], failed: [], ok: false };

  log.log(`${TAG} 패치 후보 ${candidates.length}개:${candidates.length ? '' : ' (없음)'}`);
  for (const f of candidates) log.log(`${TAG}   - ${f}`);

  for (const f of candidates) {
    const status = patchFile(f, log);
    if (status === 'patched') result.patched.push(f);
    else if (status === 'unchanged') result.unchanged.push(f);
    else if (status === 'no-anchor') result.noAnchor.push(f);
    else result.failed.push(f);
  }

  result.ok = result.patched.length + result.unchanged.length > 0;
  if (!result.ok) {
    loudWarning(log, [
      'setMetadata 호출을 가진 패치 대상을 찾지 못했다. 이대로 ait build하면 .ait의 isGame=false, runtimeVersion=""이 된다.',
      ...notes,
      ...(result.noAnchor.length ? [`setMetadata 객체 리터럴이 없는 후보: ${result.noAnchor.join(', ')}`] : []),
      ...(result.failed.length ? [`읽기/쓰기에 실패한 후보: ${result.failed.join(', ')}`] : []),
      'web-framework 버전의 CLI 구조가 바뀌었을 수 있다. 빌드 후 ait-verify-metadata.mjs가 결과를 확인한다(AIT_STRICT_METADATA=1이면 실패).',
    ]);
  }
  return result;
}

function main() {
  const rawIsGame = process.env.AIT_IS_GAME;
  const known = typeof rawIsGame === 'string' && ['true', 'false'].includes(rawIsGame.trim().toLowerCase());
  console.log(
    `${TAG} AIT_IS_GAME=${rawIsGame === undefined ? '(미설정)' : JSON.stringify(rawIsGame)} → isGame=${resolveIsGame(rawIsGame)}` +
      (known ? '' : ' (미설정/유효하지 않은 값 — 기본값 true)'),
  );
  patchProject(resolve(process.cwd()), console);
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
