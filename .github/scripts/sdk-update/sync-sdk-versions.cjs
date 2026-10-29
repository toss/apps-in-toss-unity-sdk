#!/usr/bin/env node
/**
 * SDK 버전 기준 파일(.github/sdk-versions.json) 동기화 스크립트.
 *
 * SDK 버전, web-framework 버전, 동반 패키지 버전, pnpm 버전은 package.json 3개와
 * Editor/AITPackageManagerHelper.cs 에 흩어져 있다. 이 값들의 기준(SSOT)은
 * .github/sdk-versions.json 한 곳이고, 나머지 파일은 이 스크립트가 쓰고 검사한다.
 *
 * 모드
 *   resolve --version X.Y.Z [--registry URL] [--dry-run]
 *       목표 web-framework 버전을 받아 npm 에서 동반 패키지 버전을 결정하고 기준 파일을 갱신한다.
 *       동반 패키지는 "같은 번호가 npm 에 있으면 그 번호, 없으면 latest" 규칙을 따른다
 *       (3.0 부터 web-framework 와 자매 패키지는 lockstep publish 가 아니다).
 *       기준 파일의 pnpm 은 건드리지 않는다.
 *   apply
 *       기준 파일을 읽어 모든 대상 파일에 쓴다. 선택(optional) 대상 키는 이미 있을 때만 갱신한다
 *       (직접 핀이 제거돼도 phantom 키를 다시 만들지 않는다).
 *   check
 *       모든 대상이 기준 파일과 일치하는지 검사한다. 어긋나면 파일/키/값을 출력하고 exit 1.
 *
 * 공통 옵션: --root DIR (기본: 저장소 루트. 테스트용)
 * 종료 코드: 0 정상, 1 check 불일치, 2 사용법·입력 오류
 *
 * Node 내장 모듈만 사용한다.
 */

'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const SSOT_REL = '.github/sdk-versions.json';
const SCRIPT_REL = '.github/scripts/sdk-update/sync-sdk-versions.cjs';
const DEFAULT_REGISTRY = 'https://registry.npmjs.org';
const DEFAULT_ROOT = path.resolve(__dirname, '..', '..', '..');

const ROOT_PKG = 'package.json';
const GENERATOR_PKG = 'sdk-runtime-generator~/package.json';
const BUILDCONFIG_PKG = 'WebGLTemplates/AITTemplate/BuildConfig~/package.json';
const PNPM_CS = 'Editor/AITPackageManagerHelper.cs';

const WEB_FRAMEWORK = '@apps-in-toss/web-framework';
const DEVTOOLS = '@apps-in-toss/devtools';

const SEMVER = /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/;
const PNPM_CONST = /(public const string PNPM_VERSION = ")([^"]*)(";)/;

// ---------------------------------------------------------------------------
// 대상 정의
// ---------------------------------------------------------------------------

/**
 * 기준 파일의 값이 쓰이는 모든 위치.
 *   ssot:     기준 파일 안의 위치 (kind: sdk | webFramework | companion | pnpm)
 *   file:     저장소 루트 기준 대상 파일
 *   section:  package.json 의 섹션 (dependencies / devDependencies). 생략하면 최상위 키
 *   key:      섹션 안의 키
 *   optional: true 면 키가 이미 있을 때만 갱신/검사한다 (phantom 키 방지)
 *   format:   값을 대상 파일 표기로 바꾸는 함수 (기본: 그대로)
 */
const JSON_SITES = [
  { ssot: { kind: 'sdk' }, file: ROOT_PKG, key: 'version' },
  { ssot: { kind: 'pnpm' }, file: ROOT_PKG, key: 'packageManager', format: (v) => `pnpm@${v}` },

  { ssot: { kind: 'webFramework' }, file: GENERATOR_PKG, section: 'dependencies', key: WEB_FRAMEWORK },
  { ssot: { kind: 'pnpm' }, file: GENERATOR_PKG, key: 'packageManager', format: (v) => `pnpm@${v}` },

  { ssot: { kind: 'webFramework' }, file: BUILDCONFIG_PKG, section: 'dependencies', key: WEB_FRAMEWORK },
  { ssot: { kind: 'companion', name: DEVTOOLS }, file: BUILDCONFIG_PKG, section: 'devDependencies', key: DEVTOOLS, optional: true },
  { ssot: { kind: 'pnpm' }, file: BUILDCONFIG_PKG, key: 'packageManager', format: (v) => `pnpm@${v}` },
];

class UsageError extends Error {}

// ---------------------------------------------------------------------------
// 기준 파일
// ---------------------------------------------------------------------------

function ssotPath(root) {
  return path.join(root, SSOT_REL);
}

function readSsot(root) {
  const file = ssotPath(root);
  if (!fs.existsSync(file)) {
    throw new UsageError(`기준 파일이 없습니다: ${SSOT_REL}`);
  }
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch (e) {
    throw new UsageError(`기준 파일을 JSON 으로 읽지 못했습니다 (${SSOT_REL}): ${e.message}`);
  }
}

function writeSsot(root, ssot) {
  fs.writeFileSync(ssotPath(root), JSON.stringify(ssot, null, 2) + '\n');
}

/** 기준 파일 자체의 형식 오류 목록. 비어 있으면 정상. */
function validateSsot(ssot) {
  const errors = [];
  const isVersion = (v) => typeof v === 'string' && SEMVER.test(v);

  if (ssot.schemaVersion !== 1) errors.push(`schemaVersion 은 1 이어야 합니다 (현재: ${JSON.stringify(ssot.schemaVersion)})`);
  for (const key of ['sdkVersion', 'webFramework', 'pnpm']) {
    if (!isVersion(ssot[key])) errors.push(`${key} 는 X.Y.Z 형식이어야 합니다 (현재: ${JSON.stringify(ssot[key])})`);
  }
  const companions = ssot.companions;
  if (companions === null || typeof companions !== 'object' || Array.isArray(companions)) {
    errors.push('companions 는 {패키지명: 버전} 객체여야 합니다');
  } else {
    for (const [name, v] of Object.entries(companions)) {
      if (!isVersion(v)) errors.push(`companions["${name}"] 는 X.Y.Z 형식이어야 합니다 (현재: ${JSON.stringify(v)})`);
    }
  }
  if (isVersion(ssot.sdkVersion) && isVersion(ssot.webFramework) && ssot.sdkVersion !== ssot.webFramework) {
    errors.push(`sdkVersion(${ssot.sdkVersion}) 과 webFramework(${ssot.webFramework}) 는 같아야 합니다 (release.yml 이 같은 값을 요구합니다)`);
  }

  // 기준 파일의 항목은 반드시 쓰이는 곳이 있어야 한다. 이름만 있고 대상이 없으면 조용히 무시된다.
  for (const name of Object.keys(ssot.companions || {})) {
    if (!JSON_SITES.some((s) => s.ssot.kind === 'companion' && s.ssot.name === name)) {
      errors.push(`companions["${name}"] 를 쓰는 대상 파일이 ${SCRIPT_REL} 에 정의돼 있지 않습니다`);
    }
  }
  return errors;
}

function ssotValue(ssot, site) {
  const { kind, name } = site.ssot;
  let raw;
  switch (kind) {
    case 'sdk': raw = ssot.sdkVersion; break;
    case 'webFramework': raw = ssot.webFramework; break;
    case 'pnpm': raw = ssot.pnpm; break;
    case 'companion': raw = (ssot.companions || {})[name]; break;
    default: throw new Error(`알 수 없는 kind: ${kind}`);
  }
  return site.format ? site.format(raw) : raw;
}

function ssotLabel(site) {
  const { kind, name } = site.ssot;
  switch (kind) {
    case 'sdk': return 'sdkVersion';
    case 'webFramework': return 'webFramework';
    case 'pnpm': return 'pnpm';
    default: return `companions["${name}"]`;
  }
}

function siteLabel(site) {
  return site.section ? `${site.section}["${site.key}"]` : `${site.key}`;
}

// ---------------------------------------------------------------------------
// 대상 파일 읽기·쓰기
// ---------------------------------------------------------------------------

function readJsonFile(root, rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) throw new UsageError(`대상 파일이 없습니다: ${rel}`);
  const text = fs.readFileSync(file, 'utf8');
  try {
    return { text, json: JSON.parse(text) };
  } catch (e) {
    throw new UsageError(`${rel} 를 JSON 으로 읽지 못했습니다: ${e.message}`);
  }
}

function serializeJson(json, originalText) {
  const body = JSON.stringify(json, null, 2);
  return originalText.endsWith('\n') ? body + '\n' : body;
}

function isSorted(keys) {
  return keys.every((k, i) => i === 0 || keys[i - 1] <= k);
}

function sortKeysInPlace(obj) {
  const entries = Object.entries(obj).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
  for (const k of Object.keys(obj)) delete obj[k];
  for (const [k, v] of entries) obj[k] = v;
}

function container(json, site) {
  return site.section ? json[site.section] : json;
}

function groupByFile(sites) {
  const map = new Map();
  for (const site of sites) {
    if (!map.has(site.file)) map.set(site.file, []);
    map.get(site.file).push(site);
  }
  return map;
}

// ---------------------------------------------------------------------------
// check
// ---------------------------------------------------------------------------

/** @returns {{file: string, where: string, actual: string, expected: string, ssotKey: string}[]} */
function collectMismatches(root, ssot) {
  const out = [];

  for (const [file, sites] of groupByFile(JSON_SITES)) {
    const { json } = readJsonFile(root, file);
    for (const site of sites) {
      const expected = ssotValue(ssot, site);
      const box = container(json, site);
      const present = box !== undefined && box !== null && Object.prototype.hasOwnProperty.call(box, site.key);
      if (!present) {
        if (site.optional) continue;
        out.push({ file, where: siteLabel(site), actual: '(없음)', expected, ssotKey: ssotLabel(site) });
        continue;
      }
      if (box[site.key] !== expected) {
        out.push({ file, where: siteLabel(site), actual: String(box[site.key]), expected, ssotKey: ssotLabel(site) });
      }
    }
  }

  const csFile = path.join(root, PNPM_CS);
  if (!fs.existsSync(csFile)) throw new UsageError(`대상 파일이 없습니다: ${PNPM_CS}`);
  const match = PNPM_CONST.exec(fs.readFileSync(csFile, 'utf8'));
  if (!match) {
    out.push({ file: PNPM_CS, where: 'PNPM_VERSION', actual: '(상수를 찾지 못함)', expected: ssot.pnpm, ssotKey: 'pnpm' });
  } else if (match[2] !== ssot.pnpm) {
    out.push({ file: PNPM_CS, where: 'PNPM_VERSION', actual: match[2], expected: ssot.pnpm, ssotKey: 'pnpm' });
  }
  return out;
}

function runCheck(root, log = console.log) {
  const ssot = readSsot(root);
  const ssotErrors = validateSsot(ssot);
  const mismatches = ssotErrors.length ? [] : collectMismatches(root, ssot);
  const annotate = process.env.GITHUB_ACTIONS === 'true';

  if (ssotErrors.length === 0 && mismatches.length === 0) {
    log(`✓ SDK 버전 기준(${SSOT_REL})과 모든 대상 파일이 일치합니다.`);
    return 0;
  }

  if (ssotErrors.length) {
    log(`✗ 기준 파일(${SSOT_REL})이 올바르지 않습니다:`);
    for (const e of ssotErrors) {
      log(`  - ${e}`);
      if (annotate) log(`::error file=${SSOT_REL}::${e}`);
    }
  } else {
    log(`✗ SDK 버전 기준(${SSOT_REL})과 다른 항목이 ${mismatches.length}건 있습니다:`);
    for (const m of mismatches) {
      const msg = `${m.where} = ${m.actual} (기준 ${m.ssotKey} = ${m.expected})`;
      log(`  - ${m.file}: ${msg}`);
      if (annotate) {
        log(`::error file=${m.file}::${msg}. 기준 파일 값으로 맞추려면: node ${SCRIPT_REL} apply`);
      }
    }
  }
  log('');
  log('해결 방법:');
  log(`  1. 기준 파일 값이 맞다면: node ${SCRIPT_REL} apply   (기준 파일 값을 모든 대상 파일에 씁니다)`);
  log(`  2. 버전을 올리려는 것이라면 대상 파일을 직접 고치지 말고 기준 파일을 먼저 바꿉니다:`);
  log(`       node ${SCRIPT_REL} resolve --version X.Y.Z   (web-framework 버전과 동반 패키지 버전을 npm 에서 결정)`);
  log(`       node ${SCRIPT_REL} apply`);
  log(`     pnpm 버전은 ${SSOT_REL} 의 "pnpm" 을 고친 뒤 apply 합니다.`);
  return 1;
}

// ---------------------------------------------------------------------------
// apply
// ---------------------------------------------------------------------------

function runApply(root, log = console.log) {
  const ssot = readSsot(root);
  const ssotErrors = validateSsot(ssot);
  if (ssotErrors.length) {
    throw new UsageError(`기준 파일(${SSOT_REL})이 올바르지 않습니다:\n  - ${ssotErrors.join('\n  - ')}`);
  }

  const changed = [];

  for (const [file, sites] of groupByFile(JSON_SITES)) {
    const { text, json } = readJsonFile(root, file);
    for (const site of sites) {
      const expected = ssotValue(ssot, site);
      let box = container(json, site);
      if (box === undefined || box === null) {
        if (site.optional) continue;
        json[site.section] = {};
        box = json[site.section];
      }
      const present = Object.prototype.hasOwnProperty.call(box, site.key);
      if (!present && site.optional) continue;
      if (box[site.key] !== expected) {
        changed.push(`${file}: ${siteLabel(site)} ${present ? box[site.key] : '(없음)'} → ${expected}`);
        const keysBefore = Object.keys(box);
        box[site.key] = expected;
        // 키가 알파벳 순으로 정렬돼 있던 섹션에 새 키를 넣으면 끝에 붙어 불필요한 diff 가 생기므로 다시 정렬한다.
        if (!present && site.section && isSorted(keysBefore)) sortKeysInPlace(box);
      }
    }
    const next = serializeJson(json, text);
    if (next !== text) fs.writeFileSync(path.join(root, file), next);
  }

  const csFile = path.join(root, PNPM_CS);
  if (!fs.existsSync(csFile)) throw new UsageError(`대상 파일이 없습니다: ${PNPM_CS}`);
  const csText = fs.readFileSync(csFile, 'utf8');
  const match = PNPM_CONST.exec(csText);
  if (!match) throw new UsageError(`${PNPM_CS} 에서 PNPM_VERSION 상수를 찾지 못했습니다`);
  if (match[2] !== ssot.pnpm) {
    changed.push(`${PNPM_CS}: PNPM_VERSION ${match[2]} → ${ssot.pnpm}`);
    fs.writeFileSync(csFile, csText.replace(PNPM_CONST, `$1${ssot.pnpm}$3`));
  }

  if (changed.length === 0) {
    log('변경 없음: 모든 대상 파일이 이미 기준 파일과 같습니다.');
  } else {
    log(`${changed.length}건 갱신:`);
    for (const c of changed) log(`  - ${c}`);
  }
  return changed;
}

// ---------------------------------------------------------------------------
// resolve
// ---------------------------------------------------------------------------

/** npm 조회. 테스트에서는 같은 모양의 객체로 바꿔 끼운다. */
function npmLookup(registry) {
  const view = (args) =>
    execFileSync('npm', ['view', ...args, '--registry', registry], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  return {
    versions(pkg) {
      // 버전이 하나뿐이면 npm 이 배열이 아니라 문자열을 돌려준다.
      return [].concat(JSON.parse(view([pkg, 'versions', '--json'])));
    },
    latest(pkg) {
      return view([pkg, 'dist-tags.latest']);
    },
  };
}

/**
 * 동반 패키지별 버전 결정: 요청 버전이 그 패키지에 있으면 그 번호, 없으면 그 패키지의 latest.
 * @returns {Record<string, string>} 패키지명 → 버전 (companions 의 키 순서를 유지)
 */
function resolveCompanions(version, names, lookup) {
  const result = {};
  for (const name of names) {
    result[name] = lookup.versions(name).includes(version) ? version : lookup.latest(name);
    if (!SEMVER.test(result[name])) {
      throw new UsageError(`${name} 의 버전을 결정하지 못했습니다 (조회 결과: ${JSON.stringify(result[name])})`);
    }
  }
  return result;
}

function runResolve(root, { version, registry = DEFAULT_REGISTRY, dryRun = false, lookup }, log = console.log) {
  if (!version || !SEMVER.test(version)) {
    throw new UsageError(`--version 은 X.Y.Z 형식이어야 합니다 (입력: ${JSON.stringify(version)})`);
  }
  const ssot = readSsot(root);
  const ssotErrors = validateSsot(ssot);
  if (ssotErrors.length) {
    throw new UsageError(`기준 파일(${SSOT_REL})이 올바르지 않습니다:\n  - ${ssotErrors.join('\n  - ')}`);
  }

  const source = lookup || npmLookup(registry);
  if (!source.versions(WEB_FRAMEWORK).includes(version)) {
    throw new UsageError(`${WEB_FRAMEWORK}@${version} 이(가) npm 에 없습니다`);
  }
  const companions = resolveCompanions(version, Object.keys(ssot.companions), source);

  const next = { ...ssot, sdkVersion: version, webFramework: version, companions };
  log(`web-framework: ${version}`);
  for (const [name, v] of Object.entries(companions)) {
    log(`${name}: ${v}${v === version ? '' : ` (v${version} 미존재 → latest)`}`);
  }
  if (dryRun) {
    log('(--dry-run: 기준 파일을 쓰지 않았습니다)');
  } else {
    writeSsot(root, next);
    log(`${SSOT_REL} 갱신 완료. 이어서 apply 로 대상 파일에 쓰세요.`);
  }
  return next;
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const [mode, ...rest] = argv;
  const opts = { mode, root: DEFAULT_ROOT, registry: DEFAULT_REGISTRY, dryRun: false };
  for (let i = 0; i < rest.length; i++) {
    const a = rest[i];
    const value = () => {
      if (i + 1 >= rest.length) throw new UsageError(`${a} 에 값이 필요합니다`);
      return rest[++i];
    };
    if (a === '--version') opts.version = value();
    else if (a === '--registry') opts.registry = value();
    else if (a === '--root') opts.root = path.resolve(value());
    else if (a === '--dry-run') opts.dryRun = true;
    else throw new UsageError(`알 수 없는 옵션: ${a}`);
  }
  return opts;
}

const USAGE = `사용법:
  node ${SCRIPT_REL} check
  node ${SCRIPT_REL} apply
  node ${SCRIPT_REL} resolve --version X.Y.Z [--registry URL] [--dry-run]
공통: --root DIR (테스트용)`;

function main(argv) {
  try {
    const opts = parseArgs(argv);
    switch (opts.mode) {
      case 'check': return runCheck(opts.root);
      case 'apply': runApply(opts.root); return 0;
      case 'resolve': runResolve(opts.root, opts); return 0;
      default: throw new UsageError(`모드를 지정하세요 (check | apply | resolve): ${opts.mode || '(없음)'}`);
    }
  } catch (e) {
    const annotate = process.env.GITHUB_ACTIONS === 'true';
    if (e instanceof UsageError) {
      if (annotate) console.error(`::error::${e.message.split('\n')[0]}`);
      console.error(e.message);
      console.error(USAGE);
      return 2;
    }
    if (annotate) console.error(`::error::${e.message.split('\n')[0]}`);
    console.error(e.stderr ? `${e.message}\n${String(e.stderr)}` : e.stack);
    return 2;
  }
}

if (require.main === module) {
  process.exitCode = main(process.argv.slice(2));
}

module.exports = {
  SSOT_REL,
  SCRIPT_REL,
  JSON_SITES,
  validateSsot,
  collectMismatches,
  resolveCompanions,
  runCheck,
  runApply,
  runResolve,
  main,
};
