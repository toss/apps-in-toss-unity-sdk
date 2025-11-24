# Unity SDK Runtime Generator

TypeScript 정의 파일에서 Unity C# SDK와 JavaScript 브릿지를 자동으로 생성하는 도구입니다.

Apps in Toss web-framework의 TypeScript API 정의를 파싱하여 Unity에서 사용할 수 있는 C# 바인딩과 JavaScript 브릿지 파일(.jslib)을 생성합니다.

## 주요 기능

- ✅ **TypeScript → C# 타입 자동 변환** - Primitive, Object, Array, Promise, Union 타입 지원
- ✅ **Discriminated Union 지원** - TypeScript union을 C# 클래스 계층으로 변환
- ✅ **재현 가능한 빌드** - 매 실행마다 기존 생성 파일 완전 삭제 후 재생성
- ✅ **자동 검증** - 생성된 C# 코드 컴파일 검증 (CSharpier)
- ✅ **Nullable 완전 지원** - C# 8.0+ nullable reference types (0 warnings)

## 빠른 시작

```bash
cd sdk-runtime-generator

# 1. 의존성 설치
pnpm install

# 2. @apps-in-toss/web-framework 설치 (별도로 필요)
pnpm add @apps-in-toss/web-framework

# 3. SDK 생성
pnpm generate

# 4. 포맷팅 (선택사항 - dotnet 필요)
pnpm format

# 5. 검증 (선택사항 - mono 필요)
pnpm validate
```

## 3단계 워크플로우

### 1️⃣ Generate (생성)
```bash
pnpm generate
```
- TypeScript 정의에서 C# + JavaScript 브릿지 생성
- 순수 생성만 수행 (포맷팅/검증 없음)
- 속도: ~1.5초

### 2️⃣ Format (포맷팅)
```bash
pnpm format
```
- 생성된 C# 파일을 CSharpier로 포맷팅
- 선택사항 (dotnet CLI + CSharpier 필요)
- 일관된 코드 스타일 유지

### 3️⃣ Validate (검증)
```bash
pnpm validate
```
- 실제 컴파일러로 생성된 코드 검증
- Mono mcs (C#) + ts-morph (JavaScript) 사용
- 실제 버그 탐지 (regex 아님)

## 테스트 실행

### Tier 1: 컴파일 가능성 검증

실제 컴파일러(Roslyn/Mono mcs, ts-morph)를 사용하여 생성된 코드가 컴파일 가능한지 검증합니다.

#### 1. 테스트 의존성 설치

```bash
cd tests/unit
npm install
```

#### 2. C# 컴파일러 설치

**macOS/Linux**:
```bash
# Mono C# Compiler 설치
brew install mono

# 설치 확인
mcs --version
# Mono C# compiler version 6.14.1.0 (또는 최신 버전)
```

**Windows**:
```powershell
# .NET SDK 설치 (Roslyn 포함)
# https://dotnet.microsoft.com/download 에서 다운로드

# 설치 확인
csc
```

#### 3. 테스트 실행

```bash
# 전체 테스트 실행
pnpm test

# Tier 1 컴파일 검증만 실행
pnpm test:tier1

# Watch 모드 (개발 시 유용)
pnpm test:watch
```

#### 4. 테스트 결과 해석

```
✅ AIT.cs (메인 partial class) 컴파일 성공
✅ AITCore.cs 컴파일 성공
✅ AIT.Types.cs 컴파일 성공
✅ 모든 C# 파일 통합 컴파일 성공 (42개 파일)
✅ 모든 .jslib 파일 문법 검증 통과
```

**테스트 실패 시**:
- C# 컴파일 에러는 Generator의 템플릿 또는 타입 매핑 버그를 의미
- JavaScript 문법 에러는 jslib 템플릿 버그를 의미
- 즉시 수정 필요!

상세한 테스트 가이드는 [`tests/unit/README.md`](./tests/unit/README.md)를 참고하세요.

## 생성되는 파일

```
Runtime/SDK/
├── AIT.cs              # 전체 API 메서드 (80+ methods)
├── AITCore.cs          # 콜백 관리 및 Unity 브릿지
├── AIT.Types.cs        # 타입 정의 (50+ types)
└── Plugins/            # 카테고리별 .jslib 파일 (20개)
    ├── AppsInToss-게임.jslib
    ├── AppsInToss-로그인.jslib
    └── ...
```

## 사용 예시

### 생성된 C# API

```csharp
/// <summary>
/// 토스 인증으로 로그인해요.
/// </summary>
public static void AppLogin(System.Action<AppLoginResult> callback)
{
    string callbackId = RegisterCallback(callback);
    appLogin(callbackId);
}
```

### 생성된 타입

```csharp
[System.Serializable]
public class AppLoginResult
{
    public string authorizationCode;
    public string referrer;
}
```

## 타입 매핑

| TypeScript | C# |
|------------|-------------|
| `string` | `string` |
| `number` | `double` |
| `boolean` | `bool` |
| `void` | `void` |
| `Promise<T>` | `System.Action<T>` callback |
| `{ foo: string }` | `class { public string foo; }` |
| `T \| U` | Discriminated union class |

## C# 코드 검증

생성된 코드의 컴파일 가능 여부를 자동으로 검증합니다:

```bash
./validate-csharp.sh
```

검증 내용:
- ✅ C# 문법 검증 (중괄호, 세미콜론)
- ✅ Unity API mock 제공 (UnityEngine.*)
- ✅ 타입 매핑 검증
- ✅ Nullable reference types 검증

## 프로젝트 구조

```
sdk-runtime-generator/
├── src/
│   ├── index.ts                    # 메인 엔트리포인트
│   ├── parser.ts                   # TypeScript 파싱 (ts-morph)
│   ├── generators/
│   │   ├── csharp.ts               # C# 코드 생성
│   │   └── jslib.ts                # JavaScript 브릿지 생성
│   ├── validators/
│   │   ├── types.ts                # 타입 검증 및 매핑
│   │   ├── completeness.ts         # API 완전성 검증
│   │   └── syntax.ts               # 문법 검증
│   ├── formatters/csharp.ts        # CSharpier 통합
│   └── templates/                  # Handlebars 템플릿
├── validate-csharp.sh              # C# 컴파일 검증 스크립트
├── dist/                           # 빌드 산출물 (gitignored)
└── .temp-csharp-validation/        # 검증 임시 파일 (gitignored)
```

## 개발

### 사용 가능한 스크립트

```bash
# 빌드 (TypeScript → JavaScript)
pnpm run build

# SDK 생성 (빌드 + 생성 자동 실행)
pnpm run generate

# 개발 모드 (tsx로 직접 실행)
pnpm run dev

# C# 검증
./validate-csharp.sh
```

### 빌드 프로세스

빌드 시 자동 실행:
1. `rm -rf dist` - 기존 빌드 삭제
2. `pnpm exec tsc` - TypeScript 컴파일
3. `cp -r src/templates dist/` - Handlebars 템플릿 복사

### web-framework 소스 지정

기본적으로 `node_modules/@apps-in-toss/web-framework`에서 타입 정의를 읽습니다.
로컬 경로를 사용하려면:

```bash
pnpm run generate -- --source-path /path/to/local/web-framework
```

## CI/CD 통합

GitHub Actions에서 자동 테스트:

```yaml
# .github/workflows/tests.yml
jobs:
  sdk-generator-build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [macos-latest, windows-latest]
        node-version: [18, 20]
    steps:
      - name: Build TypeScript
        run: npm run build
      - name: Run Type Checking
        run: npx tsc --noEmit
```

## 문제 해결

### 컴파일 에러: 50개 에러 발생

**증상:**
- 익명 객체 타입 이름 오류 (`string { params, }`)
- Union 타입 파이프 문자 (`|`) C#에서 불가
- Enum 값이 숫자로 시작 (`2G`, `3G`)

**해결:**
- `src/validators/types.ts`의 `mapToCSharpType()` 개선
- 특수문자 제거 로직: `replace(/["'{}()|,\s]/g, '')`
- Enum 숫자 접두사: `/^\d/.test()` → `_2G`, `_3G`

### Nullable 경고 7개

**해결:**
- Nullable annotations (`?`) 추가
- Null-forgiving operator (`!`) 사용
- Default 값 초기화

## 기여

1. 브랜치 생성
2. 코드 수정
3. `npm run build` 실행
4. `./validate-csharp.sh` 검증
5. 커밋 (한국어 커밋 메시지)
6. Pull Request
