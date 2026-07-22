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
        /// 결제 완료 시 상품 지급 여부를 결정하는 콜백. 반드시 동기로 값을 반환해야 한다 —
        /// 이 콜백 안에서 <c>await</c>를 쓰면 결제가 완료되지 않는다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 이 콜백은 네이티브 결제 오버레이가 화면을 덮고 있는 동안 호출된다. 그 구간에는
        /// 브라우저가 <c>visibilityState = hidden</c> 상태라 requestAnimationFrame이 멈추고,
        /// 그것이 유일한 구동원인 Unity WebGL player loop도 함께 멈춘다. player loop가 멈추면
        /// <c>await</c>의 continuation이 재개되지 않는다.
        /// </para>
        /// <para>
        /// 그래서 서버 검증, <c>UnityWebRequest</c>, 코루틴 대기 등을 await하면 순환 교착이 생긴다.
        /// 오버레이는 이 콜백의 응답을 기다리고, 이 콜백은 오버레이가 닫혀야 오는 프레임을 기다린다.
        /// 실기기 실측에서 115초간 정지한 뒤 "문제가 생겼어요. 환불을 신청해주세요" 페이지가
        /// 노출됐다. (<c>Task.Delay</c>는 WebGL에 타이머 스레드가 없어 아예 완료되지 않으므로
        /// 어떤 경우에도 쓸 수 없다.)
        /// </para>
        /// <para>
        /// 서버 검증이 필요하면 결제를 시작하기 전에 끝내고, 그 결과를 캡처해서 반환한다.
        /// </para>
        /// <example>
        /// <code>
        /// // 1) 오버레이가 뜨기 전 — 여기서는 프레임이 정상적으로 돈다
        /// bool authorized = await MyServer.ReserveEntitlement(sku);
        /// if (!authorized) return;   // 결제 자체를 시작하지 않는 선택지가 생긴다
        ///
        /// // 2) 콜백은 이미 결정된 값을 동기로 반환한다 (await 0회)
        /// options.ProcessProductGrant = _ => Task.FromResult(authorized);
        /// </code>
        /// </example>
        /// <para>
        /// 지급 가능 여부를 미리 알 수 없다면 <c>Task.FromResult(false)</c>를 반환한다. 주문이
        /// PAYMENT_COMPLETED 상태로 남아 <c>IAPGetPendingOrders</c>에 계속 보이므로, 검증을 마친
        /// 뒤 <c>IAPCompleteProductGrant</c>로 지급을 완료할 수 있다. 반대로 <c>true</c>는 주문을
        /// PURCHASED로 확정하며 되돌리는 API가 없다 — 확신이 없으면 <c>false</c>가 안전한 방향이다.
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
