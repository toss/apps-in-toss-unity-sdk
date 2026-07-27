# 문제 해결

SDK를 쓰다 자주 막히는 지점과 그 해결 방법입니다. 증상으로 찾으세요.

## 빌드가 되지 않을 때

### Node.js 를 찾을 수 없다는 오류

빌드 파이프라인은 Node.js를 사용합니다. 시스템에 설치돼 있지 않아도 SDK가 내장 Node.js를 자동으로 내려받으므로, 다운로드 다이얼로그가 뜨면 받으면 됩니다.

내장 Node.js는 아래에 저장됩니다. 경로에 Node 버전과 플랫폼이 하위 폴더로 붙습니다.

```text
macOS/Linux   ~/.ait-unity-sdk/nodejs/v<버전>/<플랫폼>/
Windows       %LOCALAPPDATA%\ait-unity-sdk\nodejs\v<버전>\<플랫폼>\
```

그래도 찾지 못한다는 오류가 계속되면 `~/.ait-unity-sdk/nodejs`를 통째로 지우고 다시 빌드하세요. 다운로드가 중간에 끊겨 손상된 경우가 대부분입니다.

### Unity WebGL 빌드 실패

1. **Unity 버전** — 최소 2021.3이 필요합니다. Unity 6 이상을 권장합니다.
2. **WebGL 모듈 미설치** — Unity Hub에서 WebGL Build Support 모듈을 설치하세요.
3. **메모리 부족** — Unity Editor를 재시작하고 다른 프로그램을 종료한 뒤 다시 시도하세요.

Console 창의 컴파일 오류와 스택 트레이스가 가장 확실한 단서입니다.

### 의존성 설치 실패

빌드 파이프라인은 pnpm을 사용합니다. `npm`이 아닙니다.

1. **네트워크** — 인터넷 연결과, 프록시 환경이라면 프록시 설정을 확인하세요.
2. **손상된 node_modules** — `ait-build/node_modules`를 삭제하고 다시 빌드하세요.
3. **직접 실행해 보기** — `ait-build` 디렉터리에서 `pnpm install`을 직접 실행하면 Unity Console보다 자세한 오류를 볼 수 있습니다.

### granite 빌드 실패

패키징 단계에서 실패한 경우입니다.

1. **TypeScript 컴파일 오류** — `BuildConfig~/`에 추가한 사용자 코드의 문법 오류를 확인하세요.
2. **의존성 충돌** — `package.json`에 추가한 패키지 버전을 확인하고, `node_modules`를 지운 뒤 다시 빌드해 보세요.

빌드 단계별로 무엇이 일어나는지는 [빌드 파이프라인](BuildProcess.md)에 정리되어 있습니다.

### 앱 설정이 올바르지 않다는 오류

`AIT` > `Configuration`에서 설정 에셋이 만들어졌는지 확인하세요. 이 오류는 설정 에셋 자체를 찾지 못할 때 납니다.

> **참고**: 필수 항목은 **앱 ID 하나뿐**입니다. 설정 창에서 `*`가 붙은 항목도 앱 ID뿐입니다. 아이콘 URL은 선택 항목이고, 입력한 경우에만 `http://` 또는 `https://`로 시작하는지 형식을 검사합니다. 비워 두어도 빌드는 진행됩니다.

## 실행이 이상할 때

### Unity Editor 에서 Mock 로그만 나옴

정상 동작입니다. SDK API는 WebGL 빌드에서만 실제로 브릿지를 탑니다. Editor에서는 `[AIT Mock] <API> called` 로그를 남기고 기본값을 돌려줍니다.

실제 동작은 WebGL로 빌드해 Apps in Toss 앱에서 확인하세요. 자세한 내용은 [API 사용 패턴](APIUsagePatterns.md#mock-브릿지)을 참고하세요.

### Dev Server 에서는 되는데 Production 에서 안 됨

Dev Server는 Mock 브릿지가 켜져 있어 일반 브라우저에서도 광고 흐름 등이 흉내로 동작합니다. Production 빌드는 실제 Apps in Toss 앱 환경을 필요로 합니다.

로컬에서 프로덕션 설정 그대로 확인하려면 이렇게 합니다.

1. `AIT` > `Production Server` > `Start Server`로 로컬 서버를 실행합니다.
2. [샌드박스 앱](https://developers-apps-in-toss.toss.im/development/test/sandbox)에서 그 로컬 서버에 연결합니다.

프로필별로 무엇이 달라지는지는 [빌드 프로필](BuildProfiles.md)에 있습니다.

### AITException 이 발생함

1. `ErrorCode`와 `Message`를 함께 확인하세요.
2. `IsPlatformUnavailable`이 `true`면 코드 문제가 아니라 실행 환경 문제입니다 — 브릿지에 닿지 못한 것입니다.
3. 네트워크 상태와 Apps in Toss 앱 버전을 확인하세요.

```csharp
try
{
    var result = await AIT.SomeAPI();
}
catch (AITException ex)
{
    Debug.LogError($"오류 코드: {ex.ErrorCode}, 메시지: {ex.Message}");
}
```

에러 처리 패턴 전반은 [API 사용 패턴](APIUsagePatterns.md)에 있습니다.

### 로딩 화면에서 멈춤

1. **Unity 초기화 실패** — 브라우저 개발자 도구의 Console 탭에서 오류를 확인하세요.
2. **리소스 로드 실패** — Network 탭에서 실패한 요청과 CORS 설정을 확인하세요.
3. **메모리 부족** — 모바일에서는 다른 앱을 종료하고 다시 시도하세요.

로딩 화면 자체를 다루는 방법은 [로딩 화면 커스터마이징](LoadingScreenCustomization.md)에 있습니다.

### 결제 API 가 동작하지 않음

1. **Mock 환경** — 실제 결제는 Apps in Toss 앱 안에서만 동작합니다.
2. **옵션 누락** — 필수 필드가 모두 채워졌는지 확인하세요. 특히 주문 생성 API는 `ProcessProductGrant`를 반드시 지정해야 합니다 (아래 항목 참고).

## 인앱결제 후 환불 안내 페이지가 뜸

결제는 성공했는데 `{앱 이름}에 문제가 생겼어요. 환불을 신청해주세요` 페이지가 뜨고 상품이 지급되지 않는 증상입니다.

`ProcessProductGrant` 콜백이 `true`가 아닌 값으로 응답한 것입니다. 대부분은 콜백을 **아예 설정하지 않은** 경우로, 이때 SDK는 등록된 핸들러가 없다는 이유로 자동으로 `false`를 응답하고 Console에 아래 에러를 남깁니다.

```text
[AITCore] Nested callback 'processProductGrant' is not registered
```

직접 `false`를 반환한 경우에도 같은 페이지가 뜹니다.

**해결**: 콜백을 설정하고 즉시 `true`를 반환하세요. 반환형이 `bool`이라 이 자리에서 서버 검증(`await`)은 애초에 컴파일되지 않습니다. 검증과 지급은 오버레이가 닫힌 뒤 `onEvent`에서 합니다.

```csharp
// ✅ 콜백은 즉시 승인하고, 검증·지급은 onEvent에서 합니다
options.ProcessProductGrant = _ => true;
// ...
onEvent: e => { ShowPurchaseSuccess(); _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }
```

`false`는 정말로 이 상품을 줄 수 없다고 지금 단정할 수 있을 때만 반환하세요. "확신이 없으니 일단 `false`"는 매 결제마다 이 페이지가 뜨는 앱이 됩니다.

**이미 실패한 주문 복구**: 이 증상으로 `true` 응답을 놓친 주문은 지급 실패 상태로 남습니다. `IAPGetPendingOrders`로 조회한 뒤 `IAPCompleteProductGrant`로 지급을 완료하세요. 승인은 됐지만 지급이 누락된 주문은 `IAPGetCompletedOrRefundedOrders`로 찾습니다.

> **중요**: 자세한 메커니즘과 전체 코드는 [API 사용 패턴](APIUsagePatterns.md#인앱결제-지급-승인과-서버-검증)을 참고하세요. 즉시 승인, `onEvent` 검증, 앱 시작 시 대사 — 이 셋은 한 묶음이라 하나만 떼어 쓰면 안 됩니다.

## 개발 환경

### AIT 메뉴가 보이지 않음

1. **패키지 설치 실패** — `Window` > `Package Manager`에서 SDK가 설치돼 있는지 확인하고, 오류가 있으면 제거 후 다시 설치하세요.
2. **컴파일 오류** — Console에 컴파일 오류가 하나라도 있으면 메뉴가 등록되지 않습니다. 모두 해결한 뒤 Unity를 재시작하세요.
3. **Unity 버전** — 2021.3 이상인지 확인하세요.

## 그래도 해결되지 않으면

1. Unity Console의 전체 오류 메시지를 확보하세요.
2. 브라우저 개발자 도구의 Console과 Network 탭을 함께 확인하세요.
3. [GitHub 이슈](https://github.com/toss/apps-in-toss-unity-sdk/issues)를 등록하거나 [TechChat](https://techchat-apps-in-toss.toss.im)으로 문의하세요.

## 관련 문서

- [시작하기](GettingStarted.md) — 설치와 기본 설정
- [API 사용 패턴](APIUsagePatterns.md) — 비동기 패턴과 에러 처리
- [빌드 프로필](BuildProfiles.md) — 프로필별 설정
- [빌드 파이프라인](BuildProcess.md) — 빌드 단계와 에러 코드
- [기여 가이드](Contributing.md) — SDK 자체를 고칠 때
