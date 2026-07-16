import { describe, test, expect } from 'vitest';
import { prepareParameters } from '../../src/generators/csharp/api-data-preparer.js';
import type { ParsedAPI, ParsedType } from '../../src/types.js';

/**
 * 회귀 방지: 옵션 파라미터 객체의 타입명 합성 규칙.
 *
 * 배경: web-bridge는 tossAds.attach/attachBanner의 옵션 타입을 내부 인터페이스명
 * (AttachOptions / AttachBannerOptions)으로 선언하면서, 공개 표면에서는
 * `AttachOptions as TossAdsAttachOptions`처럼 API-스코프 별칭으로 re-export 한다.
 * 이 옵션 타입들은 `options?:`로 optional이다.
 *
 * 규칙(HEAD 배포 동작):
 *  - optional named 객체 파라미터 → `<Api><Param>` 합성명 (예: TossAdsAttachOptions).
 *    (내부 인터페이스명을 그대로 노출하면 전역 충돌 위험 + 공개 표면명과 불일치.)
 *  - 익명 객체 파라미터 → 기존과 동일하게 `<Api><Param>` 합성.
 *  - required named 파라미터 → 원래 이름 유지 (예: initialize의 InitializeOptions).
 *
 * 이 규칙은 prepareParameters(시그니처)와 CSharpTypeGenerator(파라미터 클래스 생성)
 * 두 곳에서 반드시 일치해야 CS0246(참조/선언 불일치)이 나지 않는다.
 * 회귀 사례: 파서의 nullable 언랩이 optional named 타입의 별칭명을 복원하면서
 * 합성 경로에서 이탈 → TossAdsAttachOptions가 AttachOptions로 바뀌는 표면 변경 발생.
 */
describe('옵션 파라미터 타입명 합성 규칙', () => {
  const namedObject = (name: string, props: string[]): ParsedType => ({
    name,
    kind: 'object',
    raw: name,
    isNullable: true, // optional 파라미터는 파서의 nullable 언랩으로 isNullable가 설정됨
    properties: props.map((p) => ({
      name: p,
      type: { name: 'string', kind: 'primitive', raw: 'string' } as ParsedType,
      optional: true,
    })),
  });

  const anonObject = (props: string[]): ParsedType => ({
    name: '__type',
    kind: 'object',
    raw: '{...}',
    properties: props.map((p) => ({
      name: p,
      type: { name: 'string', kind: 'primitive', raw: 'string' } as ParsedType,
      optional: true,
    })),
  });

  const makeApi = (
    apiName: string,
    paramName: string,
    paramType: ParsedType,
    optional: boolean,
  ): ParsedAPI => ({
    name: apiName,
    pascalName: apiName,
    originalName: apiName,
    category: 'Advertising',
    file: 'index.d.ts',
    parameters: [{ name: paramName, type: paramType, optional }],
    returnType: { name: 'void', kind: 'primitive', raw: 'void' },
    isAsync: true,
    hasPermission: false,
  });

  test('optional named 객체 파라미터는 <Api><Param> 합성명을 쓴다 (attach → TossAdsAttachOptions)', () => {
    const api = makeApi('TossAdsAttach', 'options', namedObject('AttachOptions', ['theme', 'padding', 'callbacks']), true);
    const [p] = prepareParameters(api);
    expect(p.paramType).toBe('TossAdsAttachOptions');
    expect(p.optional).toBe(true);
  });

  test('optional named 객체 파라미터 (attachBanner → TossAdsAttachBannerOptions)', () => {
    const api = makeApi('TossAdsAttachBanner', 'options', namedObject('AttachBannerOptions', ['theme', 'tone', 'variant', 'callbacks']), true);
    const [p] = prepareParameters(api);
    expect(p.paramType).toBe('TossAdsAttachBannerOptions');
  });

  test('required named 객체 파라미터는 원래 이름을 유지한다 (initialize → InitializeOptions)', () => {
    const api = makeApi('TossAdsInitialize', 'options', { ...namedObject('InitializeOptions', ['adUnitId', 'callbacks']), isNullable: false }, false);
    const [p] = prepareParameters(api);
    expect(p.paramType).toBe('InitializeOptions');
  });

  test('익명 객체 파라미터는 기존대로 <Api><Param>로 합성된다', () => {
    const api = makeApi('AnalyticsScreen', 'params', anonObject(['log_name']), true);
    const [p] = prepareParameters(api);
    expect(p.paramType).toBe('AnalyticsScreenParams');
  });

  test('합성명은 CSharpTypeGenerator의 파라미터 클래스명 공식과 동일하다 (CS0246 방지)', () => {
    // 두 생성 사이트는 반드시 `capitalize(api.name) + capitalize(param.name)`로 일치해야 한다.
    const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);
    const api = makeApi('TossAdsAttach', 'options', namedObject('AttachOptions', ['theme']), true);
    const [p] = prepareParameters(api);
    expect(p.paramType).toBe(`${cap(api.name)}${cap('options')}`);
  });
});
