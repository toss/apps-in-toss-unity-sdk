/**
 * vite-plugin-unity-bridge.ts
 *
 * Vite 플러그인: bridges/ 폴더의 TypeScript 함수를 Unity C# + jslib로 자동 변환
 *
 * 사용법:
 * 1. bridges/ 폴더에 TypeScript 파일 작성
 * 2. export 함수 정의 (async 함수도 지원)
 * 3. 빌드 시 자동으로 C# 바인딩 + jslib 생성
 *
 * 네임스페이스:
 * - bridges/analytics/firebase.ts → Bridges.Analytics.Firebase
 * - bridges/utils.ts → Bridges.Utils
 */

import { Plugin } from 'vite';
import { Project, SyntaxKind, FunctionDeclaration, SourceFile, Type } from 'ts-morph';
import * as fs from 'fs';
import * as path from 'path';

// ============================================================================
// 타입 정의
// ============================================================================

interface BridgeParam {
  name: string;
  type: string;
  optional: boolean;
}

interface BridgeFunction {
  name: string;
  params: BridgeParam[];
  returnType: string;
  isAsync: boolean;
  jsDoc?: string;
}

interface BridgeModule {
  /** 원본 파일 경로 (상대 경로) */
  filePath: string;
  /** 네임스페이스 배열 (예: ['Analytics', 'Firebase']) */
  namespace: string[];
  /** 함수 목록 */
  functions: BridgeFunction[];
}

interface BridgeManifest {
  version: string;
  generatedAt: string;
  modules: BridgeModule[];
}

// ============================================================================
// 타입 매핑
// ============================================================================

/** TypeScript → C# 타입 매핑 */
function mapToCSharpType(tsType: string): string {
  // 기본 타입
  const typeMap: Record<string, string> = {
    'string': 'string',
    'number': 'double',
    'boolean': 'bool',
    'void': 'void',
  };

  // Promise<T> → T
  const promiseMatch = tsType.match(/^Promise<(.+)>$/);
  if (promiseMatch) {
    return mapToCSharpType(promiseMatch[1]);
  }

  // T[] → T[]
  const arrayMatch = tsType.match(/^(.+)\[\]$/);
  if (arrayMatch) {
    return `${mapToCSharpType(arrayMatch[1])}[]`;
  }

  // Record<string, T> → Dictionary<string, T>
  const recordMatch = tsType.match(/^Record<string,\s*(.+)>$/);
  if (recordMatch) {
    return `Dictionary<string, ${mapToCSharpType(recordMatch[1])}>`;
  }

  // T | undefined, T | null → T (optional handled separately)
  const unionMatch = tsType.match(/^(.+)\s*\|\s*(undefined|null)$/);
  if (unionMatch) {
    return mapToCSharpType(unionMatch[1]);
  }

  return typeMap[tsType] || 'object';
}

/** TypeScript → jslib 인수 처리 타입 */
function getJsArgProcessing(param: BridgeParam): { argType: string; processing: string } {
  const baseType = param.type.replace(/\?$/, '').replace(/\s*\|\s*(undefined|null)$/, '');

  if (baseType === 'string') {
    return { argType: 'ptr', processing: `UTF8ToString(${param.name})` };
  }
  if (baseType === 'number') {
    return { argType: 'number', processing: param.name };
  }
  if (baseType === 'boolean') {
    return { argType: 'number', processing: `${param.name} !== 0` };
  }
  // 복합 타입은 JSON 문자열로 전달
  return { argType: 'ptr', processing: `JSON.parse(UTF8ToString(${param.name}))` };
}

// ============================================================================
// TypeScript 파서
// ============================================================================

function parseBridgesFolder(bridgesDir: string): BridgeModule[] {
  const modules: BridgeModule[] = [];

  if (!fs.existsSync(bridgesDir)) {
    return modules;
  }

  const project = new Project({
    compilerOptions: {
      strict: true,
      target: 99, // ESNext
      module: 99, // ESNext
    },
  });

  // 재귀적으로 .ts 파일 탐색
  function scanDir(dir: string, namespace: string[] = []) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);

      if (entry.isDirectory()) {
        // 디렉토리 이름을 네임스페이스에 추가 (PascalCase 변환)
        const nsName = toPascalCase(entry.name);
        scanDir(fullPath, [...namespace, nsName]);
      } else if (entry.name.endsWith('.ts') && !entry.name.endsWith('.d.ts')) {
        // TypeScript 파일 파싱
        const sourceFile = project.addSourceFileAtPath(fullPath);
        const functions = extractExportedFunctions(sourceFile);

        if (functions.length > 0) {
          // 파일 이름에서 모듈 이름 추출 (PascalCase 변환)
          const moduleName = toPascalCase(entry.name.replace(/\.ts$/, ''));

          modules.push({
            filePath: path.relative(bridgesDir, fullPath),
            namespace: [...namespace, moduleName],
            functions,
          });
        }
      }
    }
  }

  scanDir(bridgesDir);
  return modules;
}

function extractExportedFunctions(sourceFile: SourceFile): BridgeFunction[] {
  const functions: BridgeFunction[] = [];

  // export function 추출
  sourceFile.getFunctions().forEach((func) => {
    if (!func.isExported()) return;

    const funcInfo = extractFunctionInfo(func);
    if (funcInfo) {
      functions.push(funcInfo);
    }
  });

  // export default도 지원 (defineBridge 패턴 용)
  // TODO: 필요시 구현

  return functions;
}

function extractFunctionInfo(func: FunctionDeclaration): BridgeFunction | null {
  const name = func.getName();
  if (!name) return null;

  // 파라미터 추출
  const params: BridgeParam[] = func.getParameters().map((param) => {
    const paramType = param.getType();
    const typeText = simplifyType(paramType.getText());

    return {
      name: param.getName(),
      type: typeText,
      optional: param.isOptional() || param.hasQuestionToken(),
    };
  });

  // 반환 타입 추출
  const returnType = func.getReturnType();
  const returnTypeText = simplifyType(returnType.getText());

  // async 함수인지 또는 Promise를 반환하는지 확인
  const isAsync = func.isAsync() || returnTypeText.startsWith('Promise<');

  // JSDoc 추출
  const jsDocNodes = func.getJsDocs();
  const jsDoc = jsDocNodes.length > 0 ? jsDocNodes[0].getDescription() : undefined;

  return {
    name,
    params,
    returnType: returnTypeText,
    isAsync,
    jsDoc,
  };
}

function simplifyType(typeText: string): string {
  // import("...").SomeType → SomeType
  return typeText.replace(/import\([^)]+\)\./g, '');
}

function toPascalCase(str: string): string {
  return str
    .split(/[-_]/)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join('');
}

// ============================================================================
// 코드 생성기
// ============================================================================

function generateManifest(modules: BridgeModule[]): BridgeManifest {
  return {
    version: '1.0.0',
    generatedAt: new Date().toISOString(),
    modules,
  };
}

function generateCSharpCode(manifest: BridgeManifest): string {
  const lines: string[] = [];

  lines.push('// -----------------------------------------------------------------------');
  lines.push('// <auto-generated>');
  lines.push('//     This file is auto-generated by vite-plugin-unity-bridge.');
  lines.push('//     Do not modify directly. 이 파일은 자동 생성되었습니다.');
  lines.push('// </auto-generated>');
  lines.push('// -----------------------------------------------------------------------');
  lines.push('');
  lines.push('using System;');
  lines.push('using System.Collections.Generic;');
  lines.push('using System.Threading.Tasks;');
  lines.push('using Newtonsoft.Json;');
  lines.push('using UnityEngine.Scripting;');
  lines.push('');
  lines.push('namespace AppsInToss.Bridges');
  lines.push('{');

  // 각 모듈에 대해 클래스 생성
  for (const module of manifest.modules) {
    const className = module.namespace.join('_');
    const fullNamespace = module.namespace.join('.');

    lines.push(`    /// <summary>`);
    lines.push(`    /// Custom Bridge: ${fullNamespace}`);
    lines.push(`    /// Source: ${module.filePath}`);
    lines.push(`    /// </summary>`);
    lines.push(`    [Preserve]`);
    lines.push(`    public static class ${className}`);
    lines.push(`    {`);

    for (const func of module.functions) {
      lines.push(...generateCSharpMethod(func, className));
      lines.push('');
    }

    lines.push(`    }`);
    lines.push('');
  }

  lines.push('}');

  return lines.join('\n');
}

function generateCSharpMethod(func: BridgeFunction, className: string): string[] {
  const lines: string[] = [];
  const methodName = toPascalCase(func.name);
  const internalFuncName = `__CustomBridge_${className}_${func.name}_Internal`;

  // 반환 타입 결정
  let returnType = mapToCSharpType(func.returnType);
  const isVoid = returnType === 'void';

  // async 함수는 Task로 래핑
  const taskReturnType = isVoid ? 'Task' : `Task<${returnType}>`;

  // JSDoc 주석
  if (func.jsDoc) {
    lines.push(`        /// <summary>${func.jsDoc}</summary>`);
  }

  // 파라미터 문자열 생성
  const paramStrings = func.params.map((p) => {
    const csType = mapToCSharpType(p.type);
    const nullable = p.optional ? '?' : '';
    // value type에만 nullable 적용
    const needsNullable = p.optional && (csType === 'double' || csType === 'bool');
    return `${csType}${needsNullable ? '?' : ''} ${p.name}${p.optional && !needsNullable ? ' = null' : ''}`;
  });

  lines.push(`        [Preserve]`);
  lines.push(`        public static async ${taskReturnType} ${methodName}(${paramStrings.join(', ')})`);
  lines.push(`        {`);
  lines.push(`#if UNITY_WEBGL && !UNITY_EDITOR`);

  // TaskCompletionSource 생성
  if (isVoid) {
    lines.push(`            var tcs = new TaskCompletionSource<object>();`);
    lines.push(`            string callbackId = AITCore.Instance.RegisterCallback<object>(`);
    lines.push(`                result => tcs.TrySetResult(null),`);
  } else {
    lines.push(`            var tcs = new TaskCompletionSource<${returnType}>();`);
    lines.push(`            string callbackId = AITCore.Instance.RegisterCallback<${returnType}>(`);
    lines.push(`                result => tcs.TrySetResult(result),`);
  }
  lines.push(`                error => tcs.TrySetException(error)`);
  lines.push(`            );`);

  // jslib 함수 호출
  const callArgs = func.params.map((p) => {
    const csType = mapToCSharpType(p.type);
    // 복합 타입은 JSON 직렬화
    if (csType.startsWith('Dictionary') || csType.endsWith('[]') || csType === 'object') {
      return `JsonConvert.SerializeObject(${p.name})`;
    }
    if (csType === 'bool') {
      return p.optional ? `(${p.name} ?? false) ? 1 : 0` : `${p.name} ? 1 : 0`;
    }
    if (csType === 'double' && p.optional) {
      return `${p.name} ?? 0`;
    }
    return p.name;
  });

  // 타입 이름 결정 (콜백 라우팅용)
  const typeName = isVoid ? 'void' : returnType;

  lines.push(`            ${internalFuncName}(${[...callArgs, 'callbackId', `"${typeName}"`].join(', ')});`);

  if (isVoid) {
    lines.push(`            await tcs.Task;`);
  } else {
    lines.push(`            return await tcs.Task;`);
  }

  lines.push(`#else`);
  lines.push(`            // Unity Editor mock implementation`);
  lines.push(`            UnityEngine.Debug.Log($"[CustomBridge Mock] ${className}.${methodName} called");`);
  lines.push(`            await Task.CompletedTask;`);
  if (!isVoid) {
    lines.push(`            return default;`);
  }
  lines.push(`#endif`);
  lines.push(`        }`);
  lines.push('');

  // DllImport 선언
  lines.push(`#if UNITY_WEBGL && !UNITY_EDITOR`);
  const dllImportParams = func.params.map((p) => {
    const csType = mapToCSharpType(p.type);
    if (csType === 'string' || csType.startsWith('Dictionary') || csType.endsWith('[]') || csType === 'object') {
      return `string ${p.name}`;
    }
    if (csType === 'bool') {
      return `int ${p.name}`;
    }
    return `${csType} ${p.name}`;
  });
  lines.push(`        [System.Runtime.InteropServices.DllImport("__Internal")]`);
  lines.push(`        private static extern void ${internalFuncName}(${[...dllImportParams, 'string callbackId', 'string typeName'].join(', ')});`);
  lines.push(`#endif`);

  return lines;
}

function generateJslibCode(manifest: BridgeManifest): string {
  const lines: string[] = [];

  lines.push('/**');
  lines.push(' * CustomBridges.jslib');
  lines.push(' *');
  lines.push(' * This file is auto-generated by vite-plugin-unity-bridge.');
  lines.push(' * Do not modify directly. 이 파일은 자동 생성되었습니다.');
  lines.push(' */');
  lines.push('');
  lines.push('mergeInto(LibraryManager.library, {');

  const funcDefs: string[] = [];

  for (const module of manifest.modules) {
    const className = module.namespace.join('_');

    for (const func of module.functions) {
      funcDefs.push(generateJslibFunction(func, className, module.namespace));
    }
  }

  lines.push(funcDefs.join(',\n\n'));
  lines.push('});');

  return lines.join('\n');
}

function generateJslibFunction(func: BridgeFunction, className: string, namespace: string[]): string {
  const internalFuncName = `__CustomBridge_${className}_${func.name}_Internal`;
  const lines: string[] = [];

  // 파라미터 목록 생성 (+ callbackId, typeName)
  const paramNames = [...func.params.map((p) => p.name), 'callbackId', 'typeName'];

  lines.push(`    ${internalFuncName}: function(${paramNames.join(', ')}) {`);
  lines.push(`        var callback = UTF8ToString(callbackId);`);
  lines.push(`        var typeNameStr = UTF8ToString(typeName);`);
  lines.push('');

  // 파라미터 처리
  for (const param of func.params) {
    const { processing } = getJsArgProcessing(param);
    lines.push(`        var ${param.name}_val = ${processing};`);
  }

  // window.Bridges 경로 구성
  const bridgePath = ['window', 'Bridges', ...namespace].join('.');
  const funcCall = `${bridgePath}.${func.name}`;
  const callArgs = func.params.map((p) => `${p.name}_val`).join(', ');

  lines.push('');
  lines.push('        try {');

  if (func.isAsync) {
    // 비동기 함수
    lines.push(`            var promiseResult = ${funcCall}(${callArgs});`);
    lines.push('');
    lines.push('            if (!promiseResult || typeof promiseResult.then !== "function") {');
    lines.push('                // Promise가 아닌 경우 즉시 응답');
    lines.push('                var payload = JSON.stringify({');
    lines.push('                    CallbackId: callback,');
    lines.push('                    TypeName: typeNameStr,');
    lines.push('                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: "" })');
    lines.push('                });');
    lines.push('                SendMessage("AITCore", "OnAITCallback", payload);');
    lines.push('                return;');
    lines.push('            }');
    lines.push('');
    lines.push('            promiseResult');
    lines.push('                .then(function(result) {');
    lines.push('                    var payload = JSON.stringify({');
    lines.push('                        CallbackId: callback,');
    lines.push('                        TypeName: typeNameStr,');
    lines.push('                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: "" })');
    lines.push('                    });');
    lines.push('                    SendMessage("AITCore", "OnAITCallback", payload);');
    lines.push('                })');
    lines.push('                .catch(function(error) {');
    lines.push('                    var payload = JSON.stringify({');
    lines.push('                        CallbackId: callback,');
    lines.push('                        TypeName: typeNameStr,');
    lines.push('                        Result: JSON.stringify({ success: false, data: "", error: error.message || String(error) })');
    lines.push('                    });');
    lines.push('                    SendMessage("AITCore", "OnAITCallback", payload);');
    lines.push('                });');
  } else {
    // 동기 함수
    lines.push(`            var result = ${funcCall}(${callArgs});`);
    lines.push('            var payload = JSON.stringify({');
    lines.push('                CallbackId: callback,');
    lines.push('                TypeName: typeNameStr,');
    lines.push('                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: "" })');
    lines.push('            });');
    lines.push('            SendMessage("AITCore", "OnAITCallback", payload);');
  }

  lines.push('        } catch (error) {');
  lines.push('            var payload = JSON.stringify({');
  lines.push('                CallbackId: callback,');
  lines.push('                TypeName: typeNameStr,');
  lines.push('                Result: JSON.stringify({ success: false, data: "", error: error.message || String(error) })');
  lines.push('            });');
  lines.push('            SendMessage("AITCore", "OnAITCallback", payload);');
  lines.push('        }');
  lines.push('    }');

  return lines.join('\n');
}

function generateBridgesEntryCode(manifest: BridgeManifest): string {
  const lines: string[] = [];

  lines.push('/**');
  lines.push(' * bridges-entry.ts');
  lines.push(' *');
  lines.push(' * Auto-generated entry point for custom bridges.');
  lines.push(' * Exposes all bridge functions to window.Bridges');
  lines.push(' */');
  lines.push('');

  // 모든 브릿지 모듈 import
  for (const module of manifest.modules) {
    const importName = module.namespace.join('_');
    const importPath = './' + module.filePath.replace(/\.ts$/, '');
    lines.push(`import * as ${importName} from '${importPath}';`);
  }

  lines.push('');
  lines.push('// window.Bridges 객체 구성');
  lines.push('declare global {');
  lines.push('  interface Window {');
  lines.push('    Bridges: Record<string, unknown>;');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('window.Bridges = window.Bridges || {};');
  lines.push('');

  // 네임스페이스 구조 생성
  for (const module of manifest.modules) {
    const importName = module.namespace.join('_');
    let currentPath = 'window.Bridges';

    // 중첩 네임스페이스 생성
    for (let i = 0; i < module.namespace.length - 1; i++) {
      const ns = module.namespace[i];
      currentPath += `.${ns}`;
      lines.push(`${currentPath} = ${currentPath} || {};`);
    }

    // 최종 모듈 할당
    const fullPath = ['window', 'Bridges', ...module.namespace].join('.');
    lines.push(`${fullPath} = ${importName};`);
  }

  lines.push('');
  lines.push('console.log("[CustomBridges] Registered:", Object.keys(window.Bridges));');

  return lines.join('\n');
}

// ============================================================================
// Vite 플러그인
// ============================================================================

export interface UnityBridgePluginOptions {
  /** bridges 폴더 경로 (기본: ./bridges) */
  bridgesDir?: string;
  /** 출력 폴더 경로 (기본: ./generated) */
  outputDir?: string;
  /** C# 출력 경로 (Unity 프로젝트 기준, 빌드 시 복사됨) */
  csharpOutputPath?: string;
  /** jslib 출력 경로 (Unity 프로젝트 기준, 빌드 시 복사됨) */
  jslibOutputPath?: string;
}

export function unityBridgePlugin(options: UnityBridgePluginOptions = {}): Plugin {
  const bridgesDir = options.bridgesDir || './bridges';
  const outputDir = options.outputDir || './generated';

  let generatedEntryPath: string | null = null;

  return {
    name: 'vite-plugin-unity-bridge',

    buildStart() {
      // bridges 폴더가 없으면 스킵
      if (!fs.existsSync(bridgesDir)) {
        console.log('[unity-bridge] No bridges/ folder found, skipping...');
        return;
      }

      console.log('[unity-bridge] Scanning bridges folder...');

      // TypeScript 파싱
      const modules = parseBridgesFolder(bridgesDir);

      if (modules.length === 0) {
        console.log('[unity-bridge] No bridge modules found.');
        return;
      }

      console.log(`[unity-bridge] Found ${modules.length} bridge module(s):`);
      for (const mod of modules) {
        console.log(`  - ${mod.namespace.join('.')} (${mod.functions.length} functions)`);
      }

      // 출력 폴더 생성
      if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
      }

      // Manifest 생성
      const manifest = generateManifest(modules);
      fs.writeFileSync(
        path.join(outputDir, 'bridge-manifest.json'),
        JSON.stringify(manifest, null, 2)
      );

      // C# 코드 생성
      const csharpCode = generateCSharpCode(manifest);
      fs.writeFileSync(path.join(outputDir, 'CustomBridges.cs'), csharpCode);

      // jslib 코드 생성
      const jslibCode = generateJslibCode(manifest);
      fs.writeFileSync(path.join(outputDir, 'CustomBridges.jslib'), jslibCode);

      // 브릿지 엔트리 코드 생성
      const entryCode = generateBridgesEntryCode(manifest);
      generatedEntryPath = path.join(outputDir, 'bridges-entry.ts');
      fs.writeFileSync(generatedEntryPath, entryCode);

      console.log('[unity-bridge] Generated files:');
      console.log(`  - ${outputDir}/bridge-manifest.json`);
      console.log(`  - ${outputDir}/CustomBridges.cs`);
      console.log(`  - ${outputDir}/CustomBridges.jslib`);
      console.log(`  - ${outputDir}/bridges-entry.ts`);
    },

    transform(code, id) {
      // index.html 또는 main entry에 브릿지 엔트리 자동 주입
      // 현재는 수동으로 import 해야 함
      return null;
    },

    configureServer(server) {
      // 개발 서버에서 bridges 폴더 변경 감지
      server.watcher.add(bridgesDir);
      server.watcher.on('change', (file) => {
        if (file.includes(bridgesDir)) {
          console.log('[unity-bridge] Bridge file changed, regenerating...');
          // TODO: 핫 리로드 지원
        }
      });
    },
  };
}

export default unityBridgePlugin;
