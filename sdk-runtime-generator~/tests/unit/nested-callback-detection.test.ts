import { describe, test, expect } from 'vitest';
import { Project } from 'ts-morph';
import { parseType } from '../../src/parser/type-parser.js';
import { detectNestedCallbacks } from '../../src/parser/namespace-parser.js';

// detectNestedCallbacks의 지원 형태 가드 회귀 방지 테스트.
//
// 중첩 콜백 렌더링(RegisterNestedCallback + `options.<콜백>` 접근 +
// `<래퍼타입명>Options` 타입명 유도)은 "단일 파라미터 + 'options' 프로퍼티 +
// 콜백이 options 직속"인 형태만 지원한다. 이 가드가 없으면
// tossAds.attach(adGroupId, target, options?) 류의 다중 파라미터 API나
// tossAds.initialize(options)처럼 콜백이 'callbacks' 프로퍼티 아래에 있는 API가
// 존재하지 않는 타입(stringOptions, InitializeOptionsOptions)을 참조하고
// 선행 파라미터를 유실하는 C#을 생성한다(CS0246).

function makeProject(): Project {
  return new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      strictNullChecks: true,
      moduleResolution: 99, // NodeNext
    },
  });
}

// 함수 선언의 파라미터들을 실제 파서와 동일하게 ParsedParameter[]로 변환
function parseParams(source: string, fnName: string) {
  const project = makeProject();
  const sf = project.createSourceFile('/test.d.ts', source);
  return sf.getFunctionOrThrow(fnName).getParameters().map(p => ({
    name: p.getName(),
    type: parseType(p.getType()),
    optional: p.isOptional(),
    description: undefined,
  }));
}

describe('detectNestedCallbacks (지원 형태 가드)', () => {
  test('지원 형태: 단일 파라미터 + options 직속 콜백 (IAP processProductGrant 패턴)', () => {
    const params = parseParams(
      `export declare function createOrder(params: {
        options: {
          sku: string;
          processProductGrant: (p: { orderId: string }) => boolean | Promise<boolean>;
        };
      }): Promise<void>;`,
      'createOrder',
    );
    const nested = detectNestedCallbacks(params);
    expect(nested.map(n => n.path)).toEqual([['options', 'processProductGrant']]);
  });

  test('다중 파라미터 API는 감지하지 않음 (tossAds.attach 패턴)', () => {
    const params = parseParams(
      `export declare function attach(adGroupId: string, target: string, options?: {
        theme?: string;
        callbacks?: { onAdRendered?: (p: { slotId: string }) => void };
      }): void;`,
      'attach',
    );
    expect(detectNestedCallbacks(params)).toEqual([]);
  });

  test("'options' 프로퍼티가 없는 단일 파라미터는 감지하지 않음 (tossAds.initialize 패턴)", () => {
    const params = parseParams(
      `export declare function initialize(options: {
        callbacks?: {
          onInitialized?: () => void;
          onInitializationFailed?: (error: { message: string }) => void;
        };
      }): void;`,
      'initialize',
    );
    expect(detectNestedCallbacks(params)).toEqual([]);
  });

  test('options보다 깊은 중첩(options.a.cb)은 감지하지 않음 (템플릿이 접근 불가)', () => {
    const params = parseParams(
      `export declare function deep(params: {
        options: {
          inner: { cb: (x: { id: string }) => void };
        };
      }): Promise<void>;`,
      'deep',
    );
    expect(detectNestedCallbacks(params)).toEqual([]);
  });
});
