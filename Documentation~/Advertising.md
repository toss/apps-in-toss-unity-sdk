# Apps in Toss 광고 가이드

Apps in Toss Unity SDK는 세 가지 광고 표면을 제공합니다.

| 광고 종류 | 진입 API | 형태 | 언제 사용 |
|----------|----------|------|----------|
| **전체 화면 광고** | `AIT.LoadFullScreenAd` / `AIT.ShowFullScreenAd` | 전면(Interstitial) · 보상형(Rewarded) | Toss 광고 네트워크의 전면/보상형 광고를 직접 노출 |
| **AdMob 광고** | `AIT.GoogleAdMobLoadAppsInTossAdMob` / `…Show…` | 전면(Interstitial) · 보상형(Rewarded) | Google AdMob 미디에이션을 통해 전면/보상형 광고를 노출 |
| **배너 광고** | `AITBannerAd` (정적) · `AITBannerAdView` (컴포넌트) | 배너 | 화면 상·하단 또는 임의 영역에 상시 배너를 노출 |

> 전면 광고와 AdMob 광고는 모두 **전면(Interstitial)** 과 **보상형(Rewarded)** 을 지원합니다. 둘은 호출 API가 같고 **광고 그룹 ID(`adGroupId`)로 구분**되며, 보상형만 사용자가 보상을 획득한 시점에 `userEarnedReward` 이벤트(보상 종류/수량 포함)를 추가로 발생시킵니다.

## 공통 전제

- **`adGroupId`** 는 Apps in Toss 콘솔에서 발급받은 광고 그룹 ID입니다. 이 값으로 광고 종류(전면/보상형, 배너 강조 유형)와 노출 정책이 결정됩니다.
- **실제 광고 렌더링은 Toss 앱(또는 샌드박스) 안에서만 동작**합니다. 일반 브라우저/Unity Editor에서는 광고 네트워크에 도달하지 못하므로, 초기화 이후 `FailedToRender`/`NoFill`(배너) 또는 에러 콜백이 오는 것이 정상입니다.
- Unity Editor·비 WebGL 환경에서는 모든 광고 API가 `[AIT Mock]` 로그만 남기고 실제 이벤트를 발생시키지 않습니다. 통합 동작 확인은 WebGL 빌드 후 Toss 앱에서 진행하세요.
- 모든 콜백형 API는 **구독 취소용 `Action`** 을 반환합니다. 컴포넌트 `OnDestroy` 등에서 호출해 정리하세요.

---

## 1. 전체 화면 광고 (Full Screen Ad)

Toss 광고 네트워크의 전면/보상형 광고를 노출하는 네이티브 경로입니다. **`Load` → `Show` 2단계**로 호출합니다.

```csharp
using AppsInToss;

// adGroupId 는 콘솔에서 발급받은 값. 전면/보상형은 adGroupId 로 구분됩니다.
private const string AD_GROUP_ID = "your-ad-group-id";
private Action _unsubscribe;

// 1단계: 미리 로드
void Load()
{
    _unsubscribe = AIT.LoadFullScreenAd(
        adGroupId: AD_GROUP_ID,
        onEvent: e =>
        {
            if (e.Type == "loaded") Show();   // 로드 완료 후 노출
        },
        onError: err => Debug.LogError($"load 실패: {err.ErrorCode} {err.Message}")
    );
}

// 2단계: 노출
void Show()
{
    AIT.ShowFullScreenAd(
        adGroupId: AD_GROUP_ID,
        onEvent: e =>
        {
            // 보상형 광고에서 사용자가 보상을 획득한 경우
            if (e.Type == "userEarnedReward" && e.Data != null)
                Debug.Log($"보상: {e.Data.UnitAmount} {e.Data.UnitType}");

            if (e.Type == "dismissed")
                Debug.Log("광고 닫힘 — 다음 노출을 위해 다시 Load 필요");
        },
        onError: err => Debug.LogError($"show 실패: {err.ErrorCode} {err.Message}")
    );
}

void OnDestroy() => _unsubscribe?.Invoke();
```

**주요 이벤트(`e.Type`)**

| 단계 | Type | 의미 |
|------|------|------|
| Load | `loaded` | 광고 로드 완료 (이후 `Show` 가능) |
| Show | `userEarnedReward` | 보상형 전용 — 사용자 보상 획득 (`e.Data.UnitType`/`UnitAmount`) |
| Show | `dismissed` | 광고 닫힘 — 다음 노출 전 재 `Load` 필요 |

> 📄 **샘플 코드:** [`FullScreenAdTester.cs`](https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Tests~/E2E/SharedScripts/Runtime/FullScreenAdTester.cs) — 전면/보상형 선택, Load→Show 흐름, 이벤트 로그를 인터랙티브하게 확인할 수 있습니다.

---

## 2. AdMob 광고 (Google AdMob 미디에이션)

Google AdMob 미디에이션을 경유해 전면/보상형 광고를 노출합니다. 전체 화면 광고와 동일하게 **`Load` → `Show`** 흐름이며, 로드 여부를 조회하는 API도 제공합니다.

> AdMob API는 SDK 1.7.0 이상에서 사용할 수 있습니다 (`AIT_SDK_1_7_OR_LATER`).

```csharp
using AppsInToss;

private const string AD_GROUP_ID = "your-admob-ad-group-id";
private Action _unsubscribe;

void Load()
{
    _unsubscribe = AIT.GoogleAdMobLoadAppsInTossAdMob(
        options: new LoadAdMobOptions { AdGroupId = AD_GROUP_ID },
        onEvent: e =>
        {
            if (e.Type == "loaded" && e.Data != null)
                Debug.Log($"loaded: adUnitId={e.Data.AdUnitId}");
        },
        onError: err => Debug.LogError($"{err.ErrorCode} {err.Message}")
    );
}

void Show()
{
    AIT.GoogleAdMobShowAppsInTossAdMob(
        options: new ShowAdMobOptions { AdGroupId = AD_GROUP_ID },
        onEvent: e =>
        {
            if (e.Type == "userEarnedReward" && e.Data != null)
                Debug.Log($"보상: {e.Data.UnitAmount} {e.Data.UnitType}");
            if (e.Type == "dismissed")
                Debug.Log("광고 닫힘 — 재 Load 필요");
        },
        onError: err => Debug.LogError($"{err.ErrorCode} {err.Message}")
    );
}

// 로드 여부 조회 (Task<bool>)
async void CheckLoaded()
{
    bool loaded = await AIT.GoogleAdMobIsAppsInTossAdMobLoaded(
        new IsAdMobLoadedOptions { AdGroupId = AD_GROUP_ID });
    Debug.Log($"loaded = {loaded}");
}

void OnDestroy() => _unsubscribe?.Invoke();
```

> 📄 **샘플 코드:** [`AdV2Tester.cs`](https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Tests~/E2E/SharedScripts/Runtime/AdV2Tester.cs) — AdMob 전면/보상형 Load·Show·IsLoaded 호출과 보상 이벤트 처리 예시입니다.

---

## 3. 배너 광고 (Banner Ad)

배너는 두 가지 방식으로 사용합니다. **HTML/CSS 지식 없이** SDK가 배너 DOM 컨테이너를 직접 생성·관리합니다.

- **`AITBannerAd` (정적 helper)** — 화면 상단/하단 프리셋 위치에 코드 한 줄로 표시. 슬롯 1개를 유지하며 재호출 시 교체.
- **`AITBannerAdView` (MonoBehaviour 컴포넌트)** — Canvas 아래 RectTransform을 평소 uGUI처럼 배치하면 그 영역 위에 배너를 오버레이. 이동·리사이즈·화면 회전을 자동 추적하며, 인스턴스마다 독립 슬롯이라 **여러 개를 동시에** 표시할 수 있습니다.

> 배너의 **강조 유형(문구 강조 ~90px 고정 / 이미지 강조 16:9 가변 높이)** 은 코드가 아니라 콘솔의 광고 그룹 설정이 결정합니다. 가변 높이는 `Resized` 이벤트로 통지됩니다.

### 3-1. 정적 helper — `AITBannerAd`

```csharp
using AppsInToss;

AITBannerAd.OnAdEvent += evt => Debug.Log($"배너 이벤트: {evt}");

// 화면 하단에 표시 (기본값: Bottom / Auto 테마 / 흑백 톤 / 확장형)
AITBannerAd.Show("your-banner-ad-group-id", AITBannerPosition.Bottom);

// 숨기기
AITBannerAd.Hide();
```

### 3-2. 컴포넌트 — `AITBannerAdView`

Inspector에서 `Ad Group Id`, `Placement`, 테마/톤/변형, `On Ad Event`(UnityEvent)를 설정하거나, 코드로 부착합니다.

```csharp
using AppsInToss;

// 배너를 띄울 RectTransform(이 GameObject) 영역을 따라 배너가 오버레이됩니다.
var view = gameObject.AddComponent<AITBannerAdView>();
view.AdGroupId = "your-banner-ad-group-id";
view.Placement = AITBannerAdPlacement.FollowRectTransform; // 또는 ScreenTop / ScreenBottom

// C# 이벤트 + Inspector UnityEvent 양쪽으로 전달됩니다.
view.OnAdEvent += evt =>
{
    if (evt.Kind == AITBannerAdEventKind.Resized)
        Debug.Log($"렌더 높이 {evt.Height}px (비율 {evt.HeightFraction:F3})");
};

view.Show();   // showOnEnable 이 true(기본)면 OnEnable 에서 자동 호출
// view.Hide();
```

**배너 이벤트(`AITBannerAdEvent.Kind`)**

| Kind | 의미 |
|------|------|
| `Initialized` / `InitializationFailed` | 광고 SDK 초기화 완료 / 실패 |
| `Rendered` | 배너 렌더링 완료 |
| `Viewable` / `Impression` | 화면 노출 / 노출 집계 |
| `Clicked` | 배너 클릭 |
| `Resized` | 렌더된 배너 크기 변경 (`Width`/`Height` CSS px, `HeightFraction` 캔버스 대비 비율) |
| `FailedToRender` / `NoFill` | 렌더 실패 / 채울 광고 없음 (`ErrorCode`/`ErrorMessage`) |

> **자동 높이 조정:** `FollowRectTransform` 모드에서 `AutoResizeHeight`(기본 true)면 RectTransform 높이를 실제 배너 높이에 맞춰 조정합니다. 레이아웃 그룹 하위에 둘 때는 `AutoResizeHeight = false`로 두고, `Resized` 이벤트에서 `view.RenderedHeightLocal`을 읽어 `LayoutElement.preferredHeight`에 직접 반영하세요.

> 📄 **샘플 코드:** [`BannerAdTester.cs`](https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Tests~/E2E/SharedScripts/Runtime/BannerAdTester.cs) — 컴포넌트(문구/이미지 강조 2종 동시 표시 = multi-slot)와 정적 helper(Top/Bottom)를 함께 띄워 이벤트와 자동 높이 동작을 확인할 수 있습니다.

---

## 광고가 보이지 않을 때 (공통 체크리스트)

1. **Toss 앱 안에서 실행 중인가?** 실제 광고 렌더링은 Toss 앱/샌드박스 안에서만 동작합니다. 일반 브라우저·Editor에서는 초기화 후 실패/노필 이벤트가 오는 것이 정상입니다.
2. **`adGroupId`가 콘솔 발급 값과 일치하는가?** 잘못된 ID는 형식/파라미터 오류(예: `code 1002`)로 거부됩니다.
3. **전면/AdMob 광고는 `Load` 완료(`loaded`) 후 `Show`** 했는가? 닫힌(`dismissed`) 뒤에는 다시 `Load`해야 합니다.
4. **이벤트 콜백/`onError`를 구독**해 실패 사유를 로깅하고 있는가? 배너는 `FailedToRender`/`NoFill`의 `ErrorCode`/`ErrorMessage`를 확인하세요.

## 샘플 프로젝트

세 광고의 인터랙티브 테스터는 SDK 저장소의 샘플에 포함되어 있습니다.

- 공유 스크립트: [`Tests~/E2E/SharedScripts/Runtime/`](https://github.com/toss/apps-in-toss-unity-sdk/tree/main/Tests~/E2E/SharedScripts/Runtime)
  ([FullScreenAdTester](https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Tests~/E2E/SharedScripts/Runtime/FullScreenAdTester.cs) ·
  [AdV2Tester](https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Tests~/E2E/SharedScripts/Runtime/AdV2Tester.cs) ·
  [BannerAdTester](https://github.com/toss/apps-in-toss-unity-sdk/blob/main/Tests~/E2E/SharedScripts/Runtime/BannerAdTester.cs))
- 버전별 샘플 Unity 프로젝트: [`Tests~/E2E/`](https://github.com/toss/apps-in-toss-unity-sdk/tree/main/Tests~/E2E)
