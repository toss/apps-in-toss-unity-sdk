# 생성된 코드 검증 가이드

## 자동 검증 (이미 구현됨)

SDK 생성기는 다음 검증을 자동으로 수행합니다:

1. **타입 매핑 검증**: TypeScript → C# 타입 변환 검증
2. **문법 검증**:
   - C# 중괄호 매칭
   - 세미콜론 체크
   - JavaScript 기본 문법
3. **API 완전성 검증**: web-framework의 모든 API 발견 및 생성 확인

```bash
npm run generate -- generate --skip-clone --source-path /path/to/web-framework
# ✓ 33개 API 발견
# ✓ 타입 매핑 완료
# ✓ API 완전성 확인
```

## 수동 검증

### 1. 구조 검증 (빠름, 현재 가능)

생성된 파일이 올바른 구조를 가지는지 확인:

```bash
cd tools/generate-unity-sdk

# Discriminated Union Result 클래스 확인
grep -A 20 "class GetUserKeyForGameResult" \
  ../../Runtime/Generated/Types.Generated.cs

# jslib 런타임 타입 체크 확인
grep -A 15 "typeof result ===" \
  ../../Runtime/Generated/Plugins/AppsInToss-게임.jslib

# API 시그니처 확인
grep "GetUserKeyForGame" \
  ../../Runtime/Generated/AIT.Generated.cs
```

**현재 검증 결과:**
- ✅ GetUserKeyForGameResult 클래스 생성됨
- ✅ Pattern Matching (Match) 메서드 포함
- ✅ Fluent API (OnSuccess/OnError) 포함
- ✅ jslib 런타임 타입 체크 로직 포함

### 2. Unity 컴파일 검증 (Unity 프로젝트 필요)

생성된 C# 코드가 실제로 컴파일되는지 확인:

```bash
# Unity 프로젝트에 SDK 추가
cd /path/to/unity/project
# Package Manager → Add package from disk → package.json 선택

# Unity Editor 열기
# Console에 컴파일 에러가 없으면 ✅ 성공
```

**또는 Unity CLI 사용:**

```bash
/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath /path/to/unity/project \
  -executeMethod UnityEditor.SyncVS.SyncSolution \
  -logFile - \
  | grep -i "error\|exception"

# 출력 없으면 ✅ 컴파일 성공
```

### 3. 런타임 동작 검증 (가장 완전한 검증)

실제 Unity WebGL 빌드에서 Discriminated Union 처리 테스트:

#### 3.1. Unity C# 테스트 코드 작성

```csharp
// TestDiscriminatedUnion.cs
using UnityEngine;

public class TestDiscriminatedUnion : MonoBehaviour
{
    void Start()
    {
        // 에러 케이스 테스트
        TestErrorCase();

        // 성공 케이스 테스트
        TestSuccessCase();
    }

    void TestErrorCase()
    {
        Debug.Log("=== Testing Error Case ===");

        AIT.GetUserKeyForGame(result => {
            // Pattern Matching 방식
            result.Match(
                onSuccess: data => {
                    Debug.LogError($"Expected error but got success: {data.hash}");
                },
                onError: error => {
                    Debug.Log($"✅ Error case works: {error}");
                }
            );

            // Fluent API 방식
            result
                .OnSuccess(data => Debug.LogError("Should not be called"))
                .OnError(error => Debug.Log($"✅ Fluent API works: {error}"));
        });
    }

    void TestSuccessCase()
    {
        Debug.Log("=== Testing Success Case ===");

        AIT.GetUserKeyForGame(result => {
            if (result.IsSuccess)
            {
                var data = result.GetSuccess();
                Debug.Log($"✅ Success case works: {data.hash}");
            }
            else
            {
                Debug.LogError($"Expected success but got error: {result.GetErrorCode()}");
            }
        });
    }
}
```

#### 3.2. 브라우저에서 Mock 설정

WebGL 빌드를 로컬 서버에서 실행 후, 브라우저 콘솔에서:

```javascript
// 에러 케이스 Mock
window.AppsInToss = {
  getUserKeyForGame: () => Promise.resolve("INVALID_CATEGORY")
};

// Unity에서 TestErrorCase() 호출
// Console 확인: "✅ Error case works: INVALID_CATEGORY"

// 성공 케이스 Mock
window.AppsInToss = {
  getUserKeyForGame: () => Promise.resolve({
    type: "HASH",
    hash: "test-user-key-123"
  })
};

// Unity에서 TestSuccessCase() 호출
// Console 확인: "✅ Success case works: test-user-key-123"
```

#### 3.3. 브라우저 DevTools로 직접 검증

WebGL 빌드에서 JavaScript 레이어 확인:

```javascript
// 1. getUserKeyForGame jslib 함수 확인
console.log(Module.getUserKeyForGame);
// → function(callbackId, typeName) { ... }

// 2. 런타임 타입 체크 동작 확인
window.AppsInToss = {
  getUserKeyForGame: () => {
    console.log("=== Testing runtime type check ===");
    // 문자열 반환 (에러 케이스)
    return Promise.resolve("ERROR");
  }
};

// Unity에서 호출하면 jslib가:
// 1. typeof result === 'string' 체크 ✅
// 2. { _type: "error", _errorCode: "ERROR" } 생성
// 3. Unity로 전달
```

## 자동화된 E2E 테스트 (향후 구현 가능)

Playwright로 완전 자동화:

```javascript
// tests/discriminated-union.test.js
import { test, expect } from '@playwright/test';

test('Discriminated Union - Error Case', async ({ page }) => {
  // Mock setup
  await page.addInitScript(() => {
    window.AppsInToss = {
      getUserKeyForGame: () => Promise.resolve("INVALID_CATEGORY")
    };
  });

  // Unity WebGL 로드
  await page.goto('http://localhost:5173');
  await page.waitForFunction(() => window.unityInstance !== undefined);

  // Unity 콘솔 메시지 캡처
  const logs = [];
  page.on('console', msg => {
    if (msg.type() === 'log') {
      logs.push(msg.text());
    }
  });

  // Unity에서 테스트 실행
  await page.evaluate(() => {
    window.unityInstance.SendMessage('TestObject', 'TestErrorCase', '');
  });

  // 결과 검증
  await page.waitForTimeout(1000);
  expect(logs.some(log => log.includes('✅ Error case works'))).toBeTruthy();
});
```

## 검증 체크리스트

### 최소 검증 (현재 완료 ✅)
- [x] 생성기 실행 성공 (33 APIs)
- [x] GetUserKeyForGameResult 클래스 생성
- [x] Pattern Matching 메서드 존재
- [x] Fluent API 메서드 존재
- [x] jslib 런타임 타입 체크 로직 존재
- [x] 문법 검증 통과

### 추가 검증 (Unity 프로젝트 필요)
- [ ] Unity Editor에서 컴파일 성공
- [ ] WebGL 빌드 성공
- [ ] 브라우저에서 에러 케이스 동작 확인
- [ ] 브라우저에서 성공 케이스 동작 확인
- [ ] Pattern Matching 실제 동작 확인
- [ ] Fluent API 실제 동작 확인

### 완전 검증 (E2E 테스트)
- [ ] Playwright E2E 테스트 작성
- [ ] CI/CD 파이프라인에 통합
- [ ] 모든 Discriminated Union API 커버리지

## 결론

**현재 상태**: 생성된 코드의 구조적 정확성은 ✅ 검증 완료

**실제 Unity 프로젝트에서 사용하기 전**:
1. Unity Editor에서 컴파일 테스트 권장
2. WebGL 빌드 + 브라우저 테스트로 런타임 동작 확인 권장

**장기적으로**:
- E2E 테스트 자동화로 회귀 방지
- CI/CD에서 매 커밋마다 검증
