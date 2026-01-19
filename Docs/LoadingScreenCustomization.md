# 로딩 화면 커스터마이징 가이드

Apps in Toss Unity SDK는 WebGL 로딩 화면의 다양한 커스터마이징 옵션을 제공합니다.

## 목차

- [로딩 화면 설정](#로딩-화면-설정)
- [전면 커스터마이징 (커스텀 HTML)](#전면-커스터마이징-커스텀-html)
- [AITLoading JavaScript API](#aitloading-javascript-api)
- [예제](#예제)

---

## 로딩 화면 설정

로딩 화면에 표시되는 앱 정보는 `granite.config.ts`의 `brand` 설정에서 관리됩니다. Build & Deploy Window에서 설정한 값이 자동으로 적용됩니다.

### 설정 항목

| 설정 | `granite.config.ts` 경로 | 설명 |
|------|--------------------------|------|
| 앱 이름 | `brand.displayName` | 로딩 화면에 표시되는 앱 이름 |
| 앱 아이콘 | `brand.icon` | 로딩 화면에 표시되는 앱 아이콘 URL |
| 기본 색상 | `brand.primaryColor` | 진행률 바 색상 |
| 로딩 제목 | `brand.loadingTitle` | 로딩 화면 상단 제목 텍스트 |

### 로딩 제목 설정

Build & Deploy Window의 "로딩 제목" 필드에서 설정할 수 있습니다. `\n`으로 줄바꿈이 가능합니다:

```
게임을 불러오고 있어요\n조금만 기다려주세요
```

### 네이티브 앱 환경

실제 토스 앱 환경에서 실행될 때, SDK는 네이티브 API를 통해 앱 정보를 자동으로 오버라이드합니다:

- 앱 아이콘 (`brandIcon`)
- 앱 이름 (`brandDisplayName`)
- 기본 색상 (`brandPrimaryColor`)

---

## 커스텀 로딩 화면

로딩 화면 전체를 직접 디자인하려면 커스텀 HTML 파일을 사용할 수 있습니다.

### 설정 방법

1. `Apps in Toss > Configuration` 메뉴에서 **로딩 화면 설정** 섹션으로 이동
2. **커스텀 로딩 화면 생성** 버튼 클릭
3. 생성된 `Assets/AppsInToss/loading.html` 파일을 편집

> **참고**: `Assets/AppsInToss/loading.html` 파일이 존재하면 자동으로 커스텀 로딩 화면이 적용됩니다. 파일을 삭제하면 기본 TDS 스타일 로딩 화면이 사용됩니다.

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

`granite.config.ts`의 `brand` 설정에서 로드된 앱 정보입니다.

```javascript
console.log(AITLoading.appInfo.iconUrl);       // 앱 아이콘 URL
console.log(AITLoading.appInfo.displayName);   // 앱 표시 이름
console.log(AITLoading.appInfo.primaryColor);  // 기본 색상
console.log(AITLoading.appInfo.loadingTitle);  // 로딩 제목
```

### 콜백 API

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

### 커스텀 로딩 화면

`Assets/AppsInToss/loading.html`:

```html
<style>
    .custom-loading {
        position: fixed;
        top: 0; left: 0; right: 0; bottom: 0;
        background: #ffffff;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        font-family: -apple-system, BlinkMacSystemFont, sans-serif;
    }

    .custom-icon {
        width: 80px;
        height: 80px;
        border-radius: 20px;
        margin-bottom: 24px;
    }

    .custom-title {
        font-size: 18px;
        font-weight: 600;
        color: #191f28;
        margin-bottom: 8px;
    }

    .custom-progress-container {
        width: 200px;
        height: 6px;
        background: #e5e8eb;
        border-radius: 3px;
        overflow: hidden;
        margin-top: 24px;
    }

    .custom-progress-bar {
        height: 100%;
        width: 0%;
        transition: width 0.3s ease;
    }

    .custom-percent {
        margin-top: 12px;
        font-size: 14px;
        color: #6b7684;
    }
</style>

<div class="custom-loading" id="custom-loading">
    <img class="custom-icon" id="app-icon" alt="앱 아이콘" />
    <div class="custom-title" id="app-name"></div>
    <div class="custom-progress-container">
        <div class="custom-progress-bar" id="progressBar"></div>
    </div>
    <div class="custom-percent" id="percentText">0%</div>
</div>

<script>
    // 앱 정보로 UI 초기화 (AITLoading.appInfo 사용)
    function initUI() {
        var appInfo = window.AITLoading ? window.AITLoading.appInfo : {};

        var iconEl = document.getElementById('app-icon');
        var nameEl = document.getElementById('app-name');
        var progressBar = document.getElementById('progressBar');

        if (iconEl && appInfo.iconUrl) {
            iconEl.src = appInfo.iconUrl;
        }
        if (nameEl && appInfo.displayName) {
            nameEl.textContent = appInfo.displayName;
        }
        if (progressBar && appInfo.primaryColor) {
            progressBar.style.background = appInfo.primaryColor;
        }
    }

    // AITLoading이 초기화된 후 UI 업데이트
    if (window.AITLoading && window.AITLoading._initialized) {
        initUI();
    } else {
        // 초기화 대기
        var checkInit = setInterval(function() {
            if (window.AITLoading && window.AITLoading._initialized) {
                clearInterval(checkInit);
                initUI();
            }
        }, 50);
    }

    // 진행률 콜백
    AITLoading.onProgress(function(progress) {
        var percent = Math.round(progress * 100);
        document.getElementById('progressBar').style.width = percent + '%';
        document.getElementById('percentText').textContent = percent + '%';
    });

    // 완료 콜백
    AITLoading.onComplete(function() {
        console.log('총 로딩 시간:', AITLoading.getTotalTime() + 'ms');
        AITLoading.hide();
    });

    // 에러 콜백
    AITLoading.onError(function(error) {
        document.getElementById('percentText').textContent = '로딩 실패';
        document.getElementById('percentText').style.color = '#f04452';
    });
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

1. Build & Deploy Window에서 `iconUrl`이 설정되어 있는지 확인
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
