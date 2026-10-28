import { describe, test, expect, beforeEach } from 'vitest';
import { Project } from 'ts-morph';
import { parseType } from '../../src/parser/type-parser.js';
import { parseBundle } from '../../src/parser/bundle-parser.js';
import {
  clearDomViolations,
  drainDomViolations,
  recordDomViolation,
} from '../../src/parser/dom-violations.js';

function makeProject(): Project {
  return new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      strictNullChecks: true,
      moduleResolution: 99, // NodeNext
    },
  });
}

function getFunctionType(source: string, funcName: string) {
  const project = makeProject();
  const sf = project.createSourceFile('/test.d.ts', source);
  const fn = sf.getFunctionOrThrow(funcName);
  return { project, sf, fn };
}

describe('DOM-only type handling in parseType', () => {
  beforeEach(() => {
    clearDomViolations();
  });

  test('단독 HTMLElement 파라미터는 dom-only sentinel을 반환', () => {
    const { fn } = getFunctionType(
      `export declare function foo(target: HTMLElement): void;`,
      'foo',
    );
    const param = fn.getParameters()[0];
    const parsed = parseType(param.getType());
    expect(parsed.kind).toBe('dom-only');
    expect(parsed.name).toBe('HTMLElement');
    expect(parsed.raw).toBe('HTMLElement');
  });

  test('Element (DOM_TYPES의 다른 멤버)도 dom-only sentinel을 반환', () => {
    const { fn } = getFunctionType(
      `export declare function bar(target: Element): void;`,
      'bar',
    );
    const param = fn.getParameters()[0];
    const parsed = parseType(param.getType());
    expect(parsed.kind).toBe('dom-only');
    expect(parsed.name).toBe('Element');
  });
});

describe('Union DOM filtering', () => {
  beforeEach(() => {
    clearDomViolations();
  });

  test('string | HTMLElement → string primitive (DOM member filtered out)', () => {
    const { fn } = getFunctionType(
      `export declare function foo(t: string | HTMLElement): void;`,
      'foo',
    );
    const param = fn.getParameters()[0];
    const typeNode = param.getType();
    const parsed = parseType(typeNode);

    expect(parsed.kind).toBe('primitive');
    expect(parsed.name).toBe('string');
    expect(parsed.raw).toBe(typeNode.getText());
    expect(parsed.isNullable).toBeFalsy();
  });

  test('string | HTMLElement | undefined → string primitive, isNullable true', () => {
    const { fn } = getFunctionType(
      `export declare function foo(t: string | HTMLElement | undefined): void;`,
      'foo',
    );
    const param = fn.getParameters()[0];
    const typeNode = param.getType();
    const parsed = parseType(typeNode);

    expect(parsed.kind).toBe('primitive');
    expect(parsed.name).toBe('string');
    expect(parsed.raw).toBe(typeNode.getText());
    expect(parsed.isNullable).toBe(true);
  });

  test('string | number | HTMLElement → union with 2 members, no dom-only member', () => {
    const { fn } = getFunctionType(
      `export declare function foo(t: string | number | HTMLElement): void;`,
      'foo',
    );
    const param = fn.getParameters()[0];
    const typeNode = param.getType();
    const parsed = parseType(typeNode);

    expect(parsed.kind).toBe('union');
    expect(parsed.unionTypes).toBeDefined();
    expect(parsed.unionTypes!.length).toBe(2);
    const memberNames = parsed.unionTypes!.map((m) => m.name);
    expect(memberNames).toContain('string');
    expect(memberNames).toContain('number');
    const domOnlyMember = parsed.unionTypes!.find((m) => m.kind === 'dom-only');
    expect(domOnlyMember).toBeUndefined();
  });

  test('HTMLElement | Node → dom-only sentinel preserving original raw', () => {
    const { fn } = getFunctionType(
      `export declare function foo(t: HTMLElement | Node): void;`,
      'foo',
    );
    const param = fn.getParameters()[0];
    const typeNode = param.getType();
    const actualTypeText = typeNode.getText();
    const parsed = parseType(typeNode);

    expect(parsed.kind).toBe('dom-only');
    expect(parsed.raw).toBe(actualTypeText);
  });
});

function parseApis(source: string) {
  const project = makeProject();
  const sf = project.createSourceFile('/index.d.ts', source);
  return parseBundle(sf).apis;
}

describe('DOM violation collection (top-level 함수)', () => {
  beforeEach(() => {
    clearDomViolations();
  });

  test('단독 HTMLElement 파라미터: API 제외 + 위반 1개 수집', () => {
    const apis = parseApis(`export declare function foo(target: HTMLElement): void;`);
    expect(apis.find((a) => a.originalName === 'foo')).toBeUndefined();
    const violations = drainDomViolations();
    expect(violations).toHaveLength(1);
    expect(violations[0]).toMatchObject({
      location: 'parameter',
      paramName: 'target',
      rawType: 'HTMLElement',
    });
  });

  test('반환 타입 HTMLElement: API 제외 + return 위반 수집', () => {
    const apis = parseApis(`export declare function foo(): HTMLElement;`);
    expect(apis.find((a) => a.originalName === 'foo')).toBeUndefined();
    const violations = drainDomViolations();
    expect(violations).toHaveLength(1);
    expect(violations[0]).toMatchObject({ location: 'return', rawType: 'HTMLElement' });
  });

  test('string | HTMLElement은 위반 아님: API 유지 + 위반 없음', () => {
    const apis = parseApis(`export declare function foo(target: string | HTMLElement): void;`);
    const api = apis.find((a) => a.originalName === 'foo');
    expect(api).toBeDefined();
    expect(api!.parameters[0].type.name).toBe('string');
    expect(drainDomViolations()).toHaveLength(0);
  });

  test('여러 함수 위반 누적: 3개 함수 파싱 후 drainDomViolations() length 3', () => {
    const apis = parseApis(
      [
        `export declare function a(el: HTMLElement): void;`,
        `export declare function b(): HTMLElement;`,
        `export declare function c(node: HTMLElement | Node): void;`,
      ].join('\n'),
    );
    expect(apis).toHaveLength(0);
    const violations = drainDomViolations();
    expect(violations).toHaveLength(3);
    const names = violations.map((v) => v.functionName.toLowerCase()).sort();
    expect(names).toEqual(['a', 'b', 'c']);
  });
});

describe('DOM violation collection (네임스페이스 멤버)', () => {
  beforeEach(() => {
    clearDomViolations();
  });

  test('네임스페이스 메서드 단독 HTMLElement: 해당 메서드를 제외하고 위반 1개 수집', () => {
    const apis = parseApis(
      [
        `declare function attach(target: HTMLElement): void;`,
        `export declare const Foo: {`,
        `  attach: typeof attach;`,
        `};`,
      ].join('\n'),
    );
    expect(apis.find((a) => a.originalName === 'attach')).toBeUndefined();

    const violations = drainDomViolations();
    expect(violations.length).toBeGreaterThanOrEqual(1);
    const v = violations.find((x) => x.functionName === 'FooAttach');
    expect(v).toBeDefined();
    expect(v).toMatchObject({
      functionName: 'FooAttach',
      location: 'parameter',
      paramName: 'target',
    });
  });

  test('네임스페이스 메서드 string | HTMLElement은 정상: API 반환되고 위반 없음', () => {
    const apis = parseApis(
      [
        `declare function attach(target: string | HTMLElement): void;`,
        `export declare const Foo: {`,
        `  attach: typeof attach;`,
        `};`,
      ].join('\n'),
    );
    const attachApi = apis.find((a) => a.originalName === 'attach');
    expect(attachApi).toBeDefined();
    expect(attachApi!.parameters[0].type.name).toBe('string');
    expect(drainDomViolations()).toHaveLength(0);
  });
});

describe('변수로 선언된 top-level 함수의 DOM 처리', () => {
  beforeEach(() => {
    clearDomViolations();
  });

  test('변수 함수 단독 HTMLElement는 제외 + 위반 수집', () => {
    const apis = parseApis(`export declare const foo: (target: HTMLElement) => void;`);
    expect(apis).toHaveLength(0);
    const violations = drainDomViolations();
    expect(violations).toHaveLength(1);
    expect(violations[0]).toMatchObject({ location: 'parameter', paramName: 'target' });
  });

  test('변수 함수 string | HTMLElement는 string으로 축소', () => {
    const apis = parseApis(`export declare const foo: (target: string | HTMLElement) => void;`);
    expect(apis).toHaveLength(1);
    expect(apis[0].parameters[0].type.name).toBe('string');
    expect(drainDomViolations()).toHaveLength(0);
  });
});

describe('Union DOM filtering — nullable preservation in 2+ residual path', () => {
  test('string | number | HTMLElement | null → union 유지 + isNullable true', () => {
    const { fn } = getFunctionType(
      `export declare function foo(t: string | number | HTMLElement | null): void;`,
      'foo',
    );
    const param = fn.getParameters()[0];
    const parsed = parseType(param.getType());
    expect(parsed.kind).toBe('union');
    expect(parsed.unionTypes).toBeDefined();
    expect(parsed.unionTypes!.length).toBe(2);
    const memberNames = parsed.unionTypes!.map((m) => m.name).sort();
    expect(memberNames).toEqual(['number', 'string']);
    expect(parsed.isNullable).toBe(true);
  });
});

describe('drainDomViolations semantics', () => {
  beforeEach(() => {
    clearDomViolations();
  });

  test('drainDomViolations는 호출 후 collector를 비운다', () => {
    recordDomViolation({
      functionName: 'foo',
      location: 'parameter',
      paramName: 'target',
      rawType: 'HTMLElement',
      file: '/foo.d.ts',
    });
    expect(drainDomViolations()).toHaveLength(1);
    expect(drainDomViolations()).toHaveLength(0);
  });
});
