import { describe, test, expect } from 'vitest';
import { Project } from 'ts-morph';
import { parseType } from '../../src/parser/type-parser.js';
import { mapToCSharpType } from '../../src/validators/types.js';

// 구조적 nullable 언랩(getNonNullableType)의 회귀 방지 테스트.
//
// 언랩의 목적: `T | undefined`(optional 콜백/객체 등)를 텍스트 분해 없이 ts-morph 노드로
// 보존하는 것. 단, 언랩은 "잔여 타입이 단일 타입"인 경우로 게이팅되어야 한다 —
// 다중 멤버 union(`A | B | 'ERROR' | undefined`)을 언랩하면 alias symbol이 없는
// 익명 필터드 union이 만들어지고, getText()가 멤버를 체커의 타입 생성 순서(type id)대로
// 출력하므로 다른 파일이 같은 문자열 리터럴을 먼저 생성했으면 리터럴이 선두로 와서
// string literal 체크가 union 전체를 string으로 붕괴시킨다.
// (실제 사례: grantPromotionReward{,ForGame} 반환이 Awaitable<string>으로 퇴화)

function makeProject(): Project {
  return new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      strictNullChecks: true,
      moduleResolution: 99, // NodeNext
    },
  });
}

describe('optional 콜백/객체의 구조적 nullable 언랩', () => {
  test('cb?: (...) => void 는 function kind + isNullable로 보존 (unknown 붕괴 금지)', () => {
    // BannerSlotCallbacks.onNoFill 패턴
    const project = makeProject();
    const sf = project.createSourceFile(
      '/callbacks.d.ts',
      `export interface BannerSlotCallbacks {
        onNoFill?: (result: { adGroupId: string }) => void;
      }`,
    );
    const prop = sf.getInterfaceOrThrow('BannerSlotCallbacks').getPropertyOrThrow('onNoFill');
    const parsed = parseType(prop.getType());

    expect(parsed.kind).toBe('function');
    expect(parsed.isNullable).toBe(true);
    expect(parsed.functionParams).toHaveLength(1);
    expect(parsed.functionParams![0].kind).toBe('object');
    expect(parsed.functionParams![0].properties?.map(p => p.name)).toEqual(['adGroupId']);
  });

  test('params?: { ... } (고정 형태 인라인 객체 | undefined)는 object kind로 프로퍼티 보존', () => {
    // 고정 형태의 인라인 익명 객체(인터섹션/인덱스 시그니처 없음)는 언랩되어 프로퍼티가 보존된다.
    // (아래 intersection+index signature 케이스와 대비: 그쪽은 열린 사전이라 object로 남겨야 한다.)
    const project = makeProject();
    const sf = project.createSourceFile(
      '/inline-object.d.ts',
      `export declare function screen(params?: { log_name?: string }): Promise<void>;`,
    );
    const param = sf.getFunctionOrThrow('screen').getParameters()[0];
    const parsed = parseType(param.getType());

    expect(parsed.kind).toBe('object');
    expect(parsed.isNullable).toBe(true);
    expect(parsed.properties?.map(p => p.name)).toEqual(['log_name']);
  });
});

describe('optional intersection(index signature) param은 언랩하지 않는다 (Analytics CS1503 회귀 방지)', () => {
  // analytics screen/impression/click의 실제 파라미터: LoggerParams = `{ log_name?: string } & { [key: string]: Primitive }`.
  // 이 intersection은 index signature를 더한 "열린 사전(Record류)"이므로 고정 형태 인터페이스가 아니다.
  // 구조적으로 언랩하면 intersection 분기가 `log_name` 하나짜리 익명 객체(__type)로 병합하고,
  // 파라미터 익명-객체 합성이 발화해 손수작성 소비자가 넘기는 object/익명객체와 타입이 어긋난다
  // (실제 사례: Runtime/Sentry/AITSentryAnalytics.cs가 `object`를 AIT.AnalyticsScreen()에 전달 → CS1503).
  // 게이팅되면 open dictionary 의미의 object로 남는다.
  test('LoggerParams 형태(intersection + index signature)는 병합 객체로 언랩되지 않는다', () => {
    const project = makeProject();
    const sf = project.createSourceFile(
      '/analytics-logger.d.ts',
      `type Primitive = string | number | boolean | null | undefined | symbol;
       type LoggerParams = { log_name?: string } & { [key: string]: Primitive };
       export declare function screen(params?: LoggerParams): Promise<void>;`,
    );
    const param = sf.getFunctionOrThrow('screen').getParameters()[0];
    const parsed = parseType(param.getType());

    // 회귀 시그니처: 언랩 → intersection 병합 → isIntersection=true + props=['log_name'].
    // 게이팅되면 병합이 일어나지 않아 intersection 플래그도, 병합된 log_name 필드도 표면화되지 않는다.
    expect(parsed.isIntersection).toBeFalsy();
    expect((parsed.properties ?? []).map(p => p.name)).not.toContain('log_name');
  });

  test('대비: 고정 형태 named 인터페이스(TossAds AttachOptions 류)는 여전히 named 객체로 언랩된다', () => {
    // 게이트가 좁게 걸려 있음을 보장 — 콜백을 품은 옵션 타입 등 고정 형태 인터페이스는 언랩되어
    // 프로퍼티가 보존되고, 다운스트림에서 API-스코프 명명 클래스로 승격될 수 있어야 한다.
    const project = makeProject();
    const sf = project.createSourceFile(
      '/attach.d.ts',
      `interface AttachOptions { theme?: string; padding?: string; }
       export declare function attach(a: string, options?: AttachOptions): void;`,
    );
    const parsed = parseType(sf.getFunctionOrThrow('attach').getParameters()[1].getType());

    expect(parsed.kind).toBe('object');
    expect(parsed.isNullable).toBe(true);
    expect(parsed.properties?.map(p => p.name)).toEqual(['theme', 'padding']);
  });
});

describe('undefined 포함 다중 멤버 union 반환의 구조 보존 (grantPromotionReward 회귀 방지)', () => {
  // 실제 붕괴 조건 재현의 핵심: 다른 파일이 'ERROR' 문자열 리터럴 타입을 먼저 생성해
  // 리터럴의 type id가 대상 파일의 인터페이스들보다 작아지게 만든다.
  // (전체 생성 파이프라인에서는 알파벳순으로 앞선 API 파일들이 이 역할을 한다.)
  function parseGrantReturn(materializeDecoyFirst: boolean) {
    const project = makeProject();
    const decoy = project.createSourceFile(
      '/aaa-decoy.d.ts',
      `export declare function decoy(): Promise<{ ok: boolean } | 'ERROR' | undefined>;`,
    );
    const target = project.createSourceFile(
      '/grant.d.ts',
      `export interface GrantSuccessResponse { key: string; }
       export interface GrantErrorResponse { code: string; }
       export interface GrantErrorResult { errorCode: string; message: string; }
       export type GrantResponse = GrantSuccessResponse | GrantErrorResponse;
       export type GrantResult = GrantResponse | GrantErrorResult | 'ERROR' | undefined;
       export declare function grant(): Promise<GrantResult>;`,
    );
    if (materializeDecoyFirst) {
      // 체커가 decoy의 'ERROR' 리터럴 타입을 먼저 생성하도록 강제
      parseType(decoy.getFunctionOrThrow('decoy').getReturnType());
    }
    return parseType(target.getFunctionOrThrow('grant').getReturnType());
  }

  test('앞선 파일이 동일 리터럴을 먼저 생성해도 union 구조가 유지된다', () => {
    const parsed = parseGrantReturn(true);

    expect(parsed.kind).toBe('promise');
    const inner = parsed.promiseType!;
    // 회귀 시그니처: kind 'primitive' + name 'string' 으로 붕괴
    expect(inner.kind).toBe('union');
    expect(inner.isNullable).toBe(true);

    // 객체 멤버 3개의 구조(프로퍼티)가 모두 보존되어야 한다
    const objectMembers = inner.unionTypes!.filter(t => t.kind === 'object');
    expect(objectMembers.map(t => t.name).sort()).toEqual([
      'GrantErrorResponse',
      'GrantErrorResult',
      'GrantSuccessResponse',
    ]);
    const allProps = objectMembers.flatMap(t => (t.properties || []).map(p => p.name)).sort();
    expect(allProps).toEqual(['code', 'errorCode', 'key', 'message']);

    // C# 매핑은 alias 이름을 유지해야 한다 (병합 Result 클래스 생성의 전제)
    expect(mapToCSharpType(inner)).toBe('GrantResult');
  });

  test('단독 파싱(리터럴 선점 없음)에서도 동일한 union 구조', () => {
    const parsed = parseGrantReturn(false);
    const inner = parsed.promiseType!;
    expect(inner.kind).toBe('union');
    expect(inner.isNullable).toBe(true);
    expect(mapToCSharpType(inner)).toBe('GrantResult');
  });
});
