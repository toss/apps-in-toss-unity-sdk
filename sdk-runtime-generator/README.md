# Unity SDK Generator

TypeScript 정의 파일에서 Unity C# SDK와 JavaScript bridge를 자동으로 생성하는 도구입니다.

## 개요

이 도구는 `@apps-in-toss/web-framework`의 TypeScript API 정의를 파싱하여 다음을 자동 생성합니다:

- **C# API 클래스** (`AIT.Generated.cs`) - Unity에서 호출할 수 있는 C# 메서드
- **C# 타입 정의** (`Types.Generated.cs`) - API 파라미터 및 응답 타입 클래스
- **JavaScript bridge 파일** (`.jslib`) - Unity WebGL과 web-framework 연결

## 주요 기능

- ✅ **자동 타입 변환** - TypeScript → C# 타입 자동 매핑
- ✅ **Union 타입 지원** - TypeScript union을 C# 클래스로 변환
- ✅ **익명 타입 처리** - 의미있는 클래스명 자동 생성
- ✅ **Promise 변환** - async 함수를 C# callback 패턴으로 변환
- ✅ **카테고리 자동 분류** - JSDoc `@category` 태그 기반 파일 분류
- ✅ **완전성 검증** - 모든 API가 생성되었는지 자동 검증
- ✅ **문법 검증** - 생성된 C#/JavaScript 코드 문법 체크

## 설치

```bash
cd tools/generate-unity-sdk
npm install
```

## 사용법

### 1. GitHub에서 web-framework clone 후 생성

```bash
npm run build
npm run generate -- generate --tag next --output /path/to/output
```

**옵션:**
- `--tag <tag>` - web-framework Git 태그/브랜치 (기본값: `next`)
- `--output <path>` - 출력 디렉토리 (기본값: `../../Runtime/Generated`)

### 2. 로컬 web-framework 사용 (개발 모드)

```bash
npm run build
npm run generate -- generate \
  --skip-clone \
  --source-path /path/to/web-framework \
  --output /path/to/output
```

**옵션:**
- `--skip-clone` - GitHub clone 생략하고 로컬 경로 사용
- `--source-path <path>` - 로컬 web-framework 경로

### 3. 개발 모드 (빌드 없이 실행)

```bash
npm run dev -- generate --tag next --output /tmp/test
```

## 생성 결과

### 출력 파일

```
output/
├── AIT.Generated.cs           # C# API 메서드 (33개)
├── Types.Generated.cs         # C# 타입 클래스 (25개)
└── Plugins/
    ├── AppsInToss-로그인.jslib
    ├── AppsInToss-토스페이.jslib
    ├── AppsInToss-게임.jslib
    └── ...                     # 카테고리별 jslib 파일 (22개)
```

### 생성 예시

#### C# API 메서드

```csharp
/// <summary>
/// 토스 인증으로 로그인해요.
/// </summary>
public static void AppLogin(System.Action<AppLoginResult> callback)
{
    string callbackId = RegisterCallback(callback);
    appLogin(callbackId);
}

[DllImport("__Internal")]
private static extern void appLogin(string callbackId);
```

#### C# 타입 클래스

```csharp
[System.Serializable]
public class AppLoginResult
{
    public string authorizationCode;
    public string referrer;
}
```

#### JavaScript bridge

```javascript
mergeInto(LibraryManager.library, {
    appLogin: function(callbackId) {
        const callback = UTF8ToString(callbackId);
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.appLogin) {
            window.AppsInToss.appLogin()
                .then(function(result) {
                    const resultJson = JSON.stringify(result);
                    Module.dynCall_vii(
                        Module.cwrap('InvokeCallback', null, ['string', 'string']),
                        [callback, resultJson]
                    );
                })
                .catch(function(error) {
                    console.error('appLogin error:', error);
                    const errorJson = JSON.stringify({ error: error.message });
                    Module.dynCall_vii(
                        Module.cwrap('InvokeCallback', null, ['string', 'string']),
                        [callback, errorJson]
                    );
                });
        } else {
            console.warn('window.AppsInToss.appLogin not available');
        }
    },
});
```

## 프로젝트 구조

```
tools/generate-unity-sdk/
├── src/
│   ├── index.ts              # CLI 진입점
│   ├── parser.ts             # TypeScript 파싱 (ts-morph)
│   ├── types.ts              # 타입 정의
│   ├── generators/
│   │   ├── csharp.ts         # C# 코드 생성
│   │   └── jslib.ts          # JavaScript bridge 생성
│   ├── validators/
│   │   ├── types.ts          # 타입 검증 및 매핑
│   │   ├── completeness.ts   # API 완전성 검증
│   │   └── syntax.ts         # 문법 검증
│   └── templates/
│       ├── csharp-class.hbs  # C# 클래스 파일 템플릿
│       ├── csharp-method.hbs # C# 메서드 템플릿
│       ├── jslib-file.hbs    # jslib 파일 템플릿
│       └── jslib-function.hbs # jslib 함수 템플릿
├── dist/                     # 빌드 결과 (자동 생성)
├── package.json
└── tsconfig.json
```

## 타입 매핑

| TypeScript | C# |
|------------|-------------|
| `string` | `string` |
| `number` | `float` |
| `boolean` | `bool` |
| `void` | `void` |
| `Promise<T>` | `System.Action<T>` callback |
| `{ foo: string }` | `class { public string foo; }` |
| `T \| undefined` | `T` (Union에서 undefined 제거) |
| `() => void` | `System.Action` |
| Array | `T[]` |

## 개발

### 빌드

```bash
npm run build
```

빌드 시 자동으로 실행:
1. `rm -rf dist` - 기존 빌드 삭제
2. `tsc` - TypeScript 컴파일
3. `cp -r src/templates dist/` - 템플릿 복사

### 테스트

```bash
# TypeScript 타입 체크
npx tsc --noEmit

# CLI 명령어 테스트
node dist/index.js --help

# 실제 생성 테스트
npm run generate -- generate --tag next --output /tmp/test
```

### 디버깅

생성 과정은 다음 단계로 진행됩니다:

1. **📦 Clone** - GitHub에서 web-framework clone
2. **🔨 Build** - npm install && npm run build
3. **📊 Parse** - TypeScript 정의 파싱 (ts-morph)
4. **🔍 Validate** - 타입 검증
5. **🔨 Generate** - C#/jslib 코드 생성
6. **✅ Verify** - 완전성 및 문법 검증
7. **📝 Write** - 파일 출력

각 단계의 로그를 확인하여 문제를 진단할 수 있습니다.

## CI/CD

GitHub Actions로 자동 테스트:

- **빌드 테스트** - TypeScript 컴파일 검증
- **템플릿 검증** - 템플릿 복사 확인
- **크로스 플랫폼** - macOS, Windows
- **Node.js 호환성** - Node 18, 20

워크플로우: `.github/workflows/tests.yml` (sdk-generator-build job)

## 문제 해결

### npm install 실패

```bash
# package-lock.json 삭제 후 재설치
rm -f package-lock.json
npm install
```

### TypeScript 정의 파일을 찾을 수 없습니다

web-framework가 빌드되지 않았거나 경로가 잘못되었습니다:

```bash
# web-framework 빌드 확인
cd /path/to/web-framework
npm run build
ls -la dist-web/index.d.ts  # 파일 존재 확인
```

### 중복 함수 생성

`bridge.d.ts` 같은 re-export 파일이 파싱되고 있습니다.
`src/parser.ts`의 skiplist에 추가:

```typescript
if (fileName === 'index.d.ts' || fileName === 'bridge.d.ts') {
  continue;
}
```

### 잘못된 타입 매핑

`src/validators/types.ts`의 `mapToCSharpType` 함수를 확인하세요.

## 기여

1. 브랜치 생성
2. 코드 수정
3. `npm run build` 실행
4. 테스트 (생성 결과 확인)
5. 커밋 (한국어 커밋 메시지)
6. Pull Request

## 라이선스

이 프로젝트는 Toss의 proprietary 소프트웨어입니다.
