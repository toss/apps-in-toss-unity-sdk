# SDK Generator 속성 기반 검증 테스트

Unity SDK Generator의 생성 코드 품질을 **실제 컴파일러**를 사용하여 검증합니다.

## 📋 테스트 계층

### Tier 1: 컴파일 가능성 (⭐⭐⭐)
**파일**: `compilation.test.ts`

**검증 내용**:
- C#: Roslyn/Mono mcs 컴파일러로 실제 컴파일
- JavaScript: TypeScript Compiler API로 문법 검증
- mergeInto 패턴 검증

**실행**:
```bash
npm run test:tier1
```

### Tier 2: 구조적 불변성 (⭐⭐)
**파일**: `invariants.test.ts` (TODO)

**검증 내용**:
- DllImport 패턴 검증
- 콜백 등록 검증
- 네임스페이스 검증

### Tier 3: 타입 안전성 (⭐⭐)
**파일**: `type-safety.test.ts` (TODO)

**검증 내용**:
- C# ↔ jslib 시그니처 일치
- 타입 마샬링 검증
- Promise → Action 변환 검증

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
