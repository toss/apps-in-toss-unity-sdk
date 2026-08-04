using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// IAPv2 (인앱결제 v2) 테스터 컴포넌트
/// 인앱결제 v2 API 워크플로우를 테스트할 수 있는 UI 제공
/// OOMTester 패턴을 따라 InteractiveAPITester에서 분리됨
///
/// ## 정상 플로우 (소모품)
/// 1. GetProductItemList() - 상품 목록 조회
/// 2. CreateOneTimePurchaseOrder() - 구매 주문 생성
///    - processProductGrant 콜백은 동기 bool — 즉시 true를 반환한다 (아래 ExecuteIAPCreateOrder 주석 참조)
///    - SDK가 자동으로 CompleteProductGrant 호출하여 주문 완료 처리
/// 3. 서버 영수증 검증과 실제 상품 지급은 onEvent에서 — 오버레이가 닫힌 뒤라 await가 안전하다
///
/// ## 복구 플로우 (지급 전에 앱이 종료되는 등으로 3단계가 실행되지 못한 경우)
/// 1. GetCompletedOrRefundedOrders() - 승인은 됐지만 배달 기록이 없는 주문 조회
/// 2. 각 주문에 대해 상품 지급 수행
/// 3. CompleteProductGrant() - 수동으로 주문 완료 처리
///
/// 배달 여부의 기준은 재설치·기기 변경에도 남는 서버 기록이어야 한다 (PlayerPrefs 등 로컬 기록은 안 된다).
///
/// ## 비소모품
/// - 한 번 구매하면 영구 소유, CompleteProductGrant 불필요
/// </summary>
public class IAPv2Tester : MonoBehaviour
{
    // IAP 테스트 상태
    private string iapSku = "";
    private string iapOrderId = "";
    private string iapStatus = "";
    private IAPGetProductItemListResult iapProducts = null;
    private IAPGetPendingOrdersResult iapPendingOrders = null;
    private CompletedOrRefundedOrdersResult iapCompletedOrders = null;
    private List<string> iapEventLog = new List<string>();
    private int _lastRenderedLogCount = 0;

    /// <summary>화면 이벤트 로그(iapEventLog) 상한. 초과 시 오래된 항목을 트리밍한다.</summary>
    private const int MaxIapEventLogCount = 300;

    /// <summary>[PLP round4] 그랜트 resolve 지연 토글 값(ms). 버튼 클릭으로 이 배열을 순환한다.</summary>
    private static readonly int[] PlpGrantDelayOptionsMs = { 0, 3000, 5000 };
    private int _plpGrantDelayIndex = 0;
    private Text _plpGrantDelayLabel;

    /// <summary>
    /// [PLP round4] 복구 API 실기기 프로브 — Grant Approve/Deny 토글. true면 ProcessProductGrant가
    /// false를 반환한다(플랫폼이 PAYMENT_COMPLETED로 기록하고 IAPGetPendingOrders에 노출하는지 실측용).
    /// 기존 PLP_EnableGrantDelay(지연 토글)와는 독립적으로 조합 가능하다.
    /// </summary>
    private bool _plpDenyGrant = false;
    private Text _plpGrantDecisionLabel;

    /// <summary>
    /// [PLP round5 v2] 결제 오버레이(visibilityState=hidden, rAF 정지) 중에도 Unity player loop를
    /// setTimeout 타이밍(Emscripten mode 0)으로 돌릴 수 있는지 실기기에서 토글하기 위한 상태.
    /// v1(Application.targetFrameRate 토글)은 실기기에서 Emscripten 루프 타이밍을 바꾸지
    /// 못함이 확인돼 폐기됐다 — true면 jslib에서 emscripten_set_main_loop_timing을 직접 호출해
    /// setTimeout(33ms)으로 강제 전환, false면 rAF로 되돌린다.
    /// </summary>
    private bool _plpUseSetTimeoutLoop = false;
    private Text _plpLoopModeLabel;

    /// <summary>[PLP round4] Deny된 주문 기록 — PlayerPrefs에 영속화되어 앱 재실행 후에도 남는다.</summary>
    [Serializable]
    private class PlpDeniedOrderRecord
    {
        public string orderId;
        public string deniedAtUtc; // DateTime.UtcNow.ToString("o") — round-trip 포맷
    }

    /// <summary>JsonUtility는 최상위 배열을 직렬화하지 못해 래퍼 클래스로 감싼다.</summary>
    [Serializable]
    private class PlpDeniedOrderRecordListWrapper
    {
        public List<PlpDeniedOrderRecord> records = new List<PlpDeniedOrderRecord>();
    }

    private const string PlpDeniedOrdersPrefKey = "PLP4_DeniedOrders";
    private List<PlpDeniedOrderRecord> _plpDeniedOrders = new List<PlpDeniedOrderRecord>();

    // 구독 해제 액션
    private Action _purchaseDisposer;

    // uGUI 참조
    private Text _statusText;
    private InputField _skuInput;
    private InputField _orderIdInput;
    private GameObject _eventLogContainer;
    private GameObject _productListContainer;
    private GameObject _quickSelectContainer;
    private GameObject _pendingOrdersContainer;
    private GameObject _completedOrdersContainer;

    /// <summary>
    /// 마지막 작업 상태 메시지
    /// </summary>
    public string Status => iapStatus;

    private void Awake()
    {
        // WebGL 빌드에서는 Debug.Log/Warning 한 줄마다 스택트레이스가 함께 캡처되어
        // vConsole 노이즈가 커지고, 문자열 생성 비용 때문에 프레임 성능도 저하된다.
        // 진단 가치가 큰 Error/Exception은 스택트레이스를 그대로 유지한다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

        LoadPlpDeniedOrders();
    }

    /// <summary>
    /// uGUI 기반 UI를 생성합니다.
    /// </summary>
    public void SetupUI(Transform parent)
    {
        var section = UIBuilder.CreatePanel(parent, UIBuilder.Theme.SectionBg);
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingSmall;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        section.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(section, "IAPv2 Tester (인앱결제v2)",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "인앱결제 v2 API 워크플로우 예제입니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 상태
        _statusText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _statusText.gameObject.SetActive(false);

        // 이벤트 로그
        _eventLogContainer = CreateEventLogContainer(section);

        // Step 1: 상품 목록
        UIBuilder.CreateText(section, "Step 1: Get Product List",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        UIBuilder.CreateButton(section, "IAPGetProductItemList()", onClick: ExecuteIAPGetProductList);

        _productListContainer = CreateDynamicContainer(section, "ProductList");
        _productListContainer.SetActive(false);

        // Step 2: 구매 주문 생성
        UIBuilder.CreateText(section, "Step 2: Create Purchase Order",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);

        var skuRow = UIBuilder.CreateHorizontalLayout(section, 8);
        skuRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var skuLabel = UIBuilder.CreateText(skuRow, "SKU:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(skuLabel.gameObject, minWidth: 50, preferredWidth: 50);
        _skuInput = UIBuilder.CreateInputField(skuRow, "",
            onValueChanged: (v) => iapSku = v);
        UIBuilder.SetLayout(_skuInput.gameObject, flexibleWidth: 1);

        _quickSelectContainer = CreateDynamicContainer(section, "QuickSelect");
        _quickSelectContainer.SetActive(false);

        UIBuilder.CreateButton(section, "IAPCreateOneTimePurchaseOrder(...)", onClick: ExecuteIAPCreateOrder);

        // Step 3: Pending Orders
        UIBuilder.CreateText(section, "Step 3: Get Pending Orders",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        UIBuilder.CreateButton(section, "IAPGetPendingOrders()", onClick: ExecuteIAPGetPendingOrders);

        _pendingOrdersContainer = CreateDynamicContainer(section, "PendingOrders");
        _pendingOrdersContainer.SetActive(false);

        // Step 4: Completed/Refunded Orders
        UIBuilder.CreateText(section, "Step 4: Get Completed/Refunded Orders (복구용)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        UIBuilder.CreateButton(section, "IAPGetCompletedOrRefundedOrders()", onClick: ExecuteIAPGetCompletedOrRefundedOrders);

        _completedOrdersContainer = CreateDynamicContainer(section, "CompletedOrders");
        _completedOrdersContainer.SetActive(false);

        // Step 5: Complete Product Grant
        UIBuilder.CreateText(section, "Step 5: Complete Product Grant (복구용)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);

        var orderIdRow = UIBuilder.CreateHorizontalLayout(section, 8);
        orderIdRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var orderIdLabel = UIBuilder.CreateText(orderIdRow, "Order ID:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(orderIdLabel.gameObject, minWidth: 80, preferredWidth: 80);
        _orderIdInput = UIBuilder.CreateInputField(orderIdRow, "",
            onValueChanged: (v) => iapOrderId = v);
        UIBuilder.SetLayout(_orderIdInput.gameObject, flexibleWidth: 1);

        UIBuilder.CreateButton(section, "IAPCompleteProductGrant(...)", onClick: ExecuteIAPCompleteGrant);

        // [PLP round4] 오버레이 중 fetch 왕복 + 지연 resolve 계측 (techchat 4377)
        UIBuilder.CreateText(section, "진단: Player Loop Probe round 4 (techchat 4377)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        _plpGrantDelayLabel = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "그랜트 resolve 지연 토글 (0/3000/5000ms)", onClick: TogglePlpGrantDelay);
        // 교착이 완전하면 onEvent/onError가 끝내 오지 않아 자동 리포트가 안 나온다.
        // 화면이 다시 움직이기 시작하면 이 버튼으로 수집분을 강제로 덤프한다.
        UIBuilder.CreateButton(section, "PLP 리포트 강제 출력", onClick: () => ReportPlayerLoopProbe("manual"));
        // jslib 주석의 설계 의도("delayMs=0도 래퍼는 그대로 설치한다")를 실제로 지키려면
        // 시작 시점에 한 번 설치해 둬야 한다 — 안 그러면 사용자가 토글 버튼을 한 번도
        // 누르지 않은 기본 상태에서는 래퍼가 아예 설치되지 않아, 아래 라벨이 주장하는
        // "래퍼는 설치, 지연 없음"이 거짓이 된다. window.AppsInToss.IAP가 아직 준비되지
        // 않았다면 PLP_EnableGrantDelay는 조용히 실패하고 __plpGrantDelayWrapped을 세팅하지
        // 않으므로, 이후 토글 클릭 시 재시도된다(멱등).
        PLP_EnableGrantDelay(PlpGrantDelayOptionsMs[_plpGrantDelayIndex]);
        UpdatePlpGrantDelayLabel();

        // [PLP round4] 복구 API 실기기 프로브 — Deny 주문이 IAPGetPendingOrders에 노출되는지,
        // 그 주문을 임의 시점(재실행 포함)에 IAPCompleteProductGrant로 늦게 지급할 수 있는지 실측.
        UIBuilder.CreateText(section, "진단: 복구 API 프로브 (Deny 주문 → Pending 노출 → 늦은 지급)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        _plpGrantDecisionLabel = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "Grant 결정 토글 (Approve ⇄ Deny)", onClick: TogglePlpGrantDecision);
        UIBuilder.CreateButton(section, "Pending 조회 (round4)", onClick: async () => await RunPendingOrdersProbeAsync());
        UIBuilder.CreateButton(section, "Completed/Refunded 조회 (round4)", onClick: async () => await RunCompletedOrdersProbeAsync());
        UIBuilder.CreateButton(section, "늦은 지급 시도 (최근 Deny 주문)", onClick: ExecuteLatePlpGrantAttempt);
        UpdatePlpGrantDecisionLabel();

        // [PLP round5] 오버레이 중 loop 타이밍(rAF↔setTimeout) 전환이 실제로 player loop를
        // 살리는지, 그때 C# await(Task.Delay/UnityWebRequest)가 재개되는지 실측한다.
        UIBuilder.CreateText(section, "진단: Player Loop Probe round 5 (오버레이 중 await 생존)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        _plpLoopModeLabel = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "루프 타이밍 토글 (rAF ⇄ setTimeout)", onClick: TogglePlpLoopTiming);
        UpdatePlpLoopModeLabel();
        // 결제 오버레이 밖(평시)에서 동일 프로브를 실행해, 토글이 실제로 루프를 살리는지
        // 대조군 없이도 미리 검증할 수 있게 한다.
        UIBuilder.CreateButton(section, "[PLP5] await probe (지금)", onClick: RunPlp5AwaitProbeAsync);
        // [PLP round5 v2] Task.Delay 자체의 생사를 평시 상태에서 격리 확정한다 — v1 실기기
        // 실측에서 예외 없이 영영 재개되지 않는 것으로 관찰됐다(별도 사실로 고정하는 용도).
        UIBuilder.CreateButton(section, "[PLP5v2] Task.Delay(3s) 단독", onClick: RunPlp5TaskDelayOnlyProbeAsync);
    }

    /// <summary>
    /// [PLP round4] 그랜트 resolve 지연 값을 0 → 3000 → 5000 → 0 순으로 순환하고
    /// jslib 래퍼(PLP_EnableGrantDelay)에 반영한다. 결제 시작(Step 2 버튼) 전에 눌러야
    /// 다음 구매 시도부터 맞물린다 — SDK jslib이 매 호출마다
    /// window.AppsInToss.IAP.createOneTimePurchaseOrder를 다시 조회하기 때문이다.
    /// </summary>
    private void TogglePlpGrantDelay()
    {
        _plpGrantDelayIndex = (_plpGrantDelayIndex + 1) % PlpGrantDelayOptionsMs.Length;
        int delayMs = PlpGrantDelayOptionsMs[_plpGrantDelayIndex];
        PLP_EnableGrantDelay(delayMs);
        UpdatePlpGrantDelayLabel();
        LogIap($"[PLP] grant resolve delay = {delayMs}ms");
        UpdateEventLog();
    }

    private void UpdatePlpGrantDelayLabel()
    {
        if (_plpGrantDelayLabel == null) return;
        int delayMs = PlpGrantDelayOptionsMs[_plpGrantDelayIndex];
        _plpGrantDelayLabel.text = delayMs > 0
            ? $"그랜트 resolve 지연: {delayMs}ms (다음 구매 시도부터 적용)"
            : "그랜트 resolve 지연: 0ms (래퍼는 설치, 지연 없음)";
    }

    /// <summary>
    /// [PLP round4] Grant Approve/Deny 결정을 토글한다. Deny면 다음 구매 시도의
    /// ProcessProductGrant가 false를 반환한다 — 기존 지연 토글(PLP_EnableGrantDelay)과는
    /// 독립적으로 동작하며 두 토글을 조합해 지연+Deny도 실측할 수 있다.
    /// </summary>
    private void TogglePlpGrantDecision()
    {
        _plpDenyGrant = !_plpDenyGrant;
        UpdatePlpGrantDecisionLabel();
        LogIap($"[PLP] Grant 결정 토글: {(_plpDenyGrant ? "Deny (다음 구매부터 false 반환)" : "Approve (다음 구매부터 true 반환)")}");
        UpdateEventLog();
    }

    private void UpdatePlpGrantDecisionLabel()
    {
        if (_plpGrantDecisionLabel == null) return;
        _plpGrantDecisionLabel.text = _plpDenyGrant
            ? "Grant 결정: Deny (다음 구매부터 false 반환)"
            : "Grant 결정: Approve (다음 구매부터 true 반환)";
    }

    /// <summary>
    /// [PLP round5 v2] Application.targetFrameRate 토글은 실기기(iOS, Unity 6000.2)에서
    /// Browser.mainLoop.method를 바꾸지 못함이 확인됐다(rAF 고정 + 내부 프레임 스킵) — 현대
    /// Unity는 targetFrameRate로 Emscripten 루프 타이밍을 전환하지 않는 것으로 보인다. v2는
    /// Unity를 우회해 jslib에서 Emscripten 함수(emscripten_set_main_loop_timing)를 직접
    /// 호출해 강제 전환한다. targetFrameRate는 더 이상 건드리지 않는다.
    /// </summary>
    private void TogglePlpLoopTiming()
    {
        _plpUseSetTimeoutLoop = !_plpUseSetTimeoutLoop;
        int rc = _plpUseSetTimeoutLoop ? PLP_ForceLoopTiming(0, 33) : PLP_ForceLoopTiming(1, 1);
        UpdatePlpLoopModeLabel();
        string timingInfo = PLP_GetLoopTimingInfo();
        LogIap($"[PLP5v2] force loop timing: mode={(_plpUseSetTimeoutLoop ? "setTimeout(33ms)" : "rAF")}, rc={rc}, info={timingInfo}");
        UpdateEventLog();
    }

    private void UpdatePlpLoopModeLabel()
    {
        if (_plpLoopModeLabel == null) return;
        _plpLoopModeLabel.text = _plpUseSetTimeoutLoop ? "Loop: 강제 setTimeout(33ms)" : "Loop: 기본(rAF)";
    }

    /// <summary>[PLP round4] Deny 주문 기록을 PlayerPrefs에서 불러온다. 앱 시작 시(Awake) 1회 호출.</summary>
    private void LoadPlpDeniedOrders()
    {
        _plpDeniedOrders = new List<PlpDeniedOrderRecord>();
        string json = PlayerPrefs.GetString(PlpDeniedOrdersPrefKey, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var wrapper = JsonUtility.FromJson<PlpDeniedOrderRecordListWrapper>(json);
            if (wrapper?.records != null)
            {
                _plpDeniedOrders = wrapper.records;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IAPv2Tester] [PLP] Deny 기록 로드 실패 (무시하고 빈 목록으로 시작): {ex.Message}");
        }
    }

    /// <summary>[PLP round4] Deny 주문 기록을 PlayerPrefs에 즉시 flush한다 — 재실행 후에도 남아야 한다.</summary>
    private void SavePlpDeniedOrders()
    {
        var wrapper = new PlpDeniedOrderRecordListWrapper { records = _plpDeniedOrders };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(PlpDeniedOrdersPrefKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// [PLP round4] ProcessProductGrant가 false를 반환할 때 호출 — 주문 식별자와 UTC 타임스탬프를
    /// 기록하고 PlayerPrefs에 영속화한다. 순수 동기 호출만 사용해 콜백의 동기 제약을 지킨다.
    /// </summary>
    private void RecordPlpDeniedOrder(string orderId)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            LogIap("[PLP][DENY] orderId 없음 — 기록 생략", toConsole: false);
            return;
        }

        var record = new PlpDeniedOrderRecord
        {
            orderId = orderId,
            deniedAtUtc = DateTime.UtcNow.ToString("o")
        };
        _plpDeniedOrders.Add(record);
        SavePlpDeniedOrders();
        LogIap($"[PLP][DENY] orderId={orderId} deniedAtUtc={record.deniedAtUtc} (PlayerPrefs 기록, 재실행 후에도 유지)");
    }

    private bool IsPlpDeniedOrderId(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return false;
        foreach (var record in _plpDeniedOrders)
        {
            if (record.orderId == orderId) return true;
        }
        return false;
    }

    /// <summary>
    /// [PLP round4] IAPGetPendingOrders()를 조회해 온스크린 로그에 덤프한다. 기록된 Deny 주문이
    /// 목록에 있으면 "[DENIED-HIT]" 접두로 명시한다. 오버레이가 닫힌 평시 버튼에서만 호출되므로
    /// await가 안전하다.
    /// </summary>
    private async Task RunPendingOrdersProbeAsync()
    {
        LogIap("[PLP] IAPGetPendingOrders() 조회 (복구 API 프로브)");
        UpdateEventLog();

        try
        {
            var result = await AIT.IAPGetPendingOrders();
#if AIT_SDK_1_7_OR_LATER
            int count = result?.Orders?.Length ?? 0;
            LogIap($"[PLP] Pending 주문 {count}건");
            if (result?.Orders != null)
            {
                foreach (var order in result.Orders)
                {
                    string prefix = IsPlpDeniedOrderId(order.OrderId) ? "[DENIED-HIT] " : "";
                    LogIap($"[PLP]   {prefix}orderId={order.OrderId}, sku={order.Sku}, paymentCompletedDate={order.PaymentCompletedDate}");
                }
            }
#else
            LogIap("[PLP] Pending 주문 상세 필드는 SDK 1.7.0+ 필요 (현재 빌드는 미지원)");
#endif
        }
        catch (AITException ex)
        {
            LogIap($"[PLP] Pending 조회 오류: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            LogIap($"[PLP] Pending 조회 예외: {ex.Message}");
        }

        UpdateEventLog();
    }

    /// <summary>
    /// [PLP round4] IAPGetCompletedOrRefundedOrders()를 조회해 온스크린 로그에 덤프한다.
    /// 기록된 Deny 주문이 목록에 있으면 상태(Completed/Refunded)와 함께 "[DENIED-HIT]"로 표시한다.
    /// </summary>
    private async Task RunCompletedOrdersProbeAsync()
    {
        LogIap("[PLP] IAPGetCompletedOrRefundedOrders() 조회 (복구 API 프로브)");
        UpdateEventLog();

        try
        {
            var result = await AIT.IAPGetCompletedOrRefundedOrders();
#if AIT_SDK_1_7_OR_LATER
            int count = result?.Orders?.Length ?? 0;
            LogIap($"[PLP] Completed/Refunded 주문 {count}건 (hasNext={result?.HasNext})");
            if (result?.Orders != null)
            {
                foreach (var order in result.Orders)
                {
                    string prefix = IsPlpDeniedOrderId(order.OrderId) ? "[DENIED-HIT] " : "";
                    LogIap($"[PLP]   {prefix}orderId={order.OrderId}, sku={order.Sku}, status={order.Status}, date={order.Date}");
                }
            }
#else
            LogIap("[PLP] Completed/Refunded 주문 상세 필드는 SDK 1.7.0+ 필요 (현재 빌드는 미지원)");
#endif
        }
        catch (AITException ex)
        {
            LogIap($"[PLP] Completed/Refunded 조회 오류: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            LogIap($"[PLP] Completed/Refunded 조회 예외: {ex.Message}");
        }

        UpdateEventLog();
    }

    /// <summary>
    /// [PLP round4] 기록된 가장 최근 Deny 주문 식별자로 IAPCompleteProductGrant()를 호출해
    /// "임의 시점(재실행 포함, 수 분~수 일 뒤)에 늦게 지급할 수 있는가"를 실측한다.
    /// 성공/실패와 무관하게 두 조회(Pending, Completed/Refunded)를 자동 재실행해 상태 전이를
    /// 온스크린 로그로 보여준다. 오버레이가 닫힌 평시 버튼에서만 호출되므로 await가 안전하다.
    /// </summary>
    private async void ExecuteLatePlpGrantAttempt()
    {
        if (_plpDeniedOrders.Count == 0)
        {
            LogIap("[PLP] 늦은 지급 시도: 기록된 Deny 주문 없음 (먼저 Grant 결정을 Deny로 토글하고 구매를 시도할 것)");
            UpdateEventLog();
            return;
        }

        var latest = _plpDeniedOrders[_plpDeniedOrders.Count - 1];
        LogIap($"[PLP] 늦은 지급 시도: orderId={latest.orderId} (Deny 시각={latest.deniedAtUtc})");
        UpdateEventLog();

        try
        {
            var args = new IAPCompleteProductGrantArgs_0
            {
                Params = new IAPCompleteProductGrantArgs_0Params { OrderId = latest.orderId }
            };
            bool success = await AIT.IAPCompleteProductGrant(args);

            string elapsedDesc = "알 수 없음";
            if (DateTime.TryParse(latest.deniedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var deniedAt))
            {
                TimeSpan elapsed = DateTime.UtcNow - deniedAt;
                elapsedDesc = $"{elapsed.TotalSeconds:F1}s";
            }

            LogIap($"[PLP] 늦은 지급 결과: success={success}, Deny 시점 대비 경과={elapsedDesc}");
        }
        catch (AITException ex)
        {
            LogIap($"[PLP] 늦은 지급 오류: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            LogIap($"[PLP] 늦은 지급 예외: {ex.Message}");
        }

        UpdateEventLog();

        // 성공/실패와 무관하게 상태 전이(Pending → Completed/Refunded)를 보여주기 위해 재조회한다.
        await RunPendingOrdersProbeAsync();
        await RunCompletedOrdersProbeAsync();
    }

    private GameObject CreateDynamicContainer(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>().SetParent(parent, false);
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private GameObject CreateEventLogContainer(Transform parent)
    {
        var go = CreateDynamicContainer(parent, "EventLog");
        go.SetActive(false);
        return go;
    }

    private void ClearContainer(GameObject container)
    {
        for (int i = container.transform.childCount - 1; i >= 0; i--)
            Destroy(container.transform.GetChild(i).gameObject);
    }

    private void UpdateStatus()
    {
        if (_statusText != null)
        {
            _statusText.text = $"Status: {iapStatus}";
            _statusText.gameObject.SetActive(!string.IsNullOrEmpty(iapStatus));
        }
    }

    /// <summary>
    /// IAP 이벤트를 화면 로그(iapEventLog)와 콘솔(Debug.Log)에 동일한 타임스탬프로 1회씩 기록한다.
    /// 실기기 콘솔 로그에도 발생 시각을 남기기 위해 "HH:mm:ss.fff" 타임스탬프를 공유한다.
    /// </summary>
    /// <param name="msg">기록할 메시지 (타임스탬프/프리픽스 제외한 본문)</param>
    /// <param name="toConsole">false면 화면(iapEventLog)에만 남기고 콘솔에는 기록하지 않는다</param>
    private void LogIap(string msg, bool toConsole = true)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        iapEventLog.Add($"[{timestamp}] {msg}");

        if (iapEventLog.Count > MaxIapEventLogCount)
        {
            // _lastRenderedLogCount 기반 증분 렌더링은 트리밍하면 인덱스가 어긋난다.
            // 렌더 카운트를 리셋해 다음 UpdateEventLog() 호출이 전체 재구축(단순·정확한 경로)을
            // 타도록 강제하는 방식으로 정합을 보장한다.
            iapEventLog.RemoveRange(0, iapEventLog.Count - MaxIapEventLogCount);
            _lastRenderedLogCount = 0;
        }

        if (toConsole)
        {
            Debug.Log($"[IAPv2Tester] [{timestamp}] {msg}");
        }
    }

    private void UpdateEventLog()
    {
        if (_eventLogContainer == null) return;

        if (iapEventLog.Count == 0)
        {
            _eventLogContainer.SetActive(false);
            _lastRenderedLogCount = 0;
            ClearContainer(_eventLogContainer);
            return;
        }

        _eventLogContainer.SetActive(true);

        int displayStart = Math.Max(0, iapEventLog.Count - 5);
        int prevDisplayStart = Math.Max(0, _lastRenderedLogCount - 5);

        if (_lastRenderedLogCount == 0 || displayStart != prevDisplayStart)
        {
            // 전체 재구축
            ClearContainer(_eventLogContainer);
            UIBuilder.CreateText(_eventLogContainer.transform, "Event Log:",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            for (int i = displayStart; i < iapEventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {iapEventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }
        else
        {
            // 새 항목만 추가
            for (int i = _lastRenderedLogCount; i < iapEventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {iapEventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }

        _lastRenderedLogCount = iapEventLog.Count;
    }

    private void UpdateProductList()
    {
        if (_productListContainer == null) return;
        ClearContainer(_productListContainer);

        if (iapProducts != null && iapProducts.Products != null && iapProducts.Products.Length > 0)
        {
            _productListContainer.SetActive(true);
            UIBuilder.CreateText(_productListContainer.transform,
                $"Products ({iapProducts.Products.Length}):",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            int displayCount = Math.Min(3, iapProducts.Products.Length);
            for (int i = 0; i < displayCount; i++)
            {
                var product = iapProducts.Products[i];
                UIBuilder.CreateText(_productListContainer.transform,
                    $"  - {product.DisplayName} ({product.DisplayAmount})",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }

            // Quick select 버튼
            ClearContainer(_quickSelectContainer);
            _quickSelectContainer.SetActive(true);
            UIBuilder.CreateText(_quickSelectContainer.transform, "Quick Select:",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            foreach (var product in iapProducts.Products)
            {
                var sku = product.Sku;
                UIBuilder.CreateButton(_quickSelectContainer.transform,
                    $"{product.DisplayName} ({product.Sku})",
                    onClick: () =>
                    {
                        iapSku = sku;
                        if (_skuInput != null) _skuInput.text = sku;
                    });
            }
        }
        else
        {
            _productListContainer.SetActive(false);
            _quickSelectContainer.SetActive(false);
        }
    }

    private void UpdatePendingOrders()
    {
        if (_pendingOrdersContainer == null) return;
        ClearContainer(_pendingOrdersContainer);

#if AIT_SDK_1_7_OR_LATER
        if (iapPendingOrders != null && iapPendingOrders.Orders != null && iapPendingOrders.Orders.Length > 0)
        {
            _pendingOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_pendingOrdersContainer.transform,
                $"Pending Orders ({iapPendingOrders.Orders.Length}):",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            UIBuilder.CreateText(_pendingOrdersContainer.transform,
                "Select to fill Order ID:",
                UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            foreach (var order in iapPendingOrders.Orders)
            {
                if (!string.IsNullOrEmpty(order.OrderId))
                {
                    var orderId = order.OrderId;
                    UIBuilder.CreateButton(_pendingOrdersContainer.transform,
                        $"→ {order.OrderId} ({order.Sku})",
                        onClick: () =>
                        {
                            iapOrderId = orderId;
                            if (_orderIdInput != null) _orderIdInput.text = orderId;
                        });
                }
            }
        }
        else
        {
            _pendingOrdersContainer.SetActive(false);
        }
#else
        if (iapPendingOrders != null && iapPendingOrders.Orders != null && iapPendingOrders.Orders.Length > 0)
        {
            _pendingOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_pendingOrdersContainer.transform,
                $"Pending Orders ({iapPendingOrders.Orders.Length}) - SDK 1.7.0+ required for details",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        }
        else
        {
            _pendingOrdersContainer.SetActive(false);
        }
#endif
    }

    private void UpdateCompletedOrders()
    {
        if (_completedOrdersContainer == null) return;
        ClearContainer(_completedOrdersContainer);

#if AIT_SDK_1_7_OR_LATER
        if (iapCompletedOrders != null && iapCompletedOrders.Orders != null && iapCompletedOrders.Orders.Length > 0)
        {
            _completedOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_completedOrdersContainer.transform,
                $"Completed/Refunded Orders ({iapCompletedOrders.Orders.Length}):",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            UIBuilder.CreateText(_completedOrdersContainer.transform,
                "Select to fill Order ID:",
                UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            foreach (var order in iapCompletedOrders.Orders)
            {
                if (!string.IsNullOrEmpty(order.OrderId))
                {
                    var orderId = order.OrderId;
                    string orderStatus = order.Status == CompletedOrRefundedOrdersResultOrderStatus.REFUNDED ? "Refunded" : "Completed";
                    UIBuilder.CreateButton(_completedOrdersContainer.transform,
                        $"→ {order.OrderId} ({order.Sku}, {orderStatus})",
                        onClick: () =>
                        {
                            iapOrderId = orderId;
                            if (_orderIdInput != null) _orderIdInput.text = orderId;
                        });
                }
            }
        }
        else
        {
            _completedOrdersContainer.SetActive(false);
        }
#else
        if (iapCompletedOrders != null && iapCompletedOrders.Orders != null && iapCompletedOrders.Orders.Length > 0)
        {
            _completedOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_completedOrdersContainer.transform,
                $"Completed/Refunded Orders ({iapCompletedOrders.Orders.Length}) - SDK 1.7.0+ required for details",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        }
        else
        {
            _completedOrdersContainer.SetActive(false);
        }
#endif
    }

    private async void ExecuteIAPGetProductList()
    {
        iapStatus = "Loading products...";
        LogIap("IAPGetProductItemList()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapProducts = await AIT.IAPGetProductItemList();
            int count = iapProducts?.Products?.Length ?? 0;
            iapStatus = $"Found {count} products";
            LogIap($"Success: {count} products");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdateProductList();
    }

    private void ExecuteIAPCreateOrder()
    {
        if (string.IsNullOrEmpty(iapSku))
        {
            iapStatus = "Please enter or select a SKU";
            UpdateStatus();
            return;
        }

        iapStatus = "Creating purchase order...";
        LogIap($"IAPCreateOneTimePurchaseOrder(sku: {iapSku})");
        ArmPlayerLoopProbe();
        UpdateStatus();
        UpdateEventLog();

        try
        {
#if AIT_SDK_1_7_OR_LATER
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku,
                // [1단계] 콜백은 즉시 승인한다. 오버레이가 player loop를 멈춘 동안 호출되는
                // 자리라 반환형이 동기 bool이다(async는 컴파일 불가). 여기서 검증할 것도
                // 없다(정보는 OrderId뿐) — 검증과 지급은 아래 onEvent(2단계)에서 한다.
                ProcessProductGrant = grantParam =>
                {
                    // [PLP round4] 복구 API 프로브: Grant Approve/Deny 토글값에 따라 반환값을 바꾼다.
                    // 반드시 동기 유지 — 이 콜백 안에서 await/Task를 쓰면 오버레이 정지 중 교착된다.
                    bool approve = !_plpDenyGrant;
                    LogIap($"ProcessProductGrant: 즉시 {(approve ? "true" : "false")} 반환 (동기, orderId={grantParam?.OrderId})");
                    // [PLP round4] 오버레이가 아직 열려 있는(= player loop 정지 중) 이 시점에
                    // JS fetch 왕복을 발사한다. 완료 시 SendMessage로 OnPlpFetchProbeComplete가
                    // 호출된다 — "fetch가 완료되고 전달까지 되는가"를 여기서 직접 관측한다.
                    PLP_StartFetchProbe();
                    LogIap("[PLP] fetch probe armed (ProcessProductGrant 진입 시점)");
                    // [PLP round5] 콜백은 계속 동기 유지 — await는 이 콜백 안에 넣지 않고
                    // fire-and-forget으로 async void 메서드만 시작한다(내부에서 await한다).
                    RunPlp5AwaitProbeAsync();
                    if (!approve)
                    {
                        // PlayerPrefs.SetString/Save는 동기 API이므로 이 콜백의 "동기 유지" 제약을
                        // 어기지 않는다 — await/Task는 여전히 없다.
                        RecordPlpDeniedOrder(grantParam?.OrderId);
                    }
                    UpdateEventLog();
                    return approve;
                }
            };

            _purchaseDisposer?.Invoke();
            _purchaseDisposer = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: (successEvent) =>
                {
                    iapStatus = "Purchase completed";
                    iapOrderId = successEvent.Data?.OrderId ?? "";
                    if (_orderIdInput != null) _orderIdInput.text = iapOrderId;
                    LogIap($"OnEvent: orderId={successEvent.Data?.OrderId}, amount={successEvent.Data?.DisplayAmount}");
                    ReportPlayerLoopProbe("onEvent");

                    // [2단계] 검증과 지급은 여기서. 오버레이가 닫혀 player loop가 살아난 뒤라
                    // 서버 왕복을 기다려도 안전하다 — OrderId와 살아있는 프레임을 동시에 갖는
                    // 첫 순간이다. (실측: 오버레이가 닫히고 71ms 뒤 도착)
                    GrantGameProduct(iapOrderId);

                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed";
                    LogIap($"OnError: {error.ErrorCode} - {error.Message}");
                    ReportPlayerLoopProbe("onError");
                    UpdateStatus();
                    UpdateEventLog();
                }
            );
#else
            ExecuteIAPCreateOrderLegacy();
#endif
            iapStatus = "Purchase order created";
            LogIap("Order created successfully");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
    }

#if !AIT_SDK_1_7_OR_LATER
    private void ExecuteIAPCreateOrderLegacy()
    {
        try
        {
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku
            };

            _purchaseDisposer?.Invoke();
            _purchaseDisposer = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: (successEvent) =>
                {
                    iapStatus = "Purchase completed (legacy)";
                    LogIap("OnEvent (legacy): success");
                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed (legacy)";
                    LogIap($"OnError (legacy): {error?.Message}");
                    UpdateStatus();
                    UpdateEventLog();
                }
            );
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception (legacy): {ex.Message}");
        }
    }
#endif

    private async void ExecuteIAPGetPendingOrders()
    {
        iapStatus = "Loading pending orders...";
        LogIap("IAPGetPendingOrders()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapPendingOrders = await AIT.IAPGetPendingOrders();
            int count = iapPendingOrders?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} pending orders";
            LogIap($"Success: {count} orders");
        }
        catch (AITException ex)
        {
            iapPendingOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapPendingOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdatePendingOrders();
    }

    private async void ExecuteIAPGetCompletedOrRefundedOrders()
    {
        iapStatus = "Loading completed/refunded orders...";
        LogIap("IAPGetCompletedOrRefundedOrders()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapCompletedOrders = await AIT.IAPGetCompletedOrRefundedOrders();
            int count = iapCompletedOrders?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} completed/refunded orders";
            LogIap($"Success: {count} orders, HasNext={iapCompletedOrders?.HasNext}");
        }
        catch (AITException ex)
        {
            iapCompletedOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapCompletedOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdateCompletedOrders();
    }

    private async void ExecuteIAPCompleteGrant()
    {
        if (string.IsNullOrEmpty(iapOrderId))
        {
            iapStatus = "Please enter Order ID";
            UpdateStatus();
            return;
        }

        iapStatus = "Processing product grant...";
        LogIap($"IAPCompleteProductGrant(orderId: {iapOrderId})");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            var args = new IAPCompleteProductGrantArgs_0
            {
                Params = new IAPCompleteProductGrantArgs_0Params
                {
                    OrderId = iapOrderId
                }
            };

            bool success = await AIT.IAPCompleteProductGrant(args);
            iapStatus = success ? "Product grant completed" : "Product grant failed";
            LogIap($"Result: {success}");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
    }

    private void OnDestroy()
    {
        _purchaseDisposer?.Invoke();
    }

    /// <summary>
    /// [2단계] 서버 영수증 검증 + 실제 상품 지급 (데모용).
    ///
    /// onEvent에서 fire-and-forget으로 호출한다. 이 자리는 오버레이가 이미 닫혀 player loop가
    /// 돌고 있으므로 await가 정상 동작한다.
    ///
    /// 실제 구현에서는 개발사 서버에 OrderId를 보내고, 서버가 Toss의 주문 상태 조회 API
    /// (mTLS, 서버 간 통신)로 결제를 확인한 뒤 지급을 기록한다. 클라이언트가 보고한 OrderId를
    /// 그대로 신뢰해서는 안 된다.
    /// </summary>
    private async void GrantGameProduct(string orderId)
    {
        try
        {
            bool granted = await VerifyReceiptOnServerAsync(orderId);
            LogIap($"[2단계] 지급 {(granted ? "완료" : "보류")}: {orderId}");
        }
        catch (Exception ex)
        {
            // 여기서 실패해도 결제는 이미 승인(PURCHASED)됐고 되돌릴 수 없다.
            // 회수는 IAPGetCompletedOrRefundedOrders() 버튼의 복구 플로우로 한다.
            LogIap($"[2단계] 지급 실패 — 복구 플로우 필요: {ex.Message}");
        }
        UpdateEventLog();
    }

    /// <summary>
    /// 서버 영수증 검증 왕복 시뮬레이션 (WebGL-safe: WaitForSecondsRealtime 코루틴).
    /// WebGL에는 타이머 스레드가 없어 <c>Task.Delay</c>가 완료되지 않으므로, player loop가
    /// 구동하는 코루틴으로 대기하고 <see cref="TaskCompletionSource{TResult}"/>로 Task 경계를
    /// 만든다 — SDK 관용.
    /// </summary>
    private Task<bool> VerifyReceiptOnServerAsync(string orderId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(VerifyReceiptRoutine(orderId, tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator VerifyReceiptRoutine(string orderId, TaskCompletionSource<bool> tcs)
    {
        LogIap($"[2단계] 서버 영수증 검증 중: {orderId}");
        yield return new WaitForSecondsRealtime(0.2f);
        LogIap($"[2단계] 검증 통과, 상품 지급: {orderId}");
        tcs.SetResult(true);
    }

    // =====================================================
    // Player loop freeze 진단 프로브 (techchat 4377 검증용) — round 4~5
    //
    // round 1~3 실측 확정 사항(미머지 브랜치 test/plp-round2/round3, 결론만 반영):
    //   rAF 갭 27.52s ≡ hidden→visible 27.51s ≡ C# 프레임 갭 27.44s (세 값 일치).
    //   원인은 웹뷰 suspend가 아니라 표준 visibilityState=hidden 처리이며, rAF가
    //   스펙대로 멈추는 것뿐이다. setTimeout은 hidden 중에도 스로틀되며 생존한다
    //   (28s간 22회 발화, 최대 갭 11.6s). await Task.Delay는 WebGL(스레드 없음)에서
    //   영영 완료되지 않는다 — 확증됐고, 이번 라운드부터는 관찰하지 않는다
    //   (코루틴 기반 대기만 사용, 위 VerifyReceiptOnServerAsync 참조).
    //
    // round 4가 새로 답하려는 두 질문(향후 AIT.HttpFetch + ProcessProductGrantDeferred의
    // 착수 게이트):
    //   (a) 오버레이 정지 중 발사한 JS fetch가 실제로 완료되고, .then이 즉시(스로틀 없이)
    //       발화하며, SendMessage로 C#까지 전달되는가?             → PLP_StartFetchProbe
    //   (b) 플랫폼이 processProductGrant의 Promise를 수 초간 pending으로 두는 것을
    //       허용하는가(지연 resolve 후에도 정상 지급되는가)?        → PLP_EnableGrantDelay
    //
    // 모든 기록은 메모리에 남겼다가 루프 재개 후 이벤트 로그로 출력한다
    // (loop가 멈춰도 SendMessage 진입·메모리 기록은 동기 경로라 유실되지 않음).
    // =====================================================
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void PLP_StartJsProbe();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string PLP_GetJsReport();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void PLP_StartFetchProbe();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void PLP_EnableGrantDelay(int delayMs);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string PLP_GetGrantDelayReport();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int PLP_ForceLoopTiming(int mode, int valueMs);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string PLP_GetLoopTimingInfo();
#else
    private static void PLP_StartJsProbe() { }
    private static string PLP_GetJsReport() { return "{\"raf\":{},\"timer\":{},\"visibility\":[]}"; }
    private static void PLP_StartFetchProbe() { }
    private static void PLP_EnableGrantDelay(int delayMs) { }
    private static string PLP_GetGrantDelayReport() { return "[]"; }
    private static int PLP_ForceLoopTiming(int mode, int valueMs) { return -999; }
    private static string PLP_GetLoopTimingInfo() { return "{}"; }
#endif

    private bool _plpArmed;
    private int _plpFrames;
    private float _plpLastFrameRealtime;
    private float _plpMaxFrameGapSec;
    private string _plpMaxGapAt = "";

    /// <summary>[PLP round4] C# 프레임 갭 하트비트. Update()가 매 프레임 도는지로 player loop 정지를 직접 잰다.</summary>
    private void Update()
    {
        if (!_plpArmed) return;
        float now = Time.realtimeSinceStartup;
        if (_plpLastFrameRealtime > 0f)
        {
            float gap = now - _plpLastFrameRealtime;
            if (gap > _plpMaxFrameGapSec)
            {
                _plpMaxFrameGapSec = gap;
                _plpMaxGapAt = DateTime.Now.ToString("HH:mm:ss.fff");
            }
        }
        _plpLastFrameRealtime = now;
        _plpFrames++;
    }

    /// <summary>[PLP round4] 구매 주문 생성 직전에 호출 — C#/JS 하트비트를 함께 무장한다.</summary>
    private void ArmPlayerLoopProbe()
    {
        _plpArmed = true;
        _plpFrames = 0;
        _plpLastFrameRealtime = 0f;
        _plpMaxFrameGapSec = 0f;
        _plpMaxGapAt = "";
        PLP_StartJsProbe();
        LogIap("[PLP] probe armed (C# Update + JS raf/timer/visibility)");
    }

    /// <summary>
    /// [PLP round4] 하트비트를 멈추고 수집분을 이벤트 로그로 덤프한다.
    /// onEvent/onError에서 자동 호출되며, 교착으로 끝내 도달하지 않는 경우를 위해
    /// "PLP 리포트 강제 출력" 버튼으로도 수동 호출할 수 있다(그 경로는 phase="manual").
    /// </summary>
    private void ReportPlayerLoopProbe(string phase)
    {
        if (!_plpArmed) return;
        _plpArmed = false;
        string jsReport = PLP_GetJsReport();
        LogIap($"[PLP:{phase}] frames={_plpFrames}, maxFrameGap={_plpMaxFrameGapSec:F2}s@{_plpMaxGapAt}");
        LogIap($"[PLP:{phase}] js={jsReport}");

        string grantDelayReport = PLP_GetGrantDelayReport();
        if (!string.IsNullOrEmpty(grantDelayReport) && grantDelayReport != "[]")
        {
            LogIap($"[PLP:{phase}] grantDelay={grantDelayReport}");
        }

        UpdateEventLog();
    }

    /// <summary>
    /// [PLP round4] fetch 왕복 프로브(PLP_StartFetchProbe) 완료 시 jslib가 SendMessage로
    /// 호출한다. 이 메서드에 진입한 시각 자체가 "SendMessage가 C#에 도달한 시각"이며,
    /// LogIap의 타임스탬프로 기록된다 — player loop가 멈춰 있으면 fetch가 네트워크적으로
    /// 끝나도 이 진입이 지연될 수 있다는 것이 이 프로브의 관찰 대상이다.
    /// </summary>
    public void OnPlpFetchProbeComplete(string jsonPayload)
    {
        LogIap($"[PLP] fetch probe complete: {jsonPayload}");
        UpdateEventLog();
    }

    // =====================================================
    // round 5 v2 — v1 실기기 실측(iOS, Unity 6000.2)에서 Application.targetFrameRate 토글이
    // Emscripten 루프 타이밍을 바꾸지 못하고(rAF 고정), await Task.Delay는 평시에도 예외 없이
    // 영영 재개되지 않는 것으로 확인됐다(측정 도구로 부적합). v2는 (1) Unity를 우회해
    // emscripten_set_main_loop_timing을 jslib에서 직접 호출해 루프 타이밍을 강제 전환하고,
    // (2) await 생존 여부는 Task.Delay 대신 Task.Yield 루프(경과 시간을 직접 재는 방식)로
    // 관측한다 — 루프가 살아있으면 3초 내외+다수 yield로 완료되고, 죽어있으면 오버레이 종료
    // 후에야(즉 강제 전환과 무관하게) 완료된다. Task.Delay 자체의 생사는 별도 버튼
    // (RunPlp5TaskDelayOnlyProbeAsync)으로 계속 고정 관측한다.
    // =====================================================

    /// <summary>
    /// [PLP round5 v2] await 생존 프로브. ProcessProductGrant 진입 시점(오버레이 정지 중)에
    /// fire-and-forget으로 무장되거나, "[PLP5] await probe (지금)" 버튼으로 평시에 수동
    /// 실행된다. Task.Delay는 쓰지 않는다(v1 실측에서 죽은 것으로 확인돼 측정 도구로 부적합) —
    /// 대신 Task.Yield 루프로 경과 시간을 직접 재며 3초 대기, 이어서 same-origin
    /// UnityWebRequest 왕복이 완료되는 데 걸린 시간을 기록한다.
    /// </summary>
    private async void RunPlp5AwaitProbeAsync()
    {
        float t0 = Time.realtimeSinceStartup;
        try
        {
            string timingInfo = PLP_GetLoopTimingInfo();
            LogIap($"[PLP5v2] await probe armed (Task.Yield 기반), info={timingInfo}");
            UpdateEventLog();

            await Task.Yield();
            float firstYieldElapsedSec = Time.realtimeSinceStartup - t0;
            LogIap($"[PLP5v2] 첫 yield 재개: 경과 {firstYieldElapsedSec:F3}s");
            UpdateEventLog();

            int yields = 0;
            while (Time.realtimeSinceStartup - t0 < 3f)
            {
                await Task.Yield();
                yields++;
            }
            float yieldDelayElapsedSec = Time.realtimeSinceStartup - t0;
            LogIap($"[PLP5v2] yield-delay 3s 완료: yields={yields}, 경과 {yieldDelayElapsedSec:F1}s");
            UpdateEventLog();

            UnityWebRequest req = null;
            try
            {
                string url = "index.html?plp5=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                req = UnityWebRequest.Get(url);
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    await Task.Yield();
                }
                float totalElapsedSec = Time.realtimeSinceStartup - t0;
                LogIap($"[PLP5v2] UnityWebRequest done: status={(long)req.responseCode}, 경과 {totalElapsedSec:F1}s");
            }
            finally
            {
                req?.Dispose();
            }
        }
        catch (Exception ex)
        {
            LogIap($"[PLP5v2] await probe 예외: {ex.Message}");
        }

        UpdateEventLog();
    }

    /// <summary>
    /// [PLP round5 v2] Task.Delay(3000ms) 단독 검증. 평시(오버레이 밖) visible 상태에서조차
    /// Task.Delay 자체가 재개되는지를 격리해 확정한다 — v1 실기기 실측에서 예외 없이 영영
    /// 재개되지 않는 것으로 관찰됐다(WebGL에 스레드 기반 타이머가 없는 것으로 보임). 이 버튼은
    /// 그 사실을 필요할 때마다 반복 재현·재확인하기 위한 것으로, 위 await 생존 프로브
    /// (Task.Yield 기반)와는 독립적으로 동작한다.
    /// </summary>
    private async void RunPlp5TaskDelayOnlyProbeAsync()
    {
        float t0 = Time.realtimeSinceStartup;
        try
        {
            LogIap("[PLP5v2] Task.Delay(3s) 단독 armed");
            UpdateEventLog();

            await Task.Delay(3000);

            float elapsedSec = Time.realtimeSinceStartup - t0;
            LogIap($"[PLP5v2] Task.Delay(3s) 단독 재개: 경과 {elapsedSec:F1}s (기대 3.0s)");
        }
        catch (Exception ex)
        {
            LogIap($"[PLP5v2] Task.Delay(3s) 단독 예외: {ex.Message}");
        }

        UpdateEventLog();
    }
}
