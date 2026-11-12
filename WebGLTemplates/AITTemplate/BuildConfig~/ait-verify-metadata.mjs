#!/usr/bin/env node
// ait-verify-metadata.mjs
//
// `ait build`가 만든 .ait를 열어 메타데이터(isGame / runtimeVersion / sdkVersion)를 출력하고,
// ait-patch-cli.mjs가 주입했어야 하는 값과 일치하는지 확인하는 빌드 후 가드.
//
// 배경: ait-patch-cli.mjs는 빌드를 막지 않도록 항상 exit 0이다. 그래서 패치 대상 파일이
// 이동하는 등의 이유로 패치가 조용히 no-op이 되어도 빌드는 성공하고, 결과물은
// isGame=false / runtimeVersion=""으로 배포 게이트나 콘솔에서 뒤늦게 문제가 된다.
// 이 스크립트는 "패치가 적용됐는지"가 아니라 "결과물에 값이 실제로 들어갔는지"를 직접 확인한다.
//
// 기대값:
//   - isGame        : env AIT_IS_GAME ("false"만 false, 미설정/그 외는 true — 패치와 같은 규칙)
//   - runtimeVersion: 비어 있지 않아야 한다 (3.x deploy 게이트 조건)
//
// 종료 코드:
//   - 기본: 불일치/확인 불가여도 경고만 남기고 exit 0 (사용자 빌드를 막지 않는다).
//   - AIT_STRICT_METADATA=1 (또는 true): 불일치/확인 불가면 exit 1. CI E2E 빌드가 켠다.
//
// 사용법: node ait-verify-metadata.mjs [path/to/app.ait]
//   인자가 없으면 cwd(프로젝트 루트)의 가장 최근 *.ait를 연다.

import { readdirSync, readFileSync, realpathSync, statSync } from 'node:fs';
import { createRequire } from 'node:module';
import { basename, dirname, extname, join, parse, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const TAG = '[ait-verify-metadata]';
const WEB_FRAMEWORK = '@apps-in-toss/web-framework';
const CLI_PACKAGE = '@apps-in-toss/cli';
const AIT_FORMAT = '@apps-in-toss/ait-format';

// ───────────────────────── 순수 함수 ─────────────────────────

/** "false"(대소문자·공백 무시)만 false, 미설정/그 외는 true. ait-patch-cli.mjs의 resolveIsGame과 같은 규칙. */
export function expectedIsGame(raw) {
  return String(raw ?? '').trim().toLowerCase() !== 'false';
}

/** AIT_STRICT_METADATA가 켜져 있는지 ("1" 또는 "true", 대소문자·공백 무시). */
export function isStrict(raw) {
  const v = String(raw ?? '').trim().toLowerCase();
  return v === '1' || v === 'true';
}

/**
 * .ait 리더의 metadata를 비교용 평문 객체로 정규화한다. metadata가 없으면 모두 기본값으로 본다.
 * @returns {{ appName: string, isGame: boolean, runtimeVersion: string, sdkVersion: string }}
 */
export function summarizeMetadata(reader) {
  const m = reader?.metadata;
  return {
    appName: String(reader?.appName ?? ''),
    isGame: m?.isGame === true,
    runtimeVersion: String(m?.runtimeVersion ?? ''),
    sdkVersion: String(m?.sdkVersion ?? ''),
  };
}

/**
 * 메타데이터가 기대값과 맞는지 평가한다.
 * @param {{ isGame: boolean, runtimeVersion: string }} meta
 * @param {Record<string, string | undefined>} env
 * @returns {{ ok: boolean, problems: string[] }}
 */
export function evaluateMetadata(meta, env = {}) {
  const problems = [];
  const want = expectedIsGame(env.AIT_IS_GAME);
  if (meta.isGame !== want) {
    problems.push(
      `isGame 불일치: .ait에는 ${meta.isGame}, 기대값은 ${want} ` +
        `(AIT_IS_GAME=${env.AIT_IS_GAME === undefined ? '(미설정)' : JSON.stringify(env.AIT_IS_GAME)})`,
    );
  }
  if (!meta.runtimeVersion) {
    problems.push('runtimeVersion이 비어 있음 (3.x deploy 게이트가 거부한다)');
  }
  return { ok: problems.length === 0, problems };
}

/** 디렉터리의 .ait 파일 후보 이름. 확장자는 정확히 비교한다(Windows 8.3 단축명 glob 문제 회피). */
export function listAitFiles(dir) {
  try {
    return readdirSync(dir).filter((n) => extname(n).toLowerCase() === '.ait').map((n) => join(dir, n));
  } catch {
    return [];
  }
}

/** 가장 최근에 수정된 파일을 고른다(`ait build`가 방금 쓴 파일). 없으면 null. */
export function pickNewest(files, mtimeOf = (f) => statSync(f).mtimeMs) {
  let best = null;
  let bestTime = -Infinity;
  for (const f of files) {
    let t;
    try { t = mtimeOf(f); } catch { continue; }
    if (t > bestTime) { best = f; bestTime = t; }
  }
  return best;
}

// ───────────────────────── ait-format 해석 ─────────────────────────

function tryRealpath(p) {
  try { return realpathSync(p); } catch { return null; }
}

/**
 * Node의 node_modules 탐색과 같은 순서로 fromFile 기준 패키지의 package.json 실제 경로를 찾는다
 * (심볼릭 링크는 realpath로 해석). require.resolve를 쓰지 않는 이유: NODE_PATH(pnpm exec가 설정한다)와
 * 전역 경로까지 뒤져 프로젝트와 무관한 다른 버전을 잡을 수 있기 때문이다. ait-patch-cli.mjs와 같은 규칙.
 */
export function findPackageJson(fromFile, name) {
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
 * @apps-in-toss/ait-format 패키지 디렉터리를 찾는다. 후보 순서:
 *   1. web-framework의 실제 위치 → cli → ait-format  (pnpm은 이 체인만 보인다)
 *   2. 프로젝트 기준 cli → ait-format
 *   3. 프로젝트 기준 ait-format, web-framework 기준 ait-format
 * @returns {string | null} 패키지 디렉터리의 실제 경로
 */
export function resolveAitFormat(root) {
  const projPkg = join(root, 'package.json');
  const viaCli = (fromFile) => {
    const cli = fromFile && findPackageJson(fromFile, CLI_PACKAGE);
    const fmt = cli && findPackageJson(cli, AIT_FORMAT);
    return fmt ? dirname(fmt) : null;
  };
  const direct = (fromFile) => {
    const fmt = fromFile && findPackageJson(fromFile, AIT_FORMAT);
    return fmt ? dirname(fmt) : null;
  };
  const wfPkg = findPackageJson(projPkg, WEB_FRAMEWORK);
  return viaCli(wfPkg) || viaCli(projPkg) || direct(projPkg) || direct(wfPkg) || null;
}

// ───────────────────────── 실행 ─────────────────────────

/**
 * 검사를 수행하고 종료 코드를 돌려준다. 예외를 던지지 않는다.
 * @returns {number} 0 또는 1
 */
export function verify({ root, aitPath, env = process.env, log = console } = {}) {
  const strict = isStrict(env.AIT_STRICT_METADATA);
  const fail = (lines) => {
    for (const l of lines) log.warn(`${TAG} WARNING: ${l}`);
    if (env.GITHUB_ACTIONS === 'true') log.log(`::${strict ? 'error' : 'warning'} title=ait-verify-metadata::${lines[0]}`);
    if (strict) {
      log.error(`${TAG} AIT_STRICT_METADATA=1 — 빌드를 실패 처리합니다.`);
      return 1;
    }
    log.warn(`${TAG} 경고만 남기고 계속합니다 (AIT_STRICT_METADATA=1이면 실패).`);
    return 0;
  };

  try {
    const file = aitPath ? resolve(aitPath) : pickNewest(listAitFiles(root));
    if (!file) return fail([`${root}에서 .ait 파일을 찾지 못함 — 메타데이터를 확인할 수 없다.`]);

    const formatDir = resolveAitFormat(root);
    if (!formatDir) {
      return fail([`${AIT_FORMAT}를 resolve하지 못함 — ${file}의 메타데이터를 확인할 수 없다.`]);
    }
    // 디렉터리 경로 require는 package.json의 main(CJS 엔트리)을 로드한다.
    const { AITReader } = createRequire(join(formatDir, 'package.json'))(formatDir);
    const reader = AITReader.fromBuffer(new Uint8Array(readFileSync(file)));
    const meta = summarizeMetadata(reader);

    log.log(`${TAG} ${file}`);
    log.log(`${TAG}   appName=${meta.appName} isGame=${meta.isGame} runtimeVersion=${JSON.stringify(meta.runtimeVersion)} sdkVersion=${JSON.stringify(meta.sdkVersion)}`);

    const { ok, problems } = evaluateMetadata(meta, env);
    if (ok) {
      log.log(`${TAG} ✓ 메타데이터가 기대값과 일치합니다.`);
      return 0;
    }
    return fail([
      ...problems,
      'ait-patch-cli.mjs가 적용되지 않았을 가능성이 크다 (패치 로그의 "WARNING"을 확인하세요).',
    ]);
  } catch (e) {
    return fail([`.ait 메타데이터를 읽지 못함: ${e?.message ?? e}`]);
  }
}

const invokedPath = process.argv[1];
if (invokedPath && import.meta.url === pathToFileURL(invokedPath).href) {
  let code = 0;
  try {
    code = verify({ root: process.cwd(), aitPath: process.argv[2] });
  } catch (e) {
    // 최후의 안전망: strict가 아니면 어떤 예외도 빌드를 막지 않는다.
    console.warn(`${TAG} 예기치 못한 오류: ${e?.message ?? e}`);
    code = isStrict(process.env.AIT_STRICT_METADATA) ? 1 : 0;
  }
  process.exitCode = code;
}
