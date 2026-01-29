# Apps in Toss Unity SDK

Apps in Toss 플랫폼을 위한 Unity/Tuanjie 엔진 SDK입니다. Unity 프로젝트를 Apps in Toss 미니앱으로 변환하고 배포할 수 있습니다.

## 설치

Unity Editor에서 `Window` > `Package Manager` > `+` > `Add package from git URL...`을 선택하고 다음 URL을 입력하세요:

```
https://github.com/toss/apps-in-toss-unity-sdk.git#release/v1.9.0
```

또는 `Packages/manifest.json`에 직접 추가:

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/v1.9.2"
  }
}
```

## 지원 Unity 버전

- **최소 버전**: Unity 2021.3 LTS
- **권장 버전**: Unity 2022.3 LTS 또는 Unity 6000.2 LTS
- Tuanjie Engine 지원

## 주요 기능

- **WebGL 최적화**: Apps in Toss 환경에 최적화된 WebGL 빌드
- **자동 변환**: Unity 프로젝트를 미니앱으로 자동 변환
- **결제 연동**: 토스페이 결제 API
- **사용자 인증**: 앱 로그인 및 사용자 정보
- **기기 정보**: 기기 ID, 플랫폼, 네트워크 상태 조회
- **권한 관리**: 카메라, 연락처 등 권한 요청
- **위치 서비스**: 현재 위치 조회
- **피드백**: 햅틱 피드백, 클립보드

## 빠른 시작

### 1. 설정

`Apps in Toss > Build & Deploy Window` 메뉴에서:
- 앱 ID 입력
- 아이콘 URL 입력 (필수)

### 2. SDK 사용

```csharp
using AppsInToss;
using UnityEngine;

public class Example : MonoBehaviour
{
    async void Start()
    {
        try
        {
            string deviceId = await AIT.GetDeviceId();
            Debug.Log($"Device ID: {deviceId}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"API 오류: {ex.Message}");
        }
    }
}
```

### 3. 빌드 및 배포

| 메뉴 | 용도 |
|------|------|
| `AIT > Dev Server > Start Server` | 로컬 개발 서버 |
| `AIT > Build & Package` | 배포용 패키지 생성 |
| `AIT > Publish` | Apps in Toss에 배포 |

## 문서

| 문서 | 내용 |
|------|------|
| [시작하기](Docs/GettingStarted.md) | 상세 설치 및 초기 설정 |
| [API 사용 패턴](Docs/APIUsagePatterns.md) | async/await, 에러 처리, Mock 브릿지 |
| [빌드 프로필](Docs/BuildProfiles.md) | 빌드 설정, 환경 변수 |
| [빌드 커스터마이징](Docs/BuildCustomization.md) | 템플릿 수정, React/TypeScript |
| [로딩 화면 커스터마이징](Docs/LoadingScreenCustomization.md) | 로딩 UI 변경 |
| [메트릭](Docs/Metrics.md) | 성능 메트릭 확인 |
| [문제 해결](Docs/Troubleshooting.md) | FAQ 및 트러블슈팅 |
| [Contributing](Docs/Contributing.md) | 개발 환경, 커밋 규칙 |
