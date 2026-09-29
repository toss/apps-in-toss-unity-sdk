/**
 * ait-patch-cli.mjs 단위 테스트
 *
 * 빌드시점 cli.js 패치의 핵심 순수 함수 `injectRuntimeVersion`을 검증한다.
 * 이 패치는 web-framework 3.x `ait build`가 emit하지 않는 .ait 메타데이터의
 * runtimeVersion을 setMetadata 호출에 주입해 3.x deploy 게이트를 통과시킨다.
 *
 * Unity E2E는 3.x 빌드를 실제로 돌리지 않으므로(비용·fixture 제약), 패치 변환의
 * 정확성(삽입 위치·중첩 brace/문자열 안전·멱등·2.x no-op)을 여기서 단위로 못 박는다.
 *
 * 대상 파일은 SDK 패키지 밖(WebGLTemplates/.../BuildConfig~)에 있으므로 상대경로로
 * 직접 import한다. 스크립트는 `import.meta.url === argv[1]`일 때만 main()을 돌리므로
 * import 시 부작용이 없다.
 */

import { describe, test, expect } from 'vitest';
import {
  injectRuntimeVersion,
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

  test('setMetadata 호출이 없으면 no-op (2.x cli.js 안전)', () => {
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
