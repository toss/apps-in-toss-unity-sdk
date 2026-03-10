/**
 * jslib TypeScript 컴파일러
 *
 * 생성된 TypeScript 브릿지 코드를 컴파일하고
 * 타입 검사를 수행합니다.
 */

import * as fs from 'fs/promises';
import * as fsSync from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import picocolors from 'picocolors';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * pnpm의 flat node_modules 구조에서 패키지 경로를 해석합니다.
 * 직접 node_modules에 없으면 .pnpm 스토어에서 최신 버전을 찾습니다.
 */
function resolvePackagePath(packageName: string): string {
  const nodeModulesDir = path.resolve(__dirname, '../../node_modules');
  const directPath = path.join(nodeModulesDir, packageName);

  if (fsSync.existsSync(directPath)) {
    return directPath;
  }

  // pnpm .pnpm 스토어에서 찾기
  const pnpmDir = path.join(nodeModulesDir, '.pnpm');
  const escapedName = packageName.replace('/', '+');
  try {
    const entries = fsSync.readdirSync(pnpmDir).filter((e: string) =>
      e.startsWith(escapedName + '@')
    );
    if (entries.length > 0) {
      // 최신 버전 선택 (semver 역순 — 숫자 부분 비교)
      entries.sort((a, b) => {
        const verA = a.slice(escapedName.length + 1).split('.').map(Number);
        const verB = b.slice(escapedName.length + 1).split('.').map(Number);
        for (let i = 0; i < Math.max(verA.length, verB.length); i++) {
          const diff = (verB[i] || 0) - (verA[i] || 0);
          if (diff !== 0) return diff;
        }
        return 0;
      });
      const resolved = path.join(pnpmDir, entries[0], 'node_modules', packageName);
      if (fsSync.existsSync(resolved)) {
        return resolved;
      }
    }
  } catch {}

  // 폴백: 원래 경로 반환
  return directPath;
}

/**
 * TypeScript 타입 검사 결과
 */
export interface TypeCheckResult {
  success: boolean;
  errors: TypeCheckError[];
  checkedFiles: string[];
}

/**
 * 타입 검사 에러
 */
export interface TypeCheckError {
  file: string;
  line: number;
  column: number;
  message: string;
  code: number;
}

/**
 * TypeScript 브릿지 코드 타입 검사
 *
 * 생성된 TypeScript 파일들이 web-framework 타입과 호환되는지 검증합니다.
 * tsc --noEmit을 사용하여 실제 컴파일 없이 타입 검사만 수행합니다.
 */
export async function typeCheckBridgeCode(
  typescriptFiles: Map<string, string>,
  cacheDir: string
): Promise<TypeCheckResult> {
  const errors: TypeCheckError[] = [];
  const checkedFiles: string[] = [];

  try {
    // 캐시 디렉토리 생성
    await fs.mkdir(cacheDir, { recursive: true });

    // TypeScript 파일들 작성
    for (const [filename, content] of typescriptFiles.entries()) {
      const filePath = path.join(cacheDir, filename);
      await fs.writeFile(filePath, content);
      checkedFiles.push(filename);
    }

    // tsconfig.json 생성
    const tsconfigPath = path.join(cacheDir, 'tsconfig.json');
    const tsconfigContent = {
      compilerOptions: {
        target: 'ES2020',
        module: 'ES2020',
        moduleResolution: 'node',
        strict: true,
        noEmit: true,
        skipLibCheck: true,
        esModuleInterop: true,
        allowSyntheticDefaultImports: true,
        resolveJsonModule: true,
        declaration: false,
        typeRoots: [
          path.resolve(__dirname, '../../node_modules/@types'),
          path.resolve(__dirname, '../types'),
        ],
        paths: {
          '@apps-in-toss/web-framework': [
            resolvePackagePath('@apps-in-toss/web-framework'),
          ],
          '@apps-in-toss/framework': [
            resolvePackagePath('@apps-in-toss/framework'),
          ],
          '@apps-in-toss/web-analytics': [
            resolvePackagePath('@apps-in-toss/web-analytics'),
          ],
        },
      },
      include: ['*.ts'],
      exclude: ['node_modules'],
    };
    await fs.writeFile(tsconfigPath, JSON.stringify(tsconfigContent, null, 2));

    // unity-jslib.d.ts 복사
    const unityJslibSource = path.resolve(__dirname, '../types/unity-jslib.d.ts');
    const unityJslibDest = path.join(cacheDir, 'unity-jslib.d.ts');
    try {
      await fs.copyFile(unityJslibSource, unityJslibDest);
    } catch {
      // 파일이 없으면 무시 (테스트 환경에서)
    }

    // tsc 실행 (ts-morph 사용)
    const { Project } = await import('ts-morph');

    const project = new Project({
      tsConfigFilePath: tsconfigPath,
      skipAddingFilesFromTsConfig: false,
    });

    // 진단 정보 가져오기
    const diagnostics = project.getPreEmitDiagnostics();

    for (const diagnostic of diagnostics) {
      const sourceFile = diagnostic.getSourceFile();
      const start = diagnostic.getStart();

      let line = 1;
      let column = 1;
      let file = 'unknown';

      if (sourceFile && start !== undefined) {
        const pos = sourceFile.getLineAndColumnAtPos(start);
        line = pos.line;
        column = pos.column;
        file = path.basename(sourceFile.getFilePath());
      }

      errors.push({
        file,
        line,
        column,
        message: diagnostic.getMessageText().toString(),
        code: diagnostic.getCode(),
      });
    }

    return {
      success: errors.length === 0,
      errors,
      checkedFiles,
    };
  } catch (error) {
    // 컴파일러 오류
    errors.push({
      file: 'compiler',
      line: 0,
      column: 0,
      message: error instanceof Error ? error.message : String(error),
      code: -1,
    });

    return {
      success: false,
      errors,
      checkedFiles,
    };
  }
}

/**
 * 타입 검사 결과 출력
 */
export function printTypeCheckResult(result: TypeCheckResult): void {
  if (result.success) {
    console.log(picocolors.green(`✓ ${result.checkedFiles.length}개 TypeScript 파일 타입 검사 통과`));
    return;
  }

  console.log(picocolors.red(`\n❌ TypeScript 타입 검사 실패 (${result.errors.length}개 오류)\n`));

  for (const error of result.errors) {
    const location = error.line > 0 ? `:${error.line}:${error.column}` : '';
    console.log(picocolors.red(`  ${error.file}${location}`));
    console.log(picocolors.gray(`    TS${error.code}: ${error.message}`));
  }
}

/**
 * 캐시 디렉토리 정리
 */
export async function cleanupCache(cacheDir: string): Promise<void> {
  try {
    await fs.rm(cacheDir, { recursive: true, force: true });
  } catch {
    // 정리 실패해도 무시
  }
}
