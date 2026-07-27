# 빌드 커스터마이징

미니앱을 감싸는 웹 레이어(HTML, TypeScript, npm 의존성, Vite 설정)를 SDK 업데이트에도 살아남는 방식으로 수정하는 방법을 설명합니다.

## 어디를 건드리나

빌드는 Unity가 WebGL 산출물을 만드는 단계와, 그 산출물을 웹 프로젝트로 감싸 패키징하는 단계로 나뉩니다. 내부 동작은 [빌드 파이프라인](BuildProcess.md)에 있고, 여기서는 **사용자가 편집할 위치**만 다룹니다.

| 단계 | 출력 | 편집 지점 |
|------|------|-----------|
| Unity WebGL 빌드 | `webgl/` (중간 산출물) | 편집하지 않음. 설정은 [빌드 프로필](BuildProfiles.md) |
| Granite 패키징 | `ait-build/` → `ait-build/dist/` | `Assets/WebGLTemplates/AITTemplate/` 하위 — 이 문서 |

> **주의**: `webgl/`과 `ait-build/`의 파일은 직접 수정하지 마세요. `webgl/`은 Unity가 매 빌드마다 새로 만드는 중간 산출물이고, 패키징은 이 폴더가 아니라 `Assets/WebGLTemplates/AITTemplate/`의 템플릿을 기준으로 동작합니다. 두 폴더를 고쳐도 최종 패키지에는 반영되지 않고 다음 빌드에서 사라집니다.

> **참고**: QR 테스트와 실제 배포가 쓰는 최종 패키지는 `ait-build/dist/`입니다. 빌드 결과를 직접 열어볼 때는 이 폴더를 보세요.

## 사용자 영역 마커

SDK 템플릿은 빌드 진입 시점마다 최신 SDK 버전과 병합됩니다. 이때 **마커 사이에 있는 내용만 보존**되고, 마커 밖은 SDK 값으로 갱신됩니다. 병합이 언제 어떤 파일에 일어나는지는 [빌드 파이프라인](BuildProcess.md)의 템플릿 병합 시점 절에 있습니다.

### HTML 마커

`index.html`은 두 영역을 제공합니다.

```html
<!-- USER_HEAD_START - 이 영역에 사용자 커스텀 스크립트/스타일을 추가하세요 -->
<!-- USER_HEAD_END -->

<!-- USER_BODY_END_START - 이 영역에 사용자 커스텀 스크립트를 추가하세요 -->
<!-- USER_BODY_END_END -->
```

`USER_HEAD`는 `<head>` 안에, `USER_BODY_END`는 `</body>` 직전에 들어갑니다.

### TypeScript 설정 파일 마커

`vite.config.ts`, `granite.config.ts`, `apps-in-toss.config.ts`가 같은 마커 쌍을 씁니다.

```typescript
//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
// SDK가 관리하는 코드. 여기 쓴 내용은 SDK 업데이트 시 사라집니다.
//// SDK_GENERATED_END ////

//// USER_CONFIG_START ////
// 사용자 커스텀 코드. SDK 업데이트 시 보존됩니다.
//// USER_CONFIG_END ////
```

> **중요**: `USER_CONFIG`에 SDK가 관리하는 설정(앱 이름, 브랜드, 권한, `webViewProps` 등)을 다시 선언하면 병합 시 SDK 값이 이기므로 아무 효과가 없습니다. 빌드는 정상이지만 다음 경고가 뜹니다 — 해당 키를 `USER_CONFIG`에서 지우세요.
>
> ```text
> [AIT]   ⚠ apps-in-toss.config.ts의 USER_CONFIG에 SDK가 관리하는 설정이 남아 있습니다.
> ```

반대로 `SDK_GENERATED` 영역에 치환되지 않은 플레이스홀더가 남으면 하드 에러로 빌드가 중단됩니다. 이 경우 Clean Build로 템플릿을 다시 만드세요.

## 커스터마이징 가능한 파일

**모든 파일은 `Assets/WebGLTemplates/AITTemplate/` 하위에 있습니다.**

| 파일 | 역할 | 머지 방식 |
|------|------|----------|
| `index.html` | HTML 엔트리 포인트 | `USER_HEAD` / `USER_BODY_END` 마커 영역 보존 |
| `BuildConfig~/package.json` | npm 의존성 | dependencies / devDependencies 머지 (충돌 시 SDK 우선) |
| `BuildConfig~/vite.config.ts` | Vite 빌드 설정 | `USER_CONFIG` 마커 영역 보존 |
| `BuildConfig~/granite.config.ts` | Granite 패키징 설정 (2.x) | `USER_CONFIG` 마커 영역 보존 |
| `BuildConfig~/apps-in-toss.config.ts` | Apps in Toss 설정 (3.x) | `USER_CONFIG` 마커 영역 보존. 비어 있으면 `granite.config.ts`의 `USER_CONFIG`를 자동 이전 |
| `BuildConfig~/tsconfig.json` | TypeScript 컴파일러 설정 | SDK 필수 옵션(`moduleResolution`, `esModuleInterop`)은 SDK 값으로 강제, 그 외는 프로젝트 값 우선 |
| `BuildConfig~/pnpm-workspace.yaml` | pnpm 워크스페이스 설정 | 프로젝트 파일이 있으면 그것을, 없으면 SDK 파일을 복사 |
| `BuildConfig~/src/` | TypeScript 진입점 및 모듈 | 폴더 전체 보존 (재귀 복사) |
| `BuildConfig~/` 기타 파일 | `.env`, 정적 자산 등 | 아래 제외 목록을 뺀 모든 루트 파일과 하위 폴더를 그대로 복사 |

기타 파일 복사에서 제외되는 것들 — 루트 파일 `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`, `vite.config.ts`, `tsconfig.json`, `unity-bridge.ts`, `granite.config.ts`, `apps-in-toss.config.ts` (각각 전용 머지 경로가 있음)와 폴더 `node_modules/`, `.npm-cache/`, `dist/`.

> **dependencies 충돌 처리**: SDK가 이미 선언한 패키지(`@apps-in-toss/web-framework`, `@apps-in-toss/web-analytics`, `vite`, `typescript` 등)를 다른 버전으로 추가하면 SDK 버전이 우선합니다. SDK가 선언하지 않은 패키지(예: `firebase`, `canvas-confetti`)는 그대로 추가됩니다.

> **참고**: `pnpm-workspace.yaml`은 pnpm의 공급망 보호(`minimumReleaseAge`)에서 `@apps-in-toss/*`를 예외 처리하기 위해 존재합니다. pnpm이 이 설정을 `pnpm-workspace.yaml`에서만 읽기 때문에 빌드 디렉토리로 반드시 복사됩니다. 특별한 이유가 없으면 SDK 기본값을 그대로 두세요.

## index.html 커스터마이징

수정 대상은 `Assets/WebGLTemplates/AITTemplate/index.html`입니다. **반드시 `_START`와 `_END` 마커 사이에 추가**해야 보존됩니다.

`USER_HEAD`는 메타 태그, 폰트, preload 힌트, 외부 스타일시트 같은 정적 리소스 선언에 씁니다.

```html
<!-- USER_HEAD_START - 이 영역에 사용자 커스텀 스크립트/스타일을 추가하세요 -->
<meta name="theme-color" content="#3182f6">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Sans+KR&display=swap">
<!-- USER_HEAD_END -->
```

`USER_BODY_END`는 사용자 코드의 진입점을 참조하는 데 씁니다. 권장 패턴은 TypeScript 진입점을 모듈로 로드하는 것입니다 — 진입점에 작성한 모든 import가 Vite의 트리 셰이킹·압축을 거쳐 하나의 번들로 묶입니다.

```html
<!-- USER_BODY_END_START - 이 영역에 사용자 커스텀 스크립트를 추가하세요 -->
<script type="module" src="./src/main.ts"></script>
<!-- USER_BODY_END_END -->
```

빌드가 끝나면 `ait-build/index.html`을 열어 작성한 코드가 들어갔는지 확인할 수 있습니다. Unity Console에 다음이 찍히면 머지가 정상 동작한 것입니다.

```text
[AIT] index.html USER_HEAD 섹션 머지됨
[AIT] index.html USER_BODY_END 섹션 머지됨
```

## TypeScript 진입점

사용자 코드는 `BuildConfig~/src/main.ts`를 진입점으로 작성합니다. Vite가 이 파일을 번들링하므로 npm 패키지 import, 트리 셰이킹, 타입 검사가 모두 적용됩니다.

```text
Assets/WebGLTemplates/AITTemplate/
├── index.html                    ← USER_BODY_END에서 main.ts 참조
└── BuildConfig~/
    ├── package.json              ← 의존성
    ├── tsconfig.json             ← TypeScript 옵션 (선택)
    └── src/
        └── main.ts               ← 진입점
```

`BuildConfig~/src/main.ts`:

```ts
window.addEventListener('load', () => {
    console.log('User entry loaded');
});
```

`BuildConfig~/tsconfig.json`을 두면 컴파일러 옵션을 커스터마이징할 수 있습니다. SDK 필수 옵션(`moduleResolution`, `esModuleInterop`)은 SDK 값으로 강제됩니다.

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

## 외부 라이브러리 추가

npm 패키지로 설치하고 진입점에서 import하는 방식을 권장합니다. 버전이 고정되어 빌드 재현성이 보장되고, CDN 장애나 네트워크 차단의 영향을 받지 않으며, 트리 셰이킹과 압축이 적용됩니다.

절차는 라이브러리와 무관하게 같습니다 — `package.json`에 의존성 추가 → `main.ts`에서 import → `index.html`에서 진입점 참조. 구체적인 예는 아래 [튜토리얼](#튜토리얼)을 보세요.

### 대안으로 CDN 직접 로드

빌드 도구 없이 빠르게 시험만 하고 싶을 때는 `USER_HEAD`에 `<script src="...">`로 직접 로드할 수 있습니다. 다만 CDN 장애 시 앱 로드가 실패하고, 버전이 URL에 박혀 재현성이 떨어지며, 트리 셰이킹과 타입 검사를 받지 못합니다. 일상적인 사용에는 권장하지 않습니다.

```html
<!-- USER_HEAD_START -->
<script src="https://cdn.jsdelivr.net/npm/canvas-confetti@1.9.3/dist/confetti.browser.min.js"></script>
<!-- USER_HEAD_END -->
```

```html
<!-- USER_BODY_END_START -->
<script>
    window.addEventListener('load', () => {
        confetti({ particleCount: 100, spread: 70, origin: { y: 0.6 } });
    });
</script>
<!-- USER_BODY_END_END -->
```

## Vite 설정 커스터마이징

`BuildConfig~/vite.config.ts`의 `USER_CONFIG` 섹션에서 플러그인이나 빌드 옵션을 추가합니다.

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

`granite.config.ts`와 `apps-in-toss.config.ts`도 같은 `USER_CONFIG` 섹션을 제공합니다.

## React 컴포넌트 사용

React로 UI 오버레이를 구현하려면 외부 라이브러리 추가와 TypeScript 진입점 흐름에 React 의존성과 Vite 플러그인을 더합니다.

`BuildConfig~/package.json`:

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

`BuildConfig~/tsconfig.json`:

```json
{
  "compilerOptions": {
    "jsx": "react-jsx"
  },
  "include": ["src"]
}
```

`BuildConfig~/vite.config.ts`:

```typescript
//// USER_CONFIG_START ////
import react from '@vitejs/plugin-react';

const userConfig = defineConfig({
  plugins: [react()],
});
//// USER_CONFIG_END ////
```

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

`index.html`:

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.tsx"></script>
<!-- USER_BODY_END_END -->
```

## 빌드 결과물 구조

패키징이 끝나면 `ait-build/`에 다음 구조가 만들어집니다.

```text
ait-build/
├── index.html              ← Unity 플레이스홀더 치환 + USER_HEAD/USER_BODY_END 머지
├── public/
│   ├── Build/              ← Unity WebGL 빌드 파일
│   ├── TemplateData/       ← 스타일, 이미지
│   ├── Runtime/            ← 플랫폼 브릿지 (appsintoss-unity-bridge.js)
│   └── StreamingAssets/    ← StreamingAssets (있는 경우)
├── src/                    ← 사용자 TypeScript 코드 (있는 경우)
├── .env                    ← 사용자 환경 변수 (있는 경우)
├── package.json            ← SDK + 사용자 의존성 머지 (충돌 시 SDK 우선)
├── vite.config.ts          ← SDK 최신 버전 + USER_CONFIG 보존
├── granite.config.ts       ← 앱 메타데이터 플레이스홀더 치환 + USER_CONFIG 보존
├── apps-in-toss.config.ts  ← 3.x 설정 (SDK 템플릿에 있을 때만)
├── tsconfig.json           ← SDK 필수 옵션 + 사용자 옵션 머지
├── pnpm-workspace.yaml     ← 프로젝트 파일 우선, 없으면 SDK 파일
├── pnpm-lock.yaml          ← 프로젝트 lockfile (정합성 검증) 또는 SDK 폴백
└── dist/                   ← 최종 배포 패키지 (granite build 결과, QR 테스트 대상)
```

`node_modules`와 `pnpm-lock.yaml`은 재빌드 시에도 보존되어 빌드 속도를 높입니다.

## SDK 업데이트 시 동작

SDK를 업데이트해도 사용자 커스터마이징은 자동으로 보존됩니다.

| 상황 | 동작 |
|------|------|
| 마커가 있는 템플릿 | 사용자 영역 보존, SDK 영역만 업데이트 |
| 마커가 없는 이전 템플릿 | 전체 파일을 새 SDK 템플릿으로 교체 + 수동 마이그레이션 경고 |

마커가 없는 기존 `index.html`은 전체 교체되며 다음 경고가 출력됩니다. 백업해둔 이전 파일의 커스텀 부분을 새 템플릿의 마커 영역에 옮겨주세요.

```text
[AIT] 템플릿 업데이트: 이전 버전 템플릿을 새 마커 기반 템플릿으로 교체합니다.
[AIT] ⚠️ 기존 index.html에 커스텀 수정이 있었다면 수동으로 USER_* 마커 영역에 재적용하세요.
```

정상적으로 병합되면 다음과 같은 로그가 남습니다.

```text
[AIT] ✓ index.html 템플릿 업데이트 (사용자 커스텀 영역 보존)
[AIT]   ✓ vite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)
[AIT]   ✓ granite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)
```

## 튜토리얼

아래 두 튜토리얼(#1 canvas-confetti, #2 Firebase Analytics)은 E2E 테스트가 실제로 빌드하고 브라우저에서 실행해 검증합니다. 코드 블록은 테스트가 기대하는 형태 그대로이므로 먼저 그대로 따라 해보고 나서 바꾸는 편이 안전합니다.

### canvas-confetti로 화면 효과 추가

[canvas-confetti](https://github.com/catdad/canvas-confetti)를 번들링해 페이지 로드 시 색종이 효과를 띄우는 가장 단순한 예제입니다. 외부 라이브러리 추가 흐름 전체를 한 번에 익힐 수 있습니다.

**1. `BuildConfig~/package.json`에 의존성 추가**

```json
{
  "dependencies": {
    "canvas-confetti": "^1.9.3"
  },
  "devDependencies": {
    "@types/canvas-confetti": "^1.6.4"
  }
}
```

**2. `BuildConfig~/src/main.ts` 작성**

```ts
import confetti from 'canvas-confetti';

window.addEventListener('load', () => {
    confetti({ particleCount: 100, spread: 70, origin: { y: 0.6 } });
});
```

**3. `index.html`에서 진입점 참조**

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.ts"></script>
<!-- USER_BODY_END_END -->
```

**4. 빌드 후 확인**

빌드를 실행하고 결과물을 브라우저에서 열면 페이지 로드 직후 화면에 색종이가 터집니다. 콘솔에 `confetti is not defined`가 보이면 진입점 참조나 `package.json` 의존성 추가 단계를 다시 확인하세요.

### Firebase Analytics 연동

Firebase Web SDK([Modular SDK](https://firebase.google.com/docs/web/modular-upgrade))를 번들링해 앱 초기화와 Analytics를 연동합니다. API 키는 `.env`로 주입합니다 — 키를 코드에 박지 않아 저장소 커밋을 막고, 환경별로 다른 값을 쓸 수 있습니다.

**1. `BuildConfig~/package.json`에 의존성 추가**

```json
{
  "dependencies": {
    "firebase": "^10.7.0"
  }
}
```

**2. `Assets/WebGLTemplates/AITTemplate/BuildConfig~/.env` 작성**

```bash
VITE_FIREBASE_API_KEY=your-api-key
VITE_FIREBASE_PROJECT_ID=your-project-id
VITE_FIREBASE_APP_ID=your-app-id
VITE_FIREBASE_MEASUREMENT_ID=your-measurement-id
```

이 파일은 빌드 시 `ait-build/.env`로 자동 복사되어 Vite가 사용합니다.

> Vite는 `VITE_` 접두사가 붙은 환경 변수만 클라이언트 번들에 노출합니다. 다른 접두사를 쓰면 `import.meta.env`로 읽을 수 없습니다.
>
> **`.gitignore` 설정**: `.env`는 비밀 키를 포함하므로 다음 두 경로를 모두 ignore에 추가하세요. 팀에서 공유할 기본값은 `.env.example`에 두는 것이 일반적입니다.
>
> ```gitignore
> # 사용자가 작성하는 원본 (Unity 프로젝트)
> Assets/WebGLTemplates/AITTemplate/BuildConfig~/.env
>
> # 빌드 산출물 (이미 ait-build/ 전체가 ignore 되어 있다면 별도 추가 불필요)
> ait-build/.env
> ```

**3. `BuildConfig~/src/main.ts` 작성**

```ts
import { initializeApp } from 'firebase/app';
import { getAnalytics } from 'firebase/analytics';

const app = initializeApp({
    apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
    projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
    appId: import.meta.env.VITE_FIREBASE_APP_ID,
    measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID,
});
getAnalytics(app);
```

**4. `index.html`에서 진입점 참조**

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.ts"></script>
<!-- USER_BODY_END_END -->
```

**5. 빌드 후 확인**

브라우저 개발자 도구 콘솔에서 다음을 확인할 수 있습니다.

```js
> getApp().options.projectId
"your-project-id"
```

Firebase 콘솔의 Analytics > DebugView에서 실시간 이벤트 수신도 확인할 수 있습니다 (디버그 모드 활성화 필요 — [공식 문서](https://firebase.google.com/docs/analytics/debugview) 참조).

> **두 튜토리얼을 함께 적용하려면**: `package.json`에 두 의존성을 모두 추가하고, `main.ts`에 두 import 블록을 차례로 두면 됩니다. 진입점은 하나(`src/main.ts`)만 있으면 충분합니다.

## 관련 문서

- [빌드 파이프라인](BuildProcess.md) — 병합과 치환이 실제로 일어나는 지점
- [빌드 프로필](BuildProfiles.md) — Unity WebGL 빌드 설정
- [로딩 화면 커스터마이징](LoadingScreenCustomization.md) — 로딩 화면 교체
- [시작하기](GettingStarted.md) — 설치 및 기본 설정
- [문제 해결](Troubleshooting.md) — 빌드가 막혔을 때
