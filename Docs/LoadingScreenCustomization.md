# 로딩 화면 커스터마이징 가이드

Apps in Toss Unity SDK는 WebGL 로딩 화면의 다양한 커스터마이징 옵션을 제공합니다.

## 목차

- [로딩 화면 설정](#로딩-화면-설정)
- [전면 커스터마이징 (커스텀 HTML)](#전면-커스터마이징-커스텀-html)
- [AITLoading JavaScript API](#aitloading-javascript-api)
- [예제](#예제)

---

## 로딩 화면 설정

### 앱 정보 우선순위

로딩 화면에 표시되는 앱 정보는 다음 순서로 결정됩니다:

1. **네이티브 앱 환경** (토스 앱 내): SDK가 네이티브 API (`getAppsInTossGlobals`)를 통해 자동으로 앱 정보를 가져옵니다.
2. **폴백** (웹 브라우저 등): AIT Configuration에서 설정한 값이 사용됩니다.

### 폴백 설정 항목

| 설정 | 설명 |
|------|------|
| 앱 이름 (`displayName`) | 로딩 화면에 표시되는 앱 이름 |
| 앱 아이콘 (`iconUrl`) | 로딩 화면에 표시되는 앱 아이콘 URL |
| 기본 색상 (`primaryColor`) | 진행률 바 색상 |

> **참고**: 실제 토스 앱 환경에서는 네이티브 API가 우선 적용되므로, 위 설정은 개발/테스트 환경에서 주로 사용됩니다.

---

## 커스텀 로딩 화면

로딩 화면 전체를 직접 디자인할 수 있습니다.

### 설정 방법

1. SDK 설치 후 Unity Editor 실행 시 `Assets/AppsInToss/loading.html` 자동 생성
2. `Assets/AppsInToss/loading.html` 파일을 편집

> **참고**: SDK가 로드될 때 기본 템플릿이 `Assets/AppsInToss/loading.html`에 자동 생성됩니다. 이 파일을 수정하면 커스텀 로딩 화면이 적용됩니다.

### 커스터마이징 가능 범위

`loading.html`의 HTML, CSS, JavaScript를 자유롭게 수정하여 완전히 커스텀한 로딩 UI를 만들 수 있습니다:

- **다양한 UI 디자인**: 프로그레스 바, 파이 차트, 원형 로딩 등
- **애니메이션**: CSS 애니메이션, JavaScript 애니메이션, GIF/Lottie 등
- **캐릭터/브랜드 요소**: 마스코트 캐릭터, 브랜드 로고 애니메이션 등
- **인터랙티브 요소**: 미니 게임, 팁 슬라이더 등

SDK가 제공하는 `AITLoading` API를 통해 로딩 진행률을 받아 원하는 방식으로 표현하면 됩니다.

### 외부 리소스 사용

이미지, 폰트, 애니메이션 파일 등 외부 리소스를 사용하려면:

1. **StreamingAssets 사용** (권장): Unity의 `Assets/StreamingAssets` 폴더에 파일을 추가하면 빌드에 자동 포함됩니다.
   ```html
   <img src="StreamingAssets/loading-character.gif" />
   <link rel="stylesheet" href="StreamingAssets/loading-fonts.css" />
   ```

   파일 구조:
   ```
   Assets/
   └── StreamingAssets/
       ├── loading-character.gif
       └── loading-fonts.css
   ```

2. **Data URI 사용**: 작은 이미지(수 KB 이하)는 Base64로 인라인 포함
   ```html
   <img src="data:image/png;base64,iVBORw0KGgo..." />
   ```

3. **CDN 사용**: 외부 URL로 리소스 로드 (네트워크 의존성 발생)
   ```html
   <img src="https://your-cdn.com/loading-character.gif" />
   ```

### 파일 구조

```
Assets/
└── AppsInToss/
    ├── Editor/
    │   └── AITConfig.asset
    └── loading.html    ← 커스텀 로딩 화면 (있으면 자동 적용)
```

---

## AITLoading JavaScript API

커스텀 로딩 화면에서 SDK가 제공하는 JavaScript API를 사용하여 로딩 상태를 제어할 수 있습니다.

### 앱 정보 API

#### `AITLoading.appInfo`

AIT Configuration에서 설정한 앱 정보입니다.

```javascript
console.log(AITLoading.appInfo.iconUrl);       // 앱 아이콘 URL
console.log(AITLoading.appInfo.displayName);   // 앱 표시 이름
console.log(AITLoading.appInfo.primaryColor);  // 기본 색상
```

### 콜백 API

#### `AITLoading.onReady(callback)`

앱 정보(`appInfo`)가 준비되면 호출됩니다. UI 초기화에 사용합니다.

```javascript
AITLoading.onReady(function(appInfo) {
    document.getElementById('app-icon').src = appInfo.iconUrl;
    document.getElementById('app-name').textContent = appInfo.displayName;
});
```

#### `AITLoading.onProgress(callback)`

진행률 업데이트 시 호출됩니다.

```javascript
AITLoading.onProgress(function(progress) {
    // progress: 0.0 ~ 1.0
    console.log('로딩 진행률:', Math.round(progress * 100) + '%');
});
```

#### `AITLoading.onComplete(callback)`

로딩 완료 시 호출됩니다.

```javascript
AITLoading.onComplete(function() {
    console.log('로딩 완료!');
    AITLoading.hide();  // 로딩 화면 숨기기
});
```

#### `AITLoading.onError(callback)`

로딩 에러 발생 시 호출됩니다.

```javascript
AITLoading.onError(function(error) {
    console.error('로딩 실패:', error.message);
});
```

#### `AITLoading.onFileProgress(callback)`

파일별 다운로드 진행 상황을 알려줍니다.

```javascript
AITLoading.onFileProgress(function(fileInfo) {
    // fileInfo: { name, url, loaded, total, percent, speed }
    console.log(fileInfo.name + ': ' + fileInfo.percent + '%');
});
```

### 통계 API

#### `AITLoading.getFileStats()`

로딩 완료 후 파일별 다운로드 정보를 반환합니다.

```javascript
AITLoading.onComplete(function() {
    var stats = AITLoading.getFileStats();
    stats.forEach(function(file) {
        console.log(file.name + ':');
        console.log('  크기: ' + (file.size / 1024 / 1024).toFixed(2) + 'MB');
        console.log('  시간: ' + file.duration + 'ms');
        console.log('  평균 속도: ' + file.avgSpeed.toFixed(1) + 'KB/s');
    });
});
```

**반환 데이터 구조:**

```javascript
[{
    name: 'game.wasm',           // 파일명
    url: 'Build/game.wasm',      // 전체 URL
    size: 4521300,               // 크기 (바이트)
    duration: 2890,              // 다운로드 시간 (ms)
    startTime: 120.5,            // 시작 시점 (ms, 페이지 로드 기준)
    responseEnd: 3010.5,         // 종료 시점 (ms)
    avgSpeed: 1564.5,            // 평균 속도 (KB/s)
    peakSpeed: 2341.8,           // 피크 속도 (KB/s)
    minSpeed: 892.1,             // 최저 속도 (KB/s)
    speedHistory: [892, 1234, 2341, 1876, 1423]  // 1초 간격 속도 기록
}, ...]
```

#### `AITLoading.getTotalTime()`

총 로딩 시간을 밀리초 단위로 반환합니다.

```javascript
AITLoading.onComplete(function() {
    console.log('총 로딩 시간:', AITLoading.getTotalTime() + 'ms');
});
```

### 제어 API

#### `AITLoading.hide()`

로딩 화면을 숨깁니다.

```javascript
AITLoading.hide();
```

---

## 예제

### 간단한 커스텀 로딩 화면

아래는 간단한 커스텀 로딩 화면 예제입니다. 실제 기본 템플릿은 `Assets/AppsInToss/loading.html`에서 확인할 수 있습니다.

```html
<!--
    CSS 변수를 수정하면 쉽게 색상과 크기를 변경할 수 있습니다.
-->

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
    // 앱 정보로 UI 초기화
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

### 퍼센트 표시가 있는 로딩 화면

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
    // 앱 정보로 UI 초기화
    AITLoading.onReady(function(appInfo) {
        document.getElementById('app-icon').src = appInfo.iconUrl || '';
        document.getElementById('app-name').textContent = appInfo.displayName || '';
        document.getElementById('progress-bar').style.background = appInfo.primaryColor || '#3182f6';
    });

    // 진행률 업데이트
    AITLoading.onProgress(function(progress) {
        var percent = Math.round(progress * 100);
        document.getElementById('progress-bar').style.width = percent + '%';
        document.getElementById('percent-text').textContent = percent + '%';
    });

    // 로딩 완료 시 화면 숨기기
    AITLoading.onComplete(function() {
        AITLoading.hide();
    });

    // 에러 처리
    AITLoading.onError(function(error) {
        document.getElementById('percent-text').textContent = '로딩 실패';
        document.getElementById('percent-text').style.color = '#f04452';
    });
})();
</script>
```

### 파일별 다운로드 진행률 표시

```html
<style>
    .file-progress {
        width: 280px;
        margin-top: 20px;
    }

    .file-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 0;
        font-size: 12px;
        color: #6b7684;
    }

    .file-name {
        flex: 1;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }

    .file-speed {
        margin-left: 8px;
        color: #3182f6;
    }
</style>

<div class="file-progress" id="fileProgress"></div>

<script>
    var fileProgress = document.getElementById('fileProgress');
    var fileItems = {};

    AITLoading.onFileProgress(function(info) {
        if (!fileItems[info.name]) {
            var item = document.createElement('div');
            item.className = 'file-item';
            item.innerHTML = '<span class="file-name">' + info.name + '</span>' +
                           '<span class="file-speed" id="speed-' + info.name.replace(/\./g, '-') + '">0 KB/s</span>';
            fileProgress.appendChild(item);
            fileItems[info.name] = item;
        }

        var speedEl = document.getElementById('speed-' + info.name.replace(/\./g, '-'));
        if (speedEl && info.speed) {
            speedEl.textContent = Math.round(info.speed) + ' KB/s';
        }
    });
</script>
```

---

## 콘솔 로그

로딩 완료 시 자동으로 콘솔에 상세 정보가 출력됩니다:

```
[AIT Loading] ========== 로딩 완료 ==========
[AIT Loading] 총 로딩 시간: 4523ms
[AIT Loading] 파일별 다운로드:
  - game.loader.js:
      크기: 0.04MB
      시간: 120ms
      평균 속도: 376.7KB/s
  - game.wasm:
      크기: 4.41MB
      시간: 2890ms
      평균 속도: 1564.5KB/s
      피크 속도: 2341.8KB/s
  - game.data:
      크기: 11.76MB
      시간: 3210ms
      평균 속도: 3751.9KB/s
[AIT Loading] ================================
```

---

## 트러블슈팅

### 아이콘이 표시되지 않음

1. AIT Configuration에서 `iconUrl`이 설정되어 있는지 확인
2. CORS 정책으로 외부 이미지가 차단될 수 있음 → 같은 도메인의 이미지 사용 권장
3. 네이티브 앱 환경에서는 자동으로 앱 아이콘이 로드됨

### 커스텀 로딩 화면이 적용되지 않음

1. `Assets/AppsInToss/loading.html` 파일이 올바른 위치에 있는지 확인
2. 빌드를 다시 실행

### 진행률이 업데이트되지 않음

1. `AITLoading.onProgress()` 콜백이 올바르게 등록되어 있는지 확인
2. 콜백은 페이지 로드 초기에 등록해야 함

### appInfo가 비어있음

1. 커스텀 로딩 화면에서는 `AITLoading._initialized`를 확인 후 사용
2. 또는 `setInterval`로 초기화 완료를 대기
