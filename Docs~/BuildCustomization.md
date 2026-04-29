# 빌드 커스터마이징

이 문서는 Apps in Toss Unity SDK의 빌드 커스터마이징 방법을 설명합니다.

## 목차

- [빌드 파이프라인 개요](#빌드-파이프라인-개요)
- [커스터마이징 가능한 파일](#커스터마이징-가능한-파일)
- [index.html 커스터마이징](#indexhtml-커스터마이징)
- [TypeScript 진입점](#typescript-진입점)
- [외부 라이브러리 추가](#외부-라이브러리-추가)
- [Vite 설정 커스터마이징](#vite-설정-커스터마이징)
- [React 컴포넌트 사용](#react-컴포넌트-사용)
- [빌드 결과물 구조](#빌드-결과물-구조)
- [SDK 업데이트 시 동작](#sdk-업데이트-시-동작)
- [튜토리얼 #1: canvas-confetti로 화면 효과 추가](#튜토리얼-1-canvas-confetti로-화면-효과-추가)
- [튜토리얼 #2: Firebase Analytics 연동](#튜토리얼-2-firebase-analytics-연동)

---

## 빌드 파이프라인 개요

SDK는 **2단계 빌드 시스템**을 사용합니다:

```
Unity WebGL 빌드 (1단계)  →  Granite 패키징 (2단계)  →  최종 배포 패키지
       webgl/                     ait-build/                ait-build/dist/
```

| 단계 | 설명 | 커스터마이징 |
|------|------|-------------|
| **1단계** Unity WebGL 빌드 | Unity 프로젝트를 WebGL로 빌드 | [빌드 프로필](BuildProfiles.md)에서 압축 포맷, Stripping Level 등 설정 |
| **2단계** Granite 패키징 | WebGL 출력을 Apps in Toss 배포 패키지로 변환 | 이 문서에서 다룸 |

> **빌드 프로필**: 1단계 Unity WebGL 빌드의 상세 설정(압축 포맷, Stripping Level, Development Build 등)은 [빌드 프로필](BuildProfiles.md)을 참조하세요.
>
> **로딩 화면**: 로딩 화면의 디자인이나 동작 변경은 [로딩 화면 커스터마이징](LoadingScreenCustomization.md)을 참조하세요.

---

## 커스터마이징 가능한 파일

2단계 패키징은 다음 파일들을 머지 가능한 형태로 노출합니다. 모든 파일은 `Assets/WebGLTemplates/AITTemplate/` 하위에 있습니다.

| 파일 | 역할 | 머지 방식 |
|------|------|----------|
| `index.html` | HTML 엔트리 포인트 | `USER_HEAD` / `USER_BODY_END` 마커 영역 보존 |
| `BuildConfig~/package.json` | npm 의존성 | SDK + 사용자 dependencies 자동 머지 |
| `BuildConfig~/vite.config.ts` | Vite 빌드 설정 | `USER_CONFIG` 섹션 보존 |
| `BuildConfig~/granite.config.ts` | Granite 패키징 설정 | `USER_CONFIG` 섹션 보존 |
| `BuildConfig~/tsconfig.json` | TypeScript 컴파일러 설정 | SDK 필수 옵션 + 사용자 옵션 머지 |
| `BuildConfig~/src/` | TypeScript 진입점 및 모듈 | 폴더 전체 보존 |

이 파일들에 대한 사용자 변경은 SDK 업데이트 시에도 자동으로 보존됩니다. 자세한 동작은 [SDK 업데이트 시 동작](#sdk-업데이트-시-동작)을 참조하세요.

---

## index.html 커스터마이징

`index.html`은 두 개의 마커 영역을 제공합니다.

### USER_HEAD

`<head>` 안에 포함되는 영역입니다. 메타 태그, 폰트, preload 힌트, 외부 스타일시트 같은 정적 리소스 선언에 사용합니다.

```html
<!-- USER_HEAD_START - 이 영역에 사용자 커스텀 스크립트/스타일을 추가하세요 -->
<meta name="theme-color" content="#3182f6">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Sans+KR&display=swap">
<!-- USER_HEAD_END -->
```

### USER_BODY_END

`</body>` 직전에 포함되는 영역입니다. 사용자 코드의 진입점을 참조하는 데 사용합니다. 권장 패턴은 [TypeScript 진입점](#typescript-진입점)을 모듈로 로드하는 것입니다.

```html
<!-- USER_BODY_END_START - 이 영역에 사용자 커스텀 스크립트를 추가하세요 -->
<script type="module" src="./src/main.ts"></script>
<!-- USER_BODY_END_END -->
```

이렇게 하면 진입점 파일에 작성한 모든 import가 Vite의 트리 셰이킹·압축을 거쳐 번들로 묶여 로드됩니다.

---

## TypeScript 진입점

사용자 코드는 `BuildConfig~/src/main.ts`를 진입점으로 작성합니다. Vite가 이 파일을 빌드 시 번들링하므로 npm 패키지 import, 트리 셰이킹, 타입 검사가 모두 자연스럽게 적용됩니다.

### 폴더 구조

```
Assets/WebGLTemplates/AITTemplate/
├── index.html                    ← USER_BODY_END에서 main.ts 참조
└── BuildConfig~/
    ├── package.json              ← 의존성
    ├── tsconfig.json             ← TypeScript 옵션 (선택)
    └── src/
        └── main.ts               ← 진입점
```

### 진입점 작성

`BuildConfig~/src/main.ts`:

```ts
window.addEventListener('load', () => {
    console.log('User entry loaded');
});
```

### index.html에서 참조

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.ts"></script>
<!-- USER_BODY_END_END -->
```

### tsconfig.json 커스터마이징 (선택)

`BuildConfig~/tsconfig.json`을 두면 TypeScript 컴파일러 옵션을 커스터마이징할 수 있습니다. SDK 필수 옵션(`moduleResolution`, `esModuleInterop`)은 SDK 값으로 강제 적용됩니다.

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

---

## 외부 라이브러리 추가

외부 라이브러리는 **npm 패키지로 설치하고 진입점에서 import하는 방식**을 권장합니다. 버전이 고정되어 빌드 재현성이 보장되고, CDN 장애나 네트워크 차단 환경의 영향을 받지 않으며, 트리 셰이킹·압축이 적용됩니다.

### 권장 방식: npm 패키지로 번들링

다음 예시는 [Firebase Web SDK](https://firebase.google.com/docs/web/setup)를 추가하는 절차입니다. 다른 라이브러리도 동일한 흐름(`package.json` 의존성 추가 → `main.ts`에서 import → `index.html`에서 진입점 참조)을 따릅니다.

**1. `BuildConfig~/package.json`에 의존성 추가**

```json
{
  "dependencies": {
    "firebase": "^10.7.0"
  }
}
```

빌드 시 SDK 패키지와 사용자 패키지가 자동으로 머지됩니다.

**2. `BuildConfig~/src/main.ts`에서 import 및 초기화**

```ts
import { initializeApp } from 'firebase/app';
import { getAnalytics } from 'firebase/analytics';

const app = initializeApp({
    apiKey: 'your-api-key',
    projectId: 'your-project-id',
    appId: 'your-app-id',
    measurementId: 'your-measurement-id',
});
getAnalytics(app);
```

> Firebase는 v9부터 [Modular SDK](https://firebase.google.com/docs/web/modular-upgrade)를 공식 권장합니다. 위 예시처럼 함수 단위로 import하면 사용하지 않는 기능이 번들에서 제거됩니다.

**3. `index.html`에서 진입점 참조**

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.ts"></script>
<!-- USER_BODY_END_END -->
```

### 대안: CDN 직접 로드

빌드 도구 없이 빠르게 시험만 하고 싶을 때는 `USER_HEAD`에 `<script src="...">`로 외부 라이브러리를 직접 로드할 수도 있습니다. 다만 다음 단점이 있어 일상적인 사용에는 권장하지 않습니다:

- CDN 장애나 네트워크 차단 환경에서 앱 로드가 실패할 수 있음
- 버전이 URL에 박혀 빌드 재현성이 떨어짐
- 트리 셰이킹·압축이 적용되지 않아 번들이 커짐
- TypeScript 타입 검사를 받지 못함

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

---

## Vite 설정 커스터마이징

`BuildConfig~/vite.config.ts`의 `USER_CONFIG` 섹션에서 Vite 플러그인이나 빌드 옵션을 추가할 수 있습니다.

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

`granite.config.ts`도 동일한 `USER_CONFIG` 섹션을 제공합니다.

---

## React 컴포넌트 사용

React로 UI 오버레이를 구현하려면 [외부 라이브러리 추가](#외부-라이브러리-추가)와 [TypeScript 진입점](#typescript-진입점) 흐름에 React 의존성과 Vite 플러그인을 더하면 됩니다.

**1. `BuildConfig~/package.json`에 React 추가**

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

**2. `BuildConfig~/tsconfig.json`에 jsx 옵션 추가**

```json
{
  "compilerOptions": {
    "jsx": "react-jsx"
  },
  "include": ["src"]
}
```

**3. `BuildConfig~/vite.config.ts`에 플러그인 추가**

```typescript
//// USER_CONFIG_START ////
import react from '@vitejs/plugin-react';

const userConfig = defineConfig({
  plugins: [react()],
});
//// USER_CONFIG_END ////
```

**4. 진입점을 `main.tsx`로 작성**

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

**5. `index.html`에서 진입점 참조**

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.tsx"></script>
<!-- USER_BODY_END_END -->
```

---

## 빌드 결과물 구조

2단계 패키징이 완료되면 `ait-build/` 디렉토리에 다음과 같은 구조가 생성됩니다:

```
ait-build/
├── index.html              ← Unity 플레이스홀더 치환 완료
├── public/
│   ├── Build/              ← Unity WebGL 빌드 파일
│   ├── TemplateData/       ← 스타일, 이미지
│   ├── Runtime/            ← 플랫폼 브릿지 (appsintoss-unity-bridge.js)
│   └── StreamingAssets/    ← StreamingAssets (있는 경우)
├── src/                    ← 사용자 TypeScript 코드 (있는 경우)
├── package.json            ← SDK + 사용자 의존성 머지
├── vite.config.ts          ← SDK + 사용자 설정 머지
├── granite.config.ts       ← 앱 메타데이터 플레이스홀더 치환 완료
├── tsconfig.json           ← SDK + 사용자 옵션 머지
└── dist/                   ← 최종 배포 패키지 (granite build 결과)
```

> **참고**: `node_modules`, `package-lock.json` 등은 재빌드 시에도 보존되어 빌드 속도가 향상됩니다.

---

## SDK 업데이트 시 동작

SDK를 업데이트해도 사용자 커스터마이징은 자동으로 보존됩니다:

| 상황 | 동작 |
|------|------|
| 마커가 있는 템플릿 | 사용자 영역 보존, SDK 영역만 업데이트 |
| 마커가 없는 이전 템플릿 | 새 템플릿으로 교체 + 수동 마이그레이션 안내 |

### 업데이트 로그

업데이트 시 Unity Console에서 다음과 같은 로그를 확인할 수 있습니다:

```
[AIT] ✓ index.html 템플릿 업데이트 (사용자 커스텀 영역 보존)
[AIT]   ✓ vite.config.ts (마커 기반 업데이트)
[AIT]   ✓ granite.config.ts (마커 기반 업데이트)
```

---

## 튜토리얼 #1: canvas-confetti로 화면 효과 추가

[canvas-confetti](https://github.com/catdad/canvas-confetti)를 번들링해 페이지 로드 시 색종이 효과를 띄우는 가장 단순한 예제입니다. 이 튜토리얼을 끝까지 따라가면 SDK의 외부 라이브러리 추가 흐름 전체를 한 번에 익힐 수 있습니다.

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

---

## 튜토리얼 #2: Firebase Analytics 연동

Firebase Web SDK([Modular SDK](https://firebase.google.com/docs/web/modular-upgrade))를 번들링해 앱 초기화와 Analytics를 연동합니다. API 키는 `.env` 파일을 통해 환경 변수로 주입합니다 — 키를 코드에 직접 박지 않아 저장소에 커밋되는 것을 방지하고, 개발/스테이징/프로덕션 환경별로 다른 값을 쓸 수 있습니다.

**1. `BuildConfig~/package.json`에 의존성 추가**

```json
{
  "dependencies": {
    "firebase": "^10.7.0"
  }
}
```

**2. `BuildConfig~/.env` 작성**

```bash
VITE_FIREBASE_API_KEY=your-api-key
VITE_FIREBASE_PROJECT_ID=your-project-id
VITE_FIREBASE_APP_ID=your-app-id
VITE_FIREBASE_MEASUREMENT_ID=your-measurement-id
```

> Vite는 `VITE_` 접두사가 붙은 환경 변수만 클라이언트 번들에 노출합니다. 다른 접두사를 쓰면 `import.meta.env`로 읽을 수 없습니다.
>
> `.env` 파일은 비밀 키를 포함하므로 `.gitignore`에 추가해 저장소에 커밋되지 않도록 하세요. 팀에서 공유할 기본값은 `.env.example`에 두는 것이 일반적입니다.

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

빌드 결과물을 브라우저에서 열고 개발자 도구 콘솔에서 다음을 확인할 수 있습니다:

```js
> getApp().options.projectId
"your-project-id"
```

Firebase 콘솔의 Analytics > DebugView에서 실시간 이벤트 수신 여부도 확인할 수 있습니다 (디버그 모드 활성화 필요 — [공식 문서](https://firebase.google.com/docs/analytics/debugview) 참조).

> **두 튜토리얼을 함께 적용하려면**: `package.json`에 두 의존성을 모두 추가하고, `main.ts`에 두 import 블록을 차례로 두면 됩니다. 진입점은 하나(`src/main.ts`)만 있으면 충분합니다.

---

## 다음 단계

- [로딩 화면 커스터마이징](LoadingScreenCustomization.md) - 로딩 UI 변경
- [메트릭](Metrics.md) - 성능 메트릭 확인
- [시작하기](GettingStarted.md) - 설치 및 기본 설정
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
