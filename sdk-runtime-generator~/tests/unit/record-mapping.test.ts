import { describe, test, expect } from 'vitest';
import { Project } from 'ts-morph';
import { parseType } from '../../src/parser/type-parser.js';
import { mapToCSharpType } from '../../src/validators/types.js';
import { determineCallbackType } from '../../src/generators/csharp/api-data-preparer.js';

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

describe('Record / Partial<Record> 반환 타입 매핑 (web-framework 2.7.0 getConsentedUserData 회귀)', () => {
  // 회귀: web-framework 2.7.0의 getConsentedUserData가 반환하는
  // Promise<Partial<Record<ConsentedUserDataKey, string>> | undefined> 가
  // "ConsentedUserDataKeystring" 같은 깨진 식별자(CS0246)로 매핑되던 버그
  const GCUD_SOURCE = `
export type ConsentedUserDataKey = "USER_NAME" | "USER_GENDER" | "USER_NATIONALITY" | "USER_BIRTHDAY" | "USER_PHONE" | "USER_ADDRESS" | "USER_EMAIL" | "USER_CONSUMPTION_HISTORY";
export interface GetConsentedUserDataOptions {
  consentedUserDataKey: string;
  shouldRequestAgreementWhenUserDeclined?: boolean;
}
export declare function getConsentedUserData(options: GetConsentedUserDataOptions): Promise<Partial<Record<ConsentedUserDataKey, string>> | undefined>;
`;

  test('Partial<Record<K, string>> | undefined 반환은 record로 파싱되고 Dictionary로 매핑', () => {
    const { fn } = getFunctionType(GCUD_SOURCE, 'getConsentedUserData');
    const parsed = parseType(fn.getReturnType());

    expect(parsed.kind).toBe('promise');
    expect(parsed.promiseType?.kind).toBe('record');
    expect(parsed.promiseType?.isNullable).toBe(true);

    const csharpType = mapToCSharpType(parsed.promiseType!);
    expect(csharpType).toBe('Dictionary<ConsentedUserDataKey, string>');
  });

  test('깨진 식별자(ConsentedUserDataKeystring)가 다시는 생성되지 않음', () => {
    const { fn } = getFunctionType(GCUD_SOURCE, 'getConsentedUserData');
    const parsed = parseType(fn.getReturnType());
    const csharpType = mapToCSharpType(parsed.promiseType!);

    // 제네릭 구분자가 strip된 흔적이 없어야 함
    expect(csharpType).not.toContain('Keystring');
    expect(csharpType).not.toBe('ConsentedUserDataKeystring');
  });

  test('determineCallbackType도 Dictionary 타입을 그대로 사용', () => {
    const { fn } = getFunctionType(GCUD_SOURCE, 'getConsentedUserData');
    const returnType = parseType(fn.getReturnType());
    const api = {
      name: 'getConsentedUserData',
      pascalName: 'GetConsentedUserData',
      returnType,
      parameters: [],
    } as any;

    expect(determineCallbackType(api)).toBe('Dictionary<ConsentedUserDataKey, string>');
  });

  test('bare Record<string, number> 반환은 Dictionary<string, double>로 매핑', () => {
    const { fn } = getFunctionType(
      `export declare function foo(): Promise<Record<string, number>>;`,
      'foo',
    );
    const parsed = parseType(fn.getReturnType());
    expect(parsed.promiseType?.kind).toBe('record');
    expect(mapToCSharpType(parsed.promiseType!)).toBe('Dictionary<string, double>');
  });

  test('Record<string, string> | undefined 는 nullable record로 파싱 (참조 타입이라 ? 미부착)', () => {
    const { fn } = getFunctionType(
      `export declare function foo(): Promise<Record<string, string> | undefined>;`,
      'foo',
    );
    const parsed = parseType(fn.getReturnType());
    expect(parsed.promiseType?.kind).toBe('record');
    expect(parsed.promiseType?.isNullable).toBe(true);
    expect(mapToCSharpType(parsed.promiseType!)).toBe('Dictionary<string, string>');
  });

  test('Readonly<Record<string, string>> 도 내부 Record로 unwrap', () => {
    const { fn } = getFunctionType(
      `export declare function foo(): Promise<Readonly<Record<string, string>>>;`,
      'foo',
    );
    const parsed = parseType(fn.getReturnType());
    expect(parsed.promiseType?.kind).toBe('record');
    expect(mapToCSharpType(parsed.promiseType!)).toBe('Dictionary<string, string>');
  });

  test('Partial<인터페이스> 는 mangle 없이 안전한 C# 타입으로 매핑', () => {
    const { fn } = getFunctionType(
      `export interface UserInfo { name: string; age?: number; }
export declare function foo(): Promise<Partial<UserInfo> | undefined>;`,
      'foo',
    );
    const parsed = parseType(fn.getReturnType());
    // nullable 텍스트 unwrap 경로(pseudo-node)에서는 명명 타입 해석이 불가하므로
    // 내부 타입 텍스트(import("...").UserInfo)까지만 unwrap된다. 핵심 회귀 조건:
    // 1) 'Partial<...>' 텍스트가 그대로 남아 mangle('PartialUserInfo' 등)되지 않을 것
    // 2) 최종 C# 매핑이 유효한 식별자일 것 (unknown 경로가 import 경로를 정리)
    expect(parsed.promiseType?.name).not.toContain('Partial');
    const csharpType = mapToCSharpType(parsed.promiseType!);
    expect(csharpType).not.toContain('Partial');
    expect(/^[A-Za-z_][A-Za-z0-9_]*$/.test(csharpType)).toBe(true);
  });

  test('방어선: unknown kind에 Record< 텍스트가 남아 있으면 Dictionary<string, object>로 우회', () => {
    const broken = {
      name: 'Partial<Record<import("/x").ConsentedUserDataKey, string>>',
      kind: 'unknown' as const,
      raw: 'Partial<Record<import("/x").ConsentedUserDataKey, string>> | undefined',
    };
    expect(mapToCSharpType(broken)).toBe('Dictionary<string, object>');
  });
});
