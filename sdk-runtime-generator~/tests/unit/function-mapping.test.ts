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

describe('콜백 함수 반환 타입 매핑 (IAP processProductGrant 비동기화)', () => {
  // 업스트림 원문 시그니처:
  //   processProductGrant: (params: { orderId: string }) => boolean | Promise<boolean>
  // TS의 관용적 MaybePromise 패턴. C#에서는 Task<bool>이 이 union의 정확한 대응물
  // (완료된 Task = 동기 케이스). 과거에는 Promise를 벗겨내 동기 Func로 축소해
  // C#에서 await가 불가능했다 — 서버 검증을 기다린 뒤 지급 여부를 결정할 수 없었음.
  test('boolean | Promise<boolean> 반환은 System.Func<..., Task<bool>>로 보존', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        processProductGrant: (params: { orderId: string }) => boolean | Promise<boolean>;
      }`,
      'Opts',
      'processProductGrant',
    );

    expect(parsed.kind).toBe('function');
    const csharp = mapToCSharpType(parsed);
    expect(csharp).toBe('System.Func<object, System.Threading.Tasks.Task<bool>>');
    // 동기 Func로 축소되지 않아야 함 (회귀 방지)
    expect(csharp).not.toBe('System.Func<object, bool>');
    expect(csharp).not.toBe('System.Func<object, object>');
  });

  test('순수 Promise<boolean> 반환도 Task<bool>로 매핑', () => {
    const parsed = parsePropertyType(
      `export interface Opts {
        cb: (params: string) => Promise<boolean>;
      }`,
      'Opts',
      'cb',
    );

    expect(parsed.kind).toBe('function');
    expect(mapToCSharpType(parsed)).toBe(
      'System.Func<string, System.Threading.Tasks.Task<bool>>',
    );
  });

  // 스코프 경계 문서화: 값을 돌려주는 콜백(processProductGrant: boolean)만 Task로 승격한다.
  // Promise<void> 콜백은 반환값이 무의미하므로 승격 대상이 아니며 기존대로 Action에 머문다.
  // (현재 SDK에 Promise<void> 중첩 콜백 소비자가 없어 파급 없음 — 이 테스트는 회귀 감시용)
  test('Promise<void> 반환은 승격하지 않고 System.Action 유지 (스코프 밖)', () => {
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
    // Task로 잘못 승격되지 않아야 함
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
  // (누락 시 delegate 직렬화 시도로 주문 생성 실패 — AIT.Types.IAP.cs에는
  //  using System.Threading.Tasks가 없어 타입은 반드시 완전수식)
  test('생성 필드는 [JsonIgnore] + 완전수식 Task 타입', () => {
    const field = generateFieldDeclaration(
      'processProductGrant',
      'System.Func<object, System.Threading.Tasks.Task<bool>>',
    );

    expect(field).toContain('[JsonIgnore]');
    expect(field).toContain(
      'public System.Func<object, System.Threading.Tasks.Task<bool>> ProcessProductGrant;',
    );
    // 직렬화 어트리뷰트가 섞이지 않아야 함
    expect(field).not.toContain('[JsonProperty');
    expect(field).not.toContain('[Preserve]');
  });
});
