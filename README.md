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
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/v1.6.2"
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

## WebGL 템플릿 커스터마이징

SDK는 사용자가 WebGL 빌드의 다양한 측면을 커스터마이징할 수 있도록 지원합니다. 커스터마이징은 SDK 업데이트 시에도 자동으로 보존됩니다.

### 커스터마이징 가능한 파일

| 파일 | 위치 | 커스터마이징 방법 |
|------|------|------------------|
| `index.html` | `Assets/WebGLTemplates/AITTemplate/` | 마커 영역에 스크립트/스타일 추가 |
| `vite.config.ts` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | `USER_CONFIG` 섹션에 플러그인 추가 |
| `granite.config.ts` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | `USER_CONFIG` 섹션에 설정 추가 |
| `package.json` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | dependencies에 npm 패키지 추가 |
| `tsconfig.json` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | compilerOptions 커스터마이징 (jsx, paths 등) |
| `src/` 폴더 | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | TypeScript/React 컴포넌트 구조화 |

### index.html 커스터마이징

`index.html`에서 `USER_HEAD`와 `USER_BODY_END` 마커 영역에 커스텀 스크립트나 스타일을 추가할 수 있습니다:

```html
<!-- USER_HEAD_START - 이 영역에 사용자 커스텀 스크립트/스타일을 추가하세요 -->
<script src="https://www.gstatic.com/firebasejs/10.7.0/firebase-app-compat.js"></script>
<script src="https://www.gstatic.com/firebasejs/10.7.0/firebase-analytics-compat.js"></script>
<link rel="stylesheet" href="custom-styles.css">
<!-- USER_HEAD_END -->
```

```html
<!-- USER_BODY_END_START - 이 영역에 사용자 커스텀 스크립트를 추가하세요 -->
<script>
    // Firebase 초기화
    firebase.initializeApp({
        apiKey: "your-api-key",
        projectId: "your-project-id"
    });
    firebase.analytics();
</script>
<!-- USER_BODY_END_END -->
```

### npm 패키지 추가

`BuildConfig~/package.json`의 `dependencies`에 필요한 패키지를 추가하세요:

```json
{
  "dependencies": {
    "@apps-in-toss/web-framework": "1.6.2",
    "lodash-es": "^4.17.21",
    "firebase": "^10.7.0"
  }
}
```

빌드 시 SDK 패키지와 사용자 패키지가 자동으로 머지됩니다.

### Vite 플러그인 추가

`BuildConfig~/vite.config.ts`의 `USER_CONFIG` 섹션에서 Vite 플러그인을 추가할 수 있습니다:

```typescript
//// USER_CONFIG_START ////
const userConfig = defineConfig({
  plugins: [
    // 사용자 플러그인 추가
  ],
  define: {
    __CUSTOM_FLAG__: JSON.stringify(true),
  },
});
//// USER_CONFIG_END ////
```

### tsconfig.json 커스터마이징

`BuildConfig~/tsconfig.json`을 생성하여 TypeScript 컴파일러 옵션을 커스터마이징할 수 있습니다.
프로젝트 옵션과 SDK 필수 옵션이 자동으로 머지됩니다:

```json
{
  "compilerOptions": {
    "jsx": "react-jsx",
    "paths": {
      "@/*": ["./src/*"]
    },
    "baseUrl": "."
  },
  "include": ["src", "*.ts", "*.tsx"]
}
```

> **참고**: SDK 필수 옵션(`moduleResolution`, `esModuleInterop`)은 SDK 값으로 강제 적용되어 호환성이 보장됩니다.

### React/TypeScript 컴포넌트 사용

`BuildConfig~/` 폴더에 `src/` 등 하위 폴더 구조를 생성하여 TypeScript/React 컴포넌트를 구조화할 수 있습니다.

#### 폴더 구조 예시

```
Assets/WebGLTemplates/AITTemplate/
├── index.html                    ← USER_BODY_END에서 tsx 파일 참조
└── BuildConfig~/
    ├── package.json              ← React 의존성 추가
    ├── tsconfig.json             ← jsx 옵션 추가
    ├── vite.config.ts            ← React 플러그인 추가
    └── src/
        ├── main.tsx              ← 진입점
        └── components/
            └── GameUI.tsx        ← React 컴포넌트
```

#### 1. package.json에 React 의존성 추가

```json
{
  "dependencies": {
    "react": "^18.2.0",
    "react-dom": "^18.2.0"
  },
  "devDependencies": {
    "@vitejs/plugin-react": "^4.0.0"
  }
}
```

#### 2. tsconfig.json 생성

```json
{
  "compilerOptions": {
    "jsx": "react-jsx"
  },
  "include": ["src"]
}
```

#### 3. vite.config.ts에 플러그인 추가

```typescript
//// USER_CONFIG_START ////
import react from '@vitejs/plugin-react';

const userConfig = defineConfig({
  plugins: [react()],
});
//// USER_CONFIG_END ////
```

#### 4. index.html에서 진입점 참조

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.tsx"></script>
<!-- USER_BODY_END_END -->
```

#### 5. React 컴포넌트 작성

`BuildConfig~/src/main.tsx`:

```tsx
import React from 'react';
import { createRoot } from 'react-dom/client';

function GameUI() {
  return <div id="game-ui">게임 UI</div>;
}

const container = document.getElementById('ui-root');
if (container) {
  createRoot(container).render(<GameUI />);
}
```

### SDK 업데이트 시 동작

SDK를 업데이트해도 사용자 커스터마이징은 자동으로 보존됩니다:

| 상황 | 동작 |
|------|------|
| 마커가 있는 템플릿 | 사용자 영역 보존, SDK 영역만 업데이트 |
| 마커가 없는 이전 템플릿 | 새 템플릿으로 교체 + 수동 마이그레이션 안내 |

업데이트 시 콘솔에서 다음과 같은 로그를 확인할 수 있습니다:

```
[AIT] ✓ index.html 템플릿 업데이트 (사용자 커스텀 영역 보존)
[AIT]   ✓ vite.config.ts (마커 기반 업데이트)
[AIT]   ✓ granite.config.ts (마커 기반 업데이트)
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

## Contributing

### 개발 환경 설정

저장소를 클론한 후, Git hooks를 활성화하세요:

```bash
./.githooks/setup.sh
```

이 스크립트는 `.meta` 파일 누락 검사를 위한 pre-commit hook을 설정합니다.

### Unity .meta 파일 규칙

Unity 패키지의 `Editor/`, `Runtime/`, `WebGLTemplates/` 디렉토리 내 모든 파일은 반드시 `.meta` 파일이 함께 있어야 합니다.

- **pre-commit hook**: 커밋 시 자동으로 `.meta` 파일 누락 검사
- **CI 검사**: PR 생성 시 GitHub Actions에서 자동 검증

`.meta` 파일이 누락된 경우:
1. Unity Editor에서 프로젝트를 열어 자동 생성
2. 또는 기존 `.meta` 파일을 참고하여 수동 생성 (고유 GUID 필요)

### 커밋 메시지 규칙

모든 커밋 메시지는 **한국어**로 작성합니다:

```
<타입>: <설명>
```

타입 예시:
- `기능`: 새로운 기능 추가
- `수정`: 버그 수정
- `개선`: 기존 기능 개선
- `리팩토링`: 코드 구조 개선
- `문서`: 문서 변경
- `테스트`: 테스트 추가/수정
- `빌드`: 빌드 설정 변경

예시:
```
기능: 사용자 인증 API 추가
수정: WebGL 빌드 오류 해결
개선: 빌드 성능 최적화
```
