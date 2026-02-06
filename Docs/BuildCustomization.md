# 빌드 커스터마이징

이 문서는 Apps in Toss Unity SDK의 빌드 커스터마이징 방법을 설명합니다.

## 목차

- [빌드 파이프라인 개요](#빌드-파이프라인-개요)
- [1단계: Unity WebGL 빌드](#1단계-unity-webgl-빌드)
- [2단계: 패키징 커스터마이징](#2단계-패키징-커스터마이징)
- [커스터마이징 가능한 파일](#커스터마이징-가능한-파일)
- [index.html 커스터마이징](#indexhtml-커스터마이징)
- [npm 패키지 추가](#npm-패키지-추가)
- [Vite 플러그인 추가](#vite-플러그인-추가)
- [tsconfig.json 커스터마이징](#tsconfigjson-커스터마이징)
- [React/TypeScript 컴포넌트 사용](#reacttypescript-컴포넌트-사용)
- [빌드 결과물 구조](#빌드-결과물-구조)
- [SDK 업데이트 시 동작](#sdk-업데이트-시-동작)

---

## 빌드 파이프라인 개요

SDK는 **2단계 빌드 시스템**을 사용합니다:

```
Unity WebGL 빌드 (1단계)  →  Granite 패키징 (2단계)  →  최종 배포 패키지
       webgl/                     ait-build/                ait-build/dist/
```

| 단계 | 설명 | 커스터마이징 |
|------|------|-------------|
| **1단계** Unity WebGL 빌드 | Unity 프로젝트를 WebGL로 빌드 | [빌드 프로필](BuildProfiles.md)에서 설정 변경 |
| **2단계** Granite 패키징 | WebGL 출력을 Apps in Toss 배포 패키지로 변환 | 이 문서의 [커스터마이징 가능한 파일](#커스터마이징-가능한-파일) 참조 |

---

## 1단계: Unity WebGL 빌드

SDK는 빌드 시작 시 Unity PlayerSettings를 자동으로 구성합니다. 대부분의 설정은 별도 조작 없이 최적값이 적용됩니다.

### 자동 구성 항목

| 항목 | 기본값 | 비고 |
|------|--------|------|
| WebGL 템플릿 | `PROJECT:AITTemplate` | 자동 선택 |
| 압축 포맷 | 프로필에 따라 다름 | Dev Server: Disabled, 나머지: Brotli |
| 메모리 크기 | Unity 2022.3: 512MB / Unity 6: 1024MB | 자동 설정 |
| 스레딩 | 비활성화 | 모바일 브라우저 호환성 |
| 데이터 캐싱 | 비활성화 | |
| 링커 타겟 | Wasm | |
| Stripping Level | 프로필에 따라 다름 | Dev Server: Minimal, 나머지: High |
| Development Build | 프로필에 따라 다름 | Dev Server: 활성화, 나머지: 비활성화 |

> 각 프로필별 상세 설정 및 커스터마이징은 [빌드 프로필](BuildProfiles.md)을 참조하세요.

---

## 2단계: 패키징 커스터마이징

SDK는 WebGL 빌드의 다양한 측면을 커스터마이징할 수 있도록 지원합니다. 커스터마이징은 SDK 업데이트 시에도 자동으로 보존됩니다.

> **빌드 프로필**: 1단계 Unity WebGL 빌드의 상세 설정(압축, Stripping Level 등)은 [빌드 프로필](BuildProfiles.md)을 참조하세요.
>
> **로딩 화면 커스터마이징**: 로딩 화면의 디자인이나 동작을 변경하려면 [로딩 화면 커스터마이징 가이드](LoadingScreenCustomization.md)를 참조하세요.
>
> **메트릭 확인**: Debug Console의 Metric Explorer에서 로딩, 웹, Unity 메트릭을 확인할 수 있습니다. 자세한 내용은 [메트릭 문서](Metrics.md)를 참조하세요.

---

## 커스터마이징 가능한 파일

| 파일 | 위치 | 커스터마이징 방법 |
|------|------|------------------|
| `index.html` | `Assets/WebGLTemplates/AITTemplate/` | 마커 영역에 스크립트/스타일 추가 |
| `vite.config.ts` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | `USER_CONFIG` 섹션에 플러그인 추가 |
| `granite.config.ts` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | `USER_CONFIG` 섹션에 설정 추가 |
| `package.json` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | dependencies에 npm 패키지 추가 |
| `tsconfig.json` | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | compilerOptions 커스터마이징 (jsx, paths 등) |
| `src/` 폴더 | `Assets/WebGLTemplates/AITTemplate/BuildConfig~/` | TypeScript/React 컴포넌트 구조화 |

---

## index.html 커스터마이징

`index.html`에서 `USER_HEAD`와 `USER_BODY_END` 마커 영역에 커스텀 스크립트나 스타일을 추가할 수 있습니다.

### HEAD 영역 추가

```html
<!-- USER_HEAD_START - 이 영역에 사용자 커스텀 스크립트/스타일을 추가하세요 -->
<script src="https://www.gstatic.com/firebasejs/10.7.0/firebase-app-compat.js"></script>
<script src="https://www.gstatic.com/firebasejs/10.7.0/firebase-analytics-compat.js"></script>
<link rel="stylesheet" href="custom-styles.css">
<!-- USER_HEAD_END -->
```

### BODY 끝 영역 추가

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

---

## npm 패키지 추가

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

---

## Vite 플러그인 추가

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

---

## tsconfig.json 커스터마이징

`BuildConfig~/tsconfig.json`을 생성하여 TypeScript 컴파일러 옵션을 커스터마이징할 수 있습니다. 프로젝트 옵션과 SDK 필수 옵션이 자동으로 머지됩니다:

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

---

## React/TypeScript 컴포넌트 사용

`BuildConfig~/` 폴더에 `src/` 등 하위 폴더 구조를 생성하여 TypeScript/React 컴포넌트를 구조화할 수 있습니다.

### 폴더 구조 예시

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

### 1. package.json에 React 의존성 추가

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

### 2. tsconfig.json 생성

```json
{
  "compilerOptions": {
    "jsx": "react-jsx"
  },
  "include": ["src"]
}
```

### 3. vite.config.ts에 플러그인 추가

```typescript
//// USER_CONFIG_START ////
import react from '@vitejs/plugin-react';

const userConfig = defineConfig({
  plugins: [react()],
});
//// USER_CONFIG_END ////
```

### 4. index.html에서 진입점 참조

```html
<!-- USER_BODY_END_START -->
<script type="module" src="./src/main.tsx"></script>
<!-- USER_BODY_END_END -->
```

### 5. React 컴포넌트 작성

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

## 다음 단계

- [로딩 화면 커스터마이징](LoadingScreenCustomization.md) - 로딩 UI 변경
- [메트릭](Metrics.md) - 성능 메트릭 확인
- [시작하기](GettingStarted.md) - 설치 및 기본 설정
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
