# 로딩 화면 커스터마이징

Unity WebGL이 로드되는 동안 보이는 화면을 원하는 대로 바꾸는 방법을 설명합니다.

## 로딩 화면 파일

로딩 화면은 두 곳에 존재합니다.

| 경로 | 역할 |
|------|------|
| `WebGLTemplates/AITTemplate/loading.html` | SDK 기본 템플릿 (원본) |
| `Assets/AppsInToss/loading.html` | 프로젝트별 커스텀 로딩 화면 |

`AITPackageInitializer`가 `[InitializeOnLoad]`로 에디터 시작 시 실행되어, `Assets/AppsInToss/loading.html`이 없으면 SDK 템플릿을 복사합니다. 이 파일을 수정하면 커스텀 로딩 화면이 적용됩니다.

SDK 템플릿 검색 순서:

1. `Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/loading.html`
2. `Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/loading.html`
3. Assembly 경로 기반

### 빌드 시 삽입 순서

빌드의 `CopyWebGLToPublic()` 단계에서 `index.html`의 `%AIT_LOADING_SCREEN%` 플레이스홀더가 로딩 화면 전체 내용으로 치환됩니다.

```text
1. Assets/AppsInToss/loading.html 존재?
   → Yes: 프로젝트 커스텀 로딩 화면 사용
   → No: SDK 기본 템플릿 폴백

2. SDK 템플릿도 없으면?
   → Debug.LogWarning("로딩 화면 파일을 찾을 수 없습니다. 빈 로딩 화면이 사용됩니다.")
   → 빈 문자열로 치환
```

즉 로딩 화면은 **빌드 시점에 index.html 안으로 인라인**됩니다. 별도 파일로 로드되지 않으므로 상대 경로 참조는 최종 `index.html` 기준으로 해석됩니다.

### 기본 템플릿으로 되돌리기

`AIT > Reset Loading Screen`을 실행하면 확인 다이얼로그 후 SDK 템플릿을 `Assets/AppsInToss/loading.html`로 다시 복사합니다. 커스텀 내용은 사라지므로 필요하면 먼저 백업하세요.

### 파일 구조

```text
Assets/
└── AppsInToss/
    ├── Editor/
    │   └── AITConfig.asset
    └── loading.html    ← 커스텀 로딩 화면 (있으면 자동 적용)
```

## 앱 정보

로딩 화면에 표시되는 앱 정보는 다음 순서로 결정됩니다.

1. **네이티브 앱 환경** (toss 앱 내) — SDK가 `getAppsInTossGlobals`로 앱 정보를 가져와 덮어씁니다
2. **폴백** (웹 브라우저 등) — AIT Configuration에서 설정한 값이 쓰입니다

| 설정 | 설명 |
|------|------|
| 앱 이름 (`displayName`) | 로딩 화면에 표시되는 앱 이름 |
| 앱 아이콘 (`iconUrl`) | 로딩 화면에 표시되는 앱 아이콘 URL |
| 기본 색상 (`primaryColor`) | 진행률 바 색상 |

> **참고**: 실제 toss 앱 환경에서는 네이티브 값이 우선하므로 위 설정은 주로 개발·테스트 환경에서 보입니다.

## 커스터마이징 가능 범위

`loading.html`의 HTML, CSS, JavaScript를 자유롭게 수정할 수 있습니다. 진행률은 `AITLoading` API로 받아서 원하는 방식으로 표현하면 됩니다.

- **UI 디자인** — 프로그레스 바, 파이 차트, 원형 로딩 등
- **애니메이션** — CSS 애니메이션, JavaScript 애니메이션, GIF, Lottie
- **브랜드 요소** — 마스코트 캐릭터, 로고 애니메이션
- **인터랙티브 요소** — 미니 게임, 팁 슬라이더

### 외부 리소스 사용

**StreamingAssets** (권장) — `Assets/StreamingAssets`에 두면 빌드에 자동 포함됩니다.

```html
<img src="StreamingAssets/loading-character.gif" />
<link rel="stylesheet" href="StreamingAssets/loading-fonts.css" />
```

```text
Assets/
└── StreamingAssets/
    ├── loading-character.gif
    └── loading-fonts.css
```

**Data URI** — 수 KB 이하의 작은 이미지는 Base64로 인라인합니다.

```html
<img src="data:image/png;base64,iVBORw0KGgo..." />
```

**CDN** — 외부 URL로 로드합니다. 네트워크 의존성이 생기고, 로딩 화면 자체가 늦게 뜰 수 있습니다.

```html
<img src="https://your-cdn.com/loading-character.gif" />
```

## AITLoading API

`window.AITLoading`은 `index.html`에서 정의되며 다음 여섯 가지가 공개 표면의 전부입니다. `_`로 시작하는 멤버는 내부 구현이므로 의존하지 마세요.

| 멤버 | 설명 |
|------|------|
| `appInfo` | `{ iconUrl, displayName, primaryColor }` |
| `onReady(callback)` | 앱 정보 준비 완료 |
| `onProgress(callback)` | 진행률 업데이트 |
| `onComplete(callback)` | 로딩 완료 |
| `onError(callback)` | 에러 발생 |
| `hide()` | 로딩 화면 숨김 |

### appInfo

```javascript
console.log(AITLoading.appInfo.iconUrl);       // 앱 아이콘 URL
console.log(AITLoading.appInfo.displayName);   // 앱 표시 이름
console.log(AITLoading.appInfo.primaryColor);  // 기본 색상
```

초기값은 빌드 시 치환된 Configuration 값이고, 네이티브 앱 정보가 도착하면 그 값으로 갱신됩니다.

### onReady

앱 정보가 준비되면 호출됩니다. UI 초기화에 사용하세요.

```javascript
AITLoading.onReady(function(appInfo) {
    document.getElementById('app-icon').src = appInfo.iconUrl;
    document.getElementById('app-name').textContent = appInfo.displayName;
});
```

> **중요**: `onReady` 콜백은 **한 번만 호출된다고 가정하면 안 됩니다.** 초기화 시점에 한 번 호출되고, 네이티브 앱 정보가 나중에 도착하면 갱신된 `appInfo`로 다시 호출됩니다. 콜백은 몇 번 실행돼도 안전하도록(idempotent) 작성하세요. 이미 초기화가 끝난 뒤에 등록하면 즉시 한 번 호출됩니다.

### onProgress

`0.0`부터 `1.0` 사이의 진행률을 받습니다.

```javascript
AITLoading.onProgress(function(progress) {
    console.log('로딩 진행률:', Math.round(progress * 100) + '%');
});
```

### onComplete

로딩이 끝나면 호출됩니다. 이미 완료된 뒤에 등록하면 즉시 호출됩니다.

```javascript
AITLoading.onComplete(function() {
    AITLoading.hide();
});
```

### onError

`{ message }` 형태의 객체를 받습니다.

```javascript
AITLoading.onError(function(error) {
    console.error('로딩 실패:', error.message);
});
```

> **참고**: WebGL 컨텍스트 생성 실패(`GLctx`, `WebGL context`, `Unable to create` 계열)는 SDK가 전용 경로로 처리하므로 이 콜백에 오지 않습니다. 기기가 WebGL을 못 여는 경우까지 직접 다루려는 것이 아니라면 신경 쓰지 않아도 됩니다.

### hide

`#ait-loading-wrapper` 요소를 `display: none`으로 숨깁니다.

```javascript
AITLoading.hide();
```

## 예제

아래는 직접 작성한 예제입니다. SDK가 실제로 제공하는 기본 템플릿(다크 테마)은 `Assets/AppsInToss/loading.html`에서 확인하세요.

### 진행률 바

```html
<style>
    /* ===== 커스터마이징 가능한 CSS 변수 ===== */
    :root {
        --loading-bg: #ffffff;
        --title-color: #191f28;
        --app-name-color: #333d4b;
        --progress-bg: #e5e8eb;
        --icon-size: 30px;
        --progress-height: 5px;
    }

    .loading-container {
        position: fixed;
        inset: 0;
        background: var(--loading-bg);
        display: flex;
        flex-direction: column;
        padding: 120px 20px 0;
        font-family: -apple-system, BlinkMacSystemFont, sans-serif;
    }

    .loading-title {
        font-size: 22px;
        font-weight: 600;
        color: var(--title-color);
        line-height: 1.4;
        margin-bottom: 44px;
    }

    .loading-card {
        padding: 16px;
        border: 1px solid #e5e8eb;
        border-radius: 16px;
    }

    .loading-header {
        display: flex;
        align-items: center;
        margin-bottom: 12px;
    }

    .loading-icon {
        width: var(--icon-size);
        height: var(--icon-size);
        border-radius: 8px;
        background: rgba(2, 32, 71, 0.05);
        overflow: hidden;
    }

    .loading-icon img { width: 100%; height: 100%; object-fit: cover; }

    .loading-app-name {
        margin-left: 12px;
        font-size: 15px;
        font-weight: 500;
        color: var(--app-name-color);
    }

    .loading-progress {
        height: var(--progress-height);
        background: var(--progress-bg);
        border-radius: 2.5px;
        overflow: hidden;
    }

    .loading-progress-bar {
        height: 100%;
        width: 0%;
        transition: width 0.3s ease;
    }
</style>

<div class="loading-container" id="ait-loading">
    <div class="loading-title" id="loading-title"></div>
    <div class="loading-card">
        <div class="loading-header">
            <div class="loading-icon"><img id="app-icon" src="" alt="" /></div>
            <div class="loading-app-name" id="app-name"></div>
        </div>
        <div class="loading-progress">
            <div class="loading-progress-bar" id="progress-bar"></div>
        </div>
    </div>
</div>

<script>
(function() {
    // 앱 정보로 UI 초기화 (네이티브 정보 도착 시 다시 호출될 수 있음)
    AITLoading.onReady(function(appInfo) {
        document.getElementById('app-icon').src = appInfo.iconUrl || '';
        document.getElementById('app-name').textContent = appInfo.displayName || '';
        document.getElementById('progress-bar').style.background =
            appInfo.primaryColor || '#3182f6';
    });

    // 진행률 업데이트
    AITLoading.onProgress(function(progress) {
        document.getElementById('progress-bar').style.width = (progress * 100) + '%';
    });

    // 로딩 완료 시 화면 숨기기
    AITLoading.onComplete(function() {
        AITLoading.hide();
    });
})();
</script>
```

### 퍼센트 표시와 에러 처리

```html
<style>
    :root {
        --loading-bg: #ffffff;
        --text-color: #191f28;
        --sub-text-color: #6b7684;
    }

    .loading-container {
        position: fixed;
        inset: 0;
        background: var(--loading-bg);
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        font-family: -apple-system, BlinkMacSystemFont, sans-serif;
    }

    .loading-icon { width: 80px; height: 80px; border-radius: 20px; margin-bottom: 24px; }
    .loading-name { font-size: 18px; font-weight: 600; color: var(--text-color); }
    .loading-progress { width: 200px; height: 6px; background: #e5e8eb; border-radius: 3px; margin-top: 24px; overflow: hidden; }
    .loading-progress-bar { height: 100%; width: 0%; transition: width 0.3s ease; }
    .loading-percent { margin-top: 12px; font-size: 14px; color: var(--sub-text-color); }
</style>

<div class="loading-container" id="ait-loading">
    <img class="loading-icon" id="app-icon" alt="" />
    <div class="loading-name" id="app-name"></div>
    <div class="loading-progress"><div class="loading-progress-bar" id="progress-bar"></div></div>
    <div class="loading-percent" id="percent-text">0%</div>
</div>

<script>
(function() {
    AITLoading.onReady(function(appInfo) {
        document.getElementById('app-icon').src = appInfo.iconUrl || '';
        document.getElementById('app-name').textContent = appInfo.displayName || '';
        document.getElementById('progress-bar').style.background = appInfo.primaryColor || '#3182f6';
    });

    AITLoading.onProgress(function(progress) {
        var percent = Math.round(progress * 100);
        document.getElementById('progress-bar').style.width = percent + '%';
        document.getElementById('percent-text').textContent = percent + '%';
    });

    AITLoading.onComplete(function() {
        AITLoading.hide();
    });

    AITLoading.onError(function(error) {
        document.getElementById('percent-text').textContent = '로딩 실패';
        document.getElementById('percent-text').style.color = '#f04452';
    });
})();
</script>
```

## 트러블슈팅

### 아이콘이 표시되지 않음

1. AIT Configuration에서 아이콘 URL이 설정되어 있는지 확인합니다
2. CORS 정책으로 외부 이미지가 차단될 수 있습니다 — 같은 도메인의 이미지를 권장합니다
3. 네이티브 앱 환경에서는 앱 아이콘이 자동으로 로드되므로 폴백 값이 보이지 않을 수 있습니다

### 커스텀 로딩 화면이 적용되지 않음

1. 파일이 `Assets/AppsInToss/loading.html`에 있는지 확인합니다 — 다른 경로는 인식되지 않습니다
2. 로딩 화면은 빌드 시점에 인라인되므로 파일만 고치고 다시 빌드하지 않으면 반영되지 않습니다

### 진행률이 업데이트되지 않음

1. `AITLoading.onProgress()`가 등록되어 있는지 확인합니다
2. 콜백은 페이지 로드 초기에 등록해야 합니다 — 로딩이 이미 진행된 뒤 등록하면 그 이전 진행률은 받지 못합니다

### appInfo가 비어 있음

`AITLoading.appInfo`를 직접 읽는 대신 `onReady` 콜백 안에서 사용하세요. 스크립트 실행 시점에 앱 정보 초기화가 아직 끝나지 않았을 수 있습니다.

## 관련 문서

- [빌드 파이프라인](BuildProcess.md) — `%AIT_LOADING_SCREEN%` 치환이 일어나는 지점
- [빌드 커스터마이징](BuildCustomization.md) — 로딩 화면 밖의 웹 진입점 수정
- [시작하기](GettingStarted.md) — 앱 정보 설정
- [문제 해결](Troubleshooting.md) — 빌드·런타임 전반
