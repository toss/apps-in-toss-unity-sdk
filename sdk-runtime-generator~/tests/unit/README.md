# SDK Generator 속성 검증 테스트

Unity SDK Generator가 생성한 코드를 **속성(property) 기반**으로 검증합니다. 정규식 매칭 대신 TypeScript Compiler API / ts-morph와 golden 파일을 조합해 구조적 불변식과 회귀를 검증합니다.

## 📋 테스트 범위

| 파일 | 검증 내용 |
|------|-----------|
| `binding.test.ts` | C# extern 선언 ↔ jslib 함수의 양방향 매핑 및 파라미터 개수 일치 |
| `invariants.test.ts` | 생성 코드 전반의 구조적 불변식 (DllImport ↔ jslib 일관성, 콜백 파라미터 위치, SendMessage 패턴 등) |
| `serialization.test.ts` | JSON 직렬화/역직렬화 경로의 안전성 |
| `differential.test.ts` | Golden 파일 기반 회귀 검증 (의도치 않은 생성 결과 변경 감지) |
| `multi-version.test.ts` | 여러 `@apps-in-toss/web-framework` 버전에 대한 호환성 |

**실행**:
```bash
npm test
```

**목적**: SDK 생성 직후 구조 검증

**언제 실행?**
- SDK Generator 코드 수정 후
- `pnpm generate` 실행 후
- Pull Request 생성 전

---

## SDK Runtime 동작 검증

SDK가 실제 브라우저 환경에서 올바르게 동작하는지는 **E2E 테스트**에서 검증합니다.

**위치**: `Tests~/E2E/tests/e2e-full-pipeline.test.js` (Test 7: Runtime API Tests)

**E2E에서 검증하는 항목**:
- ✅ C# API → jslib 함수 호출 성공
- ✅ 콜백 기반 비동기 처리
- ✅ 타입 마샬링 (C# string/double/bool ↔ JavaScript)
- ✅ 브라우저 WebGL 환경 실행

**실행**:
```bash
cd ../../..  # 프로젝트 루트
./run-local-tests.sh --all
```

**결과 확인**:
```bash
cat Tests~/E2E/tests/benchmark-results.json
# Test 7 섹션에 Runtime 검증 결과 포함
```

## 🚀 실행 방법

### 1. 의존성 설치

```bash
cd sdk-runtime-generator~/tests/unit
pnpm install
```

### 2. 테스트 실행

```bash
# 모든 테스트 실행
npm test

# 개별 테스트 스위트 실행
npm run test:binding
npm run test:invariants
npm run test:serialization
npm run test:differential
npm run test:multi-version

# Watch 모드
npm run test:watch

# UI 모드
npm run test:ui
```

## 📊 테스트 철학

### ❌ 사용하지 않는 것

**정규식 기반 검증**: 휴리스틱은 오탐/미탐이 많음
- 중괄호 카운팅
- 문자열 패턴 매칭
- 주석 무시 시도

### ✅ 사용하는 것

**AST 기반 구조 검증**:
- TypeScript Compiler API / ts-morph로 생성 코드 파싱
- 외부 컴파일러 설치 불필요 (Node.js만 있으면 됨)

**속성 기반 검증**:
- "출력이 뭐냐"가 아니라 "출력이 올바른가"
- 의미 있는 회귀만 탐지
- 공백/주석 변경에 강건

## 🔧 CI/CD 통합

GitHub Actions에서 자동 실행:

```yaml
- name: Run SDK Generator Tests
  run: |
    cd sdk-runtime-generator~/tests/unit
    pnpm install
    npm test
```

## 📝 새 테스트 추가하기

1. 기존 테스트 파일(`invariants.test.ts` 등)을 참고하여 새 파일 생성
2. TypeScript Compiler API / ts-morph 사용 (정규식 금지)
3. 의미 있는 구조적 불변식만 검증

## 🐛 트러블슈팅

### 에러: TypeScript 정의 파일을 찾을 수 없습니다

**해결**:
```bash
cd ../../../sdk-runtime-generator~
pnpm install
```

## 📚 참고 자료

- [TypeScript Compiler API](https://github.com/microsoft/TypeScript/wiki/Using-the-Compiler-API)
- [ts-morph](https://ts-morph.com/)
- [Property-Based Testing](https://en.wikipedia.org/wiki/QuickCheck)
