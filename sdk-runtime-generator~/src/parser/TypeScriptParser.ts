import { Project } from 'ts-morph';
import * as path from 'path';
import * as fs from 'fs';
import { ParsedAPI, ParsedTypeDefinition } from '../types.js';
import { parseSourceFile } from './api-parser.js';
import { parseNamespaceObjects } from './namespace-parser.js';
import { parseTypeDefinitionsFromFile } from './type-definition-parser.js';
import { parseFrameworkAPIs, parseFrameworkTypeDefinitions, parseNativeModulesType } from './framework-parser.js';

/**
 * TypeScript 소스 파일들을 파싱하여 API 정보 추출
 */
export class TypeScriptParser {
  private project: Project;

  constructor(private sourceDir: string) {
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
  parseFrameworkAPIs(apiNames: string[]): ParsedAPI[] {
    return parseFrameworkAPIs(apiNames);
  }

  /**
   * @apps-in-toss/framework에서 특정 API 관련 타입 정의 파싱
   * (LoadFullScreenAdEvent, ShowFullScreenAdEvent, Options 등)
   */
  parseFrameworkTypeDefinitions(apiNames: string[]): ParsedTypeDefinition[] {
    return parseFrameworkTypeDefinitions(apiNames);
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
      const frameworkAPIs = this.parseFrameworkAPIs(frameworkApiNames);
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
