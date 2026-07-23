import { describe, test, expect } from 'vitest';
import { Project } from 'ts-morph';
import { parseType } from '../../src/parser/type-parser.js';
import { mapToCSharpType } from '../../src/validators/types.js';
import { generateFieldDeclaration } from '../../src/generators/csharp/type-collector.js';

function makeProject(): Project {
  return new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      strictNullChecks: true,
      moduleResolution: 99, // NodeNext
    },
  });
}

// 인터페이스 속성의 함수 타입을 실제 생성 경로(type-collector가 옵션 객체의
// 콜백 속성을 파싱하는 방식)와 동일하게 파싱한다.
function parsePropertyType(source: string, ifaceName: string, propName: string) {
  const project = makeProject();
  const sf = project.createSourceFile('/test.d.ts', source);
  const iface = sf.getInterfaceOrThrow(ifaceName);
  const prop = iface.getPropertyOrThrow(propName);
  return parseType(prop.getType());
}

describe('콜백 함수 반환 타입 매핑 (IAP processProductGrant 동기 bool 계약)', () => {
  // 업스트림 원문 시그니처:
  //   processProductGrant: (params: { orderId: string }) => boolean | Promise<boolean>
  // TS의 관용적 MaybePromise 패턴이지만, Unity WebGL에서는 네이티브 결제 오버레이가
  // player loop를 멈추는 동안 이 콜백이 호출되므로 어떤 await도 재개되지 않는다.
  // 그래서 Promise 멤버를 Task로 승격하지 않고 내부 타입을 동기 반환으로 벗겨내,
  // 교착을 유발하는 async 람다가 애초에 컴파일되지 않도록 bool로 강제한다.
  // (서버 검증은 오버레이가 닫힌 뒤 onEvent에서 한다.)
  test('boolean | Promise<boolean> 반환은 동기 System.Func<..., bool>로 매핑', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        processProductGrant: (params: { orderId: string }) => boolean | Promise<boolean>;
      }`,
      'Opts',
      'processProductGrant',
    );

    expect(parsed.kind).toBe('function');
    const csharp = mapToCSharpType(parsed);
    expect(csharp).toBe('System.Func<object, bool>');
    // Task<bool>로 승격되지 않아야 함 (async 람다 컴파일 차단 — 교착 원천 봉쇄)
    expect(csharp).not.toContain('Task');
  });

  test('순수 Promise<boolean> 반환도 동기 bool로 벗겨냄', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        cb: (params: string) => Promise<boolean>;
      }`,
      'Opts',
      'cb',
    );

    expect(parsed.kind).toBe('function');
    const csharp = mapToCSharpType(parsed);
    expect(csharp).toBe('System.Func<string, bool>');
    expect(csharp).not.toContain('Task');
  });

  // 스코프 경계 문서화: Promise<void> 콜백은 반환값이 무의미하므로 동기 벗김 대상이
  // 아니며 기존대로 Action에 머문다. (현재 SDK에 Promise<void> 중첩 콜백 소비자가
  // 없어 파급 없음 — 이 테스트는 회귀 감시용)
  test('Promise<void> 반환은 System.Action 유지 (스코프 밖)', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        cb: () => Promise<void>;
      }`,
      'Opts',
      'cb',
    );

    expect(parsed.kind).toBe('function');
    expect(mapToCSharpType(parsed)).toBe('System.Action');
  });

  test('비-Promise boolean 반환은 동기 System.Func<..., bool> 유지 (회귀 방지)', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        cb: (params: string) => boolean;
      }`,
      'Opts',
      'cb',
    );

    expect(parsed.kind).toBe('function');
    const csharp = mapToCSharpType(parsed);
    expect(csharp).toBe('System.Func<string, bool>');
    expect(csharp).not.toContain('Task');
  });

  test('void 반환 콜백은 System.Action으로 불변', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        cb: (data: string) => void;
      }`,
      'Opts',
      'cb',
    );

    expect(parsed.kind).toBe('function');
    expect(mapToCSharpType(parsed)).toBe('System.Action<string>');
  });

  test('파라미터 없는 void 콜백은 System.Action으로 불변', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        cb: () => void;
      }`,
      'Opts',
      'cb',
    );

    expect(parsed.kind).toBe('function');
    expect(mapToCSharpType(parsed)).toBe('System.Action');
  });

  // 필드 선언 경로: System.Func 타입은 직렬화 불가이므로 [JsonIgnore]가 붙어야 한다.
  // (누락 시 delegate 직렬화 시도로 주문 생성 실패)
  test('생성 필드는 [JsonIgnore] + 동기 Func<..., bool> 타입', () => {
    const field = generateFieldDeclaration(
      'processProductGrant',
      'System.Func<object, bool>',
    );

    expect(field).toContain('[JsonIgnore]');
    expect(field).toContain(
      'public System.Func<object, bool> ProcessProductGrant;',
    );
    // Task로 승격되지 않아야 함
    expect(field).not.toContain('Task');
    // 직렬화 어트리뷰트가 섞이지 않아야 함
    expect(field).not.toContain('[JsonProperty');
    expect(field).not.toContain('[Preserve]');
  });
});
