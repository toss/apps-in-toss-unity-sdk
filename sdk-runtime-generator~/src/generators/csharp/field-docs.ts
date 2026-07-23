import { xmlSafe } from './utils.js';

/**
 * SDK가 직접 작성하는 필드 XML 문서.
 *
 * 업스트림 JSDoc은 웹(JS/TS) 기준으로 쓰여 있어서 Unity WebGL 고유의 제약을 담지 못한다.
 * 그런 제약 중 잘못 쓰면 결제가 실패하는 수준의 것은 IntelliSense에 직접 띄워야 하므로,
 * 여기에 prop 이름으로 키를 걸어 업스트림 description을 대체한다.
 *
 * 키는 원본 prop 이름(camelCase)이며, 값은 이미 XML로 유효한 완성된 문단이어야 한다
 * (xmlSafe를 거치지 않는다). 들여쓰기는 8칸 — 생성되는 클래스 필드 위치에 맞춘다.
 */
const FIELD_DOC_OVERRIDES: Record<string, string> = {
  // 일회성/구독 결제 양쪽 모두 같은 prop 이름을 쓴다.
  processProductGrant: `        /// <summary>
        /// 결제 완료 시 상품 지급 여부를 <c>bool</c>로 즉시 반환하는 콜백. 결제 오버레이가
        /// player loop를 멈춘 동안 호출되므로 async로 서버를 기다리면 교착이 된다 — 그래서
        /// 반환형이 동기 bool이다. 서버 검증·지급은 오버레이가 닫힌 뒤 <c>onEvent</c>에서 한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// nullable 필드지만 사실상 필수다. 지정하지 않으면 결제가 완료될 때마다 SDK가 자동으로
        /// false를 응답해("Nested callback 'processProductGrant' is not registered" 에러)
        /// 모든 결제가 지급 실패로 처리된다.
        /// </para>
        /// <example>
        /// <code>
        /// options.ProcessProductGrant = _ => true;                          // 1) 즉시 승인
        /// onEvent: e => { _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }  // 2) 검증·지급
        /// var completed = await AIT.IAPGetCompletedOrRefundedOrders();      // 3) 앱 시작 시 미배달 대사
        /// </code>
        /// </example>
        /// <para>
        /// <c>true</c>는 비가역이다 — 주문이 PURCHASED로 확정되고 되돌리는 API가 없어, 승인 직후
        /// 앱이 죽으면 <c>IAPGetPendingOrders</c>에도 남지 않는다. 회수 창구는
        /// <c>IAPGetCompletedOrRefundedOrders</c>뿐이며, 배달 여부의 기준은 서버 기록이어야
        /// 한다(PlayerPrefs 등 로컬 기록은 재설치·기기 변경에 사라진다).
        /// </para>
        /// <para>
        /// <c>false</c>는 정말로 이 상품을 줄 수 없을 때만 반환한다 — true가 아닌 응답은
        /// 사용자에게 환불 안내 페이지를 띄운다. 자세한 절차는 Docs~/APIUsagePatterns.md의
        /// "인앱결제: 지급 승인과 서버 검증" 절 참고.
        /// </para>
        /// </remarks>
`,
};

/**
 * 필드 위에 붙일 XML 문서 주석을 만든다.
 * 오버라이드가 있으면 그것을, 없으면 업스트림 description을 한 줄 summary로 쓴다.
 */
export function generateFieldDoc(propName: string, description?: string): string {
  const override = FIELD_DOC_OVERRIDES[propName];
  if (override) return override;

  return description ? `        /// <summary>${xmlSafe(description)}</summary>\n` : '';
}
