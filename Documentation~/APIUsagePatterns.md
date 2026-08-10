# API 사용 패턴

SDK API를 C#에서 호출할 때 반복해서 마주치는 패턴을 다룹니다. 개별 API가 무엇을 하는지가 아니라, **어떤 API를 부르든 똑같이 적용되는 규칙**을 모았습니다.

## API 원문은 어디에 있나

`Runtime/SDK/`의 C# 표면은 클라이언트 SDK(`@apps-in-toss/web-framework`)의 타입 정의에서 자동 생성됩니다. 현재 **24개 카테고리에 85개 API**가 있습니다.

| 어디서 | 무엇을 |
|--------|--------|
| Unity IntelliSense | 개별 API의 설명, 파라미터, 반환값. 상위 SDK의 JSDoc이 C# XML 주석으로 옮겨져 있습니다 |
| [앱인토스 개발자센터](https://developers-apps-in-toss.toss.im/) | 플랫폼 정책, 콘솔 설정, 서버 연동 등 클라이언트 SDK 공식 문서 |
| 이 문서 모음 | 위 둘에 없는 Unity 고유 사정 |

이 저장소의 문서에 API 레퍼런스를 따로 두지 않는 이유는, 그것이 상위 문서의 수기 사본이 되기 때문입니다. C# 표면은 SDK를 업데이트할 때마다 재생성되지만 손으로 쓴 마크다운은 그렇지 않아서, 시간이 지나면 반드시 어긋납니다. 대신 **IntelliSense가 항상 최신**이고, 이 문서는 상위 문서가 다루지 않는 것만 씁니다 — async/await, `Awaitable`과 `Task`의 분기, `timeoutMs`, `AITException.ErrorCode`, Mock(Editor mock, devtools), IL2CPP 스트리핑.

SDK 버전에 따라 C# 표면이 어떻게 달라졌는지는 [API 변경 이력](changelog/index.html)에서 확인할 수 있습니다.

## 기본 패턴

SDK API는 비동기입니다. `await`로 결과를 기다리면 Unity 메인 스레드를 막지 않습니다.

```csharp
using AppsInToss;
using UnityEngine;

public class Example : MonoBehaviour
{
    async void Start()
    {
        // await 키워드로 비동기 결과를 대기
        string deviceId = await AIT.GetDeviceId();
        Debug.Log($"Device ID: {deviceId}");
    }
}
```

> **중요**: 예외가 하나 있습니다. 인앱결제의 `ProcessProductGrant` 콜백만은 동기 `bool`을 반환합니다. 이유와 올바른 구조는 [인앱결제 절](#인앱결제-지급-승인과-서버-검증)을 참고하세요.

### Awaitable 과 Task

같은 API라도 Unity 버전에 따라 반환형이 다릅니다.

| Unity 버전 | 반환형 |
|-----------|--------|
| 6000.0 이상 | `Awaitable`, `Awaitable<T>` |
| 그 이하 | `Task`, `Task<T>` |

`await`로 소비하는 코드는 양쪽에서 그대로 동작하므로 대부분은 신경 쓸 필요가 없습니다. 반환형을 **명시적으로 적을 때만** 갈라집니다.

```csharp
// ❌ Unity 6 이상에서만 컴파일됩니다
public async Awaitable<bool> ProcessPayment(string orderId) { ... }

// ✅ 어느 쪽에서도 컴파일됩니다 — 반환형을 적지 않습니다
async void ProcessPayment(string orderId) { ... }
```

두 버전을 모두 지원해야 하는데 반환형이 필요하다면 조건부 컴파일로 나눕니다.

```csharp
#if UNITY_6000_0_OR_NEWER
    public async Awaitable<bool> ProcessPayment(string orderId)
#else
    public async Task<bool> ProcessPayment(string orderId)
#endif
    {
        try
        {
            var result = await AIT.CheckoutPayment(options);
            return result != null;
        }
        catch (AITException)
        {
            return false;
        }
    }
```

> **참고**: `Task.WhenAll`은 `Task`에만 있고 `Awaitable`에는 없습니다. Unity 6 이상에서 여러 API를 동시에 진행시키려면 아래 방식을 쓰세요.

### 여러 API 호출하기

순차 호출은 그냥 이어서 `await` 합니다.

```csharp
async void InitializeGame()
{
    string deviceId = await AIT.GetDeviceId();
    string platform = await AIT.GetPlatformOS();
    string locale = await AIT.GetLocale();

    Debug.Log($"기기: {deviceId}, 플랫폼: {platform}, 언어: {locale}");
}
```

서로 독립적인 호출이라면 먼저 전부 시작해 두고 나중에 각각 기다리면 왕복이 겹칩니다. 이 방식은 `Awaitable`과 `Task` 양쪽에서 동일하게 동작합니다.

```csharp
async void InitializeGameParallel()
{
    // 먼저 전부 시작 — 여기서는 await 하지 않습니다
    var deviceIdOp = AIT.GetDeviceId();
    var platformOp = AIT.GetPlatformOS();
    var localeOp = AIT.GetLocale();

    // 그 다음 각각 수거
    string deviceId = await deviceIdOp;
    string platform = await platformOp;
    string locale = await localeOp;

    Debug.Log($"기기: {deviceId}, 플랫폼: {platform}, 언어: {locale}");
}
```

## 타임아웃

모든 비동기 API는 마지막 인자로 `timeoutMs`를 받습니다. 기본값 `0`은 **무제한 대기**입니다.

```csharp
try
{
    string deviceId = await AIT.GetDeviceId(timeoutMs: 3000);
}
catch (AITClientTimeoutException ex)
{
    Debug.LogWarning($"{ex.TimeoutMs}ms 안에 응답이 오지 않았습니다");
}
```

이 타임아웃은 **C# 쪽 대기만 포기합니다.** 브릿지 너머의 JavaScript와 플랫폼 작업은 계속 진행될 수 있고, 뒤늦게 도착한 결과는 버려집니다. 따라서 부수 효과가 있는 API(결제, 공유, 권한 요청 등)에 타임아웃을 걸 때는 "타임아웃 = 실행되지 않음"으로 단정하면 안 됩니다.

`AITClientTimeoutException`은 `AITException`을 상속하므로 기존 `catch (AITException)` 블록이 그대로 받아냅니다. 타임아웃만 따로 다루고 싶을 때만 먼저 잡으세요. `ErrorCode`는 `TIMEOUT`입니다.

## 인앱결제: 지급 승인과 서버 검증

`IAPCreateOneTimePurchaseOrder` / `IAPCreateSubscriptionPurchaseOrder`에 넘기는 `ProcessProductGrant` 콜백은 지급 여부를 `bool`로 **동기 반환**합니다. 핵심은 이 콜백에서 검증하지 않는 것입니다 — 콜백은 즉시 승인하고, 서버 검증과 실제 지급은 오버레이가 닫힌 **뒤** `onEvent`에서 합니다.

### 이 콜백은 선택이 아닙니다

`ProcessProductGrant`는 nullable 필드라 지정하지 않아도 컴파일되지만, **지정하지 않으면 모든 결제가 지급 실패로 처리됩니다.**

```csharp
// ❌ 컴파일도 되고 결제 창도 뜨지만, 상품이 지급되지 않습니다
var options = new IapCreateOneTimePurchaseOrderOptionsOptions { Sku = sku };
```

JS 브릿지는 이 콜백을 **항상** 플랫폼에 넘기므로, C#에 등록된 핸들러가 없으면 SDK가 결제 완료 시마다 자동으로 `false`를 응답합니다. 이때 Console에 다음 에러가 남습니다.

```text
[AITCore] Nested callback 'processProductGrant' is not registered (id: ...); responding false.
The payment already succeeded, so the product will NOT be granted and the user may see a
refund notice. Set ProcessProductGrant on the order options and return the grant decision
(e.g. _ => true); verify and deliver later in onEvent.
```

결제 흐름을 붙일 때 이 필드부터 채우세요.

### 왜 동기여야 하나

결제 오버레이가 떠 있는 동안은 `visibilityState = hidden`이라 `requestAnimationFrame`이 멈추고, 그것으로만 도는 Unity WebGL player loop도 함께 멈춥니다. 그래서 콜백 안에서 `await`한 continuation은 오버레이가 닫혀야 오는 프레임을 기다리고, 오버레이는 그 콜백의 응답을 기다리는 교착이 됩니다. 실기기 실측에서 이 고리가 **115초** 유지된 뒤 `"{앱 이름}에 문제가 생겼어요. 환불을 신청해주세요"` 페이지가 떴고(결제 성공 후 30초 내 `true` 응답이 없으면 노출될 수 있음), 즉시 승인한 결제는 오버레이가 **1.5초**에 닫히고 정상 완료됐습니다. 반환형을 `bool`로 고정한 것은 이 `await` 형태를 컴파일 단계에서 막기 위해서입니다.

### 장부가 두 개입니다

콜백의 반환값과 내 서버의 지급 기록은 **서로 다른 두 장부**입니다.

| | 무엇을 기록하나 | 소유 | 마감 |
|---|---|---|---|
| `ProcessProductGrant` 반환값 | **결제가 소비됐는가** | Toss | 30초 (프레임 없음) |
| 내 서버의 지급 기록 | **아이템을 배달했는가** | 개발사 | 마감 없음, 재시도 가능 |

검증은 첫 번째 장부를 막는 게 아니라 **두 번째 장부를 막습니다.** 콜백은 "결제 소비를 접수했다"고 답하는 자리고, 검증과 지급은 그 뒤에 여유롭게 합니다.

그래서 이 콜백에 넣을 코드는 사실상 한 줄로 정해져 있습니다.

### 1단계 콜백은 즉시 승인한다

```csharp
var options = new IapCreateOneTimePurchaseOrderOptionsOptions
{
    Sku = sku,
    ProcessProductGrant = _ => true
};
```

이 콜백이 호출됐다는 것 자체가 이미 앱이 결제 성공을 판정했다는 뜻입니다. 콜백이 들고 오는 정보는 `OrderId` 하나뿐이라, 여기서 새로 검증할 수 있는 것도 없습니다.

### 2단계 검증과 지급은 onEvent 에서

서버 검증을 **호출할 수 있는 시점은 둘뿐**입니다.

1. 정상 흐름이면 **`onEvent`** — 오버레이가 닫힌 직후.
2. 그마저 놓쳤으면 **앱 시작 시 대사**(3단계).

`onEvent`가 첫 번째 유효 시점인 이유는, 그때가 **`OrderId`와 살아 있는 player loop를 동시에 갖는 가장 이른 순간**이기 때문입니다. 아래는 실기기에서 측정한 한 결제의 타임라인입니다.

```text
00:35:48.563  결제 오버레이가 화면을 덮음      player loop 정지 ─┐
                                                                │ 이 구간에서는 await가
                 ⋮  (사용자가 결제 UI 조작)                      │ 재개되지 않는다.
                                                                │ 검증을 호출하면 교착.
00:36:01.413  ProcessProductGrant → 즉시 true   [1단계]         │
00:36:02.725  오버레이 닫힘                     loop 재개 ──────┘
00:36:02.796  onEvent 도착              (+71ms)  [2단계] ← 서버 검증은 여기서 호출
00:36:02.998  검증 완료                (+202ms)          await가 정상 재개
```

`onEvent`부터는 프레임이 정상 속도로 돌므로 `await`를 마음껏 써도 됩니다 (`WaitForSecondsRealtime(0.2f)`가 202ms에 완료).

```csharp
_disposer = AIT.IAPCreateOneTimePurchaseOrder(
    onEvent: e =>
    {
        // 결제는 이미 확정됐으므로 UI에 즉시 반영해도 됩니다
        ShowPurchaseSuccess(e.Data.DisplayAmount);

        // 검증·지급은 서버에 맡기고 기다리지 않습니다
        _ = DeliverAsync(e.Data.OrderId);
    },
    options: options,
    onError: err => Debug.LogError(err.Message)
);

async Task DeliverAsync(string orderId)
{
    // 여기서는 프레임이 정상적으로 돌므로 await가 안전합니다
    await MyServer.VerifyAndDeliver(orderId);
}
```

> **주의**: `SuccessEvent.Data`에는 `Sku`가 없습니다. 어떤 상품인지는 구매를 시작할 때 넘긴 `sku`를 클로저로 잡아두거나, 서버가 `OrderId`로 조회해야 합니다.

### 서버는 무엇을 검증하나

클라이언트가 보낸 `OrderId`를 그대로 믿으면 안 됩니다. 개발사 서버는 **주문 상태 조회 API**로 Toss에 직접 확인합니다.

```text
POST https://apps-in-toss-api.toss.im/api-partner/v1/apps-in-toss/order/get-order-status
{ "orderId": "..." }
```

- **mTLS 인증서가 필수**입니다 (서버 간 통신). 발급 방법은 [연동 절차 문서](https://developers-apps-in-toss.toss.im/development/integration-process.html)를 참고하세요.
- `x-toss-user-key` 헤더에 토스 로그인으로 얻은 userKey를 넣으면 **그 유저의 주문만** 응답합니다. 넣지 않으면 모든 주문이 조회되므로, 다른 유저의 `OrderId`를 가로채 재사용하는 것을 막으려면 이 헤더를 함께 보내야 합니다.
- 응답의 `sku`로 실제 결제된 상품을 확인할 수 있습니다. 클라이언트가 알려준 SKU를 신뢰하지 마세요.

응답 `status`가 이 API의 핵심입니다.

| status | 의미 |
|---|---|
| `PURCHASED` | 결제와 상품 지급이 모두 완료 |
| `PAYMENT_COMPLETED` | 결제는 완료됐으나 **상품 지급 실패** |
| `REFUNDED` | 환불 완료 |
| `FAILED` / `ORDER_IN_PROGRESS` / `NOT_FOUND` | 결제 실패 / 진행 중 / 주문 없음 |

앞의 두 값이 곧 `ProcessProductGrant` 반환값의 결과입니다. `true`를 반환한 주문은 `PURCHASED`, 그렇지 않은 주문은 `PAYMENT_COMPLETED`로 남습니다.

자세한 명세는 [공식 IAP 문서](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/IAP.html)를 참고하세요.

### 3단계 앱 시작 시 미배달 대사

2단계가 항상 실행된다는 보장은 없습니다. 콜백이 `true`를 보낸 직후 앱이 종료되면 `onEvent`를 받지 못하고, 그 주문은 이미 결제 소비가 확정돼 `IAPGetPendingOrders`에도 나타나지 않습니다.

이 경우를 회수하는 것이 `IAPGetCompletedOrRefundedOrders`입니다. 앱 시작이나 포그라운드 복귀 시 한 번 훑어, 내 서버가 배달하지 않은 주문을 찾습니다.

```csharp
var completed = await AIT.IAPGetCompletedOrRefundedOrders();
if (completed.Orders == null) return;   // 플랫폼 미지원 시 error 필드에 사유가 담깁니다

foreach (var order in completed.Orders)
{
    if (order.Status != CompletedOrRefundedOrdersResultOrderStatus.COMPLETED) continue;

    // 배달 여부의 기준은 서버 기록입니다. PlayerPrefs 같은 로컬 기록은
    // 재설치·기기 변경으로 사라지므로 이 대사의 기준이 될 수 없습니다.
    await MyServer.DeliverIfMissing(order.OrderId, order.Sku);
}
```

이 3단계가 없으면 1단계의 즉시 승인이 위험해집니다. **셋은 한 묶음입니다.**

> **중요**: 환불은 폴링으로만 알 수 있습니다. 결제나 환불이 발생했을 때 개발사 서버로 알려주는 웹훅은 제공되지 않습니다. 사용자가 환불을 받아도 앱이 다시 실행되어 이 대사가 돌기 전까지는 개발사가 알 수 없습니다. 환불된 주문의 상품을 회수해야 한다면, 지급한 주문의 `OrderId`를 서버에 보관해두고 주문 상태 조회 API로 주기적으로 확인해야 합니다.

### false 는 언제 반환하나

공식 문서는 `true`가 아닌 응답에 대해 환불 안내 페이지가 *노출될 수 있다*고 안내합니다. (직접 측정한 것은 무응답 경로이며, 명시적 `false`에서도 같은 화면이 나오는지는 확인하지 않았습니다.) 따라서 `false`는 **정말로 이 상품을 줄 수 없을 때만** 씁니다 — 예를 들어 이미 보유한 비소모품을 결제 도중 다른 기기에서 획득한 경우처럼, 지급이 불가능하다고 지금 단정할 수 있을 때입니다.

"확신이 없으니 일단 `false`"는 성립하지 않습니다. 매 결제마다 환불 안내가 뜨는 앱이 되기 때문입니다. 확신은 1~3단계로 확보하는 것이지 `false`로 확보하는 것이 아닙니다.

> **참고**: 구버전 토스앱에서는 반환값이 무시됩니다. `processProductGrant`를 지원하지 않는 버전(Android 5.231.1 미만 / iOS 5.230.0 미만)에서는 브릿지가 구 결제 경로로 폴백하며, 이때 콜백의 반환값은 플랫폼에 전달되지 않고 버려집니다. 반환값에 의존하는 로직을 짤 때 이 구간을 염두에 두세요.

## 에러 처리

API 호출이 실패하면 `AITException`이 throw됩니다.

```csharp
using AppsInToss;
using UnityEngine;

public class ErrorHandling : MonoBehaviour
{
    async void CallAPI()
    {
        try
        {
            var result = await AIT.GetDeviceId();
            Debug.Log($"성공: {result}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"API 오류: {ex.Message}");
            Debug.LogError($"오류 코드: {ex.ErrorCode}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"예상치 못한 오류: {ex.Message}");
        }
    }
}
```

| 속성 | 타입 | 설명 |
|------|------|------|
| `Message` | `string` | 사람이 읽을 수 있는 오류 메시지 |
| `ErrorCode` | `string` | 오류 코드. 플랫폼이 주지 않으면 빈 문자열 |
| `APIName` | `string` | 실패한 API 이름. 알 수 없으면 빈 문자열 |
| `IsPlatformUnavailable` | `bool` | 플랫폼 브릿지 부재로 인한 오류인지 여부 |

`ErrorCode`로 분기하려면 값이 비어 있을 수 있다는 점을 감안하세요.

```csharp
catch (AITException ex)
{
    switch (ex.ErrorCode)
    {
        case "PAYMENT_CANCELLED":
            Debug.Log("사용자가 결제를 취소했습니다.");
            break;
        case "PAYMENT_FAILED":
            Debug.LogError("결제 처리 중 오류가 발생했습니다.");
            break;
        case "NETWORK_ERROR":
            Debug.LogError("네트워크 연결을 확인해주세요.");
            break;
        default:
            Debug.LogError($"알 수 없는 오류: {ex.Message}");
            break;
    }
}
```

### IsPlatformUnavailable

이 플래그는 별도 필드로 전달되는 것이 아니라 **에러 메시지를 보고 판정**합니다. 아래 문자열 중 하나라도 들어 있으면 `true`가 됩니다.

| 판정 문자열 | 언제 |
|-------------|------|
| `__GRANITE_NATIVE_EMITTER` | 네이티브 이미터가 없음 |
| `ReactNativeWebView` | 토스 앱 WebView 바깥에서 실행 중 |
| `is not a constant handler` | 그 API의 브릿지 핸들러가 없음 |
| `Cannot read properties of undefined` | `window.AppsInToss`가 아직 초기화되지 않음 |

`true`라면 코드 버그가 아니라 **실행 환경 문제**입니다. 일반 브라우저나 개발 환경에서 흔히 발생하므로, 에러 리포팅에 올릴 때는 이 케이스를 낮은 심각도로 내리거나 걸러내는 편이 낫습니다.

## 실행 환경별 동작

| 환경 | 동작 |
|------|------|
| WebGL 빌드 + Apps in Toss 앱 | 실제 네이티브 API 호출 |
| WebGL 빌드 + 일반 브라우저 | 대부분 실패. devtools가 켜져 있으면(Dev Server) mock으로 응답 |
| Unity Editor | Editor mock 호출 |
| 그 외 플랫폼 (Windows, macOS 등) | Editor mock 호출 |

Editor mock은 빌드 프로필과 무관합니다. `Runtime/SDK/`의 각 API가 `#if UNITY_WEBGL && !UNITY_EDITOR`로 갈라져 있어, WebGL 빌드가 아니면 **컴파일 시점에** mock 경로만 남습니다.

필요하면 실행 환경으로 분기할 수 있습니다.

```csharp
void Start()
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL 전용 로직
#else
    // 개발·테스트용 로직
#endif
}
```

실제 네이티브 동작을 확인하려면 WebGL로 빌드해 Apps in Toss 앱에서 실행해야 합니다. Editor에서는 무엇을 해도 mock입니다.

## Mock

"Mock"이라는 이름으로 불리는 것이 **두 가지**이고, 서로 다르게 동작합니다.

| | Editor mock | devtools |
|---|---|---|
| 어디에 | `Runtime/SDK/`의 C# | `@apps-in-toss/devtools`(npm 패키지, 빌드 산출물을 브라우저에서 열 때 동작) |
| 무엇을 | 모든 SDK API | 60개 이상의 SDK API + 상태를 조작하는 플로팅 패널 |
| 언제 | WebGL 빌드가 아닐 때 (컴파일 시점 결정) | Dev Server로 실행한 빌드를 일반 브라우저에서 열 때 |
| 어떻게 끄나 | 끌 수 없음 | `AIT > Configuration`의 devtools 설정, 또는 서버 실행 시 환경 변수 `AIT_DEVTOOLS=0` |

### Editor mock

Unity Editor와 비 WebGL 플랫폼에서 API를 호출하면 로그를 남기고 기본값을 돌려줍니다. 예외를 던지지 않으므로 Editor에서 게임 로직이 멈추지 않습니다.

```text
[AIT Mock] GetDeviceId called
[AIT Mock] GetPlatformOS called
```

| 반환 타입 | Mock 반환값 |
|-----------|------------|
| `string` | 빈 문자열 `""` |
| `bool` | `false` |
| 배열 | 빈 배열 |
| 클래스 타입 | `default`, 즉 `null` |
| 구독 취소 `Action` | 로그만 남기는 함수. `SafeAreaInsetsSubscribe`만 `null` |

클래스 타입이 `null`로 온다는 점이 중요합니다. Editor에서 `result.SomeField`를 바로 읽으면 `NullReferenceException`이 납니다. Editor에서도 돌려볼 로직이라면 null 체크를 넣으세요. 배열을 돌려주는 API는 빈 배열이 오므로 `foreach`가 안전합니다.

### devtools

`@apps-in-toss/devtools`는 `@apps-in-toss/web-framework` **3.x 전용** 개발 도구입니다. Dev Server를 실행하면 vite 플러그인이 `@apps-in-toss/web-framework` import를 mock 구현으로 alias해, 토스 앱 없이 일반 브라우저에서 60개 이상의 SDK API가 mock으로 동작합니다. 동시에 화면에 플로팅 패널이 떠서 로그인 상태·광고 결과·스토리지 값 같은 mock 상태를 직접 조작할 수 있습니다.

패널은 기본으로 켜져 있습니다. devtools 전체(또는 패널만)를 끄려면 `AIT > Configuration`의 devtools 설정을 바꾸세요 — 빌드 산출물은 그대로이므로 **서버 재시작만으로 반영**됩니다. CI나 임시 확인처럼 설정을 건드리지 않고 한 번만 끄고 싶다면 서버 실행 환경 변수 `AIT_DEVTOOLS=0`으로 오버라이드할 수 있습니다.

devtools가 꺼져 있는 상태(예: 일반 브라우저에서 열되 devtools를 비활성화한 경우)에서 SDK API를 부르면 `IsPlatformUnavailable`이 `true`인 `AITException`이 납니다.

## 관련 문서

- [시작하기](GettingStarted.md) — 설치와 기본 설정
- [광고 연동](Advertising.md) — 광고 API 사용법
- [Sentry 연동](SentryIntegration.md) — 에러를 Sentry로 수집하기
- [빌드 프로필](BuildProfiles.md) — devtools 설정 위치
- [문제 해결](Troubleshooting.md) — 자주 막히는 지점
