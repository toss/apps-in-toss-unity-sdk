# 문제 해결

이 문서는 Apps in Toss Unity SDK 사용 중 발생할 수 있는 문제와 해결 방법을 안내합니다.

## 목차

- [자주 묻는 질문](#자주-묻는-질문)
- [빌드 오류](#빌드-오류)
- [런타임 오류](#런타임-오류)
- [개발 환경 문제](#개발-환경-문제)

---

## 자주 묻는 질문

### Q1. 빌드 시 Node.js가 없다는 오류가 발생합니다

**원인**: SDK의 빌드 파이프라인은 Node.js를 사용합니다.

**해결 방법**:
- SDK는 시스템에 Node.js가 설치되어 있지 않아도 자동으로 내장 Node.js를 다운로드합니다.
- 다운로드 다이얼로그가 표시되면 "다운로드"를 선택하세요.
- 내장 Node.js 저장 위치:
  - macOS/Linux: `~/.ait-unity-sdk/nodejs/`
  - Windows: `%LOCALAPPDATA%\ait-unity-sdk\nodejs\`

---

### Q2. 아이콘 URL을 입력하라는 오류가 발생합니다

**원인**: 앱 아이콘 URL은 필수 설정입니다.

**해결 방법**:
1. `AIT > Configuration` 메뉴 열기
2. "앱 아이콘 URL" 필드에 이미지 URL 입력
3. 이 URL은 Apps in Toss 앱에서 미니앱 아이콘으로 표시됩니다.

---

### Q3. Unity Editor에서 API 호출 시 Mock 로그만 출력됩니다

**원인**: SDK API는 WebGL 빌드에서만 실제로 동작합니다.

**해결 방법**:
- Unity Editor에서는 Mock 구현이 호출되어 테스트 로그만 출력됩니다.
- 실제 동작은 WebGL로 빌드 후 Apps in Toss 앱에서 확인하세요.
- 이는 정상적인 동작입니다. 자세한 내용은 [API 사용 패턴 - Mock 브릿지](APIUsagePatterns.md#mock-브릿지)를 참조하세요.

---

### Q4. Dev Server에서는 동작하는데 Production에서 동작하지 않습니다

**원인**: Dev Server 모드는 Mock 브릿지가 활성화되어 있어 일반 브라우저에서도 동작합니다. Production 빌드는 실제 Apps in Toss 앱 환경이 필요합니다.

**해결 방법**:
- Production 빌드는 Apps in Toss 앱 내에서 테스트해야 합니다.
- 로컬에서 Production 환경을 테스트하려면 `Production Server` 모드를 사용하세요:
  1. `AIT > Production Server > Start Server`로 로컬 서버 실행
  2. [샌드박스 앱(테스트앱)](https://developers-apps-in-toss.toss.im/development/test/sandbox)에서 로컬 서버에 연결하여 실제 환경과 동일하게 테스트
- 자세한 내용은 [빌드 프로필](BuildProfiles.md)을 참조하세요.

---

## 빌드 오류

### Unity WebGL 빌드 실패

**증상**: WebGL 빌드 중 오류 발생

**가능한 원인 및 해결 방법**:

1. **지원되지 않는 Unity 버전**
   - 최소 Unity 2021.3 이상이 필요합니다 (권장: Unity 6 이상).

2. **WebGL 모듈 미설치**
   - Unity Hub에서 WebGL Build Support 모듈을 설치하세요.

3. **메모리 부족**
   - Unity Editor를 재시작하세요.
   - 다른 메모리 사용 프로그램을 종료하세요.

---

### npm install 실패

**증상**: 빌드 중 `npm install` 단계에서 오류 발생

**가능한 원인 및 해결 방법**:

1. **네트워크 문제**
   - 인터넷 연결을 확인하세요.
   - 프록시 환경이라면 npm 프록시 설정을 확인하세요.

2. **손상된 node_modules**
   - `ait-build/node_modules` 폴더를 삭제하고 다시 빌드하세요.

3. **Node.js 버전 문제**
   - SDK 내장 Node.js를 사용하거나, 시스템 Node.js v18 이상을 설치하세요.

---

### Granite 빌드 실패

**증상**: `npm run build` (granite build) 단계에서 오류 발생

**가능한 원인 및 해결 방법**:

1. **TypeScript 컴파일 오류**
   - 커스텀 TypeScript 코드의 문법 오류를 확인하세요.
   - `BuildConfig~/` 폴더의 사용자 코드를 점검하세요.

2. **의존성 충돌**
   - `package.json`에 추가한 패키지 버전을 확인하세요.
   - `node_modules`를 삭제하고 다시 빌드해보세요.

---

## 런타임 오류

### AITException 발생

**증상**: SDK API 호출 시 `AITException` throw

**해결 방법**:
1. 예외의 `ErrorCode`와 `Message` 속성을 확인하세요.
2. 네트워크 연결 상태를 확인하세요.
3. Apps in Toss 앱이 최신 버전인지 확인하세요.

```csharp
try
{
    var result = await AIT.SomeAPI();
}
catch (AITException ex)
{
    Debug.LogError($"오류 코드: {ex.ErrorCode}, 메시지: {ex.Message}");
}
```

---

### 로딩 화면이 사라지지 않음

**증상**: WebGL 빌드 실행 시 로딩 화면에서 멈춤

**가능한 원인 및 해결 방법**:

1. **Unity 초기화 실패**
   - 브라우저 개발자 도구(F12)의 Console 탭에서 오류 메시지 확인

2. **리소스 로드 실패**
   - Network 탭에서 실패한 요청 확인
   - CORS 설정 확인

3. **메모리 부족**
   - 모바일 기기의 경우 다른 앱을 종료하고 다시 시도

---

### 결제 API가 동작하지 않음

**증상**: `CheckoutPayment` 호출 시 오류 또는 무응답

**가능한 원인 및 해결 방법**:

1. **Mock 환경에서 테스트 중**
   - 실제 결제는 Apps in Toss 앱 내에서만 동작합니다.
   - Dev Server 모드에서는 Mock 결과가 반환됩니다.

2. **결제 옵션 누락**
   - `CheckoutPaymentOptions`의 필수 필드가 모두 설정되었는지 확인하세요.

---

## 개발 환경 문제

### Unity Editor에서 SDK 메뉴가 보이지 않음

**증상**: `AIT` 메뉴가 Unity Editor에 표시되지 않음

**가능한 원인 및 해결 방법**:

1. **패키지 설치 실패**
   - `Window > Package Manager`에서 SDK가 설치되어 있는지 확인
   - 설치 오류가 있다면 패키지를 제거하고 다시 설치

2. **컴파일 오류**
   - Console 창에서 컴파일 오류가 있는지 확인
   - 모든 오류를 해결한 후 Unity를 재시작

3. **Unity 버전 호환성**
   - Unity 2021.3 이상인지 확인

---

## 추가 도움

문제가 해결되지 않는 경우:

1. Unity Console의 전체 오류 메시지를 확인하세요.
2. 브라우저 개발자 도구의 Console과 Network 탭을 확인하세요.
3. [GitHub 저장소](https://github.com/toss/apps-in-toss-unity-sdk/issues)에 이슈를 등록하세요.
4. [TechChat](https://techchat-apps-in-toss.toss.im)에서 문의하세요.

---

## 관련 문서

- [시작하기](GettingStarted.md) - 설치 및 기본 설정
- [API 사용 패턴](APIUsagePatterns.md) - async/await 패턴, 에러 처리
- [빌드 프로필](BuildProfiles.md) - 빌드 설정 상세 안내
- [Contributing](Contributing.md) - 개발 환경 설정
