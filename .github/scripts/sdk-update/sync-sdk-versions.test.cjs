/**
 * sync-sdk-versions.cjs 단위 테스트 (Node 내장 test runner, 네트워크 호출 없음).
 *
 * 실행: node --test .github/scripts/sdk-update/sync-sdk-versions.test.cjs
 */

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const sync = require('./sync-sdk-versions.cjs');

const SCRIPT = path.join(__dirname, 'sync-sdk-versions.cjs');
const BC = 'WebGLTemplates/AITTemplate/BuildConfig~/package.json';
const GEN = 'sdk-runtime-generator~/package.json';
const CS = 'Editor/AITPackageManagerHelper.cs';

const SSOT = {
  schemaVersion: 1,
  sdkVersion: '3.5.0',
  webFramework: '3.5.0',
  companions: {
    '@apps-in-toss/web-analytics': '2.10.9',
    '@apps-in-toss/bridge-core': '2.10.9',
    '@apps-in-toss/devtools': '3.5.0',
  },
  pnpm: '11.6.0',
};

function write(root, rel, text) {
  const file = path.join(root, rel);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text);
}

function writeJson(root, rel, obj) {
  write(root, rel, JSON.stringify(obj, null, 2) + '\n');
}

function readJson(root, rel) {
  return JSON.parse(fs.readFileSync(path.join(root, rel), 'utf8'));
}

/** 기준 파일과 일치하는 최소 저장소 트리를 임시 디렉터리에 만든다. */
function makeRepo(overrides = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'sync-sdk-versions-'));
  writeJson(root, sync.SSOT_REL, overrides.ssot || SSOT);
  writeJson(root, 'package.json', { name: 'sdk', version: '3.5.0', keywords: ['토스'], packageManager: 'pnpm@11.6.0' });
  writeJson(root, GEN, {
    name: 'gen',
    dependencies: {
      '@apps-in-toss/web-analytics': '2.10.9',
      '@apps-in-toss/web-framework': '3.5.0',
      commander: '15.0.0',
    },
    devDependencies: { 'web-framework-3.5.0': 'npm:@apps-in-toss/web-framework@3.5.0' },
    packageManager: 'pnpm@11.6.0',
  });
  writeJson(
    root,
    BC,
    overrides.buildConfig || {
      name: 'unity-miniapp',
      dependencies: {
        '@apps-in-toss/bridge-core': '2.10.9',
        '@apps-in-toss/web-analytics': '2.10.9',
        '@apps-in-toss/web-framework': '3.5.0',
      },
      devDependencies: { '@apps-in-toss/devtools': '3.5.0', vite: '8.3.4' },
      packageManager: 'pnpm@11.6.0',
    },
  );
  write(root, CS, 'public static class H\n{\n    public const string PNPM_VERSION = "11.6.0";\n}\n');
  return root;
}

function capture() {
  const lines = [];
  return { lines, log: (l = '') => lines.push(l), text: () => lines.join('\n') };
}

function fakeLookup(published, latest) {
  return {
    versions: (pkg) => published[pkg] || [],
    latest: (pkg) => latest[pkg],
  };
}

test('일치하는 트리는 check 를 통과한다', () => {
  const root = makeRepo();
  const out = capture();
  assert.equal(sync.runCheck(root, out.log), 0);
  assert.match(out.text(), /일치합니다/);
});

test('BuildConfig devtools 가 기준과 어긋나면 파일·키·값·수정 명령을 알려 준다 (web-framework 3.5.0 + devtools 3.7.0 사고)', () => {
  const root = makeRepo();
  const bc = readJson(root, BC);
  bc.devDependencies['@apps-in-toss/devtools'] = '3.7.0';
  writeJson(root, BC, bc);

  const out = capture();
  assert.equal(sync.runCheck(root, out.log), 1);
  const text = out.text();
  assert.ok(text.includes(BC), text);
  assert.ok(text.includes('devDependencies["@apps-in-toss/devtools"] = 3.7.0'), text);
  assert.ok(text.includes('companions["@apps-in-toss/devtools"] = 3.5.0'), text);
  assert.ok(text.includes(`node ${sync.SCRIPT_REL} apply`), text);
});

test('여러 파일의 어긋남을 한 번에 모두 보고한다', () => {
  const root = makeRepo();
  const pkg = readJson(root, 'package.json');
  pkg.version = '3.6.0';
  writeJson(root, 'package.json', pkg);
  const gen = readJson(root, GEN);
  gen.dependencies['@apps-in-toss/web-framework'] = '3.4.1';
  gen.packageManager = 'pnpm@10.0.0';
  writeJson(root, GEN, gen);

  const mismatches = sync.collectMismatches(root, SSOT);
  assert.deepEqual(
    mismatches.map((m) => `${m.file} ${m.where}`).sort(),
    [
      `${GEN} dependencies["@apps-in-toss/web-framework"]`,
      `${GEN} packageManager`,
      'package.json version',
    ].sort(),
  );
});

test('PNPM_VERSION 상수가 어긋나면 잡는다', () => {
  const root = makeRepo();
  write(root, CS, 'public const string PNPM_VERSION = "10.28.0";\n');
  const mismatches = sync.collectMismatches(root, SSOT);
  assert.equal(mismatches.length, 1);
  assert.equal(mismatches[0].file, CS);
  assert.equal(mismatches[0].actual, '10.28.0');
  assert.equal(mismatches[0].expected, '11.6.0');
});

test('PNPM_VERSION 상수를 못 찾으면 check 는 실패하고 apply 는 오류로 멈춘다', () => {
  const root = makeRepo();
  write(root, CS, 'public static class H {}\n');
  assert.equal(sync.collectMismatches(root, SSOT).length, 1);
  assert.throws(() => sync.runApply(root, () => {}), /PNPM_VERSION/);
});

test('필수 키가 없으면 check 가 보고하고, apply 가 만든다', () => {
  const root = makeRepo();
  const bc = readJson(root, BC);
  delete bc.dependencies['@apps-in-toss/web-analytics'];
  writeJson(root, BC, bc);

  assert.equal(sync.collectMismatches(root, SSOT).length, 1);
  sync.runApply(root, () => {});
  assert.equal(readJson(root, BC).dependencies['@apps-in-toss/web-analytics'], '2.10.9');
  assert.equal(sync.runCheck(root, () => {}), 0);
});

test('선택 키(bridge-core, devtools)는 없어도 check 를 통과하고 apply 가 만들지 않는다 (phantom 키 방지)', () => {
  const root = makeRepo({
    buildConfig: {
      name: 'unity-miniapp',
      dependencies: { '@apps-in-toss/web-analytics': '2.10.9', '@apps-in-toss/web-framework': '3.5.0' },
      devDependencies: { vite: '8.3.4' },
      packageManager: 'pnpm@11.6.0',
    },
  });
  assert.equal(sync.runCheck(root, () => {}), 0);

  writeJson(root, sync.SSOT_REL, { ...SSOT, sdkVersion: '3.8.0', webFramework: '3.8.0' });
  sync.runApply(root, () => {});
  const bc = readJson(root, BC);
  assert.equal(bc.dependencies['@apps-in-toss/web-framework'], '3.8.0');
  assert.equal('@apps-in-toss/bridge-core' in bc.dependencies, false);
  assert.equal('@apps-in-toss/devtools' in bc.devDependencies, false);
});

test('apply 는 모든 대상을 기준 값으로 쓰고 이후 check 가 통과한다', () => {
  const root = makeRepo();
  writeJson(root, sync.SSOT_REL, {
    ...SSOT,
    sdkVersion: '3.8.0',
    webFramework: '3.8.0',
    companions: {
      '@apps-in-toss/web-analytics': '2.10.9',
      '@apps-in-toss/bridge-core': '2.10.9',
      '@apps-in-toss/devtools': '3.8.0',
    },
    pnpm: '11.7.0',
  });

  assert.equal(sync.runCheck(root, () => {}), 1);
  const changed = sync.runApply(root, () => {});
  assert.ok(changed.length >= 8, changed.join('\n'));
  assert.equal(sync.runCheck(root, () => {}), 0);

  assert.equal(readJson(root, 'package.json').version, '3.8.0');
  assert.equal(readJson(root, 'package.json').packageManager, 'pnpm@11.7.0');
  assert.equal(readJson(root, GEN).dependencies['@apps-in-toss/web-framework'], '3.8.0');
  assert.equal(readJson(root, BC).devDependencies['@apps-in-toss/devtools'], '3.8.0');
  assert.match(fs.readFileSync(path.join(root, CS), 'utf8'), /PNPM_VERSION = "11\.7\.0";/);
});

test('apply 는 두 번 실행해도 파일을 바꾸지 않고, 관련 없는 내용과 서식을 보존한다', () => {
  const root = makeRepo();
  writeJson(root, sync.SSOT_REL, { ...SSOT, sdkVersion: '3.6.0', webFramework: '3.6.0' });
  sync.runApply(root, () => {});
  const snapshot = [ 'package.json', GEN, BC, CS ].map((f) => fs.readFileSync(path.join(root, f), 'utf8'));

  const second = sync.runApply(root, () => {});
  assert.deepEqual(second, []);
  assert.deepEqual([ 'package.json', GEN, BC, CS ].map((f) => fs.readFileSync(path.join(root, f), 'utf8')), snapshot);

  const pkgText = snapshot[0];
  assert.ok(pkgText.endsWith('}\n'));
  assert.ok(pkgText.includes('"토스"'));
  assert.equal(readJson(root, GEN).dependencies.commander, '15.0.0');
  assert.equal(readJson(root, BC).devDependencies.vite, '8.3.4');
});

test('apply 가 정렬된 섹션에 빠진 필수 키를 넣으면 알파벳 순서를 유지한다', () => {
  const root = makeRepo();
  const gen = readJson(root, GEN);
  delete gen.dependencies['@apps-in-toss/web-analytics'];
  writeJson(root, GEN, gen);
  sync.runApply(root, () => {});
  const keys = Object.keys(readJson(root, GEN).dependencies);
  assert.deepEqual(keys, [...keys].sort());
  assert.ok(keys.includes('@apps-in-toss/web-analytics'));
});

test('기준 파일의 sdkVersion 과 webFramework 가 다르면 형식 오류로 본다', () => {
  const errors = sync.validateSsot({ ...SSOT, sdkVersion: '3.6.0' });
  assert.equal(errors.length, 1);
  assert.match(errors[0], /sdkVersion\(3\.6\.0\).*webFramework\(3\.5\.0\)/);
});

test('기준 파일 형식 오류: 버전 형식, schemaVersion, 쓰이는 곳 없는 항목', () => {
  assert.ok(sync.validateSsot({ ...SSOT, pnpm: '^11.6.0' }).some((e) => e.startsWith('pnpm')));
  assert.ok(sync.validateSsot({ ...SSOT, schemaVersion: 2 }).some((e) => e.startsWith('schemaVersion')));
  const orphan = sync.validateSsot({ ...SSOT, companions: { ...SSOT.companions, '@apps-in-toss/nope': '1.0.0' } });
  assert.ok(orphan.some((e) => e.includes('@apps-in-toss/nope')));
  assert.deepEqual(sync.validateSsot(SSOT), []);
});

test('기준 파일이 올바르지 않으면 check 는 1, apply 는 오류', () => {
  const root = makeRepo({ ssot: { ...SSOT, pnpm: 'latest' } });
  const out = capture();
  assert.equal(sync.runCheck(root, out.log), 1);
  assert.match(out.text(), /기준 파일/);
  assert.throws(() => sync.runApply(root, () => {}), /올바르지 않습니다/);
});

test('resolve: 같은 번호가 있는 패키지는 그 번호, 없으면 latest', () => {
  const lookup = fakeLookup(
    {
      '@apps-in-toss/web-framework': ['3.7.0', '3.8.0'],
      '@apps-in-toss/web-analytics': ['2.10.9'],
      '@apps-in-toss/bridge-core': ['3.8.0'],
      '@apps-in-toss/devtools': ['3.7.0', '3.8.0'],
    },
    { '@apps-in-toss/web-analytics': '2.10.9', '@apps-in-toss/bridge-core': '3.8.0', '@apps-in-toss/devtools': '3.8.0' },
  );
  const root = makeRepo();
  const next = sync.runResolve(root, { version: '3.8.0', lookup }, () => {});

  assert.equal(next.sdkVersion, '3.8.0');
  assert.equal(next.webFramework, '3.8.0');
  assert.deepEqual(next.companions, {
    '@apps-in-toss/web-analytics': '2.10.9',
    '@apps-in-toss/bridge-core': '3.8.0',
    '@apps-in-toss/devtools': '3.8.0',
  });
  assert.equal(next.pnpm, SSOT.pnpm);
  assert.deepEqual(readJson(root, sync.SSOT_REL), next);
});

test('resolve: 요청 버전이 없는 패키지는 그 패키지의 latest 로 떨어진다 (devtools 3.7.0 사고 재발 방지)', () => {
  const lookup = fakeLookup(
    {
      '@apps-in-toss/web-framework': ['3.5.0'],
      '@apps-in-toss/web-analytics': ['2.10.9'],
      '@apps-in-toss/bridge-core': ['2.10.9'],
      '@apps-in-toss/devtools': ['3.5.0', '3.7.0'],
    },
    { '@apps-in-toss/web-analytics': '2.10.9', '@apps-in-toss/bridge-core': '2.10.9', '@apps-in-toss/devtools': '3.7.0' },
  );
  const resolved = sync.resolveCompanions('3.5.0', Object.keys(SSOT.companions), lookup);
  assert.equal(resolved['@apps-in-toss/devtools'], '3.5.0');
  assert.equal(resolved['@apps-in-toss/web-analytics'], '2.10.9');
});

test('resolve --dry-run 은 기준 파일을 쓰지 않는다', () => {
  const lookup = fakeLookup(
    { '@apps-in-toss/web-framework': ['3.8.0'] },
    { '@apps-in-toss/web-analytics': '2.10.9', '@apps-in-toss/bridge-core': '2.10.9', '@apps-in-toss/devtools': '3.8.0' },
  );
  const root = makeRepo();
  sync.runResolve(root, { version: '3.8.0', dryRun: true, lookup }, () => {});
  assert.deepEqual(readJson(root, sync.SSOT_REL), SSOT);
});

test('resolve: 잘못된 버전 형식, npm 에 없는 web-framework 버전은 오류', () => {
  const root = makeRepo();
  const lookup = fakeLookup({ '@apps-in-toss/web-framework': ['3.5.0'] }, {});
  assert.throws(() => sync.runResolve(root, { version: 'latest', lookup }, () => {}), /X\.Y\.Z/);
  assert.throws(() => sync.runResolve(root, { version: '', lookup }, () => {}), /X\.Y\.Z/);
  assert.throws(() => sync.runResolve(root, { version: '9.9.9', lookup }, () => {}), /npm 에 없습니다/);
  assert.deepEqual(readJson(root, sync.SSOT_REL), SSOT);
});

test('resolve: latest 조회 결과가 버전 형식이 아니면 오류', () => {
  const lookup = fakeLookup({ '@apps-in-toss/web-framework': ['3.8.0'] }, { '@apps-in-toss/web-analytics': '' });
  assert.throws(() => sync.resolveCompanions('3.8.0', ['@apps-in-toss/web-analytics'], lookup), /결정하지 못했습니다/);
});

test('CLI: check 는 일치하면 0, 어긋나면 1, 알 수 없는 모드는 2', () => {
  const root = makeRepo();
  const run = (...args) => spawnSync(process.execPath, [SCRIPT, ...args, '--root', root], { encoding: 'utf8' });

  assert.equal(run('check').status, 0);

  const pkg = readJson(root, 'package.json');
  pkg.version = '9.9.9';
  writeJson(root, 'package.json', pkg);
  const bad = run('check');
  assert.equal(bad.status, 1);
  assert.match(bad.stdout, /package\.json: version = 9\.9\.9/);

  assert.equal(run('apply').status, 0);
  assert.equal(run('check').status, 0);
  assert.equal(run('bogus').status, 2);
});

test('CLI: GitHub Actions 환경에서는 어긋난 파일에 ::error 주석을 단다', () => {
  const root = makeRepo();
  const bc = readJson(root, BC);
  bc.dependencies['@apps-in-toss/web-framework'] = '3.4.0';
  writeJson(root, BC, bc);
  const res = spawnSync(process.execPath, [SCRIPT, 'check', '--root', root], {
    encoding: 'utf8',
    env: { ...process.env, GITHUB_ACTIONS: 'true' },
  });
  assert.equal(res.status, 1);
  assert.ok(res.stdout.includes(`::error file=${BC}::`), res.stdout);
});
