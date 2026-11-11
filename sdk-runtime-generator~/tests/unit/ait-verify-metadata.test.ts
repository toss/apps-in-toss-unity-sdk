/**
 * ait-verify-metadata.mjs 단위 테스트
 *
 * `ait build` 직후 .ait를 열어 isGame/runtimeVersion을 확인하는 가드의 판정 로직과 종료 코드를 검증한다.
 * 실제 @apps-in-toss/ait-format 대신, 같은 API(AITReader.fromBuffer → metadata/appName)를 흉내 낸 가짜 패키지를
 * pnpm 레이아웃(.pnpm 가상 스토어 + 형제 symlink)에 두고 resolve부터 종료 코드까지 이어서 확인한다.
 */

import { describe, test, expect, afterEach } from 'vitest';
import {
  mkdirSync, mkdtempSync, writeFileSync, rmSync, symlinkSync, realpathSync, utimesSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import {
  expectedIsGame,
  isStrict,
  summarizeMetadata,
  evaluateMetadata,
  listAitFiles,
  pickNewest,
  findPackageJson,
  resolveAitFormat,
  verify,
} from '../../../WebGLTemplates/AITTemplate/BuildConfig~/ait-verify-metadata.mjs';

const tmpRoots: string[] = [];
function makeTmp(): string {
  const d = mkdtempSync(join(tmpdir(), 'ait-verify-test-'));
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

/**
 * 가짜 ait-format: .ait 파일의 본문(JSON)을 그대로 metadata로 돌려준다.
 * 실제 패키지처럼 exports는 두고 main은 CJS 엔트리로 둔다.
 */
const FAKE_AIT_FORMAT = `
exports.AITReader = {
  fromBuffer(buf) {
    const j = JSON.parse(Buffer.from(buf).toString('utf8'));
    return { appName: j.appName, metadata: j.metadata };
  },
};
`;

/** pnpm 구조: 프로젝트 -> web-framework(.pnpm) -> 형제 cli(.pnpm) -> 형제 ait-format(.pnpm). 프로젝트에는 ait-format이 없다. */
function makePnpmProject() {
  const root = makeTmp();
  const pnpm = join(root, 'node_modules', '.pnpm');
  const scope = (id: string) => join(pnpm, id, 'node_modules', '@apps-in-toss');
  const wf = join(scope('wf@3.9.0'), 'web-framework');
  const cli = join(scope('cli@3.9.0'), 'cli');
  const fmt = join(scope('fmt@1.0.0'), 'ait-format');
  writePkg(root, { name: 'proj' });
  writePkg(wf, { name: '@apps-in-toss/web-framework', version: '3.9.0' });
  writePkg(cli, { name: '@apps-in-toss/cli', version: '3.9.0' });
  writePkg(fmt, { name: '@apps-in-toss/ait-format', version: '1.0.0', main: './dist/index.js', exports: { '.': { require: './dist/index.js' } } }, {
    'dist/index.js': FAKE_AIT_FORMAT,
  });
  symlinkSync(cli, join(scope('wf@3.9.0'), 'cli'), 'dir');
  symlinkSync(fmt, join(scope('cli@3.9.0'), 'ait-format'), 'dir');
  mkdirSync(join(root, 'node_modules', '@apps-in-toss'), { recursive: true });
  symlinkSync(wf, join(root, 'node_modules', '@apps-in-toss', 'web-framework'), 'dir');
  return { root, fmt };
}

function writeAit(root: string, name: string, appName: string, metadata: Record<string, unknown> | undefined) {
  const p = join(root, name);
  writeFileSync(p, JSON.stringify({ appName, metadata }));
  return p;
}

function recordingLog() {
  const out: { log: string[]; warn: string[]; error: string[] } = { log: [], warn: [], error: [] };
  return {
    out,
    log: (...a: unknown[]) => { out.log.push(a.join(' ')); },
    warn: (...a: unknown[]) => { out.warn.push(a.join(' ')); },
    error: (...a: unknown[]) => { out.error.push(a.join(' ')); },
  };
}

describe('expectedIsGame / isStrict', () => {
  test('expectedIsGame은 "false"(대소문자·공백 무시)만 false, 나머지는 true (패치와 같은 규칙)', () => {
    expect(expectedIsGame('false')).toBe(false);
    expect(expectedIsGame(' FALSE ')).toBe(false);
    expect(expectedIsGame('true')).toBe(true);
    expect(expectedIsGame(undefined)).toBe(true);
    expect(expectedIsGame('')).toBe(true);
    expect(expectedIsGame('0')).toBe(true);
  });

  test.each([
    ['1', true], ['true', true], [' TRUE ', true],
    ['0', false], ['false', false], ['', false], [undefined, false], ['yes', false],
  ])('AIT_STRICT_METADATA=%j -> %s', (raw, expected) => {
    expect(isStrict(raw as string | undefined)).toBe(expected);
  });
});

describe('summarizeMetadata', () => {
  test('metadata가 없으면 기본값(isGame=false, 빈 문자열)', () => {
    expect(summarizeMetadata({ appName: 'a' })).toEqual({ appName: 'a', isGame: false, runtimeVersion: '', sdkVersion: '' });
    expect(summarizeMetadata(undefined)).toEqual({ appName: '', isGame: false, runtimeVersion: '', sdkVersion: '' });
  });

  test('값이 있으면 그대로 옮긴다', () => {
    const r = summarizeMetadata({ appName: 'a', metadata: { isGame: true, runtimeVersion: '0.84.0', sdkVersion: '3.9.0' } });
    expect(r).toEqual({ appName: 'a', isGame: true, runtimeVersion: '0.84.0', sdkVersion: '3.9.0' });
  });
});

describe('evaluateMetadata', () => {
  const good = { isGame: true, runtimeVersion: '0.84.0' };

  test('AIT_IS_GAME=true + isGame=true + runtimeVersion 있음 -> ok', () => {
    expect(evaluateMetadata(good, { AIT_IS_GAME: 'true' })).toEqual({ ok: true, problems: [] });
  });

  test('AIT_IS_GAME=false + isGame=false -> ok (partner 빌드)', () => {
    expect(evaluateMetadata({ ...good, isGame: false }, { AIT_IS_GAME: 'false' }).ok).toBe(true);
  });

  test('AIT_IS_GAME 미설정은 true로 기대한다', () => {
    expect(evaluateMetadata(good, {}).ok).toBe(true);
    expect(evaluateMetadata({ ...good, isGame: false }, {}).ok).toBe(false);
  });

  test('패치가 무음 no-op이던 시절의 결과(isGame=false, runtimeVersion="")는 둘 다 문제로 보고한다', () => {
    const r = evaluateMetadata({ isGame: false, runtimeVersion: '' }, { AIT_IS_GAME: 'true' });
    expect(r.ok).toBe(false);
    expect(r.problems).toHaveLength(2);
    expect(r.problems[0]).toContain('isGame');
    expect(r.problems[1]).toContain('runtimeVersion');
  });

  test('isGame만 어긋나도 실패', () => {
    const r = evaluateMetadata({ ...good, isGame: true }, { AIT_IS_GAME: 'false' });
    expect(r.ok).toBe(false);
    expect(r.problems).toHaveLength(1);
  });
});

describe('listAitFiles / pickNewest', () => {
  test('확장자가 정확히 .ait인 파일만 고른다 (.aitpart, 디렉터리 제외)', () => {
    const d = makeTmp();
    writeFileSync(join(d, 'a.ait'), '');
    writeFileSync(join(d, 'b.AIT'), '');
    writeFileSync(join(d, 'c.aitpart'), '');
    writeFileSync(join(d, 'ait'), '');
    expect(listAitFiles(d).map((f) => f.split('/').pop()).sort()).toEqual(['a.ait', 'b.AIT']);
  });

  test('디렉터리가 없으면 빈 배열', () => {
    expect(listAitFiles(join(makeTmp(), 'nope'))).toEqual([]);
  });

  test('pickNewest는 mtime이 가장 큰 파일, 비어 있으면 null, stat 실패 파일은 건너뜀', () => {
    const t: Record<string, number> = { a: 1, b: 5, c: 3 };
    expect(pickNewest(['a', 'b', 'c'], (f) => t[f])).toBe('b');
    expect(pickNewest([], () => 0)).toBeNull();
    expect(pickNewest(['x', 'a'], (f) => { if (f === 'x') throw new Error('gone'); return t[f]; })).toBe('a');
  });
});

describe('findPackageJson / resolveAitFormat', () => {
  test('pnpm 구조에서 web-framework -> cli -> ait-format 체인으로 찾는다 (프로젝트에는 ait-format이 없다)', () => {
    const { root, fmt } = makePnpmProject();
    expect(resolveAitFormat(root)).toBe(realpathSync(fmt));
  });

  test('프로젝트에 직접 설치된 평탄 구조도 찾는다', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    writePkg(join(root, 'node_modules/@apps-in-toss/ait-format'), { name: '@apps-in-toss/ait-format', main: './dist/index.js' }, {
      'dist/index.js': FAKE_AIT_FORMAT,
    });
    expect(resolveAitFormat(root)).toBe(realpathSync(join(root, 'node_modules/@apps-in-toss/ait-format')));
  });

  test('어디에도 없으면 null', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    expect(resolveAitFormat(root)).toBeNull();
    expect(findPackageJson(join(root, 'package.json'), '@apps-in-toss/cli')).toBeNull();
  });
});

describe('verify — 종료 코드', () => {
  const good = { isGame: true, runtimeVersion: '0.84.0', sdkVersion: '3.9.0' };
  const bad = { isGame: false, runtimeVersion: '', sdkVersion: '3.9.0' };

  test('일치하면 0이고 isGame/runtimeVersion/sdkVersion을 출력한다', () => {
    const { root } = makePnpmProject();
    writeAit(root, 'app.ait', 'app', good);
    const log = recordingLog();
    const code = verify({ root, env: { AIT_IS_GAME: 'true', AIT_STRICT_METADATA: '1' }, log });
    expect(code).toBe(0);
    const text = log.out.log.join('\n');
    expect(text).toContain('isGame=true');
    expect(text).toContain('runtimeVersion="0.84.0"');
    expect(text).toContain('sdkVersion="3.9.0"');
    expect(log.out.warn).toHaveLength(0);
  });

  test('불일치 + 기본 모드: 경고만 남기고 0', () => {
    const { root } = makePnpmProject();
    writeAit(root, 'app.ait', 'app', bad);
    const log = recordingLog();
    const code = verify({ root, env: { AIT_IS_GAME: 'true' }, log });
    expect(code).toBe(0);
    expect(log.out.warn.join('\n')).toContain('WARNING');
    expect(log.out.warn.join('\n')).toContain('isGame 불일치');
    expect(log.out.error).toHaveLength(0);
  });

  test('불일치 + AIT_STRICT_METADATA=1: 1', () => {
    const { root } = makePnpmProject();
    writeAit(root, 'app.ait', 'app', bad);
    const log = recordingLog();
    const code = verify({ root, env: { AIT_IS_GAME: 'true', AIT_STRICT_METADATA: '1' }, log });
    expect(code).toBe(1);
    expect(log.out.warn.join('\n')).toContain('runtimeVersion');
    expect(log.out.error.join('\n')).toContain('AIT_STRICT_METADATA=1');
  });

  test('GITHUB_ACTIONS에서 strict면 ::error::, 아니면 ::warning:: 주석', () => {
    const { root } = makePnpmProject();
    writeAit(root, 'app.ait', 'app', bad);
    const strictLog = recordingLog();
    verify({ root, env: { GITHUB_ACTIONS: 'true', AIT_STRICT_METADATA: '1' }, log: strictLog });
    expect(strictLog.out.log.some((l) => l.startsWith('::error'))).toBe(true);
    const softLog = recordingLog();
    verify({ root, env: { GITHUB_ACTIONS: 'true' }, log: softLog });
    expect(softLog.out.log.some((l) => l.startsWith('::warning'))).toBe(true);
  });

  test('.ait가 없으면: 기본 0(경고), strict 1', () => {
    const { root } = makePnpmProject();
    expect(verify({ root, env: {}, log: recordingLog() })).toBe(0);
    expect(verify({ root, env: { AIT_STRICT_METADATA: '1' }, log: recordingLog() })).toBe(1);
  });

  test('ait-format을 찾지 못하면: 기본 0(경고), strict 1', () => {
    const root = makeTmp();
    writePkg(root, { name: 'proj' });
    writeAit(root, 'app.ait', 'app', good);
    expect(verify({ root, env: {}, log: recordingLog() })).toBe(0);
    expect(verify({ root, env: { AIT_STRICT_METADATA: 'true' }, log: recordingLog() })).toBe(1);
  });

  test('.ait가 깨져 읽을 수 없으면(예외): 기본 0(경고), strict 1 — throw하지 않는다', () => {
    const { root } = makePnpmProject();
    writeFileSync(join(root, 'broken.ait'), 'not json');
    expect(verify({ root, env: {}, log: recordingLog() })).toBe(0);
    expect(verify({ root, env: { AIT_STRICT_METADATA: '1' }, log: recordingLog() })).toBe(1);
  });

  test('여러 .ait가 있으면 가장 최근 파일을 검사한다 (이전 빌드의 잔여 파일에 속지 않는다)', () => {
    const { root } = makePnpmProject();
    const stale = writeAit(root, 'old.ait', 'old', good);
    const fresh = writeAit(root, 'new.ait', 'new', bad);
    utimesSync(stale, new Date(1_000_000), new Date(1_000_000));
    utimesSync(fresh, new Date(2_000_000), new Date(2_000_000));
    expect(verify({ root, env: { AIT_STRICT_METADATA: '1' }, log: recordingLog() })).toBe(1);
  });

  test('경로를 명시하면 그 파일을 검사한다', () => {
    const { root } = makePnpmProject();
    const p = writeAit(root, 'only.ait', 'only', good);
    expect(verify({ root, aitPath: p, env: { AIT_STRICT_METADATA: '1' }, log: recordingLog() })).toBe(0);
  });
});
