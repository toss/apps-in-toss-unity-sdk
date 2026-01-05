using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 커스텀 브릿지 생성기
    /// bridges/ 폴더의 TypeScript 파일에서 C# 바인딩과 jslib을 자동 생성합니다.
    /// </summary>
    public static class AITCustomBridgeGenerator
    {
        /// <summary>
        /// 생성된 파일이 위치할 경로 (ait-build/generated/)
        /// </summary>
        private const string GENERATED_DIR = "generated";

        /// <summary>
        /// 커스텀 브릿지 C# 파일이 복사될 경로
        /// </summary>
        private const string CSHARP_OUTPUT_DIR = "Assets/AppsInToss/Runtime/CustomBridges";

        /// <summary>
        /// 커스텀 브릿지 jslib 파일이 복사될 경로
        /// </summary>
        private const string JSLIB_OUTPUT_DIR = "Assets/AppsInToss/Plugins";

        /// <summary>
        /// bridges 폴더에 TypeScript 파일이 있는지 확인
        /// </summary>
        public static bool HasBridgeFiles()
        {
            string bridgesPath = GetBridgesPath();
            if (!Directory.Exists(bridgesPath))
            {
                return false;
            }

            // 재귀적으로 .ts 파일 검색 (index.ts, .d.ts 제외)
            return HasTsFilesRecursive(bridgesPath);
        }

        private static bool HasTsFilesRecursive(string dir)
        {
            foreach (var file in Directory.GetFiles(dir, "*.ts"))
            {
                string fileName = Path.GetFileName(file);
                if (!fileName.EndsWith(".d.ts") && fileName != "index.ts")
                {
                    return true;
                }
            }

            foreach (var subDir in Directory.GetDirectories(dir))
            {
                if (HasTsFilesRecursive(subDir))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// bridges 폴더 경로 가져오기
        /// 프로젝트의 WebGLTemplates/AITTemplate/BuildConfig~/bridges 또는
        /// SDK 패키지의 WebGLTemplates/AITTemplate/BuildConfig~/bridges
        /// </summary>
        private static string GetBridgesPath()
        {
            // 1. 프로젝트 경로 확인
            string projectBridgesPath = Path.Combine(Application.dataPath, "WebGLTemplates/AITTemplate/BuildConfig~/bridges");
            if (Directory.Exists(projectBridgesPath))
            {
                return projectBridgesPath;
            }

            // 2. SDK 패키지 경로 확인
            string[] possibleSdkPaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/BuildConfig~/bridges"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/BuildConfig~/bridges"),
            };

            foreach (string path in possibleSdkPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return projectBridgesPath; // 기본값 반환
        }

        /// <summary>
        /// BuildConfig~ 경로 가져오기
        /// </summary>
        private static string GetBuildConfigPath()
        {
            // 1. 프로젝트 경로 확인
            string projectPath = Path.Combine(Application.dataPath, "WebGLTemplates/AITTemplate/BuildConfig~");
            if (Directory.Exists(projectPath))
            {
                return projectPath;
            }

            // 2. SDK 패키지 경로 확인
            string[] possibleSdkPaths = new string[]
            {
                Path.GetFullPath("Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/BuildConfig~"),
                Path.GetFullPath("Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/BuildConfig~"),
            };

            foreach (string path in possibleSdkPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return projectPath;
        }

        /// <summary>
        /// 커스텀 브릿지 생성 (Vite 플러그인 실행)
        /// </summary>
        /// <returns>성공 여부</returns>
        public static bool GenerateBridges()
        {
            if (!HasBridgeFiles())
            {
                Debug.Log("[CustomBridge] bridges 폴더에 TypeScript 파일이 없습니다.");
                return true; // 파일이 없으면 성공으로 처리
            }

            Debug.Log("[CustomBridge] 커스텀 브릿지 생성 시작...");

            string buildConfigPath = GetBuildConfigPath();
            if (!Directory.Exists(buildConfigPath))
            {
                Debug.LogError($"[CustomBridge] BuildConfig 폴더를 찾을 수 없습니다: {buildConfigPath}");
                return false;
            }

            // pnpm 경로 찾기
            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (string.IsNullOrEmpty(pnpmPath))
            {
                Debug.LogError("[CustomBridge] pnpm을 찾을 수 없습니다.");
                return false;
            }

            // node_modules가 없으면 먼저 설치
            string nodeModulesPath = Path.Combine(buildConfigPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[CustomBridge] node_modules가 없습니다. pnpm install 실행 중...");

                EditorUtility.DisplayProgressBar("커스텀 브릿지", "의존성 설치 중...", 0.2f);

                string localCachePath = Path.Combine(buildConfigPath, ".npm-cache");
                var installResult = AITNpmRunner.RunNpmCommandWithCache(buildConfigPath, pnpmPath, "install", localCachePath, "pnpm install...");
                if (installResult != AITConvertCore.AITExportError.SUCCEED)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogError("[CustomBridge] pnpm install 실패");
                    return false;
                }
            }

            // Vite 플러그인을 통해 브릿지 생성 (pnpm run generate-bridges 또는 직접 실행)
            // 현재는 빌드 시 자동으로 생성되므로, 여기서는 빌드를 트리거
            EditorUtility.DisplayProgressBar("커스텀 브릿지", "브릿지 코드 생성 중...", 0.5f);

            // tsx를 사용하여 생성 스크립트 직접 실행
            var generateResult = RunBridgeGenerator(buildConfigPath, pnpmPath);

            EditorUtility.ClearProgressBar();

            if (!generateResult)
            {
                Debug.LogError("[CustomBridge] 브릿지 생성 실패");
                return false;
            }

            // 생성된 파일을 Unity 프로젝트에 복사
            if (!CopyGeneratedFiles(buildConfigPath))
            {
                Debug.LogError("[CustomBridge] 생성된 파일 복사 실패");
                return false;
            }

            Debug.Log("[CustomBridge] ✓ 커스텀 브릿지 생성 완료");
            AssetDatabase.Refresh();

            return true;
        }

        /// <summary>
        /// 브릿지 생성기 실행
        /// </summary>
        private static bool RunBridgeGenerator(string buildConfigPath, string pnpmPath)
        {
            // Vite 플러그인의 buildStart 훅을 직접 실행하기 위해
            // tsx를 사용하여 생성 스크립트 실행
            string generatorScript = Path.Combine(buildConfigPath, "generate-bridges.ts");

            // generate-bridges.ts가 없으면 생성
            if (!File.Exists(generatorScript))
            {
                CreateGeneratorScript(generatorScript);
            }

            // pnpm tsx generate-bridges.ts 실행
            string pnpmDir = Path.GetDirectoryName(pnpmPath);
            string command = $"\"{pnpmPath}\" tsx generate-bridges.ts";

            var result = AITPlatformHelper.ExecuteCommand(command, buildConfigPath, new[] { pnpmDir }, verbose: true);

            return result.Success;
        }

        /// <summary>
        /// 브릿지 생성 스크립트 생성
        /// </summary>
        private static void CreateGeneratorScript(string scriptPath)
        {
            string scriptContent = @"/**
 * generate-bridges.ts
 *
 * 커스텀 브릿지 생성 스크립트
 * Vite 플러그인의 생성 로직을 직접 실행합니다.
 */

import { Project, SyntaxKind } from 'ts-morph';
import * as fs from 'fs';
import * as path from 'path';

const bridgesDir = './bridges';
const outputDir = './generated';

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
  filePath: string;
  namespace: string[];
  functions: BridgeFunction[];
}

function toPascalCase(str: string): string {
  return str
    .split(/[-_]/)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join('');
}

function simplifyType(typeText: string): string {
  return typeText.replace(/import\([^)]+\)\./g, '');
}

function mapToCSharpType(tsType: string): string {
  const typeMap: Record<string, string> = {
    'string': 'string',
    'number': 'double',
    'boolean': 'bool',
    'void': 'void',
  };

  const promiseMatch = tsType.match(/^Promise<(.+)>$/);
  if (promiseMatch) {
    return mapToCSharpType(promiseMatch[1]);
  }

  const arrayMatch = tsType.match(/^(.+)\[\]$/);
  if (arrayMatch) {
    return `${mapToCSharpType(arrayMatch[1])}[]`;
  }

  const recordMatch = tsType.match(/^Record<string,\s*(.+)>$/);
  if (recordMatch) {
    return `Dictionary<string, ${mapToCSharpType(recordMatch[1])}>`;
  }

  const unionMatch = tsType.match(/^(.+)\s*\|\s*(undefined|null)$/);
  if (unionMatch) {
    return mapToCSharpType(unionMatch[1]);
  }

  return typeMap[tsType] || 'object';
}

function parseBridgesFolder(): BridgeModule[] {
  const modules: BridgeModule[] = [];

  if (!fs.existsSync(bridgesDir)) {
    return modules;
  }

  const project = new Project({
    compilerOptions: {
      strict: true,
      target: 99,
      module: 99,
    },
  });

  function scanDir(dir: string, namespace: string[] = []) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);

      if (entry.isDirectory()) {
        const nsName = toPascalCase(entry.name);
        scanDir(fullPath, [...namespace, nsName]);
      } else if (entry.name.endsWith('.ts') && !entry.name.endsWith('.d.ts')) {
        const sourceFile = project.addSourceFileAtPath(fullPath);
        const functions: BridgeFunction[] = [];

        sourceFile.getFunctions().forEach((func) => {
          if (!func.isExported()) return;

          const name = func.getName();
          if (!name) return;

          const params: BridgeParam[] = func.getParameters().map((param) => {
            const paramType = param.getType();
            const typeText = simplifyType(paramType.getText());

            return {
              name: param.getName(),
              type: typeText,
              optional: param.isOptional() || param.hasQuestionToken(),
            };
          });

          const returnType = func.getReturnType();
          const returnTypeText = simplifyType(returnType.getText());
          const isAsync = func.isAsync() || returnTypeText.startsWith('Promise<');

          const jsDocNodes = func.getJsDocs();
          const jsDoc = jsDocNodes.length > 0 ? jsDocNodes[0].getDescription() : undefined;

          functions.push({ name, params, returnType: returnTypeText, isAsync, jsDoc });
        });

        if (functions.length > 0) {
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

function generateCSharpCode(modules: BridgeModule[]): string {
  const lines: string[] = [];

  lines.push('// -----------------------------------------------------------------------');
  lines.push('// <auto-generated>');
  lines.push('//     This file is auto-generated by vite-plugin-unity-bridge.');
  lines.push('//     Do not modify directly.');
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

  for (const module of modules) {
    const className = module.namespace.join('_');

    lines.push(`    /// <summary>`);
    lines.push(`    /// Custom Bridge: ${module.namespace.join('.')}`);
    lines.push(`    /// Source: ${module.filePath}`);
    lines.push(`    /// </summary>`);
    lines.push(`    [Preserve]`);
    lines.push(`    public static class ${className}`);
    lines.push(`    {`);

    for (const func of module.functions) {
      const methodName = toPascalCase(func.name);
      const internalFuncName = `__CustomBridge_${className}_${func.name}_Internal`;

      let returnType = mapToCSharpType(func.returnType);
      const isVoid = returnType === 'void';
      const taskReturnType = isVoid ? 'Task' : `Task<${returnType}>`;

      if (func.jsDoc) {
        lines.push(`        /// <summary>${func.jsDoc}</summary>`);
      }

      const paramStrings = func.params.map((p) => {
        const csType = mapToCSharpType(p.type);
        const needsNullable = p.optional && (csType === 'double' || csType === 'bool');
        return `${csType}${needsNullable ? '?' : ''} ${p.name}${p.optional && !needsNullable ? ' = null' : ''}`;
      });

      lines.push(`        [Preserve]`);
      lines.push(`        public static async ${taskReturnType} ${methodName}(${paramStrings.join(', ')})`);
      lines.push(`        {`);
      lines.push(`#if UNITY_WEBGL && !UNITY_EDITOR`);

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

      const callArgs = func.params.map((p) => {
        const csType = mapToCSharpType(p.type);
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

      const typeName = isVoid ? 'void' : returnType;
      lines.push(`            ${internalFuncName}(${[...callArgs, 'callbackId', `""${typeName}""`].join(', ')});`);

      if (isVoid) {
        lines.push(`            await tcs.Task;`);
      } else {
        lines.push(`            return await tcs.Task;`);
      }

      lines.push(`#else`);
      lines.push(`            UnityEngine.Debug.Log($""[CustomBridge Mock] ${className}.${methodName} called"");`);
      lines.push(`            await Task.CompletedTask;`);
      if (!isVoid) {
        lines.push(`            return default;`);
      }
      lines.push(`#endif`);
      lines.push(`        }`);
      lines.push('');

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
      lines.push(`        [System.Runtime.InteropServices.DllImport(""__Internal"")]`);
      lines.push(`        private static extern void ${internalFuncName}(${[...dllImportParams, 'string callbackId', 'string typeName'].join(', ')});`);
      lines.push(`#endif`);
      lines.push('');
    }

    lines.push(`    }`);
    lines.push('');
  }

  lines.push('}');

  return lines.join('\n');
}

function generateJslibCode(modules: BridgeModule[]): string {
  const lines: string[] = [];

  lines.push('/**');
  lines.push(' * CustomBridges.jslib');
  lines.push(' *');
  lines.push(' * This file is auto-generated by vite-plugin-unity-bridge.');
  lines.push(' */');
  lines.push('');
  lines.push('mergeInto(LibraryManager.library, {');

  const funcDefs: string[] = [];

  for (const module of modules) {
    const className = module.namespace.join('_');

    for (const func of module.functions) {
      const internalFuncName = `__CustomBridge_${className}_${func.name}_Internal`;
      const funcLines: string[] = [];

      const paramNames = [...func.params.map((p) => p.name), 'callbackId', 'typeName'];

      funcLines.push(`    ${internalFuncName}: function(${paramNames.join(', ')}) {`);
      funcLines.push(`        var callback = UTF8ToString(callbackId);`);
      funcLines.push(`        var typeNameStr = UTF8ToString(typeName);`);
      funcLines.push('');

      for (const param of func.params) {
        const baseType = param.type.replace(/\?$/, '').replace(/\s*\|\s*(undefined|null)$/, '');
        let processing: string;
        if (baseType === 'string') {
          processing = `UTF8ToString(${param.name})`;
        } else if (baseType === 'number') {
          processing = param.name;
        } else if (baseType === 'boolean') {
          processing = `${param.name} !== 0`;
        } else {
          processing = `JSON.parse(UTF8ToString(${param.name}))`;
        }
        funcLines.push(`        var ${param.name}_val = ${processing};`);
      }

      const bridgePath = ['window', 'Bridges', ...module.namespace].join('.');
      const funcCall = `${bridgePath}.${func.name}`;
      const callArgs = func.params.map((p) => `${p.name}_val`).join(', ');

      funcLines.push('');
      funcLines.push('        try {');

      if (func.isAsync) {
        funcLines.push(`            var promiseResult = ${funcCall}(${callArgs});`);
        funcLines.push('');
        funcLines.push('            if (!promiseResult || typeof promiseResult.then !== ""function"") {');
        funcLines.push('                var payload = JSON.stringify({');
        funcLines.push('                    CallbackId: callback,');
        funcLines.push('                    TypeName: typeNameStr,');
        funcLines.push('                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: """" })');
        funcLines.push('                });');
        funcLines.push('                SendMessage(""AITCore"", ""OnAITCallback"", payload);');
        funcLines.push('                return;');
        funcLines.push('            }');
        funcLines.push('');
        funcLines.push('            promiseResult');
        funcLines.push('                .then(function(result) {');
        funcLines.push('                    var payload = JSON.stringify({');
        funcLines.push('                        CallbackId: callback,');
        funcLines.push('                        TypeName: typeNameStr,');
        funcLines.push('                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: """" })');
        funcLines.push('                    });');
        funcLines.push('                    SendMessage(""AITCore"", ""OnAITCallback"", payload);');
        funcLines.push('                })');
        funcLines.push('                .catch(function(error) {');
        funcLines.push('                    var payload = JSON.stringify({');
        funcLines.push('                        CallbackId: callback,');
        funcLines.push('                        TypeName: typeNameStr,');
        funcLines.push('                        Result: JSON.stringify({ success: false, data: """", error: error.message || String(error) })');
        funcLines.push('                    });');
        funcLines.push('                    SendMessage(""AITCore"", ""OnAITCallback"", payload);');
        funcLines.push('                });');
      } else {
        funcLines.push(`            var result = ${funcCall}(${callArgs});`);
        funcLines.push('            var payload = JSON.stringify({');
        funcLines.push('                CallbackId: callback,');
        funcLines.push('                TypeName: typeNameStr,');
        funcLines.push('                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: """" })');
        funcLines.push('            });');
        funcLines.push('            SendMessage(""AITCore"", ""OnAITCallback"", payload);');
      }

      funcLines.push('        } catch (error) {');
      funcLines.push('            var payload = JSON.stringify({');
      funcLines.push('                CallbackId: callback,');
      funcLines.push('                TypeName: typeNameStr,');
      funcLines.push('                Result: JSON.stringify({ success: false, data: """", error: error.message || String(error) })');
      funcLines.push('            });');
      funcLines.push('            SendMessage(""AITCore"", ""OnAITCallback"", payload);');
      funcLines.push('        }');
      funcLines.push('    }');

      funcDefs.push(funcLines.join('\n'));
    }
  }

  lines.push(funcDefs.join(',\n\n'));
  lines.push('});');

  return lines.join('\n');
}

function generateBridgesEntryCode(modules: BridgeModule[]): string {
  const lines: string[] = [];

  lines.push('/**');
  lines.push(' * bridges-entry.ts');
  lines.push(' *');
  lines.push(' * Auto-generated entry point for custom bridges.');
  lines.push(' */');
  lines.push('');

  for (const module of modules) {
    const importName = module.namespace.join('_');
    const importPath = './bridges/' + module.filePath.replace(/\.ts$/, '');
    lines.push(`import * as ${importName} from '${importPath}';`);
  }

  lines.push('');
  lines.push('declare global {');
  lines.push('  interface Window {');
  lines.push('    Bridges: Record<string, unknown>;');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('window.Bridges = window.Bridges || {};');
  lines.push('');

  for (const module of modules) {
    const importName = module.namespace.join('_');
    let currentPath = 'window.Bridges';

    for (let i = 0; i < module.namespace.length - 1; i++) {
      const ns = module.namespace[i];
      currentPath += `.${ns}`;
      lines.push(`${currentPath} = ${currentPath} || {};`);
    }

    const fullPath = ['window', 'Bridges', ...module.namespace].join('.');
    lines.push(`${fullPath} = ${importName};`);
  }

  lines.push('');
  lines.push('console.log(""[CustomBridges] Registered:"", Object.keys(window.Bridges));');

  return lines.join('\n');
}

// Main
const modules = parseBridgesFolder();

if (modules.length === 0) {
  console.log('[CustomBridge] No bridge modules found.');
  process.exit(0);
}

console.log(`[CustomBridge] Found ${modules.length} bridge module(s):`);
for (const mod of modules) {
  console.log(`  - ${mod.namespace.join('.')} (${mod.functions.length} functions)`);
}

// Create output directory
if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir, { recursive: true });
}

// Generate manifest
const manifest = {
  version: '1.0.0',
  generatedAt: new Date().toISOString(),
  modules,
};
fs.writeFileSync(path.join(outputDir, 'bridge-manifest.json'), JSON.stringify(manifest, null, 2));

// Generate C#
const csharpCode = generateCSharpCode(modules);
fs.writeFileSync(path.join(outputDir, 'CustomBridges.cs'), csharpCode);

// Generate jslib
const jslibCode = generateJslibCode(modules);
fs.writeFileSync(path.join(outputDir, 'CustomBridges.jslib'), jslibCode);

// Generate entry
const entryCode = generateBridgesEntryCode(modules);
fs.writeFileSync(path.join(outputDir, 'bridges-entry.ts'), entryCode);

console.log('[CustomBridge] Generated files:');
console.log(`  - ${outputDir}/bridge-manifest.json`);
console.log(`  - ${outputDir}/CustomBridges.cs`);
console.log(`  - ${outputDir}/CustomBridges.jslib`);
console.log(`  - ${outputDir}/bridges-entry.ts`);
";
            File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.UTF8);
            Debug.Log("[CustomBridge] generate-bridges.ts 생성됨");
        }

        /// <summary>
        /// 생성된 파일을 Unity 프로젝트에 복사
        /// </summary>
        private static bool CopyGeneratedFiles(string buildConfigPath)
        {
            string generatedPath = Path.Combine(buildConfigPath, GENERATED_DIR);

            if (!Directory.Exists(generatedPath))
            {
                Debug.LogWarning("[CustomBridge] 생성된 파일이 없습니다.");
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // C# 파일 복사
            string csharpSrc = Path.Combine(generatedPath, "CustomBridges.cs");
            if (File.Exists(csharpSrc))
            {
                string csharpDest = Path.Combine(projectRoot, CSHARP_OUTPUT_DIR);
                if (!Directory.Exists(csharpDest))
                {
                    Directory.CreateDirectory(csharpDest);
                }

                string destFile = Path.Combine(csharpDest, "CustomBridges.cs");
                File.Copy(csharpSrc, destFile, true);
                Debug.Log($"[CustomBridge] ✓ CustomBridges.cs → {CSHARP_OUTPUT_DIR}");
            }

            // jslib 파일 복사
            string jslibSrc = Path.Combine(generatedPath, "CustomBridges.jslib");
            if (File.Exists(jslibSrc))
            {
                string jslibDest = Path.Combine(projectRoot, JSLIB_OUTPUT_DIR);
                if (!Directory.Exists(jslibDest))
                {
                    Directory.CreateDirectory(jslibDest);
                }

                string destFile = Path.Combine(jslibDest, "CustomBridges.jslib");
                File.Copy(jslibSrc, destFile, true);
                Debug.Log($"[CustomBridge] ✓ CustomBridges.jslib → {JSLIB_OUTPUT_DIR}");
            }

            return true;
        }

        /// <summary>
        /// 빌드 전 커스텀 브릿지 생성 체크
        /// </summary>
        public static bool CheckAndGenerateBridges()
        {
            if (!HasBridgeFiles())
            {
                return true; // 브릿지 파일이 없으면 스킵
            }

            Debug.Log("[CustomBridge] bridges 폴더에서 TypeScript 파일 감지됨. 브릿지 생성 중...");
            return GenerateBridges();
        }

        #region Menu Items

        [MenuItem("Apps in Toss/Custom Bridges/Generate Bridges", false, 300)]
        public static void MenuGenerateBridges()
        {
            if (GenerateBridges())
            {
                EditorUtility.DisplayDialog("커스텀 브릿지", "브릿지 생성이 완료되었습니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("커스텀 브릿지", "브릿지 생성에 실패했습니다.\nConsole 창을 확인해주세요.", "확인");
            }
        }

        [MenuItem("Apps in Toss/Custom Bridges/Open Bridges Folder", false, 301)]
        public static void MenuOpenBridgesFolder()
        {
            string bridgesPath = GetBridgesPath();

            if (!Directory.Exists(bridgesPath))
            {
                Directory.CreateDirectory(bridgesPath);
                Debug.Log($"[CustomBridge] bridges 폴더 생성됨: {bridgesPath}");
            }

            EditorUtility.RevealInFinder(bridgesPath);
        }

        #endregion
    }
}
