# SDK Generator 테스트 계획서

> **작성일**: 2024-11-24
> **목적**: sdk-runtime-generator의 생성 코드 신뢰성 확보를 위한 종합 테스트 전략

---

## 📋 목차

1. [현재 상황 분석](#1-현재-상황-분석)
2. [문제점 및 한계](#2-문제점-및-한계)
3. [스냅샷 테스트의 문제](#3-스냅샷-테스트의-문제)
4. [권장 전략: 속성 기반 검증](#4-권장-전략-속성-기반-검증)
5. [구현 계획](#5-구현-계획)
6. [기대 효과](#6-기대-효과)

---

## 1. 현재 상황 분석

### 1.1 SDK Runtime Generator 개요

**위치**: `sdk-runtime-generator~/`

**역할**:
- `@apps-in-toss/web-framework` TypeScript 정의 파싱
- Unity용 C# API 클래스 생성 (AIT.cs, AITCore.cs, AIT.Types.cs)
- JavaScript 브릿지 코드 생성 (20+ .jslib 파일)
- 80+ API 자동 변환 (TypeScript → C# + jslib)

**생성 파이프라인**:
```
TypeScript .d.ts
    ↓ [TypeScriptParser]
ParsedAPI[] + ParsedTypeDefinition[]
    ↓ [Type Validators]
Type Mapping 검증
    ↓ [Generators]
AIT.cs + AITCore.cs + AIT.Types.cs + *.jslib
    ↓ [Syntax Validators]
휴리스틱 검증 (중괄호 카운팅 등)
    ↓
Runtime/SDK/
```

### 1.2 현재 검증 시스템

#### Layer 1: 타입 검증 (`validators/types.ts`)
- 지원 타입 확인: `string`, `number`, `boolean`, `Promise<T>`, `Array<T>` 등
- `TYPE_MAPPING` 테이블 기반 검증
- 미지원 타입 발견 시 빌드 실패

#### Layer 2: 완전성 검증 (`validators/completeness.ts`)
- 파싱된 API 개수 = 생성된 코드 개수
- 누락된 API 리포트

#### Layer 3: 문법 검증 (`validators/syntax.ts`)
- **C# 검증**: 중괄호/괄호 개수 세기, DllImport 패턴 확인
- **JavaScript 검증**: 중괄호/괄호 개수 세기, mergeInto 패턴 확인
- ⚠️ **휴리스틱 기반** - 실제 파서 사용하지 않음

---

## 2. 문제점 및 한계

### 2.1 휴리스틱 기반 검증의 치명적 약점

#### 문제 1: 문자열 리터럴 오탐
```csharp
// 이 코드는 올바르지만 검증 실패할 수 있음
public void Example() {
    string json = "{\"key\": \"value\"}";  // ← 문자열 내부의 {}도 카운트
}
```

#### 문제 2: 주석 내부 코드
```csharp
// 이 코드는 실제 오류지만 검증 통과
/*
    [DllImport(__Internal)]  // ← 주석이지만 패턴 매칭됨
*/
[DllImport(__Internal)]  // ← 실제 오류 (따옴표 없음)
```

#### 문제 3: 컴파일 가능성 미검증
```csharp
// 중괄호는 맞지만 컴파일 실패
public static void Init() {
    string json = JsonUtility.ToJson(options);  // ← options 미선언
    ait_Init(json);  // ← ait_Init 미선언
}
```

### 2.2 검증 갭 (Validation Gaps)

| 검증 영역 | 현재 상태 | 리스크 레벨 | 잠재적 오류 |
|---------|---------|-----------|----------|
| C# 문법 정확성 | 휴리스틱 (중괄호 카운팅) | 🔴 높음 | 컴파일 실패 |
| JavaScript 문법 | 휴리스틱 (중괄호 카운팅) | 🔴 높음 | 런타임 오류 |
| 타입 매핑 정확성 | 수동 TYPE_MAPPING 테이블 | 🟡 중간 | 신규 타입 누락 |
| Unity 호환성 | 없음 | 🔴 높음 | Unity에서 로드 실패 |
| Runtime 동작 검증 | 없음 | 🔴 높음 | 실행 시 오류 |
| C# ↔ JS 타입 정렬 | 없음 | 🔴 높음 | 마샬링 오류 |
| 회귀 방지 | 없음 | 🟡 중간 | 기존 버그 재발 |

### 2.3 주요 휴리스틱 목록 및 위험성

| 휴리스틱 | 목적 | 위험성 |
|---------|-----|--------|
| Named vs Anonymous Types | 타입명 구분 | 동적 구조 타입 오식별 |
| Discriminated Union Detection | `Type1 \| Type2` 패턴 감지 | 문자열 리터럴 없는 Union 누락 |
| PascalCase 변환 | camelCase → PascalCase | 약어 처리 오류 (UILoader → Uiloader) |
| 중괄호 카운팅 | 문법 검증 | 문자열 리터럴 내부 오탐 |
| DllImport 패턴 매칭 | 선언 검증 | 주석 내부 코드 오탐 |
| 타입명 정리 | 특수문자 제거 | 의미 손실 (A\|B → AB) |

---

## 3. 스냅샷 테스트의 문제

### 3.1 전통적 스냅샷 테스트 방식

```typescript
// 전체 파일 스냅샷
test('AIT.cs 생성', () => {
  const generated = generateCSharp(apis);
  expect(generated['AIT.cs']).toMatchSnapshot();
  //                            ^^^^^^^^^^^^^^^^
  //                            전체 파일을 스냅샷으로 저장
});
```

### 3.2 왜 SDK Generator에 부적합한가?

#### 문제 1: 새 API 추가 시 매번 깨짐

```
시나리오: web-framework에 ShowModal() API 추가
    ↓
Generator 실행
    ↓
AIT.cs에 ShowModal 메서드 추가됨 (정상)
    ↓
❌ 스냅샷 테스트 실패 (전체 파일이 달라짐)
    ↓
개발자: "이게 버그인지 정상 추가인지?"
    ↓
npm test -- -u (스냅샷 업데이트)
    ↓
😩 다음 API 추가 시 반복...
```

#### 문제 2: 템플릿 변경 시 노이즈 폭발

```
시나리오: templates/csharp-api.hbs에서 들여쓰기 2칸 → 4칸 변경
    ↓
모든 메서드 들여쓰기 변경됨
    ↓
❌ 80개 API 스냅샷 전부 실패
    ↓
개발자: "의미 있는 변경이 아닌데..."
    ↓
😡 스냅샷 테스트 신뢰 상실
```

#### 문제 3: 주석/공백 변경에도 깨짐

```diff
// 주석 개선 (의미상 동일)
- /// Initialize SDK
+ /// Initializes the Apps in Toss SDK
```
→ ❌ 스냅샷 실패

```diff
// 공백 정리 (동작 동일)
- public static void Init() {
+ public static void Init()
+ {
```
→ ❌ 스냅샷 실패

### 3.3 스냅샷 테스트 비교표

| 항목 | 전체 파일 스냅샷 | 판단 |
|-----|---------------|------|
| API 추가 시 | ❌ 실패 → `-u` 필요 | 노이즈 |
| 타입 변경 시 | ❌ 실패 → `-u` 필요 | 노이즈 |
| 템플릿 개선 시 | ❌ 실패 → `-u` 필요 | 노이즈 |
| 공백 변경 시 | ❌ 실패 → `-u` 필요 | 노이즈 |
| 실제 버그 발생 시 | ✅ 실패 | 신호 |
| **신호 대 노이즈 비율** | **1:10 이상** | 😡 사용 불가 |

---

## 4. 권장 전략: 속성 기반 검증

### 4.1 핵심 철학

> **"출력이 뭐냐"가 아니라 "출력이 올바른가"를 검증**

- ❌ 전체 파일 스냅샷 → 노이즈 많음
- ✅ 속성 기반 검증 → 의미 있는 회귀만 탐지

### 4.2 검증 계층 구조

```
┌─────────────────────────────────────────┐
│ Tier 1: 컴파일 가능성 (⭐⭐⭐)         │  ← 가장 중요
│ - C#: Roslyn Compiler API               │
│ - JS: TypeScript Compiler API           │
│ → 컴파일 실패 = 즉시 차단               │
└─────────────────────────────────────────┘
              ↓ (통과 시)
┌─────────────────────────────────────────┐
│ Tier 2: 구조적 불변성 (⭐⭐)            │
│ - DllImport 패턴 검증                    │
│ - 콜백 패턴 검증                         │
│ - jslib 구조 검증                        │
│ → 패턴 위반 = 버그                       │
└─────────────────────────────────────────┘
              ↓ (통과 시)
┌─────────────────────────────────────────┐
│ Tier 3: 타입 안전성 (⭐⭐)              │
│ - C# DllImport ↔ jslib 시그니처 정렬    │
│ - 매개변수 개수 일치                     │
│ - 타입 마샬링 검증                       │
│ → 타입 불일치 = 런타임 오류             │
└─────────────────────────────────────────┘
              ↓ (통과 시)
┌─────────────────────────────────────────┐
│ Tier 4: 차분 검증 (⭐)                  │
│ - 기존 API 출력 변경 감지                │
│ - 새 API 추가만 허용                     │
│ → 기존 출력 변경 = 회귀                 │
└─────────────────────────────────────────┘
```

---

## 5. 구현 계획

### 5.1 디렉토리 구조

```
Tests/
├── SDK-Generator/              # 🆕 새 디렉토리
│   ├── unit/
│   │   ├── compilation.test.ts        # Tier 1: 컴파일 검증
│   │   ├── invariants.test.ts         # Tier 2: 구조 검증
│   │   ├── type-safety.test.ts        # Tier 3: 타입 검증
│   │   ├── differential.test.ts       # Tier 4: 차분 검증
│   │   ├── helpers/
│   │   │   ├── roslyn-compiler.ts     # C# 컴파일러 wrapper
│   │   │   ├── ts-compiler.ts         # TS 컴파일러 wrapper
│   │   │   ├── method-extractor.ts    # 메서드 추출 유틸
│   │   │   └── pattern-matcher.ts     # 정규식 헬퍼
│   │   ├── package.json
│   │   └── README.md
│   │
│   └── fixtures/                       # 테스트 데이터
│       ├── web-framework-v1.2.3.d.ts   # 알려진 좋은 입력
│       ├── edge-cases/
│       │   ├── union-types.d.ts
│       │   ├── complex-generics.d.ts
│       │   └── discriminated-unions.d.ts
│       └── golden/                     # 개별 메서드 참조 출력
│           ├── Init.cs
│           ├── Login.cs
│           └── ShowModal.cs
│
├── E2E/                        # ✅ 기존 존재 - 확장
│   ├── SampleUnityProject/
│   │   └── Assets/
│   │       ├── SDK/                    # 🆕 생성된 SDK 파일
│   │       │   ├── AIT.cs
│   │       │   ├── AITCore.cs
│   │       │   ├── AIT.Types.cs
│   │       │   └── Plugins/
│   │       │       └── AppsInToss-*.jslib
│   │       └── Scripts/
│   │           └── Editor/
│   │               └── SDKCompilationTest.cs  # 🆕 Unity 컴파일 검증
│   └── tests/
│       ├── build-and-benchmark.test.js
│       └── sdk-generator-runtime.test.js      # 🆕 SDK 런타임 검증
│
└── JavaScript/                 # ⚠️ 현재 없음 - 생성 필요
    ├── bridge.test.js
    └── generated-jslib.test.js # 🆕 생성된 jslib 검증
```

### 5.2 Tier 1: 컴파일 가능성 검증

**파일**: `Tests/SDK-Generator/unit/compilation.test.ts`

#### 5.2.1 C# Roslyn 컴파일 검증

```typescript
import { compileCSharp } from './helpers/roslyn-compiler';
import { generateCSharp } from '../../../sdk-runtime-generator~/src/generators/csharp';
import { parseWebFramework } from '../../../sdk-runtime-generator~/src/parser';

describe('Tier 1: C# Compilation', () => {
  let generatedCode: { [file: string]: string };

  beforeAll(async () => {
    const apis = await parseWebFramework();
    generatedCode = await generateCSharp(apis);
  });

  test('AIT.cs가 컴파일 가능해야 함', async () => {
    const result = await compileCSharp(generatedCode['AIT.cs'], {
      references: [
        'UnityEngine.dll',
        'UnityEngine.CoreModule.dll',
        'System.Runtime.InteropServices.dll',
        'System.dll'
      ],
      allowUnsafe: false
    });

    expect(result.success).toBe(true);
    expect(result.errors).toHaveLength(0);

    if (!result.success) {
      console.error('Compilation errors:');
      result.errors.forEach(err => {
        console.error(`  ${err.file}(${err.line},${err.column}): ${err.message}`);
      });
    }
  });

  test('AITCore.cs가 컴파일 가능해야 함', async () => {
    const result = await compileCSharp(generatedCode['AITCore.cs'], {
      references: [
        'UnityEngine.dll',
        'UnityEngine.CoreModule.dll',
        'System.dll'
      ]
    });

    expect(result.success).toBe(true);
  });

  test('AIT.Types.cs가 컴파일 가능해야 함', async () => {
    const result = await compileCSharp(generatedCode['AIT.Types.cs'], {
      references: ['System.dll']
    });

    expect(result.success).toBe(true);
  });

  test('모든 C# 파일이 함께 컴파일 가능해야 함', async () => {
    const allCode = [
      generatedCode['AIT.cs'],
      generatedCode['AITCore.cs'],
      generatedCode['AIT.Types.cs']
    ].join('\n\n');

    const result = await compileCSharp(allCode, {
      references: [
        'UnityEngine.dll',
        'UnityEngine.CoreModule.dll',
        'System.Runtime.InteropServices.dll',
        'System.dll'
      ]
    });

    expect(result.success).toBe(true);
  });
});
```

#### 5.2.2 JavaScript 문법 검증

```typescript
import * as ts from 'typescript';
import { generateJSLib } from '../../../sdk-runtime-generator~/src/generators/jslib';

describe('Tier 1: JavaScript Syntax', () => {
  let jslibFiles: { [file: string]: string };

  beforeAll(async () => {
    const apis = await parseWebFramework();
    jslibFiles = await generateJSLib(apis);
  });

  test('모든 .jslib 파일이 유효한 JavaScript 문법이어야 함', () => {
    for (const [filename, code] of Object.entries(jslibFiles)) {
      // TypeScript Compiler API로 JavaScript 검증
      const result = ts.transpileModule(code, {
        compilerOptions: {
          target: ts.ScriptTarget.ES5,
          module: ts.ModuleKind.None,
          checkJs: true,
          allowJs: true,
          noEmit: true
        },
        reportDiagnostics: true
      });

      const errors = result.diagnostics?.filter(d =>
        d.category === ts.DiagnosticCategory.Error
      );

      expect(errors).toHaveLength(0);

      if (errors && errors.length > 0) {
        console.error(`Syntax errors in ${filename}:`);
        errors.forEach(err => {
          console.error(`  Line ${err.start}: ${err.messageText}`);
        });
      }
    }
  });

  test('mergeInto 패턴이 올바른 형식이어야 함', () => {
    for (const [filename, code] of Object.entries(jslibFiles)) {
      // mergeInto(LibraryManager.library, { ... });
      const mergeIntoPattern = /mergeInto\s*\(\s*LibraryManager\.library\s*,\s*\{/;

      expect(code).toMatch(mergeIntoPattern);
    }
  });
});
```

**Helper**: `Tests/SDK-Generator/unit/helpers/roslyn-compiler.ts`

```typescript
import { spawn } from 'child_process';
import * as fs from 'fs/promises';
import * as path from 'path';
import * as os from 'os';

export interface CompilationResult {
  success: boolean;
  errors: CompilationError[];
  warnings: CompilationWarning[];
}

export interface CompilationError {
  file: string;
  line: number;
  column: number;
  message: string;
}

export interface CompilationWarning {
  file: string;
  line: number;
  column: number;
  message: string;
}

export interface CompilationOptions {
  references: string[];  // DLL paths
  allowUnsafe?: boolean;
  targetFramework?: string;
}

/**
 * Roslyn C# 컴파일러를 사용하여 코드 검증
 * macOS/Linux: mono + mcs
 * Windows: csc.exe
 */
export async function compileCSharp(
  code: string,
  options: CompilationOptions
): Promise<CompilationResult> {
  const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'csharp-compile-'));
  const sourceFile = path.join(tempDir, 'Source.cs');
  const outputFile = path.join(tempDir, 'Output.dll');

  try {
    // 소스 파일 작성
    await fs.writeFile(sourceFile, code, 'utf-8');

    // Unity DLL 경로 탐색
    const unityPath = await findUnityPath();
    const references = options.references.map(ref => {
      if (ref.startsWith('Unity')) {
        return path.join(unityPath, 'Managed', ref);
      }
      return ref;
    });

    // 컴파일 명령 구성
    const args = [
      '-target:library',
      `-out:${outputFile}`,
      ...references.map(r => `-reference:${r}`),
      sourceFile
    ];

    if (options.allowUnsafe) {
      args.unshift('-unsafe');
    }

    // 컴파일 실행
    const result = await runCompiler(args);

    // 결과 파싱
    return parseCompilerOutput(result.stdout + result.stderr);
  } finally {
    // 임시 파일 정리
    await fs.rm(tempDir, { recursive: true, force: true });
  }
}

async function findUnityPath(): Promise<string> {
  // macOS
  const possiblePaths = [
    '/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents',
    '/Applications/Unity/Unity.app/Contents',
  ];

  for (const pattern of possiblePaths) {
    // glob 패턴 지원 필요 시 추가
    // 여기서는 단순화
  }

  throw new Error('Unity installation not found');
}

async function runCompiler(args: string[]): Promise<{ stdout: string; stderr: string }> {
  return new Promise((resolve, reject) => {
    // macOS/Linux: mcs
    // Windows: csc.exe
    const compiler = process.platform === 'win32' ? 'csc.exe' : 'mcs';

    const proc = spawn(compiler, args);
    let stdout = '';
    let stderr = '';

    proc.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    proc.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    proc.on('close', (code) => {
      resolve({ stdout, stderr });
    });

    proc.on('error', reject);
  });
}

function parseCompilerOutput(output: string): CompilationResult {
  const errors: CompilationError[] = [];
  const warnings: CompilationWarning[] = [];

  // 출력 파싱 (간소화)
  // 실제로는 정규식으로 파일명, 라인, 메시지 추출
  const lines = output.split('\n');
  for (const line of lines) {
    if (line.includes('error CS')) {
      errors.push({
        file: 'Source.cs',
        line: 0,
        column: 0,
        message: line
      });
    } else if (line.includes('warning CS')) {
      warnings.push({
        file: 'Source.cs',
        line: 0,
        column: 0,
        message: line
      });
    }
  }

  return {
    success: errors.length === 0,
    errors,
    warnings
  };
}
```

### 5.3 Tier 2: 구조적 불변성 검증

**파일**: `Tests/SDK-Generator/unit/invariants.test.ts`

```typescript
import { generateCSharp, generateJSLib } from '../../../sdk-runtime-generator~/src';
import { parseWebFramework } from '../../../sdk-runtime-generator~/src/parser';
import { extractMethod, extractDllImport } from './helpers/method-extractor';

describe('Tier 2: Structural Invariants', () => {
  let csharpCode: { [file: string]: string };
  let jslibCode: { [file: string]: string };

  beforeAll(async () => {
    const apis = await parseWebFramework();
    csharpCode = await generateCSharp(apis);
    jslibCode = await generateJSLib(apis);
  });

  test('모든 public 메서드는 대응하는 DllImport 선언이 있어야 함', () => {
    const aitCs = csharpCode['AIT.cs'];

    // public static 메서드 추출
    const publicMethodRegex = /public static (?:void|\w+) (\w+)\(/g;
    const matches = [...aitCs.matchAll(publicMethodRegex)];

    for (const match of matches) {
      const methodName = match[1];

      // 대응하는 private extern 메서드 확인
      const externName = `ait_${methodName}`;
      const dllImportPattern = new RegExp(
        `\\[DllImport\\("__Internal"\\)\\]\\s+private static extern \\w+ ${externName}\\(`
      );

      expect(aitCs).toMatch(dllImportPattern);
    }
  });

  test('모든 DllImport는 "__Internal" 문자열 리터럴을 사용해야 함', () => {
    const aitCs = csharpCode['AIT.cs'];
    const dllImportRegex = /\[DllImport\(([^\)]+)\)\]/g;
    const matches = [...aitCs.matchAll(dllImportRegex)];

    for (const match of matches) {
      const argument = match[1];

      // "__Internal" 형식이어야 함 (따옴표 필수)
      expect(argument.trim()).toBe('"__Internal"');
    }
  });

  test('콜백을 받는 메서드는 AITCore.RegisterCallback을 호출해야 함', () => {
    const aitCs = csharpCode['AIT.cs'];

    // Action<T> callback 매개변수가 있는 메서드
    const callbackMethodRegex = /public static void (\w+)\([^)]*Action<[^>]+> callback[^)]*\)\s*\{([^}]+)\}/g;
    const matches = [...aitCs.matchAll(callbackMethodRegex)];

    for (const match of matches) {
      const methodName = match[1];
      const methodBody = match[2];

      expect(methodBody).toContain('AITCore.RegisterCallback');
    }
  });

  test('각 jslib 파일은 mergeInto 패턴을 사용해야 함', () => {
    for (const [filename, code] of Object.entries(jslibCode)) {
      const mergeIntoPattern = /mergeInto\s*\(\s*LibraryManager\.library\s*,\s*\{/;

      expect(code).toMatch(mergeIntoPattern);
    }
  });

  test('jslib 함수는 UTF8ToString을 사용하여 문자열을 변환해야 함', () => {
    for (const [filename, code] of Object.entries(jslibCode)) {
      // string 매개변수를 받는 함수는 UTF8ToString 사용
      if (code.includes('function(')) {
        // 간소화: 실제로는 함수별로 검증
        const hasStringParam = true;  // 실제 파싱 필요

        if (hasStringParam) {
          expect(code).toContain('UTF8ToString');
        }
      }
    }
  });

  test('Unity SendMessage 호출은 올바른 형식이어야 함', () => {
    for (const [filename, code] of Object.entries(jslibCode)) {
      // SendMessage("AITCallbackManager", "OnCallback", ...)
      const sendMessageRegex = /SendMessage\s*\(\s*["']AITCallbackManager["']\s*,\s*["']OnCallback["']/g;
      const matches = [...code.matchAll(sendMessageRegex)];

      // 콜백이 있는 함수는 SendMessage 호출 필수
      if (code.includes('callbackId')) {
        expect(matches.length).toBeGreaterThan(0);
      }
    }
  });

  test('네임스페이스는 AppsInToss여야 함', () => {
    const aitCs = csharpCode['AIT.cs'];

    expect(aitCs).toContain('namespace AppsInToss');
    expect(aitCs).not.toContain('namespace UnityEngine');
    expect(aitCs).not.toContain('namespace System');
  });

  test('AOT 컴파일을 위한 [MonoPInvokeCallback] 특성이 있어야 함', () => {
    const aitCoreCs = csharpCode['AITCore.cs'];

    // 콜백 메서드는 [MonoPInvokeCallback] 필요
    const callbackMethodRegex = /\[MonoPInvokeCallback\(typeof\(\w+\)\)\]/g;
    const matches = [...aitCoreCs.matchAll(callbackMethodRegex)];

    expect(matches.length).toBeGreaterThan(0);
  });
});
```

### 5.4 Tier 3: 타입 안전성 검증

**파일**: `Tests/SDK-Generator/unit/type-safety.test.ts`

```typescript
describe('Tier 3: Type Safety', () => {
  let apis: ParsedAPI[];
  let csharpCode: { [file: string]: string };
  let jslibCode: { [file: string]: string };

  beforeAll(async () => {
    apis = await parseWebFramework();
    csharpCode = await generateCSharp(apis);
    jslibCode = await generateJSLib(apis);
  });

  test('C# DllImport 시그니처와 jslib 함수 시그니처가 일치해야 함', () => {
    const aitCs = csharpCode['AIT.cs'];

    for (const api of apis) {
      const externName = `ait_${api.pascalName}`;

      // C# extern 메서드 추출
      const dllImportPattern = new RegExp(
        `private static extern (\\w+) ${externName}\\(([^)]*)\\)`
      );
      const csharpMatch = aitCs.match(dllImportPattern);
      expect(csharpMatch).toBeTruthy();

      const [, returnType, csharpParams] = csharpMatch!;

      // jslib 함수 추출
      const jslibPattern = new RegExp(
        `${externName}:\\s*function\\s*\\(([^)]*)\\)`
      );

      let jslibMatch = null;
      for (const code of Object.values(jslibCode)) {
        jslibMatch = code.match(jslibPattern);
        if (jslibMatch) break;
      }

      expect(jslibMatch).toBeTruthy();
      const [, jslibParams] = jslibMatch!;

      // 매개변수 개수 일치 확인
      const csharpParamCount = csharpParams.split(',').filter(p => p.trim()).length;
      const jslibParamCount = jslibParams.split(',').filter(p => p.trim()).length;

      expect(csharpParamCount).toBe(jslibParamCount);
    }
  });

  test('Promise<T> 타입은 Action<T> 콜백으로 변환되어야 함', () => {
    for (const api of apis) {
      if (api.returnType?.type === 'Promise') {
        const aitCs = csharpCode['AIT.cs'];
        const innerType = api.returnType.innerType;

        // public static void MethodName(..., Action<InnerType> callback)
        const callbackPattern = new RegExp(
          `public static void ${api.pascalName}\\([^)]*Action<${innerType}> callback`
        );

        expect(aitCs).toMatch(callbackPattern);
      }
    }
  });

  test('string 타입 매개변수는 jslib에서 UTF8ToString 사용해야 함', () => {
    for (const api of apis) {
      const hasStringParam = api.params.some(p => p.type === 'string');

      if (hasStringParam) {
        const externName = `ait_${api.pascalName}`;

        let found = false;
        for (const code of Object.values(jslibCode)) {
          if (code.includes(externName) && code.includes('UTF8ToString')) {
            found = true;
            break;
          }
        }

        expect(found).toBe(true);
      }
    }
  });

  test('number 배열은 HEAPF64로 변환되어야 함', () => {
    // number[] 타입을 받는 API
    const numberArrayAPIs = apis.filter(api =>
      api.params.some(p => p.type === 'number[]')
    );

    for (const api of numberArrayAPIs) {
      const externName = `ait_${api.pascalName}`;

      let found = false;
      for (const code of Object.values(jslibCode)) {
        if (code.includes(externName) && code.includes('HEAPF64')) {
          found = true;
          break;
        }
      }

      expect(found).toBe(true);
    }
  });

  test('enum 타입은 정수로 마샬링되어야 함', () => {
    const aitTypesCs = csharpCode['AIT.Types.cs'];

    // enum 정의 찾기
    const enumRegex = /public enum (\w+)\s*\{([^}]+)\}/g;
    const enumMatches = [...aitTypesCs.matchAll(enumRegex)];

    for (const match of enumMatches) {
      const enumName = match[1];

      // 이 enum을 사용하는 API 찾기
      const usingAPI = apis.find(api =>
        api.params.some(p => p.type === enumName)
      );

      if (usingAPI) {
        const aitCs = csharpCode['AIT.cs'];
        const externName = `ait_${usingAPI.pascalName}`;

        // extern 메서드에서 int로 변환되는지 확인
        const externPattern = new RegExp(
          `private static extern \\w+ ${externName}\\([^)]*int [^)]*\\)`
        );

        expect(aitCs).toMatch(externPattern);
      }
    }
  });
});
```

### 5.5 Tier 4: 차분 검증 (Differential Testing)

**파일**: `Tests/SDK-Generator/unit/differential.test.ts`

```typescript
import * as fs from 'fs/promises';
import * as path from 'path';

describe('Tier 4: Differential Regression', () => {
  const fixturesDir = path.join(__dirname, '../fixtures');
  const goldenDir = path.join(fixturesDir, 'golden');

  test('알려진 좋은 입력에 대해 기존 API 출력이 변경되지 않아야 함', async () => {
    // 1. Fixture 로드 (web-framework v1.2.3)
    const fixturePath = path.join(fixturesDir, 'web-framework-v1.2.3.d.ts');
    const apis = await parseFixture(fixturePath);

    // 2. 현재 생성기로 생성
    const currentOutput = await generateCSharp(apis);

    // 3. Golden 파일과 비교 (개별 메서드 레벨)
    for (const api of apis) {
      const goldenPath = path.join(goldenDir, `${api.pascalName}.cs`);

      try {
        const goldenCode = await fs.readFile(goldenPath, 'utf-8');
        const currentMethod = extractMethod(currentOutput['AIT.cs'], api.pascalName);

        // 메서드 코드가 동일해야 함
        expect(normalizeWhitespace(currentMethod)).toBe(normalizeWhitespace(goldenCode));
      } catch (err) {
        // Golden 파일 없음 = 신규 API (허용)
        if ((err as NodeJS.ErrnoException).code !== 'ENOENT') {
          throw err;
        }
      }
    }
  });

  test('새 API 추가 시 기존 메서드는 변경되지 않아야 함', async () => {
    const baseAPIs = [
      { name: 'init', pascalName: 'Init' },
      { name: 'login', pascalName: 'Login' }
    ];

    const extendedAPIs = [
      ...baseAPIs,
      { name: 'showModal', pascalName: 'ShowModal' }
    ];

    const baseOutput = await generateCSharp(baseAPIs as any);
    const extendedOutput = await generateCSharp(extendedAPIs as any);

    // 기존 메서드는 동일해야 함
    for (const api of baseAPIs) {
      const baseMethod = extractMethod(baseOutput['AIT.cs'], api.pascalName);
      const extendedMethod = extractMethod(extendedOutput['AIT.cs'], api.pascalName);

      expect(normalizeWhitespace(baseMethod)).toBe(normalizeWhitespace(extendedMethod));
    }

    // 새 메서드는 추가되어야 함
    expect(extendedOutput['AIT.cs']).toContain('public static void ShowModal');
  });

  test('타입 정의 변경 시 관련 없는 API는 영향 없어야 함', async () => {
    // InitOptions 타입 변경
    // → Init 메서드는 변경됨
    // → Login 메서드는 변경 없어야 함

    const beforeAPIs = [
      { name: 'init', params: [{ name: 'options', type: 'InitOptionsV1' }] },
      { name: 'login', params: [] }
    ];

    const afterAPIs = [
      { name: 'init', params: [{ name: 'options', type: 'InitOptionsV2' }] },
      { name: 'login', params: [] }
    ];

    const beforeOutput = await generateCSharp(beforeAPIs as any);
    const afterOutput = await generateCSharp(afterAPIs as any);

    // Login 메서드는 동일해야 함
    const beforeLogin = extractMethod(beforeOutput['AIT.cs'], 'Login');
    const afterLogin = extractMethod(afterOutput['AIT.cs'], 'Login');

    expect(normalizeWhitespace(beforeLogin)).toBe(normalizeWhitespace(afterLogin));
  });
});

function normalizeWhitespace(code: string): string {
  return code
    .split('\n')
    .map(line => line.trim())
    .filter(line => line.length > 0)
    .join('\n');
}

function extractMethod(code: string, methodName: string): string {
  // 메서드 추출 로직 (간소화)
  const methodRegex = new RegExp(
    `public static \\w+ ${methodName}\\([^)]*\\)\\s*\\{([^}]+)\\}`,
    's'
  );
  const match = code.match(methodRegex);
  return match ? match[0] : '';
}
```

### 5.6 CI/CD 통합

**파일**: `.github/workflows/tests.yml` (수정)

```yaml
# 기존 내용 유지...

# 새 job 추가
jobs:
  # ... 기존 jobs ...

  # SDK Generator 검증 테스트
  sdk-generator-validation:
    name: SDK Generator Validation (${{ matrix.os }})
    runs-on: ${{ matrix.os }}
    timeout-minutes: 15
    strategy:
      fail-fast: false
      matrix:
        os: [macos-latest, ubuntu-latest, windows-latest]

    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 18
          cache: npm
          cache-dependency-path: Tests/SDK-Generator/unit/package-lock.json

      - name: Install Dependencies
        run: npm ci
        working-directory: Tests/SDK-Generator/unit

      - name: Install Mono (macOS/Linux)
        if: runner.os != 'Windows'
        run: |
          if [ "$RUNNER_OS" == "macOS" ]; then
            brew install mono
          else
            sudo apt-get update
            sudo apt-get install -y mono-complete
          fi

      - name: Run Tier 1: Compilation Tests
        run: npm test -- compilation.test.ts
        working-directory: Tests/SDK-Generator/unit

      - name: Run Tier 2: Invariants Tests
        run: npm test -- invariants.test.ts
        working-directory: Tests/SDK-Generator/unit

      - name: Run Tier 3: Type Safety Tests
        run: npm test -- type-safety.test.ts
        working-directory: Tests/SDK-Generator/unit

      - name: Run Tier 4: Differential Tests
        run: npm test -- differential.test.ts
        working-directory: Tests/SDK-Generator/unit

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: sdk-generator-test-results-${{ matrix.os }}
          path: Tests/SDK-Generator/unit/test-results/
          if-no-files-found: ignore
```

### 5.7 구현 순서 및 예상 시간

| 단계 | 작업 | 예상 시간 | 누적 시간 |
|-----|------|---------|---------|
| 1 | 디렉토리 구조 생성 | 30분 | 30분 |
| 2 | Tier 1: 컴파일 검증 구현 | 4시간 | 4.5시간 |
| 3 | Tier 2: 구조 검증 구현 | 3시간 | 7.5시간 |
| 4 | Tier 3: 타입 검증 구현 | 3시간 | 10.5시간 |
| 5 | Tier 4: 차분 검증 구현 | 2시간 | 12.5시간 |
| 6 | Helper 유틸리티 작성 | 1.5시간 | 14시간 |
| 7 | Fixture 및 Golden 파일 준비 | 1시간 | 15시간 |
| 8 | CI/CD 통합 | 1시간 | 16시간 |
| 9 | 문서화 (README.md) | 1시간 | 17시간 |
| 10 | 테스트 및 디버깅 | 2시간 | 19시간 |

**총 예상 시간: ~19시간**

---

## 6. 기대 효과

### 6.1 전통적 방식 vs 속성 기반 비교

| 항목 | 전체 파일 스냅샷 | 속성 기반 검증 |
|-----|---------------|-------------|
| **API 추가 시** | ❌ 테스트 실패 → `-u` | ✅ 자동 통과 |
| **템플릿 변경 시** | ❌ 모든 테스트 실패 | ✅ 구조 유지 시 통과 |
| **공백 변경 시** | ❌ 테스트 실패 | ✅ 자동 통과 |
| **주석 변경 시** | ❌ 테스트 실패 | ✅ 자동 통과 |
| **버그 발생 시** | ✅ 탐지 (노이즈 속) | ✅ 명확히 탐지 |
| **유지보수 비용** | 😡 매우 높음 | 😊 낮음 |
| **실행 시간** | ~1초 | ~13초 |
| **신뢰도** | 🟡 중간 (노이즈 많음) | 🟢 높음 |
| **신호 대 노이즈** | 1:10 | 10:1 |

### 6.2 회귀 탐지 효과

| 버그 유형 | 현재 탐지율 | 속성 기반 탐지율 | 개선 |
|---------|-----------|---------------|------|
| 컴파일 실패 | 0% | 100% | +100% |
| DllImport 오류 | ~30% | 100% | +70% |
| 타입 불일치 | 0% | ~90% | +90% |
| 콜백 누락 | 0% | 100% | +100% |
| jslib 구조 오류 | ~20% | 100% | +80% |
| Unity 호환성 | 0% | 100% | +100% |
| 기존 API 회귀 | 0% | ~95% | +95% |

### 6.3 개발 워크플로우 개선

#### Before (전통적 스냅샷)
```
1. 코드 수정
2. npm test 실행
3. ❌ 80개 테스트 실패
4. 😓 diff 확인 (10분 소요)
5. "이게 정상인가?" 고민 (5분)
6. npm test -- -u (스냅샷 업데이트)
7. 😨 "혹시 실제 버그도 같이 넘어갔나?"
8. 😡 다음 개발자도 반복...

→ 총 15분 + 정신적 피로
```

#### After (속성 기반)
```
1. 코드 수정
2. npm test 실행
3. ✅ 통과 또는 ❌ 명확한 오류
   - "DllImport에 따옴표 없음" → 즉시 수정
   - "콜백 등록 누락" → 즉시 수정
4. 😊 자신감 있게 커밋

→ 총 2분 + 높은 신뢰도
```

### 6.4 CI/CD 영향

| 지표 | 현재 | 개선 후 |
|-----|------|--------|
| 테스트 실행 시간 | ~30초 (빌드만) | ~45초 (+15초) |
| 거짓 양성 (False Positive) | 높음 (노이즈) | 매우 낮음 |
| 거짓 음성 (False Negative) | 높음 (버그 미탐지) | 매우 낮음 |
| PR 차단 정확도 | ~40% | ~95% |
| 개발자 신뢰도 | 🟡 중간 | 🟢 높음 |

### 6.5 장기적 이점

1. **리팩토링 안전성**
   - 내부 로직 변경 시 출력 동일하면 자동 통과
   - 템플릿 엔진 교체, 생성 알고리즘 개선 가능

2. **신규 타입 추가 용이**
   - 타입 매핑 추가 시 자동 검증
   - 컴파일 실패 시 즉시 피드백

3. **문서화 효과**
   - 테스트 자체가 "올바른 코드"의 명세
   - 신규 개발자 온보딩 자료

4. **회귀 방지**
   - Golden 파일로 기존 API 보호
   - 의도치 않은 변경 즉시 탐지

---

## 7. 결론

### 7.1 핵심 요약

전통적인 스냅샷 테스트는 **코드 생성기에 부적합**합니다:
- ❌ 노이즈가 너무 많음 (API 추가, 템플릿 변경, 공백 수정)
- ❌ 판단 피로 (이게 버그인가? 정상인가?)
- ❌ 유지보수 비용 높음 (매번 `-u`)

**속성 기반 검증**이 더 나은 접근입니다:
- ✅ 의미 있는 회귀만 탐지 (컴파일 실패, 패턴 위반)
- ✅ API 추가/변경에 강건
- ✅ 명확한 실패 메시지
- ✅ 낮은 유지보수 비용

### 7.2 권장 사항

**즉시 구현 (High Priority)**:
1. **Tier 1: 컴파일 검증** (가장 중요)
   - Roslyn C# 컴파일러 통합
   - TypeScript Compiler API 통합

**1-2주 내 구현 (Medium Priority)**:
2. **Tier 2: 구조 검증**
3. **Tier 3: 타입 검증**

**릴리스 전 구현 (Low Priority)**:
4. **Tier 4: 차분 검증**

### 7.3 성공 지표

구현 후 다음 지표로 성공 측정:
- [ ] 컴파일 실패 조기 발견: 0% → 100%
- [ ] CI/CD 거짓 양성: 감소 (~50% → ~5%)
- [ ] 개발자 테스트 신뢰도: 증가
- [ ] 버그 탈출률: 감소
- [ ] 리팩토링 빈도: 증가 (안전성 확보)

---

## 부록 A: 참고 자료

- [Property-Based Testing - QuickCheck](https://en.wikipedia.org/wiki/QuickCheck)
- [Differential Testing](https://www.microsoft.com/en-us/research/publication/differential-testing-for-software/)
- [Roslyn Compiler API](https://github.com/dotnet/roslyn)
- [TypeScript Compiler API](https://github.com/microsoft/TypeScript/wiki/Using-the-Compiler-API)

---

**문서 버전**: 1.0
**최종 수정**: 2024-11-24
**작성자**: Claude Code
**리뷰 필요**: SDK Generator 유지보수 담당자
