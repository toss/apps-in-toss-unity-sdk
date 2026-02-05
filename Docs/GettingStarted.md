# 시작하기

이 문서는 Apps in Toss Unity SDK의 설치부터 첫 번째 빌드까지의 전체 과정을 안내합니다.

## 목차

- [SDK 설치](#sdk-설치)
- [설정](#설정)
- [첫 번째 빌드](#첫-번째-빌드)
- [SDK 사용 예제](#sdk-사용-예제)

---

## SDK 설치

### 방법 1: Package Manager (권장)

1. Unity Editor에서 `Window` > `Package Manager` 열기
2. 왼쪽 상단 `+` 버튼 클릭
3. `Add package from git URL...` 선택
4. Git URL 입력:

```
https://github.com/toss/apps-in-toss-unity-sdk.git#release/v1.9.2
```

### 방법 2: manifest.json 직접 수정

프로젝트의 `Packages/manifest.json` 파일에 직접 추가:

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/v1.9.2"
  }
}
```

> **버전 선택**: `#release/v1.9.2` 부분을 원하는 버전으로 변경하세요. 최신 버전은 [GitHub Releases](https://github.com/toss/apps-in-toss-unity-sdk/releases)에서 확인할 수 있습니다.

### 지원 Unity 버전

| 버전 | 지원 상태 |
|------|----------|
| Unity 6000.3 (Unity 6.3) | ✅ 지원 |
| Unity 6000.2 (Unity 6 LTS) | ✅ 권장 |
| Unity 6000.0 (Unity 6) | ✅ 지원 |
| Unity 2022.3 LTS | ✅ 권장 |
| Unity 2021.3 LTS | ✅ 최소 지원 버전 |
| Tuanjie Engine | ✅ 지원 |

---

## 설정

SDK 설치 후 Unity Editor 메뉴에서 `AIT` > `Configuration`을 클릭하여 설정 패널을 엽니다.

### 필수 설정

| 설정 | 설명 |
|------|------|
| **앱 ID** | Apps in Toss 플랫폼에서 발급받은 앱 ID |
| **앱 아이콘 URL** | 미니앱 아이콘으로 표시될 이미지 URL (필수) |
| **표시 이름** | 로딩 화면에 표시될 앱 이름 |
| **기본 색상** | 브랜드 색상 (진행률 바 등에 사용) |

### 빌드 옵션

| 옵션 | 설명 |
|------|------|
| **Dev Server** | 로컬 개발 서버 실행 (Mock 브릿지 활성화) |
| **Production Server** | 프로덕션 환경 로컬 확인 |
| **Build & Package** | 배포용 패키지 생성 |
| **Publish** | Apps in Toss에 배포 |

> 빌드 옵션별 상세 설정은 [빌드 프로필 문서](BuildProfiles.md)를 참조하세요.

---

## 첫 번째 빌드

### 1. 설정 확인

1. `AIT` > `Configuration` 메뉴에서 설정 확인
2. **앱 아이콘 URL**이 입력되어 있는지 확인 (필수)

### 2. 개발 서버 실행

개발 단계에서는 Dev Server 모드를 사용합니다:

1. `AIT` > `Dev Server` > `Start Server` 메뉴 클릭
2. Unity WebGL 빌드가 자동으로 실행됨
3. 빌드 완료 후 로컬 개발 서버가 시작됨
4. 브라우저에서 자동으로 열리거나, 콘솔에 표시된 URL로 접속

### 3. 프로덕션 빌드

배포용 빌드를 생성하려면:

1. `AIT` > `Build & Package` 메뉴 클릭
2. 빌드 완료 후 `ait-build/dist/` 폴더에서 결과물 확인

### 4. 배포

Apps in Toss 플랫폼에 배포:

1. `AIT` > `Publish` 메뉴 클릭
2. 배포 키가 설정되어 있어야 함 (Configuration에서 설정)

---

## SDK 사용 예제

SDK API는 async/await 패턴을 사용합니다. 자세한 사용 패턴은 [API 사용 패턴 문서](APIUsagePatterns.md)를 참조하세요.

### 기본 사용법

```csharp
using AppsInToss;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    async void Start()
    {
        try
        {
            // 기기 ID 조회
            string deviceId = await AIT.GetDeviceId();
            Debug.Log($"Device ID: {deviceId}");

            // 플랫폼 OS 조회
            string os = await AIT.GetPlatformOS();
            Debug.Log($"Platform: {os}");

            // 네트워크 상태 확인
            NetworkStatus status = await AIT.GetNetworkStatus();
            Debug.Log($"Network: {status}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"API 호출 실패: {ex.Message} (code: {ex.ErrorCode})");
        }
    }
}
```

### 결제 요청

```csharp
using AppsInToss;
using UnityEngine;
using System.Threading.Tasks;

public class PaymentManager : MonoBehaviour
{
    public async Task RequestPayment()
    {
        try
        {
            var options = new CheckoutPaymentOptions {
                PayToken = "your-pay-token"
            };

            CheckoutPaymentResult result = await AIT.CheckoutPayment(options);
            Debug.Log($"Payment success: {result.Success}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"결제 실패: {ex.Message}");
        }
    }
}
```

### 햅틱 피드백

```csharp
using AppsInToss;
using UnityEngine;

public class FeedbackManager : MonoBehaviour
{
    public async void VibrateDevice()
    {
        try
        {
            var options = new HapticFeedbackOptions {
                Type = HapticFeedbackType.Tap
            };

            await AIT.GenerateHapticFeedback(options);
            Debug.Log("Haptic feedback generated");
        }
        catch (AITException ex)
        {
            Debug.LogError($"햅틱 피드백 실패: {ex.Message}");
        }
    }
}
```

---

## 다음 단계

- [API 사용 패턴](APIUsagePatterns.md) - async/await 패턴, 에러 처리
- [빌드 프로필](BuildProfiles.md) - 빌드 설정 상세 안내
- [빌드 커스터마이징](BuildCustomization.md) - 템플릿 커스터마이징
- [로딩 화면 커스터마이징](LoadingScreenCustomization.md) - 로딩 UI 변경
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
