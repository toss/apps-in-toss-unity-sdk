/**
 * Roslyn C# 컴파일러를 사용한 실제 컴파일 검증
 *
 * macOS/Linux: mono + mcs (Mono C# Compiler)
 * Windows: csc.exe (Roslyn C# Compiler)
 */

import { spawn } from 'child_process';
import * as fs from 'fs/promises';
import * as path from 'path';
import * as os from 'os';
import { glob } from 'glob';

export interface CompilationResult {
  success: boolean;
  errors: CompilationError[];
  warnings: CompilationWarning[];
  outputPath?: string;
}

export interface CompilationError {
  file: string;
  line: number;
  column: number;
  code: string;
  message: string;
}

export interface CompilationWarning {
  file: string;
  line: number;
  column: number;
  code: string;
  message: string;
}

export interface CompilationOptions {
  references: string[];  // DLL paths or names
  allowUnsafe?: boolean;
  targetFramework?: string;
  outputType?: 'library' | 'exe';
}

/**
 * Roslyn/Mono C# 컴파일러를 사용하여 실제 컴파일 수행
 */
export async function compileCSharp(
  code: string,
  options: CompilationOptions
): Promise<CompilationResult> {
  const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'csharp-compile-'));
  const sourceFile = path.join(tempDir, 'Source.cs');
  const outputFile = path.join(tempDir, options.outputType === 'exe' ? 'Output.exe' : 'Output.dll');

  try {
    // 소스 파일 작성
    await fs.writeFile(sourceFile, code, 'utf-8');

    // 컴파일러 선택 및 실행
    const result = await runCompiler(sourceFile, outputFile, options);

    return result;
  } finally {
    // 임시 파일 정리
    await fs.rm(tempDir, { recursive: true, force: true });
  }
}

/**
 * 여러 C# 파일을 함께 컴파일
 */
export async function compileCSharpFiles(
  files: { [filename: string]: string },
  options: CompilationOptions
): Promise<CompilationResult> {
  const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'csharp-compile-'));
  const sourceFiles: string[] = [];

  try {
    // 모든 소스 파일 작성
    for (const [filename, code] of Object.entries(files)) {
      const sourceFile = path.join(tempDir, filename);
      await fs.writeFile(sourceFile, code, 'utf-8');
      sourceFiles.push(sourceFile);
    }

    const outputFile = path.join(tempDir, options.outputType === 'exe' ? 'Output.exe' : 'Output.dll');

    // 컴파일 실행
    const result = await runCompiler(sourceFiles, outputFile, options);

    return result;
  } finally {
    // 임시 파일 정리
    await fs.rm(tempDir, { recursive: true, force: true });
  }
}

/**
 * 컴파일러 실행
 */
async function runCompiler(
  sourceFiles: string | string[],
  outputFile: string,
  options: CompilationOptions
): Promise<CompilationResult> {
  const sources = Array.isArray(sourceFiles) ? sourceFiles : [sourceFiles];

  // Unity DLL 경로 찾기
  const unityPath = await findUnityPath();
  const references = await resolveReferences(options.references, unityPath);

  // 플랫폼별 컴파일러 선택
  const isWindows = process.platform === 'win32';
  const compiler = isWindows ? 'csc.exe' : 'mcs';

  // 컴파일 인자 구성
  const args = [
    `-target:${options.outputType === 'exe' ? 'exe' : 'library'}`,
    `-out:${outputFile}`,
    ...references.map(r => `-reference:${r}`),
    ...sources,
  ];

  if (options.allowUnsafe) {
    args.unshift('-unsafe');
  }

  try {
    // 컴파일러 실행
    const { stdout, stderr, exitCode } = await execCompiler(compiler, args);

    // 출력 파싱
    const result = parseCompilerOutput(stdout + stderr);

    // 컴파일 성공 여부는 exit code와 에러 개수로 판단
    result.success = exitCode === 0 && result.errors.length === 0;

    if (result.success) {
      result.outputPath = outputFile;
    }

    return result;
  } catch (error) {
    // 컴파일러가 없으면 명확한 에러 던지기
    throw error;
  }
}

/**
 * Unity 설치 경로 찾기
 */
async function findUnityPath(): Promise<string> {
  const possiblePaths = [
    // macOS Unity Hub
    '/Applications/Unity/Hub/Editor/*/Unity.app/Contents',
    '/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents',
    '/Applications/Unity/Hub/Editor/2023.3.*/Unity.app/Contents',
    // macOS Unity 직접 설치
    '/Applications/Unity/Unity.app/Contents',
    '/Applications/Unity.app/Contents',
    // Linux
    '/opt/unity/Editor/Data',
    '~/Unity/Hub/Editor/*/Editor/Data',
  ];

  for (const pattern of possiblePaths) {
    const matches = await glob(pattern, { absolute: true });
    if (matches.length > 0) {
      // 가장 최신 버전 선택
      return matches[matches.length - 1];
    }
  }

  // Unity 없으면 시스템 기본 C# 라이브러리 사용
  console.warn('Unity installation not found, using system C# libraries');
  return '';
}

/**
 * 참조 DLL 경로 해석
 */
async function resolveReferences(
  references: string[],
  unityPath: string
): Promise<string[]> {
  const resolved: string[] = [];
  const stubsDir = path.resolve(__dirname, '../stubs');

  // Unity stub DLL 경로 (tests가 실행되는 동안 Unity 없이도 컴파일 가능하도록)
  const unityStubPath = path.join(stubsDir, 'UnityEngine.dll');
  const newtonsoftStubPath = path.join(stubsDir, 'Newtonsoft.Json.dll');

  let hasUnityStub = false;
  let hasNewtonsoftStub = false;

  try {
    await fs.access(unityStubPath);
    hasUnityStub = true;
  } catch {
    // stub 없음
  }

  try {
    await fs.access(newtonsoftStubPath);
    hasNewtonsoftStub = true;
  } catch {
    // stub 없음
  }

  // Unity DLL이 하나라도 없으면 모든 Unity DLL을 stub으로 대체
  // (부분적으로 찾으면 버전 불일치로 인한 컴파일 오류 발생)
  let useStubForAllUnity = false;
  if (hasUnityStub && unityPath) {
    for (const ref of references) {
      if (ref.startsWith('Unity')) {
        const dllPath = path.join(unityPath, 'Managed', ref);
        try {
          await fs.access(dllPath);
        } catch {
          useStubForAllUnity = true;
          break;
        }
      }
    }
  } else if (hasUnityStub) {
    // Unity가 설치되지 않았으면 stub 사용
    useStubForAllUnity = true;
  }

  // 중복 방지를 위한 Set
  const addedStubs = new Set<string>();

  for (const ref of references) {
    if (ref.startsWith('Unity')) {
      // Unity stub을 사용하기로 결정했으면 모든 Unity DLL을 stub으로 대체
      if (useStubForAllUnity && !addedStubs.has('unity')) {
        resolved.push(unityStubPath);
        addedStubs.add('unity');
      } else if (!useStubForAllUnity && unityPath) {
        // 실제 Unity DLL 사용
        const dllPath = path.join(unityPath, 'Managed', ref);
        try {
          await fs.access(dllPath);
          resolved.push(dllPath);
        } catch {
          console.warn(`Unity DLL not found: ${dllPath}`);
        }
      }
    } else if (ref === 'Newtonsoft.Json.dll' && hasNewtonsoftStub) {
      // Newtonsoft.Json은 항상 stub 사용 (Unity 패키지에서 제공되므로)
      if (!addedStubs.has('newtonsoft')) {
        resolved.push(newtonsoftStubPath);
        addedStubs.add('newtonsoft');
      }
    } else if (ref.includes(path.sep) || ref.includes('/')) {
      // 절대 경로
      resolved.push(ref);
    } else {
      // 시스템 DLL (예: System.dll)
      resolved.push(ref);
    }
  }

  return resolved;
}

/**
 * 컴파일러 실행
 */
async function execCompiler(
  compiler: string,
  args: string[]
): Promise<{ stdout: string; stderr: string; exitCode: number }> {
  return new Promise((resolve, reject) => {
    const proc = spawn(compiler, args, {
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    let stdout = '';
    let stderr = '';

    proc.stdout?.on('data', (data) => {
      stdout += data.toString();
    });

    proc.stderr?.on('data', (data) => {
      stderr += data.toString();
    });

    proc.on('close', (exitCode) => {
      resolve({ stdout, stderr, exitCode: exitCode ?? 1 });
    });

    proc.on('error', (error) => {
      // 컴파일러를 찾지 못한 경우
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
        reject(new Error(`Compiler not found: ${compiler}. Please install Mono (macOS/Linux) or .NET SDK (Windows)`));
      } else {
        reject(error);
      }
    });
  });
}

/**
 * 컴파일러 출력 파싱
 *
 * mcs 출력 형식:
 *   Source.cs(10,5): error CS0103: The name 'foo' does not exist in the current context
 *   Source.cs(15,10): warning CS0168: The variable 'bar' is declared but never used
 */
function parseCompilerOutput(output: string): CompilationResult {
  const errors: CompilationError[] = [];
  const warnings: CompilationWarning[] = [];

  const lines = output.split('\n');

  for (const line of lines) {
    // 에러/경고 패턴: filename(line,column): error/warning CSxxxx: message
    const match = line.match(/^(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(CS\d+):\s+(.+)$/);

    if (match) {
      const [, file, lineStr, columnStr, type, code, message] = match;

      const diagnostic = {
        file: path.basename(file),
        line: parseInt(lineStr, 10),
        column: parseInt(columnStr, 10),
        code,
        message: message.trim(),
      };

      if (type === 'error') {
        errors.push(diagnostic);
      } else {
        warnings.push(diagnostic);
      }
    }
  }

  return {
    success: errors.length === 0,
    errors,
    warnings,
  };
}

/**
 * 컴파일 결과를 콘솔에 출력
 */
export function printCompilationResult(result: CompilationResult): void {
  if (result.success) {
    console.log('✅ Compilation successful');
    if (result.outputPath) {
      console.log(`   Output: ${result.outputPath}`);
    }
  } else {
    console.log('❌ Compilation failed');
  }

  if (result.errors.length > 0) {
    console.log('\nErrors:');
    for (const error of result.errors) {
      console.log(`  ${error.file}(${error.line},${error.column}): error ${error.code}: ${error.message}`);
    }
  }

  if (result.warnings.length > 0) {
    console.log('\nWarnings:');
    for (const warning of result.warnings) {
      console.log(`  ${warning.file}(${warning.line},${warning.column}): warning ${warning.code}: ${warning.message}`);
    }
  }
}
