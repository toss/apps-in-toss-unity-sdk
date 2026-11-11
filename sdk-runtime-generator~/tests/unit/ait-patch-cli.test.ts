/**
 * ait-patch-cli.mjs 단위 테스트
 *
 * 빌드시점 cli.js 패치의 핵심 순수 함수 `injectRuntimeVersion`을 검증한다.
 * 이 패치는 web-framework 3.x `ait build`가 emit하지 않는 .ait 메타데이터의
 * runtimeVersion을 setMetadata 호출에 주입해 3.x deploy 게이트를 통과시킨다.
 *
 * Unity E2E는 3.x 빌드를 실제로 돌리지 않으므로(비용·fixture 제약), 패치 변환의
 * 정확성(삽입 위치·중첩 brace/문자열 안전·멱등·setMetadata 없는 입력 no-op)을 여기서 단위로 못 박는다.
 *
 * 대상 파일은 SDK 패키지 밖(WebGLTemplates/.../BuildConfig~)에 있으므로 상대경로로
 * 직접 import한다. 스크립트는 `import.meta.url === argv[1]`일 때만 main()을 돌리므로
 * import 시 부작용이 없다.
 */

import { describe, test, expect, afterEach } from 'vitest';
import {
  mkdirSync, mkdtempSync, writeFileSync, readFileSync, rmSync, symlinkSync, linkSync, chmodSync, statSync, realpathSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import {
  injectRuntimeVersion,
  injectIsGame,
  injectMetadataKey,
  resolveIsGame,
  patchSource,
  resolvePatchCandidates,
  patchProject,
  IS_GAME_EXPR,
  RUNTIME_VERSION,
} from '../../../WebGLTemplates/AITTemplate/BuildConfig~/ait-patch-cli.mjs';

/**
 * 주입된 `setMetadata({...})` 단일 표현식을 실제로 평가해 결과 객체를 돌려준다.
 * 삽입이 구문상 유효한지 + 올바른(최상위) 위치에 들어갔는지 실증한다.
 */
function evalObject(injectedExpr: string): any {
  // setMetadata는 인자를 그대로 돌려주는 stub.
  const fn = new Function('setMetadata', `return ${injectedExpr};`);
  return fn((o: any) => o);
}

describe('RUNTIME_VERSION 상수', () => {
  test('@apps-in-toss/cli@2.6.1 RUNTIME_BUILD_DEFINITIONS[0]과 일치하는 0.84.0', () => {
    expect(RUNTIME_VERSION).toBe('0.84.0');
  });
});

describe('injectRuntimeVersion — 정상 주입', () => {
  test('현실적인 3.x cli.js setMetadata 블록에 runtimeVersion을 여는 { 직후로 삽입', () => {
    const src = [
      'async function buildCommand() {',
      '  const writer = new BundleWriter();',
      '  writer.setMetadata({',
      '    platform: PlatformType.WEB,',
      '    sdkVersion: sdkPackageJson.version,',
      '    packageJson: appPackageJson,',
      '  });',
      '  await writer.write(outPath);',
      '}',
    ].join('\n');

    const { changed, source, reason } = injectRuntimeVersion(src);
    expect(changed).toBe(true);
    expect(reason).toContain('0.84.0');
    // 여는 `{` 바로 다음에 삽입.
    expect(source).toContain('writer.setMetadata({\n    runtimeVersion: "0.84.0",');
    // 기존 필드 보존.
    expect(source).toContain('platform: PlatformType.WEB,');
    expect(source).toContain('sdkVersion: sdkPackageJson.version,');
    expect(source).toContain('packageJson: appPackageJson,');
  });

  test('minified 단일 라인 setMetadata도 주입하고 구문이 유효', () => {
    const expr = 'setMetadata({platform:"WEB",sdkVersion:"3.0.0"})';
    const { changed, source } = injectRuntimeVersion(expr);
    expect(changed).toBe(true);
    const meta = evalObject(source);
    expect(meta.runtimeVersion).toBe('0.84.0');
    expect(meta.platform).toBe('WEB');
    expect(meta.sdkVersion).toBe('3.0.0');
  });

  test('runtimeVersion 인자를 명시하면 그 값이 들어간다', () => {
    const { changed, source } = injectRuntimeVersion('setMetadata({a:1})', '9.9.9');
    expect(changed).toBe(true);
    expect(evalObject(source).runtimeVersion).toBe('9.9.9');
  });
});

describe('injectRuntimeVersion — brace 파서 견고성', () => {
  test('중첩 객체 + 문자열 내부 중괄호에 속지 않고 최상위 키로 삽입', () => {
    const expr =
      'setMetadata({ platform: "WEB", extra: { tag: "a}b{c", deep: { y: 2 } }, sdkVersion: "3.0.0" })';
    const { changed, source } = injectRuntimeVersion(expr);
    expect(changed).toBe(true);

    const meta = evalObject(source);
    // runtimeVersion이 최상위 키여야 한다(중첩 객체 안이 아니라).
    expect(meta.runtimeVersion).toBe('0.84.0');
    expect(meta.platform).toBe('WEB');
    expect(meta.extra.tag).toBe('a}b{c');
    expect(meta.extra.deep.y).toBe(2);
    expect(meta.sdkVersion).toBe('3.0.0');
  });

  test('객체 끝보다 앞서 등장하는 문자열 } 들에 종료를 오인하지 않는다(멱등 스캔 검증)', () => {
    const expr = 'setMetadata({ a: "}}}", b: 1 })';
    const once = injectRuntimeVersion(expr);
    expect(once.changed).toBe(true);
    expect(evalObject(once.source).a).toBe('}}}');

    // 두 번째 주입은 no-op이어야 한다 — findSetMetadataObject가 문자열 } 너머
    // 진짜 객체 끝까지 스캔해 runtimeVersion 존재를 정확히 감지함을 의미.
    const twice = injectRuntimeVersion(once.source);
    expect(twice.changed).toBe(false);
  });
});

describe('injectRuntimeVersion — 멱등 / no-op (안전 규칙)', () => {
  test('이미 runtimeVersion이 있으면 no-op (멱등 / upstream emit)', () => {
    const expr = 'setMetadata({ runtimeVersion: "0.84.0", platform: "WEB" })';
    const { changed } = injectRuntimeVersion(expr);
    expect(changed).toBe(false);
  });

  test('한 번 주입한 결과를 다시 넣어도 no-op', () => {
    const src = 'writer.setMetadata({\n    platform: "WEB",\n  })';
    const first = injectRuntimeVersion(src);
    expect(first.changed).toBe(true);
    const second = injectRuntimeVersion(first.source);
    expect(second.changed).toBe(false);
    expect(second.source).toBe(first.source);
  });

  test('setMetadata 호출이 없으면 no-op (구형 cli.js 안전)', () => {
    const src = 'function build() { return granite.build(); }';
    const { changed, source } = injectRuntimeVersion(src);
    expect(changed).toBe(false);
    expect(source).toBe(src);
  });

  test('setMetadata 인자가 객체 리터럴이 아니면 패치하지 않는다', () => {
    const src = 'const meta = buildMeta(); writer.setMetadata(meta);';
    const { changed } = injectRuntimeVersion(src);
    expect(changed).toBe(false);
  });

  test('괄호가 닫히지 않은 깨진 입력에도 안전하게 no-op', () => {
    const src = 'writer.setMetadata({ platform: "WEB",';
    const { changed } = injectRuntimeVersion(src);
    expect(changed).toBe(false);
  });
});

/** 주입된 소스를 AIT_IS_GAME 값으로 평가한다(ait build 시점 동작 재현). */
function evalWithEnv(injectedExpr: string, envValue: string | undefined): any {
  const env: Record<string, string | undefined> = { AIT_IS_GAME: envValue };
  // `writer.setMetadata(...)`과 단독 `setMetadata(...)` 호출 형태를 모두 평가할 수 있게 둘 다 stub으로 준다.
  const fn = new Function('writer', 'setMetadata', 'process', `return ${injectedExpr};`);
  return fn({ setMetadata: (o: any) => o }, (o: any) => o, { env });
}

describe('injectIsGame', () => {
  const base = 'writer.setMetadata({\n    platform: "WEB",\n    sdkVersion: "3.0.0",\n  })';

  test('여는 { 직후에 isGame 주입, 기존 필드 보존', () => {
    const { changed, source } = injectIsGame(base);
    expect(changed).toBe(true);
    expect(source).toContain(`writer.setMetadata({\n    isGame: ${IS_GAME_EXPR},`);
    expect(source).toContain('platform: "WEB",');
  });

  test.each([
    ['true', true],
    ['false', false],
    ['FALSE', false],
    [' false ', false],
    [undefined, true],
    ['', true],
    ['garbage', true],
  ])('AIT_IS_GAME=%j -> isGame=%s (런타임 평가)', (env, expected) => {
    const { source } = injectIsGame(base);
    expect(evalWithEnv(source, env as string | undefined).isGame).toBe(expected);
  });

  test('같은 주입 소스가 env만 바꿔도 값이 따라간다(stale 값 고정 없음)', () => {
    const { source } = injectIsGame(base);
    expect(evalWithEnv(source, 'true').isGame).toBe(true);
    expect(evalWithEnv(source, 'false').isGame).toBe(false);
  });

  test('멱등: 두 번째 주입은 no-op이고 키가 중복되지 않는다', () => {
    const first = injectIsGame(base);
    const second = injectIsGame(first.source);
    expect(second.changed).toBe(false);
    expect(second.source).toBe(first.source);
    expect(first.source.match(/\bisGame\s*:/g)).toHaveLength(1);
  });

  test('upstream이 이미 isGame을 emit하면 no-op (값 형태 무관, 단축 프로퍼티 포함)', () => {
    for (const expr of [
      'setMetadata({ isGame: true, platform: "WEB" })',
      'setMetadata({ platform: "WEB", isGame: opts.isGame })',
      'setMetadata({ platform: "WEB", isGame })',
    ]) {
      const r = injectIsGame(expr);
      expect(r.changed).toBe(false);
      expect(r.source).toBe(expr);
    }
  });

  test('중첩 객체 안의 문자열/키에 속지 않고 최상위에 주입', () => {
    const expr = 'setMetadata({ extra: { tag: "}{" }, platform: "WEB" })';
    const { source } = injectIsGame(expr);
    const meta = evalWithEnv(source, 'false');
    expect(meta.isGame).toBe(false);
    expect(meta.extra.tag).toBe('}{');
  });

  test('setMetadata 없음 / 비리터럴 인자는 no-op', () => {
    expect(injectIsGame('function f() {}').changed).toBe(false);
    expect(injectIsGame('writer.setMetadata(meta);').changed).toBe(false);
  });

  test('runtimeVersion과 isGame을 연이어 주입해도 둘 다 한 번씩만 들어간다', () => {
    const a = injectRuntimeVersion(base);
    const b = injectIsGame(a.source);
    expect(b.changed).toBe(true);
    const meta = evalWithEnv(b.source, 'false');
    expect(meta.runtimeVersion).toBe('0.84.0');
    expect(meta.isGame).toBe(false);
    const again = injectIsGame(injectRuntimeVersion(b.source).source);
    expect(again.changed).toBe(false);
    expect(again.source).toBe(b.source);
  });

  test('upstream에 runtimeVersion만 있어도 isGame은 독립적으로 주입된다', () => {
    const { changed, source } = injectIsGame('setMetadata({ runtimeVersion: "1.0.0" })');
    expect(changed).toBe(true);
    expect(evalWithEnv(source, undefined).isGame).toBe(true);
  });
});

describe('resolveIsGame / injectMetadataKey', () => {
  test('resolveIsGame은 "false"만 false', () => {
    expect(resolveIsGame('false')).toBe(false);
    expect(resolveIsGame('true')).toBe(true);
    expect(resolveIsGame(undefined)).toBe(true);
    expect(resolveIsGame('0')).toBe(true);
  });

  test('injectMetadataKey는 임의 키를 주입하고 멱등', () => {
    const a = injectMetadataKey('setMetadata({a:1})', 'foo', '"bar"', 'desc');
    expect(a.changed).toBe(true);
    expect(evalObject(a.source).foo).toBe('bar');
    expect(injectMetadataKey(a.source, 'foo', '"baz"', 'desc').changed).toBe(false);
  });
});

// ───────────────────────── 실제 @apps-in-toss/cli 구조 ─────────────────────────

/**
 * @apps-in-toss/cli@3.9.0 dist/index.js의 buildArtifact 블록(원문 그대로 고정한 스니펫).
 * 3.1.0 ~ 3.9.0 전 버전에서 dist/index.js는 binaryVersion 한 줄을 빼고 동일하다(실측).
 * `ait` bin(web-framework/bin/ait.js)은 `import "@apps-in-toss/cli"`만 하고 setMetadata 호출은 여기에만 있다.
 * 합성 소스가 아니라 이 스니펫으로 검증해, 대상 파일/앵커가 다시 어긋나면 테스트가 깨지게 한다.
 */
const REAL_CLI_SNIPPET = `//#region src/commands/build/buildArtifact.ts
async function buildArtifact() {
\tconst projectRoot = getPackageRoot(process.cwd());
\tconst sdkPackageJson = await getSDKPackageJson();
\tconst appsInTossConfig = await getAppsInTossConfig();
\tconst deploymentId = uuidv7();
\tconst writer = AppsInTossBundle.writer({
\t\tdeploymentId,
\t\tappName: appsInTossConfig.appName,
\t\tcreatedBy: sdkPackageJson.name
\t});
\tconst unityMetadata = getUnityMetadata();
\twriter.setMetadata({
\t\tplatform: PlatformType.WEB,
\t\tsdkVersion: sdkPackageJson.version,
\t\tpackageJson: projectPackageJson,
\t\textra: {
\t\t\tunityMetadata,
\t\t\tappsInTossConfig
\t\t}
\t});
\tappsInTossConfig.permissions.forEach(({ name, access }) => writer.addPermission(name, access));
}
`;

/** 패치된 스니펫의 writer.setMetadata({...}) 호출을 실제로 실행해 넘겨진 객체를 돌려준다. */
function runPatchedSnippet(patched: string, aitIsGame: string | undefined): any {
  const start = patched.indexOf('writer.setMetadata(');
  const end = patched.indexOf('appsInTossConfig.permissions');
  const call = patched.slice(start, end);
  let captured: any;
  const fn = new Function(
    'writer', 'PlatformType', 'sdkPackageJson', 'projectPackageJson', 'unityMetadata', 'appsInTossConfig', 'process',
    call,
  );
  fn({ setMetadata: (o: any) => { captured = o; } }, { WEB: 'WEB' }, { version: '3.9.0' }, { name: 'p' }, { u: 1 }, { appName: 'a' }, {
    env: { AIT_IS_GAME: aitIsGame },
  });
  return captured;
}

describe('patchSource — 실제 cli 스니펫', () => {
  test('runtimeVersion과 isGame이 setMetadata 최상위에 주입되고 기존 필드는 보존된다', () => {
    const r = patchSource(REAL_CLI_SNIPPET);
    expect(r.found).toBe(true);
    expect(r.changed).toBe(true);
    const meta = runPatchedSnippet(r.source, 'false');
    expect(meta.runtimeVersion).toBe(RUNTIME_VERSION);
    expect(meta.isGame).toBe(false);
    expect(meta.platform).toBe('WEB');
    expect(meta.sdkVersion).toBe('3.9.0');
    expect(meta.extra.unityMetadata).toEqual({ u: 1 });
    expect(runPatchedSnippet(r.source, 'true').isGame).toBe(true);
    expect(runPatchedSnippet(r.source, undefined).isGame).toBe(true);
  });

  test('멱등: 두 번째 패치는 no-op이고 키가 중복되지 않는다', () => {
    const first = patchSource(REAL_CLI_SNIPPET);
    const second = patchSource(first.source);
    expect(second.found).toBe(true);
    expect(second.changed).toBe(false);
    expect(second.source).toBe(first.source);
    expect(first.source.match(/\bisGame\s*:/g)).toHaveLength(1);
    expect(first.source.match(/\bruntimeVersion\s*:/g)).toHaveLength(1);
  });

  test('앵커가 없는 소스는 found=false (대상 파일이 어긋났음을 호출부가 알 수 있다)', () => {
    expect(patchSource('console.log("no metadata here")').found).toBe(false);
  });
});

describe('setMetadata 탐색 견고성', () => {
  test('중첩 객체 안의 isGame/runtimeVersion 키는 최상위 키로 취급하지 않는다', () => {
    const src = 'writer.setMetadata({ platform: 1, extra: { isGame: false, runtimeVersion: "x" } })';
    const r = patchSource(src);
    expect(r.changed).toBe(true);
    expect(r.source).toContain('writer.setMetadata({\n    isGame:');
  });

  test('값 문자열 안의 "isGame:" 텍스트에 속지 않는다', () => {
    const src = 'writer.setMetadata({ note: "isGame: x, runtimeVersion: y", platform: 1 })';
    expect(patchSource(src).changed).toBe(true);
  });

  test('따옴표 키 "isGame": 도 이미 존재하는 키로 본다', () => {
    const src = 'writer.setMetadata({ "isGame": true, "runtimeVersion": "1" })';
    expect(patchSource(src).changed).toBe(false);
  });

  test('메서드 정의 setMetadata(e){...}는 건너뛰고 뒤의 실제 호출만 패치한다', () => {
    const src = 'class W { setMetadata(e) { return e; } }\nw.setMetadata({ platform: 1 });';
    const r = patchSource(src);
    expect(r.found).toBe(true);
    expect(r.changed).toBe(true);
    expect(r.source.startsWith('class W { setMetadata(e) { return e; } }')).toBe(true);
    expect(r.source).toContain('w.setMetadata({\n    isGame:');
  });

  test('호출이 여러 개면 모두 패치한다', () => {
    const src = 'a.setMetadata({ x: 1 });\nb.setMetadata({ y: 2 });';
    const r = patchSource(src);
    expect(r.source.match(/\bisGame\s*:/g)).toHaveLength(2);
    expect(r.source.match(/\bruntimeVersion\s*:/g)).toHaveLength(2);
    expect(patchSource(r.source).changed).toBe(false);
  });
});

// ───────────────────────── 패치 대상 해석 / 파일 패치 ─────────────────────────

const SUPPORTED_VERSIONS = ['3.1.0', '3.1.1', '3.2.0', '3.3.0', '3.4.0', '3.4.1', '3.5.0', '3.6.0', '3.7.0', '3.8.0', '3.9.0'];

const tmpRoots: string[] = [];
function makeTmp(): string {
  const d = mkdtempSync(join(tmpdir(), 'ait-patch-test-'));
  tmpRoots.push(d);
  return d;
}
afterEach(() => {
  while (tmpRoots.length) rmSync(tmpRoots.pop()!, { recursive: true, force: true });
});

function writePkg(dir: string, pkg: Record<string, unknown>, files: Record<string, string> = {}) {
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, 'package.json'), JSON.stringify(pkg));
  for (const [rel, body] of Object.entries(files)) {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), body);
  }
}

function webFrameworkPkg(version: string, withPackageJsonExport = true) {
  return {
    name: '@apps-in-toss/web-framework',
    version,
    type: 'module',
    bin: { ait: 'bin/ait.js' },
    exports: {
      '.': { import: './dist/index.js', require: './dist/index.cjs' },
      ...(withPackageJsonExport ? { './package.json': './package.json' } : {}),
    },
    dependencies: { '@apps-in-toss/cli': version },
  };
}

function cliPkg(version: string) {
  return {
    name: '@apps-in-toss/cli',
    version,
    type: 'module',
    bin: { ait: 'dist/index.js' },
    exports: { '.': './dist/index.js', './package.json': './package.json' },
  };
}

const WF_FILES = { 'bin/ait.js': 'import "@apps-in-toss/cli";\n', 'dist/index.js': 'export {};\n' };

/** pnpm 구조: 프로젝트 node_modules/@apps-in-toss/web-framework -> .pnpm 가상 스토어, cli는 그 형제 symlink. */
function makePnpmProject(version: string, opts: { withPackageJsonExport?: boolean } = {}) {
  const root = makeTmp();
  const pnpm = join(root, 'node_modules', '.pnpm');
  const wfReal = join(pnpm, `@apps-in-toss+web-framework@${version}`, 'node_modules', '@apps-in-toss', 'web-framework');
  const cliReal = join(pnpm, `@apps-in-toss+cli@${version}`, 'node_modules', '@apps-in-toss', 'cli');
  writePkg(join(root), { name: 'proj', dependencies: { '@apps-in-toss/web-framework': version } });
  writePkg(wfReal, webFrameworkPkg(version, opts.withPackageJsonExport ?? true), WF_FILES);
  writePkg(cliReal, cliPkg(version), { 'dist/index.js': REAL_CLI_SNIPPET, 'dist/index.d.ts': 'export {};' });
  // web-framework의 형제로 cli symlink (pnpm 방식)
  const siblingScope = join(pnpm, `@apps-in-toss+web-framework@${version}`, 'node_modules', '@apps-in-toss');
  symlinkSync(cliReal, join(siblingScope, 'cli'), 'dir');
  // 프로젝트 직접 의존만 최상위 node_modules에 symlink (cli는 hoist되지 않는다)
  mkdirSync(join(root, 'node_modules', '@apps-in-toss'), { recursive: true });
  symlinkSync(wfReal, join(root, 'node_modules', '@apps-in-toss', 'web-framework'), 'dir');
  return { root, wfReal, cliReal, cliFile: join(cliReal, 'dist', 'index.js') };
}

function recordingLog() {
  const logs: string[] = [];
  const warns: string[] = [];
  return {
    logs, warns,
    log: (...a: unknown[]) => { logs.push(a.join(' ')); },
    warn: (...a: unknown[]) => { warns.push(a.join(' ')); },
  };
}

describe.each(SUPPORTED_VERSIONS)('pnpm 레이아웃 — web-framework %s', (version) => {
  test('web-framework -> 형제 cli를 따라 dist/index.js를 찾아 패치한다', () => {
    const { root, cliFile } = makePnpmProject(version);
    const log = recordingLog();
    const res = patchProject(root, log);
    expect(res.ok).toBe(true);
    expect(res.patched).toHaveLength(1);
    expect(res.patched[0]).toBe(realpathOf(cliFile));
    const patched = readFileSync(cliFile, 'utf8');
    expect(patched).toContain(`runtimeVersion: "${RUNTIME_VERSION}"`);
    expect(patched).toContain('isGame:');
    expect(log.warns.filter((w) => w.includes('WARNING'))).toHaveLength(0);
    expect(log.logs.join('\n')).toContain(realpathOf(cliFile));
  });
});

function realpathOf(p: string) { return realpathSync(p); }

describe('resolvePatchCandidates', () => {
  test('web-framework exports에 ./package.json이 없어도 node_modules 직접 경로로 폴백해 찾는다', () => {
    const { root, cliFile } = makePnpmProject('3.9.0', { withPackageJsonExport: false });
    const { candidates } = resolvePatchCandidates(root);
    expect(candidates).toContain(realpathOf(cliFile));
  });

  test('npm/yarn 평탄 구조: 프로젝트 node_modules에 cli가 hoist돼 있어도 한 번만 나온다', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    writePkg(join(root, 'node_modules/@apps-in-toss/web-framework'), webFrameworkPkg('3.9.0'), WF_FILES);
    writePkg(join(root, 'node_modules/@apps-in-toss/cli'), cliPkg('3.9.0'), { 'dist/index.js': REAL_CLI_SNIPPET });
    const { candidates } = resolvePatchCandidates(root);
    expect(candidates).toEqual([realpathOf(join(root, 'node_modules/@apps-in-toss/cli/dist/index.js'))]);
  });

  test('web-framework 하위 node_modules에 중첩된 cli도 web-framework 기준으로 찾는다', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    const wf = join(root, 'node_modules/@apps-in-toss/web-framework');
    writePkg(wf, webFrameworkPkg('3.9.0'), WF_FILES);
    writePkg(join(wf, 'node_modules/@apps-in-toss/cli'), cliPkg('3.9.0'), { 'dist/index.js': REAL_CLI_SNIPPET });
    const { candidates } = resolvePatchCandidates(root);
    expect(candidates).toEqual([realpathOf(join(wf, 'node_modules/@apps-in-toss/cli/dist/index.js'))]);
  });

  test('레거시: web-framework/dist/cli.js만 있어도 후보에 포함된다', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    const wf = join(root, 'node_modules/@apps-in-toss/web-framework');
    writePkg(wf, { ...webFrameworkPkg('3.0.0'), dependencies: {} }, { ...WF_FILES, 'dist/cli.js': REAL_CLI_SNIPPET });
    const { candidates } = resolvePatchCandidates(root);
    expect(candidates).toEqual([realpathOf(join(wf, 'dist/cli.js'))]);
    const res = patchProject(root, recordingLog());
    expect(res.patched).toEqual([realpathOf(join(wf, 'dist/cli.js'))]);
  });

  test('cli bin 필드가 가리키는 파일과 같은 디렉터리의 chunk(.js)까지 후보로 본다', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    writePkg(join(root, 'node_modules/@apps-in-toss/web-framework'), webFrameworkPkg('4.0.0'), WF_FILES);
    writePkg(
      join(root, 'node_modules/@apps-in-toss/cli'),
      { ...cliPkg('4.0.0'), bin: { ait: 'dist/main.js' } },
      { 'dist/main.js': 'import "./chunk.js";', 'dist/chunk.js': REAL_CLI_SNIPPET, 'dist/index.d.ts': 'x' },
    );
    const res = patchProject(root, recordingLog());
    expect(res.patched).toHaveLength(1);
    expect(res.patched[0].endsWith('chunk.js')).toBe(true);
    expect(res.noAnchor).toHaveLength(1); // main.js: setMetadata 없음 -> 건드리지 않음
  });
});

describe('patchProject — 안전 규칙', () => {
  test('두 번 실행해도 두 번째는 unchanged (멱등)', () => {
    const { root, cliFile } = makePnpmProject('3.9.0');
    patchProject(root, recordingLog());
    const once = readFileSync(cliFile, 'utf8');
    const res = patchProject(root, recordingLog());
    expect(res.patched).toHaveLength(0);
    expect(res.unchanged).toHaveLength(1);
    expect(res.ok).toBe(true);
    expect(readFileSync(cliFile, 'utf8')).toBe(once);
  });

  test('upstream이 이미 두 키를 emit하면 파일을 건드리지 않는다', () => {
    const { root, cliFile } = makePnpmProject('3.9.0');
    const upstream = 'w.setMetadata({ isGame: opts.g, runtimeVersion: "9.9.9", platform: 1 });';
    writeFileSync(cliFile, upstream);
    const res = patchProject(root, recordingLog());
    expect(res.unchanged).toHaveLength(1);
    expect(readFileSync(cliFile, 'utf8')).toBe(upstream);
  });

  test('pnpm 하드링크: 패치 후에도 하드링크된 다른 경로(공유 store)는 원본 그대로, 파일 모드는 보존', () => {
    const { root, cliFile } = makePnpmProject('3.9.0');
    const storeCopy = join(root, 'store-copy.js');
    linkSync(cliFile, storeCopy);
    chmodSync(cliFile, 0o755);
    expect(statSync(cliFile).nlink).toBe(2);

    patchProject(root, recordingLog());

    expect(readFileSync(storeCopy, 'utf8')).toBe(REAL_CLI_SNIPPET); // store 쪽은 변조되지 않음
    expect(readFileSync(cliFile, 'utf8')).toContain('isGame:');
    expect(statSync(cliFile).nlink).toBe(1);
    expect(statSync(cliFile).mode & 0o777).toBe(0o755);
  });

  test('대상이 하나도 없으면 조용히 넘어가지 않고 큰 경고를 남긴다 (throw 없음, ok=false)', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    const log = recordingLog();
    const res = patchProject(root, log);
    expect(res.ok).toBe(false);
    expect(res.patched).toHaveLength(0);
    const warn = log.warns.join('\n');
    expect(warn).toContain('WARNING');
    expect(warn).toContain('isGame=false');
    expect(warn).toContain('runtimeVersion=""');
  });

  test('파일은 있지만 setMetadata 앵커가 없으면(구조 변경) 큰 경고, 파일은 그대로', () => {
    const { root, cliFile } = makePnpmProject('3.9.0');
    const moved = 'export const build = () => writer.setMetadataV2(meta);\n';
    writeFileSync(cliFile, moved);
    const log = recordingLog();
    const res = patchProject(root, log);
    expect(res.ok).toBe(false);
    expect(res.noAnchor).toHaveLength(1);
    expect(log.warns.join('\n')).toContain('WARNING');
    expect(readFileSync(cliFile, 'utf8')).toBe(moved);
  });

  test('GITHUB_ACTIONS에서는 ::warning:: 주석도 남긴다', () => {
    const prev = process.env.GITHUB_ACTIONS;
    process.env.GITHUB_ACTIONS = 'true';
    try {
      const root = makeTmp();
      writePkg(root, { name: 'proj' });
      const log = recordingLog();
      patchProject(root, log);
      expect(log.logs.some((l) => l.startsWith('::warning'))).toBe(true);
    } finally {
      if (prev === undefined) delete process.env.GITHUB_ACTIONS; else process.env.GITHUB_ACTIONS = prev;
    }
  });
});
