import { Project, ts } from 'ts-morph';
import * as path from 'path';
import * as fs from 'fs';
import { ParsedAPI, ParsedTypeDefinition } from '../types.js';
import { parseSourceFile } from './api-parser.js';
import { parseNamespaceObjects } from './namespace-parser.js';
import { parseTypeDefinitionsFromFile } from './type-definition-parser.js';
import { findFrameworkPath, parseFrameworkAPIs, parseFrameworkTypeDefinitions, parseNativeModulesType } from './framework-parser.js';
import { resolvePackagePath } from '../generators/jslib-compiler.js';

/**
 * TypeScript 소스 파일들을 파싱하여 API 정보 추출
 */
export class TypeScriptParser {
  private project: Project;
  private _frameworkDtsPath?: string;

  constructor(private sourceDir: string, private webFrameworkPath?: string) {
    // tsconfig.json 경로 찾기 (상위 디렉토리도 확인)
    const possibleConfigs = [
      path.join(sourceDir, 'tsconfig.json'),
      path.join(sourceDir, '..', 'tsconfig.json'),
      path.join(sourceDir, '../..', 'tsconfig.json'),
    ];

    let tsConfigPath: string | undefined;
    for (const configPath of possibleConfigs) {
      try {
        if (fs.existsSync(configPath)) {
          tsConfigPath = configPath;
          break;
        }
      } catch {
        continue;
      }
    }

    this.project = new Project({
      tsConfigFilePath: tsConfigPath,
      skipAddingFilesFromTsConfig: true, // 수동으로 파일 추가
      compilerOptions: {
        // module resolution 설정 (import 따라가기)
        moduleResolution: 99, // NodeNext
        resolveJsonModule: true,
        // nullable 타입 감지를 위해 strictNullChecks 활성화
        strictNullChecks: true,
      },
    });

    // sourceDir의 .d.ts 파일들을 재귀적으로 추가
    this.project.addSourceFilesAtPaths(path.join(sourceDir, '**', '*.d.ts'));

    // 의존성 타입 해결: ts-morph의 module resolution으로 자동 처리
    // node_modules 파일을 수동 추가하지 않음 (순환 참조, 불필요한 API 파싱 방지)
    // native-modules의 비-export 타입은 parseNativeModulesType()으로 별도 처리
  }

  /**
   * 추가 소스 디렉토리의 .d.ts 파일들을 프로젝트에 등록
   * web-analytics 등 별도 패키지의 타입 정의를 파싱 대상에 추가할 때 사용
   */
  addSourceDirectory(dir: string): void {
    this.project.addSourceFilesAtPaths(path.join(dir, '**', '*.d.ts'));
  }

  /**
   * @apps-in-toss/web-framework가 공개적으로 export하는 심볼 이름 집합을 계산한다.
   *
   * jslib 타입 검사(jslib-compiler.ts)와 **동일한** 방식으로 web-framework 패키지를
   * 해석(resolvePackagePath + bundler resolution)하므로, 생성된 브릿지가 import하게 될
   * 심볼이 그 버전에서 실제로 존재하는지를 생성 이전에 판별할 수 있다.
   *
   * 용도: web-framework 메이저 업데이트(예: 3.0.0)에서 public export가 제거된 API를
   * 발견 단계에서 걸러내, 존재하지 않는 심볼을 import하는 브릿지가 생성되어
   * 타입 검사(TS2305)에서 실패하는 것을 방지한다. (구버전 발견 경로가 stale .d.ts로
   * 폴백하여 제거된 API를 계속 발견하는 케이스 대응.)
   *
   * 해석에 실패하면 **빈 집합**을 반환한다 — 호출부는 이를 "판별 불가"로 보고
   * 필터를 건너뛰어야 한다(fail-open). 빈 집합으로 필터링하면 모든 web-framework API가
   * 제거되는 치명적 오작동이 발생하므로, 절대 빈 집합으로 필터를 적용하지 말 것.
   */
  getWebFrameworkExportedNames(): Set<string> {
    const names = new Set<string>();
    try {
      const wfPkgDir = resolvePackagePath('@apps-in-toss/web-framework');

      // jslib-compiler.ts의 타입 검사와 동일한 resolution(bundler + paths)으로
      // 격리된 probe 프로젝트를 구성한다. 파일 위치에 의존하는 node_modules 탐색
      // 대신 paths 매핑으로 명시 해석하여 stale 패키지로의 폴백을 차단한다.
      const probeProject = new Project({
        compilerOptions: {
          moduleResolution: ts.ModuleResolutionKind.Bundler,
          module: ts.ModuleKind.ESNext,
          target: ts.ScriptTarget.ES2020,
          strict: true,
          skipLibCheck: true,
          esModuleInterop: true,
          allowSyntheticDefaultImports: true,
          resolveJsonModule: true,
          baseUrl: '.',
          paths: {
            '@apps-in-toss/web-framework': [wfPkgDir],
          },
        },
        skipAddingFilesFromTsConfig: true,
      });

      const probe = probeProject.createSourceFile(
        path.join(wfPkgDir, '..', '__ait_wf_export_probe__.ts'),
        `export * from '@apps-in-toss/web-framework';`,
        { overwrite: true },
      );

      for (const [name] of probe.getExportedDeclarations()) {
        names.add(name);
      }
    } catch (err) {
      // 해석 실패 → 빈 집합 반환(호출부에서 필터 스킵). 경고만 남긴다.
      console.warn(
        `⚠️  web-framework 공개 export 목록 계산 실패 — export 필터를 건너뜁니다: ${err instanceof Error ? err.message : String(err)}`,
      );
    }
    return names;
  }

  /**
   * framework .d.ts 경로를 한 번 resolve하고 캐시
   */
  get frameworkDtsPath(): string | undefined {
    if (this._frameworkDtsPath === undefined) {
      this._frameworkDtsPath = findFrameworkPath(this.webFrameworkPath) ?? '';
    }
    return this._frameworkDtsPath || undefined;
  }

  /**
   * native-modules에서 특정 타입 정의 파싱 (on-demand)
   * 순환 참조로 인한 스택 오버플로우를 방지하기 위해 별도 메서드로 분리
   */
  parseNativeModulesType(typeName: string): ParsedTypeDefinition | null {
    return parseNativeModulesType(typeName);
  }

  /**
   * @apps-in-toss/framework에서 특정 API 파싱 (loadFullScreenAd, showFullScreenAd 등)
   * 이 API들은 web-framework에서 re-export되지 않으므로 직접 파싱
   */
  parseFrameworkAPIs(apiNames: string[], frameworkDtsPath?: string): ParsedAPI[] {
    return parseFrameworkAPIs(apiNames, frameworkDtsPath, this.webFrameworkPath);
  }

  /**
   * @apps-in-toss/framework에서 특정 API 관련 타입 정의 파싱
   * (LoadFullScreenAdEvent, ShowFullScreenAdEvent, Options 등)
   */
  parseFrameworkTypeDefinitions(apiNames: string[], frameworkDtsPath?: string): ParsedTypeDefinition[] {
    return parseFrameworkTypeDefinitions(apiNames, frameworkDtsPath, this.webFrameworkPath);
  }

  /**
   * 모든 API 파싱
   * @param frameworkApiNames @apps-in-toss/framework에서 직접 파싱할 API 이름 목록 (선택)
   */
  async parseAPIs(frameworkApiNames?: string[]): Promise<ParsedAPI[]> {
    const apis: ParsedAPI[] = [];
    const sourceFiles = this.project.getSourceFiles();

    for (const sourceFile of sourceFiles) {
      const filePath = sourceFile.getFilePath();
      const fileName = path.basename(filePath);

      // index.d.ts는 네임스페이스 객체만 파싱 (IAP, Storage 등)
      if (fileName === 'index.d.ts') {
        const namespaceAPIs = parseNamespaceObjects(sourceFile);
        apis.push(...namespaceAPIs);
        continue;
      }

      // index.d.cts, bridge.d.ts, types.d.ts 파일은 스킵 (전체 re-export 파일)
      if (fileName === 'index.d.cts' || fileName === 'types.d.ts' || fileName === 'bridge.d.ts') {
        continue;
      }

      const fileAPIs = parseSourceFile(sourceFile);
      apis.push(...fileAPIs);
    }

    // @apps-in-toss/framework에서 추가 API 파싱 (web-framework에서 re-export되지 않는 API)
    if (frameworkApiNames && frameworkApiNames.length > 0) {
      const frameworkAPIs = this.parseFrameworkAPIs(frameworkApiNames, this.frameworkDtsPath);
      apis.push(...frameworkAPIs);
    }

    return apis;
  }

  /**
   * 모든 타입 정의 파싱 (enum, interface)
   */
  async parseTypeDefinitions(): Promise<ParsedTypeDefinition[]> {
    const typeMap = new Map<string, ParsedTypeDefinition>(); // 중복 제거용
    const sourceFiles = this.project.getSourceFiles();

    for (const sourceFile of sourceFiles) {
      const filePath = sourceFile.getFilePath();
      const fileName = path.basename(filePath);

      // web-bridge의 index, bridge, types 파일은 스킵
      // native-modules는 parseNativeModulesType으로 별도 처리
      if (fileName === 'index.d.ts' || fileName === 'index.d.cts' || fileName === 'types.d.ts' || fileName === 'bridge.d.ts') {
        continue;
      }

      parseTypeDefinitionsFromFile(sourceFile, typeMap);
    }

    // Map에서 중복이 제거된 타입 정의들을 반환
    return Array.from(typeMap.values());
  }
}
