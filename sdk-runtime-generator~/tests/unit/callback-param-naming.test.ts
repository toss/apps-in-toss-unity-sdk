import { describe, test, expect } from 'vitest';
import { Project } from 'ts-morph';
import { parseType } from '../../src/parser/type-parser.js';
import { mapToCSharpType } from '../../src/validators/types.js';
import {
  collectReferencedTypes,
  InlineTypeTracker,
  isInlineAnonymousObjectParam,
} from '../../src/generators/csharp/type-collector.js';
import { functionParamTypeName } from '../../src/generators/csharp/utils.js';
import {
  extractCallbackTypes,
  extractCallbackEventType,
} from '../../src/generators/csharp/api-data-preparer.js';

// 콜백(함수 타입) 파라미터가 인라인 익명 객체 리터럴일 때 C# 'object'로 뭉개지지 않고
// 명명 클래스(...Param)로 생성되는 기능의 회귀 방지 테스트.
// 실제 대상: IAP processProductGrant(one-time/subscription), requestNotificationAgreement onEvent 등.

function makeProject(): Project {
  return new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      strictNullChecks: true,
      moduleResolution: 99, // NodeNext
    },
  });
}

// 인터페이스 속성의 콜백(함수) 타입을 실제 생성 경로와 동일하게 파싱한다.
function parseCallback(source: string, ifaceName: string, propName: string): any {
  const project = makeProject();
  const sf = project.createSourceFile('/test.d.ts', source);
  const iface = sf.getInterfaceOrThrow(ifaceName);
  const prop = iface.getPropertyOrThrow(propName);
  return parseType(prop.getType());
}

// collectReferencedTypes를 stub generateClassType과 함께 실행하고
// typeMap 등록 결과 및 (name, properties) 캡처를 반환한다.
function collectWithParent(callbackName: string, fnType: any, parentTypeName?: string) {
  const typeMap = new Map<string, string>();
  const registered: { name: string; properties: any[] }[] = [];
  const stub = (name: string, properties: any[]) => {
    registered.push({ name, properties });
    return `public class ${name} {}`;
  };
  collectReferencedTypes(
    [{ name: callbackName, type: fnType }],
    typeMap,
    new Set<string>(),
    new InlineTypeTracker(),
    stub,
    parentTypeName,
  );
  return { typeMap, registered };
}

describe('functionParamTypeName (콜백 param 클래스 네이밍 공식)', () => {
  test('parent + Callback(PascalCase) + "Param"', () => {
    expect(
      functionParamTypeName('IapCreateOneTimePurchaseOrderOptionsOptions', 'processProductGrant'),
    ).toBe('IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam');
    expect(functionParamTypeName('RequestNotificationAgreementOptions', 'onEvent')).toBe(
      'RequestNotificationAgreementOptionsOnEventParam',
    );
  });

  // 필드 타입 문자열(generateClassType)과 클래스 등록(collectFunctionParamTypes)이
  // 반드시 같은 식별자를 산출해야 CS0246을 피한다 → 두 소비처가 이 헬퍼를 공유한다.
  test('one-time vs subscription: parent가 달라 이름 충돌 없음', () => {
    const oneTime = functionParamTypeName(
      'IapCreateOneTimePurchaseOrderOptionsOptions',
      'processProductGrant',
    );
    const subscription = functionParamTypeName(
      'CreateSubscriptionPurchaseOrderOptionsOptions',
      'processProductGrant',
    );
    expect(oneTime).not.toBe(subscription);
  });
});

describe('isInlineAnonymousObjectParam (raw-aware 익명 판정 — CS0246 방지 가드)', () => {
  test('인라인 익명 객체 리터럴 param → true', () => {
    const fn = parseCallback(
      `export interface Opts { cb: (p: { orderId: string }) => boolean; }`,
      'Opts',
      'cb',
    );
    expect(isInlineAnonymousObjectParam(fn.functionParams[0])).toBe(true);
  });

  // 핵심 회귀 방지: named 타입 param은 이미 자체 이름이 있어 'object'로 뭉개지지 않으므로
  // 승격 대상이 아니다. (name이 '__type'이라도 raw가 named type을 가리키면 mapToCSharpType이
  // 그 이름을 복구 → 'object'가 아니게 되어 배제. GoogleAdMob onEvent의 dangling 참조 방지.)
  test('named 타입 param → false (mapToCSharpType이 이름을 복구하므로 승격 안 함)', () => {
    const fn = parseCallback(
      `export interface Named { id: string; }
       export interface Opts { cb: (p: Named) => void; }`,
      'Opts',
      'cb',
    );
    expect(mapToCSharpType(fn.functionParams[0])).not.toBe('object');
    expect(isInlineAnonymousObjectParam(fn.functionParams[0])).toBe(false);
  });

  test('빈 객체 {} param → false (프로퍼티 0개)', () => {
    const fn = parseCallback(`export interface Opts { cb: (p: {}) => void; }`, 'Opts', 'cb');
    expect(isInlineAnonymousObjectParam(fn.functionParams[0])).toBe(false);
  });

  test('primitive param → false', () => {
    const fn = parseCallback(`export interface Opts { cb: (p: string) => void; }`, 'Opts', 'cb');
    expect(isInlineAnonymousObjectParam(fn.functionParams[0])).toBe(false);
  });

  test('null/undefined 방어 → false', () => {
    expect(isInlineAnonymousObjectParam(undefined)).toBe(false);
    expect(isInlineAnonymousObjectParam(null)).toBe(false);
  });
});

describe('collectReferencedTypes: 콜백 인라인 익명 param → 명명 클래스 등록', () => {
  test('processProductGrant 인라인 param → parent 기준 ...Param 클래스 등록 (OrderId 필드)', () => {
    const fn = parseCallback(
      `export interface Opts { processProductGrant: (p: { orderId: string }) => boolean | Promise<boolean>; }`,
      'Opts',
      'processProductGrant',
    );
    const { typeMap, registered } = collectWithParent(
      'processProductGrant',
      fn,
      'IapCreateOneTimePurchaseOrderOptionsOptions',
    );

    const expected = functionParamTypeName(
      'IapCreateOneTimePurchaseOrderOptionsOptions',
      'processProductGrant',
    );
    expect(typeMap.has(expected)).toBe(true);
    const cls = registered.find((r) => r.name === expected);
    expect(cls).toBeDefined();
    expect(cls!.properties.map((p: any) => p.name)).toContain('orderId');
  });

  test('subscription 변형 param → orderId + subscriptionId 필드, one-time과 다른 클래스명', () => {
    const fn = parseCallback(
      `export interface Opts { processProductGrant: (p: { orderId: string; subscriptionId?: string }) => boolean | Promise<boolean>; }`,
      'Opts',
      'processProductGrant',
    );
    const { typeMap, registered } = collectWithParent(
      'processProductGrant',
      fn,
      'CreateSubscriptionPurchaseOrderOptionsOptions',
    );

    const expected = functionParamTypeName(
      'CreateSubscriptionPurchaseOrderOptionsOptions',
      'processProductGrant',
    );
    expect(typeMap.has(expected)).toBe(true);
    const names = registered.find((r) => r.name === expected)!.properties.map((p: any) => p.name);
    expect(names).toContain('orderId');
    expect(names).toContain('subscriptionId');
    expect(expected).not.toBe(
      functionParamTypeName('IapCreateOneTimePurchaseOrderOptionsOptions', 'processProductGrant'),
    );
  });

  // named 타입 param 콜백(예: GoogleAdMob onEvent → LoadAdMobEvent)은 합성 ...Param 클래스를
  // 만들면 안 된다 — 그렇지 않으면 등록되지 않은 클래스를 참조해 CS0246이 난다.
  test('named 타입 param 콜백 → ...Param 합성 클래스 미등록 (CS0246 유발 방지)', () => {
    const fn = parseCallback(
      `export interface LoadAdMobEvent { adId: string; }
       export interface Opts { onEvent: (e: LoadAdMobEvent) => void; }`,
      'Opts',
      'onEvent',
    );
    const { typeMap } = collectWithParent('onEvent', fn, 'GoogleAdMobLoadAppsInTossAdMobArgs');
    expect(
      typeMap.has(functionParamTypeName('GoogleAdMobLoadAppsInTossAdMobArgs', 'onEvent')),
    ).toBe(false);
  });

  test('parentTypeName 없으면 익명 param 승격 안 함 (전역 기본 동작 보존)', () => {
    const fn = parseCallback(
      `export interface Opts { cb: (p: { orderId: string }) => boolean; }`,
      'Opts',
      'cb',
    );
    const { typeMap } = collectWithParent('cb', fn, undefined);
    expect([...typeMap.keys()].some((k) => k.endsWith('Param'))).toBe(false);
  });
});

describe('API 본문 typeName ↔ AITCore 구독 디스패치 case 일관성', () => {
  // API 본문(extractCallbackTypes)과 AITCore RouteSubscriptionCallback case 수집
  // (extractCallbackEventType)이 다른 이름을 산출하면, 해당 이벤트는 런타임에
  // "Unknown subscription type"으로 조용히 드롭된다.
  // (실제 사례: requestNotificationAgreement onEvent가 ...OnEventParam으로 명명되는데
  // case 목록에는 'object'만 수집되어 이벤트가 전달되지 않던 문제)
  test('인라인 익명 onEvent param: 두 경로가 동일한 ...OnEventParam 이름 산출', () => {
    const project = makeProject();
    const sf = project.createSourceFile(
      '/rna.d.ts',
      `export interface RequestNotificationAgreementOptions {
        options: { alarmType: string };
        onEvent: (result: { type: 'newAgreement' | 'alreadyAgreed' }) => void;
        onError: (error: unknown) => void;
      }
      export declare function requestNotificationAgreement(params: RequestNotificationAgreementOptions): () => void;`,
    );
    const param = sf.getFunctionOrThrow('requestNotificationAgreement').getParameters()[0];
    const api: any = {
      name: 'requestNotificationAgreement',
      pascalName: 'RequestNotificationAgreement',
      isCallbackBased: true,
      parameters: [{ name: 'params', type: parseType(param.getType()), optional: false }],
      returnType: { name: 'Function', kind: 'function', raw: '() => void' },
    };

    const bodyType = extractCallbackTypes(api).callbackEventType;
    const dispatchType = extractCallbackEventType(api);

    expect(bodyType).toBe(
      functionParamTypeName('RequestNotificationAgreementOptions', 'onEvent'),
    );
    expect(dispatchType).toBe(bodyType);
  });
});
