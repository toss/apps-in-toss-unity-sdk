# SDK Generator 문법 검증 테스트

Unity SDK Generator가 생성한 코드의 문법 오류를 **실제 컴파일러**로 검증합니다.

## 📋 테스트 범위

### 문법 검증 (Syntax Validation)
**파일**: `compilation.test.ts`

**검증 내용**:
- ✅ C# 컴파일 가능 여부 (Roslyn/Mono mcs)
- ✅ JavaScript 문법 오류 검사 (TypeScript Compiler API)
- ✅ jslib mergeInto 패턴 정합성

**실행**:
```bash
npm test
```

**목적**: SDK 생성 직후 빠른 문법 검증 (~10초)

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
cd sdk-runtime-generator/tests/unit
npm install
```

### 2. 컴파일러 설치

#### macOS/Linux
```bash
# Mono C# Compiler 설치
brew install mono
```

#### Windows
```bash
# .NET SDK 설치 (Roslyn 포함)
# https://dotnet.microsoft.com/download
```

### 3. 테스트 실행

```bash
# 모든 테스트 실행
npm test

# Tier 1만 실행
npm run test:tier1

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

**실제 컴파일러**:
- C#: Roslyn/Mono mcs (실제 빌드)
- JavaScript: TypeScript Compiler API (AST 기반)

**속성 기반 검증**:
- "출력이 뭐냐"가 아니라 "출력이 올바른가"
- 의미 있는 회귀만 탐지
- 공백/주석 변경에 강건

## 🔧 CI/CD 통합

GitHub Actions에서 자동 실행:

```yaml
- name: Run SDK Generator Tests
  run: |
    cd sdk-runtime-generator/tests/unit
    npm ci
    npm test
```

## 📝 새 테스트 추가하기

1. `compilation.test.ts`를 참고하여 새 파일 생성
2. 실제 컴파일러 사용 (정규식 금지)
3. 의미 있는 검증만 수행

## 🐛 트러블슈팅

### 에러: `mcs` or `csc.exe` not found

**해결**:
```bash
# macOS
brew install mono

# Windows
# .NET SDK 설치
```

### 에러: Unity DLL을 찾을 수 없습니다

**해결**:
- Unity를 설치하거나
- 테스트가 시스템 C# 라이브러리로 대체하도록 수정

### 에러: TypeScript 정의 파일을 찾을 수 없습니다

**해결**:
```bash
cd ../../../sdk-runtime-generator
pnpm install
```

## 📚 참고 자료

- [Roslyn Compiler API](https://github.com/dotnet/roslyn)
- [TypeScript Compiler API](https://github.com/microsoft/TypeScript/wiki/Using-the-Compiler-API)
- [Property-Based Testing](https://en.wikipedia.org/wiki/QuickCheck)
