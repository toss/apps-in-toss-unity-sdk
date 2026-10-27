import { describe, test, expect } from 'vitest';
import { Project } from 'ts-morph';
import { recoverParameterNames, isGeneratedParamName } from '../../src/parser/param-name-recovery.js';
import type { ParsedAPI, ParsedParameter } from '../../src/types.js';

// 회귀 방지: web-bridge .d.ts의 `args_N` 파라미터 이름을 web-framework 번들 선언의
// 실제 이름으로 복구한다. 복구 대상이 아니면(개수 불일치 등) args_N을 그대로 둔다.

const STRING: ParsedParameter['type'] = { name: 'string', kind: 'primitive', raw: 'string' };

function makeProject(): Project {
  return new Project({
    useInMemoryFileSystem: true,
    compilerOptions: { strictNullChecks: true, moduleResolution: 99 },
  });
}

function makeApi(partial: Partial<ParsedAPI> & { names: string[] }): ParsedAPI {
  const { names, ...rest } = partial;
  return {
    name: 'StorageSetItem',
    pascalName: 'StorageSetItem',
    originalName: 'setItem',
    namespace: 'Storage',
    category: 'Storage',
    file: 'index.d.ts',
    parameters: names.map(name => ({ name, type: STRING, optional: false })),
    returnType: { name: 'void', kind: 'primitive', raw: 'void' },
    isAsync: true,
    hasPermission: false,
    ...rest,
  };
}

describe('args_N 파라미터 이름 복구', () => {
  test('isGeneratedParamName은 args_N만 자리표시자로 본다', () => {
    expect(isGeneratedParamName('args_0')).toBe(true);
    expect(isGeneratedParamName('args_12')).toBe(true);
    expect(isGeneratedParamName('args')).toBe(false);
    expect(isGeneratedParamName('key')).toBe(false);
  });

  test('네임스페이스 메서드의 args_N을 번들의 실제 이름으로 되돌린다', () => {
    const bundle = makeProject().createSourceFile(
      '/bundle.d.ts',
      `export declare const Storage: {
        setItem: (key: string, value: string) => Promise<void>;
      };`,
    );
    const api = makeApi({ names: ['args_0', 'args_1'] });

    expect(recoverParameterNames([api], bundle)).toBe(1);
    expect(api.parameters.map(p => p.name)).toEqual(['key', 'value']);
    // 타입명 합성에 쓰는 이전 이름은 보존 (공개 타입명 유지)
    expect(api.parameters.map(p => p.typeNameSeed)).toEqual(['args_0', 'args_1']);
  });

  test('교차 타입(& { isSupported })로 감싼 메서드도 복구한다', () => {
    const bundle = makeProject().createSourceFile(
      '/bundle.d.ts',
      `export declare const Storage: {
        setItem: ((key: string, value: string) => Promise<void>) & { isSupported: () => boolean };
      };`,
    );
    const api = makeApi({ names: ['args_0', 'args_1'] });
    recoverParameterNames([api], bundle);
    expect(api.parameters.map(p => p.name)).toEqual(['key', 'value']);
  });

  test('최상위 함수 선언과 변수 선언의 이름도 복구한다', () => {
    const bundle = makeProject().createSourceFile(
      '/bundle.d.ts',
      `export declare function foo(label: string): void;
       export declare const bar: (count: string) => void;`,
    );
    const foo = makeApi({ name: 'foo', originalName: 'foo', namespace: undefined, names: ['args_0'] });
    const bar = makeApi({ name: 'bar', originalName: 'bar', namespace: undefined, names: ['args_0'] });
    expect(recoverParameterNames([foo, bar], bundle)).toBe(2);
    expect(foo.parameters[0].name).toBe('label');
    expect(bar.parameters[0].name).toBe('count');
  });

  test('이미 실제 이름이 있는 API는 건드리지 않는다', () => {
    const bundle = makeProject().createSourceFile(
      '/bundle.d.ts',
      `export declare const Storage: { setItem: (a: string, b: string) => Promise<void> };`,
    );
    const api = makeApi({ names: ['key', 'value'] });
    expect(recoverParameterNames([api], bundle)).toBe(0);
    expect(api.parameters.map(p => p.name)).toEqual(['key', 'value']);
    expect(api.parameters[0].typeNameSeed).toBeUndefined();
  });

  test('개수 불일치·번들에 없음·번들도 args_N이면 args_N을 유지한다', () => {
    const bundle = makeProject().createSourceFile(
      '/bundle.d.ts',
      `export declare const Storage: {
        setItem: (key: string) => Promise<void>;
        other: (args_0: string) => Promise<void>;
      };`,
    );
    const mismatch = makeApi({ names: ['args_0', 'args_1'] });
    const missing = makeApi({ originalName: 'nope', names: ['args_0'] });
    const stillGenerated = makeApi({ originalName: 'other', names: ['args_0'] });
    expect(recoverParameterNames([mismatch, missing, stillGenerated], bundle)).toBe(0);
    expect(mismatch.parameters.map(p => p.name)).toEqual(['args_0', 'args_1']);
    expect(missing.parameters[0].name).toBe('args_0');
    expect(stillGenerated.parameters[0].name).toBe('args_0');
  });

  test('레이블드 튜플 rest 파라미터는 레이블 이름으로 복구한다', () => {
    // (...args: [key: string, value: string]) 는 호출 시그니처 파라미터가 key/value로 펼쳐진다.
    const bundle = makeProject().createSourceFile(
      '/bundle.d.ts',
      `export declare const Storage: {
        setItem: (...args: [key: string, value: string]) => void;
      };`,
    );
    const api = makeApi({ names: ['args_0', 'args_1'] });
    recoverParameterNames([api], bundle);
    expect(api.parameters.map(p => p.name)).toEqual(['key', 'value']);
  });
});
