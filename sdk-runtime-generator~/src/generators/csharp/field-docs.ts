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
        /// 이 콜백은 검증하는 자리가 아니라 접수하는 자리다. 콜백이 호출됐다는 것 자체가 이미
        /// 앱이 결제 성공을 판정했다는 뜻이고, 전달되는 정보도 OrderId 하나뿐이라 여기서
        /// 새로 검증할 수 있는 것이 없다. 서버 영수증 검증과 실제 아이템 지급은 오버레이가
        /// 닫힌 뒤 <c>onEvent</c>에서 한다 — 그때부터는 프레임이 정상적으로 돌아 await가 안전하다.
        /// 검증 자체는 개발사 서버가 Toss의 주문 상태 조회 API(mTLS, 서버 간 통신)로 OrderId를
        /// 확인하는 것이며, 클라이언트가 보고한 OrderId를 그대로 신뢰해서는 안 된다.
        /// </para>
        /// <example>
        /// <code>
        /// // 1) 콜백은 즉시 승인한다 (await 0회)
        /// options.ProcessProductGrant = _ => Task.FromResult(true);
        ///
        /// // 2) 검증·지급은 onEvent에서. OrderId와 살아있는 player loop를 동시에 갖는 첫 순간이다.
        /// onEvent: e => { ShowPurchaseSuccess(); _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }
        ///
        /// // 3) 앱 시작 시 미배달 대사 — 2)가 실행되기 전에 앱이 죽은 경우를 회수한다.
        /// var completed = await AIT.IAPGetCompletedOrRefundedOrders();
        /// </code>
        /// </example>
        /// <para>
        /// 3단계가 빠지면 1단계가 위험해진다. <c>true</c>는 주문을 PURCHASED로 확정하고 되돌리는
        /// API가 없어서, 승인 직후 앱이 종료되면 그 주문은 <c>IAPGetPendingOrders</c>에도 나타나지
        /// 않는다. 회수 창구는 <c>IAPGetCompletedOrRefundedOrders</c>뿐이며, 배달 여부의 기준은
        /// 재설치·기기 변경에도 남는 서버 기록이어야 한다 (PlayerPrefs 등 로컬 기록은 안 된다).
        /// </para>
        /// <para>
        /// <c>false</c>는 정말로 이 상품을 줄 수 없을 때만 반환한다 — true가 아닌 응답은 사용자에게
        /// 환불 안내 페이지를 띄우므로 "확신이 없으니 일단 false"는 매 결제마다 환불 안내가 뜨는
        /// 앱이 된다. 판정 근거는 이미 메모리에 있어야 한다. false를 반환하려고 무언가를 조회하면
        /// 그 조회가 위에서 설명한 교착을 그대로 일으킨다.
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
