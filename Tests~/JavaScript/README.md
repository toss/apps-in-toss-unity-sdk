# JavaScript 브릿지 테스트

Apps in Toss Unity SDK의 JavaScript 브릿지 코드 단위 테스트입니다.

## 개요

Unity WebGL과 JavaScript 간 통신 브릿지의 핵심 기능을 검증합니다. 실제 Unity 빌드 없이 빠르게 실행되며, CI/CD에서 자동으로 실행됩니다.

## 테스트 범위

### 1. 브라우저 감지 (5개 테스트)
- Chrome 감지
- Safari 감지
- Firefox 감지
- Edge 감지
- 알 수 없는 브라우저 처리

### 2. OS 감지 (6개 테스트)
- iOS (iPhone)
- iOS (iPad)
- Android
- Windows
- macOS
- 알 수 없는 OS 처리

### 3. ReactNativeWebView 감지 (3개 테스트)
- WebView 내부 감지
- 일반 브라우저 감지
- null 값 처리

### 4. 환경 변수 감지 (5개 테스트)
- Production 모드 (string "true")
- Production 모드 (boolean true)
- Development 모드 (string "false")
- Development 모드 (boolean false)
- Undefined 기본값

### 5. Unity 통신 모킹 (3개 테스트)
- SendMessage 정상 호출
- Unity 인스턴스 미준비 처리
- SendMessage 메서드 없음 처리

## 설치

```bash
cd Tests/JavaScript
npm install
```

## 테스트 실행

### 기본 실행
```bash
npm test
```

### Watch 모드 (개발 시)
```bash
npm run test:watch
```

### UI 모드
```bash
npm run test:ui
```

### 커버리지 포함
```bash
npm run coverage
```

## 테스트 결과

### 성공 시

```
 RUN  v1.6.1 /path/to/Tests/JavaScript

 ✓ bridge.test.js  (22 tests) 3ms
   ✓ Browser Detection (5)
   ✓ OS Detection (6)
   ✓ ReactNativeWebView Detection (3)
   ✓ Environment Detection (5)
   ✓ Unity SendMessage Mock (3)

 Test Files  1 passed (1)
      Tests  22 passed (22)
   Start at  12:34:56
   Duration  256ms
```

## 테스트 구조

```javascript
// bridge.test.js

describe('Browser Detection', () => {
  it('should detect Chrome browser', () => {
    global.window.navigator.userAgent = 'Chrome/120.0.0.0';
    const browser = detectBrowser();
    expect(browser.name).toBe('Chrome');
  });
  // ... 4 more tests
});

describe('OS Detection', () => {
  it('should detect iOS', () => {
    global.window.navigator.userAgent = 'iPhone; CPU iPhone OS 17_0';
    const os = detectOS();
    expect(os).toBe('iOS');
  });
  // ... 5 more tests
});

// ... 3 more describe blocks
```

## CI/CD에서 실행

GitHub Actions에서 자동으로 실행됩니다:

```yaml
jobs:
  javascript-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: npm ci
      - run: npm test
```

### 실행 조건
- Push to `master`, `dave` 브랜치
- Pull Request to `master` 브랜치

### 소요 시간
- ~2-3분 (의존성 설치 포함)

## 커버리지

목표: **80-90%** 코드 커버리지

```bash
npm run coverage
```

결과 예시:
```
---------------------|---------|----------|---------|---------|
File                 | % Stmts | % Branch | % Funcs | % Lines |
---------------------|---------|----------|---------|---------|
All files            |   85.71 |    88.23 |   90.00 |   85.71 |
 bridge.js           |   85.71 |    88.23 |   90.00 |   85.71 |
---------------------|---------|----------|---------|---------|
```

## 사용 기술

- **Vitest**: 빠른 단위 테스트 러너 (Vite 기반)
- **happy-dom**: 경량 DOM 환경 (jsdom보다 빠름)
- **vi (Vitest Mock)**: 함수 모킹

## 디렉토리 구조

```
Tests/JavaScript/
├── bridge.test.js           # 테스트 파일
├── vitest.config.ts         # Vitest 설정
├── package.json             # 의존성
├── package-lock.json
└── README.md                # 이 파일
```

## 새 테스트 추가

### 1. bridge.test.js에 테스트 추가

```javascript
describe('New Feature', () => {
  it('should do something', () => {
    // Arrange
    const input = 'test';

    // Act
    const result = newFunction(input);

    // Assert
    expect(result).toBe('expected');
  });
});
```

### 2. 테스트 실행

```bash
npm run test:watch
```

### 3. 모든 테스트 통과 확인

```bash
npm test
```

## 문제 해결

### 테스트 실패 시

```bash
# 상세 로그 확인
npm test -- --reporter=verbose

# 특정 테스트만 실행
npm test -- -t "Browser Detection"

# 디버그 모드
npm test -- --inspect-brk
```

### 의존성 문제

```bash
# node_modules 삭제 후 재설치
rm -rf node_modules package-lock.json
npm install
```

### Vitest 버전 문제

```bash
# Vitest 업데이트
npm update vitest @vitest/ui
```

## 실제 브릿지 코드와 연결

이 테스트는 **독립적인 헬퍼 함수**를 테스트합니다. 실제 SDK의 `appsintoss-unity-bridge.js`에서 이 함수들을 export하여 사용할 수 있습니다.

### 예시: bridge.js에서 export

```javascript
// WebGLTemplates/AITTemplate2022/Runtime/appsintoss-unity-bridge.js

export function detectBrowser() {
  const ua = window.navigator.userAgent;
  if (ua.includes('Edg/')) return { name: 'Edge' };
  if (ua.includes('Chrome')) return { name: 'Chrome' };
  // ...
}

export function detectOS() {
  const ua = window.navigator.userAgent;
  if (ua.includes('iPhone') || ua.includes('iPad')) return 'iOS';
  // ...
}

// ... other exports
```

### 테스트에서 import

```javascript
import { detectBrowser, detectOS } from '../../WebGLTemplates/AITTemplate2022/Runtime/appsintoss-unity-bridge.js';

describe('Browser Detection', () => {
  it('should detect Chrome', () => {
    const browser = detectBrowser();
    expect(browser.name).toBe('Chrome');
  });
});
```

## 다음 단계

1. ✅ 22개 테스트 모두 통과
2. 🔄 실제 bridge.js와 연결 (선택)
3. 🔄 커버리지 80% 이상 달성
4. 🔄 새로운 브릿지 기능 추가 시 테스트 작성

## 참고

- Vitest는 Vite와 동일한 설정을 사용하여 빠른 실행 속도 제공
- happy-dom은 jsdom보다 2-3배 빠름
- 브라우저 환경 모킹으로 Node.js에서 실행 가능
