# Apps in Toss Unity SDK

Apps in Toss 플랫폼을 위한 Unity/Tuanjie 엔진 SDK입니다.

## 설치 가이드

Unity 엔진 또는 [Tuanjie 엔진](https://unity.cn/tuanjie/tuanjieyinqing)으로 게임 프로젝트를 생성/열기한 후,
Unity Editor 메뉴바에서 `Window` - `Package Manager` - `오른쪽 상단 + 버튼` - `Add package from git URL...`을 클릭하여 본 저장소 Git 리소스 주소를 입력하면 됩니다.

## 지원 Unity 버전

- **최소 버전**: Unity 2021.3 LTS
- **권장 버전**: Unity 2022.3 LTS 이상
- Tuanjie Engine 지원

## 주요 기능

### 플랫폼 연동
- **WebGL 최적화**: Apps in Toss 환경에 최적화된 WebGL 빌드
- **자동 변환**: Unity 프로젝트를 Apps in Toss 미니앱으로 자동 변환
- **성능 최적화**: 모바일 환경에 최적화된 성능 튜닝

### API 기능
- **결제**: 토스페이 결제 연동 (`CheckoutPayment`)
- **사용자 인증**: 앱 로그인 및 사용자 정보 (`AppLogin`, `GetUserKeyForGame`)
- **기기 정보**: 기기 ID, 플랫폼, 네트워크 상태 조회
- **권한 관리**: 카메라, 연락처 등 권한 요청 및 확인
- **위치 서비스**: 현재 위치 조회
- **피드백**: 햅틱 피드백, 클립보드 접근
- **공유**: 컨텐츠 공유 기능

## 시작하기

### 1. SDK 설치

Package Manager에서 Git URL로 설치하거나, Packages/manifest.json에 직접 추가:

```json
{
  "dependencies": {
    "com.toss.appsintoss": "https://github.toss.bz/toss/apps-in-toss-unity-sdk.git"
  }
}
```

### 2. 기본 설정

Unity Editor에서 `Apps in Toss > Build & Deploy Window` 메뉴를 클릭하여 설정 패널을 열고:
- 앱 ID 입력
- 아이콘 URL 입력 (필수)
- 빌드 설정 구성

### 3. SDK 사용 예제

SDK API는 async/await 패턴을 사용합니다:

```csharp
using AppsInToss;
using UnityEngine;
using System.Threading.Tasks;

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
            PlatformOS os = await AIT.GetPlatformOS();
            Debug.Log($"Platform: {os}");

            // 네트워크 상태 확인
            NetworkStatus status = await AIT.GetNetworkStatus();
            Debug.Log($"Network: {status}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"API 호출 실패: {ex.Message} (code: {ex.Code})");
        }
    }

    // 결제 요청 예제
    public async Task RequestPayment()
    {
        try
        {
            var options = new CheckoutPaymentOptions {
                // 결제 옵션 설정
            };

            CheckoutPaymentResult result = await AIT.CheckoutPayment(options);
            Debug.Log($"Payment result: {result.paymentKey}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"결제 실패: {ex.Message}");
        }
    }

    // 햅틱 피드백 예제
    public async void VibrateDevice()
    {
        try
        {
            var options = new GenerateHapticFeedbackOptions {
                style = "medium"
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

### 4. 빌드 및 배포

1. `AIT > Configuration` 메뉴에서 설정 확인
2. 메뉴에서 원하는 작업 선택:
   - `AIT > Dev Server > Start Server`: 로컬 개발 서버 실행
   - `AIT > Production Server > Start Server`: 프로덕션 환경 로컬 확인
   - `AIT > Build & Package`: 배포용 패키지 생성
   - `AIT > Publish`: Apps in Toss에 배포
3. 빌드 완료 후 `ait-build/dist/` 폴더에서 결과물 확인

## 빌드 프로필 시스템

SDK는 각 작업 메뉴별로 다른 빌드 설정(프로필)을 자동 적용합니다.

### 기본 프로필 매트릭스

| 작업 | Mock 브릿지 | 디버그 심볼 | 디버그 콘솔 | LZ4 압축 |
|------|:-----------:|:-----------:|:-----------:|:--------:|
| **Dev Server** | ✅ 활성화 | Embedded | ✅ 활성화 | ✅ 활성화 |
| **Production Server** | ❌ 비활성화 | External | ❌ 비활성화 | ✅ 활성화 |
| **Build & Package** | ❌ 비활성화 | External | ❌ 비활성화 | ✅ 활성화 |
| **Publish** | ❌ 비활성화 | External | ❌ 비활성화 | ✅ 활성화 |

### 각 설정의 의미

- **Mock 브릿지**: 로컬 브라우저에서 테스트할 때 네이티브 API 없이 동작하도록 Mock 구현 사용
- **디버그 심볼**: `External`은 심볼을 별도 파일로 분리하여 빌드 크기 감소, `Embedded`는 빌드에 포함
- **디버그 콘솔**: 개발/테스트용 콘솔 UI 활성화
- **LZ4 압축**: 빌드 속도 향상을 위한 LZ4 압축 사용

### 프로필 커스터마이징

`AIT > Configuration` 메뉴에서 각 프로필의 설정을 변경할 수 있습니다:

1. "빌드 프로필" 섹션 확장
2. 원하는 프로필(Dev Server, Production Server 등)을 펼치기
3. 각 옵션의 체크박스를 변경
4. 변경 사항은 자동 저장됨

### 환경 변수 오버라이드

CI/CD 환경이나 자동화 스크립트에서 환경 변수를 통해 빌드 프로필 설정을 오버라이드할 수 있습니다.

| 환경 변수 | 설명 | 값 |
|----------|------|-----|
| `AIT_DEBUG_CONSOLE` | 디버그 콘솔 활성화 | `true`/`false` |

**사용 예시:**

```bash
# 로컬 테스트
AIT_DEBUG_CONSOLE=true ./run-local-tests.sh --all

# Unity 직접 실행
AIT_DEBUG_CONSOLE=true /Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath ./MyProject \
  -executeMethod AITConvertCore.CommandLineBuild
```

**GitHub Actions 예시:**

```yaml
- name: Build with Debug Console
  env:
    AIT_DEBUG_CONSOLE: "true"
  run: |
    unity -executeMethod E2EBuildRunner.CommandLineBuild ...
```

### 빌드 로그

빌드 시작 시 적용된 프로필이 Console에 출력됩니다:

```
[AIT] ========================================
[AIT] 빌드 프로필: Dev Server
[AIT] ========================================
[AIT]   Mock 브릿지: 활성화
[AIT]   디버그 심볼: Embedded
[AIT]   디버그 콘솔: 활성화
[AIT]   LZ4 압축: 활성화
[AIT] ========================================
```

## 자주 묻는 질문

### Q1. 빌드 시 Node.js가 없다는 오류가 발생합니다

SDK는 시스템에 Node.js가 설치되어 있지 않아도 자동으로 내장 Node.js를 다운로드합니다.
다운로드 다이얼로그가 표시되면 "다운로드"를 선택하세요.

### Q2. 아이콘 URL을 입력하라는 오류가 발생합니다

Build & Deploy Window에서 앱 아이콘 URL을 반드시 입력해야 합니다.
이 URL은 Apps in Toss 앱에서 미니앱 아이콘으로 표시됩니다.

### Q3. Unity Editor에서 API 호출 시 Mock 로그만 출력됩니다

SDK API는 WebGL 빌드에서만 실제로 동작합니다.
Unity Editor에서는 Mock 구현이 호출되어 테스트 로그만 출력됩니다.
실제 동작은 WebGL로 빌드 후 Apps in Toss 앱에서 확인하세요.

