import { Project, SourceFile, FunctionDeclaration, SyntaxKind } from 'ts-morph';
import { ParsedAPI, ParsedParameter, ParsedProperty, ParsedType, ParsedTypeDefinition, NestedCallback } from './types.js';
import { getCategory } from './categories.js';
import * as path from 'path';
import * as fs from 'fs';

/**
 * TypeScript 빌드 시 생성되는 $1, $2 등의 접미사 제거
 * 예: Location$1 -> Location
 */
function cleanTypeName(name: string): string {
  return name.replace(/\$\d+$/, '');
}

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
    // TossAdsAttachOptions -> AttachOptions 변환 (export alias 처리)
    const typeNameMappings: Record<string, string> = {
      'TossAdsAttachOptions': 'AttachOptions',
      'TossAdsInitializeOptions': 'InitializeOptions',
      'TossAdsBannerSlotCallbacks': 'BannerSlotCallbacks',
    };
    const actualTypeName = typeNameMappings[typeName] || typeName;

    // pnpm virtual store 경로 찾기
    const nmPath = path.join(process.cwd(), 'node_modules');
    const pnpmPath = path.join(nmPath, '.pnpm');

    if (!fs.existsSync(pnpmPath)) {
      return null;
    }

    const pnpmDirs = fs.readdirSync(pnpmPath);

    // 1. native-modules에서 먼저 찾기
    let nativeModulesPath: string | null = null;
    for (const dir of pnpmDirs) {
      if (dir.startsWith('@apps-in-toss+native-modules')) {
        const indexPath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'native-modules', 'dist', 'index.d.ts');
        if (fs.existsSync(indexPath)) {
          nativeModulesPath = indexPath;
          break;
        }
      }
    }

    if (nativeModulesPath) {
      const result = this.parseTypeFromFile(nativeModulesPath, actualTypeName, typeName);
      if (result) return result;
    }

    // 2. web-bridge에서도 찾기 (TossAds 관련 타입 등)
    let webBridgePath: string | null = null;
    for (const dir of pnpmDirs) {
      if (dir.startsWith('@apps-in-toss+web-bridge')) {
        const indexPath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'web-bridge', 'built', 'index.d.ts');
        if (fs.existsSync(indexPath)) {
          webBridgePath = indexPath;
          break;
        }
      }
    }

    if (webBridgePath) {
      const result = this.parseTypeFromFile(webBridgePath, actualTypeName, typeName);
      if (result) return result;
    }

    return null;
  }

  /**
   * 특정 파일에서 타입 정의 파싱 (interface 또는 type alias)
   */
  private parseTypeFromFile(
    filePath: string,
    searchTypeName: string,
    outputTypeName: string
  ): ParsedTypeDefinition | null {
    // 별도 프로젝트로 파싱 (메인 프로젝트 오염 방지)
    const tempProject = new Project({
      skipAddingFilesFromTsConfig: true,
    });
    const sourceFile = tempProject.addSourceFileAtPath(filePath);
    const fileName = path.basename(filePath);

    // 1. interface 찾기
    const interfaces = sourceFile.getInterfaces();
    const targetInterface = interfaces.find(i => i.getName() === searchTypeName);

    if (targetInterface) {
      const members = targetInterface.getMembers();
      const properties = this.parseTypeMembers(members);

      if (properties.length === 0) {
        return null;
      }

      return {
        name: outputTypeName,
        kind: 'interface',
        file: fileName,
        description: undefined,
        properties,
      };
    }

    // 2. type alias 찾기
    const typeAliases = sourceFile.getTypeAliases();
    const targetTypeAlias = typeAliases.find(t => t.getName() === searchTypeName);

    if (targetTypeAlias) {
      const typeNode = targetTypeAlias.getTypeNode();
      if (typeNode && typeNode.getKind() === SyntaxKind.TypeLiteral) {
        const typeLiteral = typeNode.asKind(SyntaxKind.TypeLiteral);
        if (typeLiteral) {
          const members = typeLiteral.getMembers();
          const properties = this.parseTypeMembers(members);

          if (properties.length === 0) {
            return null;
          }

          return {
            name: outputTypeName,
            kind: 'interface', // type alias도 interface로 처리 (C# class로 변환됨)
            file: fileName,
            description: undefined,
            properties,
          };
        }
      }
    }

    return null;
  }

  /**
   * @apps-in-toss/framework에서 특정 API 파싱 (loadFullScreenAd, showFullScreenAd 등)
   * 이 API들은 web-framework에서 re-export되지 않으므로 직접 파싱
   */
  parseFrameworkAPIs(apiNames: string[]): ParsedAPI[] {
    const nmPath = path.join(process.cwd(), 'node_modules');
    const pnpmPath = path.join(nmPath, '.pnpm');

    if (!fs.existsSync(pnpmPath)) {
      return [];
    }

    const pnpmDirs = fs.readdirSync(pnpmPath);
    let frameworkPath: string | null = null;

    for (const dir of pnpmDirs) {
      if (dir.startsWith('@apps-in-toss+framework@')) {
        const indexPath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'framework', 'dist', 'index.d.cts');
        if (fs.existsSync(indexPath)) {
          frameworkPath = indexPath;
          break;
        }
      }
    }

    if (!frameworkPath) {
      return [];
    }

    // 별도 프로젝트로 파싱 (메인 프로젝트 오염 방지)
    const tempProject = new Project({
      skipAddingFilesFromTsConfig: true,
      compilerOptions: {
        strictNullChecks: true,
      },
    });
    const sourceFile = tempProject.addSourceFileAtPath(frameworkPath);
    const apis: ParsedAPI[] = [];

    for (const apiName of apiNames) {
      const api = this.parseFrameworkAPI(sourceFile, apiName);
      if (api) {
        apis.push(api);
      }
    }

    return apis;
  }

  /**
   * @apps-in-toss/framework에서 특정 API 관련 타입 정의 파싱
   * (LoadFullScreenAdEvent, ShowFullScreenAdEvent, Options 등)
   */
  parseFrameworkTypeDefinitions(apiNames: string[]): ParsedTypeDefinition[] {
    const nmPath = path.join(process.cwd(), 'node_modules');
    const pnpmPath = path.join(nmPath, '.pnpm');

    if (!fs.existsSync(pnpmPath)) {
      return [];
    }

    const pnpmDirs = fs.readdirSync(pnpmPath);
    let frameworkPath: string | null = null;

    for (const dir of pnpmDirs) {
      if (dir.startsWith('@apps-in-toss+framework@')) {
        const indexPath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'framework', 'dist', 'index.d.cts');
        if (fs.existsSync(indexPath)) {
          frameworkPath = indexPath;
          break;
        }
      }
    }

    if (!frameworkPath) {
      return [];
    }

    // 별도 프로젝트로 파싱 (메인 프로젝트 오염 방지)
    const tempProject = new Project({
      skipAddingFilesFromTsConfig: true,
      compilerOptions: {
        strictNullChecks: true,
      },
    });
    const sourceFile = tempProject.addSourceFileAtPath(frameworkPath);
    const typeDefinitions: ParsedTypeDefinition[] = [];

    for (const apiName of apiNames) {
      // API 관련 타입 이름 패턴 생성
      // loadFullScreenAd -> LoadFullScreenAd
      const pascalName = apiName.charAt(0).toUpperCase() + apiName.slice(1);

      // 필요한 타입들: Options, Event만 (Params는 내부 사용 타입이므로 제외)
      const typePatterns = [
        `${pascalName}Options`,      // LoadFullScreenAdOptions
        `${pascalName}Event`,        // LoadFullScreenAdEvent
      ];

      for (const typeName of typePatterns) {
        const typeDef = this.parseFrameworkInterface(sourceFile, typeName);
        if (typeDef) {
          typeDefinitions.push(typeDef);
        }
      }

      // ShowFullScreenAd의 경우 Union 타입 이벤트 및 데이터 타입 처리
      if (apiName === 'showFullScreenAd') {
        // ShowFullScreenAdEventData 수동 추가 (userEarnedReward의 data 타입)
        typeDefinitions.push({
          name: 'ShowFullScreenAdEventData',
          kind: 'interface',
          file: 'framework/index.d.cts',
          description: 'Data for userEarnedReward event',
          properties: [
            {
              name: 'unitType',
              type: { name: 'string', kind: 'primitive', raw: 'string' },
              optional: false,
              description: undefined,
            },
            {
              name: 'unitAmount',
              type: { name: 'number', kind: 'primitive', raw: 'number' },
              optional: false,
              description: undefined,
            },
          ],
        });

        // ShowFullScreenAdEvent는 Union 타입이므로 특별 처리
        const showEventTypeDef = this.parseFrameworkUnionEventType(sourceFile, 'ShowFullScreenAdEvent');
        if (showEventTypeDef) {
          typeDefinitions.push(showEventTypeDef);
        }
      }
    }

    return typeDefinitions;
  }

  /**
   * framework 패키지에서 인터페이스 타입 정의 파싱
   */
  private parseFrameworkInterface(sourceFile: SourceFile, typeName: string): ParsedTypeDefinition | null {
    const interfaceDecl = sourceFile.getInterface(typeName);

    if (interfaceDecl) {
      const members = interfaceDecl.getMembers();
      const properties: ParsedProperty[] = [];

      for (const member of members) {
        if (member.getKind() === SyntaxKind.PropertySignature) {
          const propSig = member.asKind(SyntaxKind.PropertySignature);
          if (!propSig) continue;

          const propName = propSig.getName();
          const propTypeNode = propSig.getTypeNode();
          const propTypeText = propTypeNode?.getText() || 'any';
          const isOptional = propSig.hasQuestionToken();

          properties.push({
            name: propName,
            type: this.parseFrameworkSimpleType(propTypeText),
            optional: isOptional,
            description: undefined,
          });
        }
      }

      if (properties.length > 0) {
        return {
          name: typeName,
          kind: 'interface',
          file: 'framework/index.d.cts',
          description: undefined,
          properties,
        };
      }
    }

    // Type alias로 정의된 경우 (type LoadFullScreenAdEvent = { type: 'loaded' })
    const typeAlias = sourceFile.getTypeAlias(typeName);
    if (typeAlias) {
      const typeNode = typeAlias.getTypeNode();
      if (typeNode && typeNode.getKind() === SyntaxKind.TypeLiteral) {
        const typeLiteral = typeNode.asKind(SyntaxKind.TypeLiteral);
        if (typeLiteral) {
          const members = typeLiteral.getMembers();
          const properties: ParsedProperty[] = [];

          for (const member of members) {
            if (member.getKind() === SyntaxKind.PropertySignature) {
              const propSig = member.asKind(SyntaxKind.PropertySignature);
              if (!propSig) continue;

              const propName = propSig.getName();
              const propTypeNode = propSig.getTypeNode();
              const propTypeText = propTypeNode?.getText() || 'any';
              const isOptional = propSig.hasQuestionToken();

              properties.push({
                name: propName,
                type: this.parseFrameworkSimpleType(propTypeText),
                optional: isOptional,
                description: undefined,
              });
            }
          }

          if (properties.length > 0) {
            return {
              name: typeName,
              kind: 'interface',
              file: 'framework/index.d.cts',
              description: undefined,
              properties,
            };
          }
        }
      }
    }

    return null;
  }

  /**
   * Framework 타입 파싱 (리터럴 타입 처리 포함)
   * 'loaded' -> string, 'userEarnedReward' -> string 등
   */
  private parseFrameworkSimpleType(typeText: string): ParsedType {
    // 문자열 리터럴 타입 ('loaded', 'userEarnedReward' 등)은 string으로 변환
    if (typeText.startsWith("'") || typeText.startsWith('"')) {
      return { name: 'string', kind: 'primitive', raw: typeText };
    }

    // 함수 타입은 무시 (Params의 콜백 등)
    if (typeText.includes('=>')) {
      return { name: 'object', kind: 'primitive', raw: typeText };
    }

    // 일반 타입은 parseSimpleType 사용
    return this.parseSimpleType(typeText);
  }

  /**
   * ShowFullScreenAdEvent Union 타입 파싱
   * type ShowFullScreenAdEvent = AdMobFullScreenEvent | AdUserEarnedReward | { type: 'requested' }
   */
  private parseFrameworkUnionEventType(sourceFile: SourceFile, typeName: string): ParsedTypeDefinition | null {
    const typeAlias = sourceFile.getTypeAlias(typeName);
    if (!typeAlias) return null;

    // ShowFullScreenAdEvent는 discriminated union
    // C#에서는 단일 클래스로 생성하고 type 프로퍼티로 구분
    // data 프로퍼티는 userEarnedReward일 때만 사용

    const properties: ParsedProperty[] = [
      {
        name: 'type',
        type: { name: 'string', kind: 'primitive', raw: 'string' },
        optional: false,
        description: 'Event type: clicked, dismissed, failedToShow, impression, show, userEarnedReward, requested',
      },
      {
        name: 'data',
        type: { name: 'ShowFullScreenAdEventData', kind: 'object', raw: 'ShowFullScreenAdEventData' },
        optional: true, // userEarnedReward일 때만 존재
        description: 'Event data (only for userEarnedReward)',
      },
    ];

    return {
      name: typeName,
      kind: 'interface',
      file: 'framework/index.d.cts',
      description: 'Full screen ad event (discriminated union)',
      properties,
    };
  }

  /**
   * 개별 framework API 파싱
   */
  private parseFrameworkAPI(sourceFile: SourceFile, apiName: string): ParsedAPI | null {
    // 함수 또는 변수 선언 찾기
    const funcDecl = sourceFile.getFunction(apiName);
    const varDecl = sourceFile.getVariableDeclaration(apiName);

    if (!funcDecl && !varDecl) {
      return null;
    }

    // 파라미터 타입 인터페이스 이름 추론 (예: loadFullScreenAd -> LoadFullScreenAdParams)
    const paramsTypeName = apiName.charAt(0).toUpperCase() + apiName.slice(1) + 'Params';
    const paramsInterface = sourceFile.getInterface(paramsTypeName);

    if (!paramsInterface) {
      return null;
    }

    // Params 인터페이스의 프로퍼티 파싱
    const parameters: ParsedParameter[] = [];
    const members = paramsInterface.getMembers();

    for (const member of members) {
      if (member.getKind() === SyntaxKind.PropertySignature) {
        const propSig = member.asKind(SyntaxKind.PropertySignature);
        if (!propSig) continue;

        const propName = propSig.getName();
        const propTypeNode = propSig.getTypeNode();
        const propTypeText = propTypeNode?.getText() || 'any';
        const isOptional = propSig.hasQuestionToken();

        // onEvent, onError는 콜백으로 특별 처리
        if (propName === 'onEvent' || propName === 'onError') {
          // 함수 타입 파싱
          const parsedType = this.parseSimpleFunctionType(propTypeText);
          parameters.push({
            name: propName,
            type: parsedType,
            optional: isOptional,
            description: propName === 'onEvent' ? '이벤트 콜백' : '에러 콜백',
          });
        } else if (propName === 'options') {
          // options 객체 내부 프로퍼티를 풀어서 파라미터로 추가
          const optionsTypeName = propTypeText;
          const optionsInterface = sourceFile.getInterface(optionsTypeName);

          if (optionsInterface) {
            const optionsMembers = optionsInterface.getMembers();
            for (const optMember of optionsMembers) {
              if (optMember.getKind() === SyntaxKind.PropertySignature) {
                const optPropSig = optMember.asKind(SyntaxKind.PropertySignature);
                if (!optPropSig) continue;

                const optPropName = optPropSig.getName();
                const optPropTypeNode = optPropSig.getTypeNode();
                const optPropTypeText = optPropTypeNode?.getText() || 'any';
                const optIsOptional = optPropSig.hasQuestionToken();

                parameters.push({
                  name: optPropName,
                  type: this.parseSimpleType(optPropTypeText),
                  optional: optIsOptional,
                  description: undefined,
                });
              }
            }
          }
        }
      }
    }

    // JSDoc에서 설명 추출
    let description: string | undefined;
    const jsDocs = funcDecl?.getJsDocs() || varDecl?.getVariableStatement()?.getJsDocs() || [];
    if (jsDocs.length > 0) {
      description = jsDocs[0].getDescription()?.trim();
    }

    // 반환 타입: () => void (구독 해제 함수)
    const returnType: ParsedType = {
      name: 'void',
      kind: 'function',
      functionParams: [],
      raw: '() => void',
    };

    // PascalCase 이름 생성
    const pascalName = this.toPascalCase(apiName);

    // 카테고리 찾기
    let category: string;
    try {
      category = getCategory(apiName);
    } catch {
      category = 'Advertising'; // 기본 카테고리
    }

    return {
      name: apiName,
      pascalName,
      originalName: apiName,
      category,
      file: 'framework/index.d.cts',
      description,
      parameters,
      returnType,
      isAsync: false, // 콜백 기반 API
      hasPermission: false,
      isDeprecated: false,
      deprecatedMessage: undefined,
      isEventSubscription: false,
      // 콜백 기반 API (onEvent/onError 콜백 사용)
      isCallbackBased: true,
      // @apps-in-toss/framework에서 직접 export됨 (AppsInToss 네임스페이스 없음)
      isTopLevelExport: true,
    };
  }

  /**
   * 간단한 함수 타입 파싱 (예: (data: SomeType) => void)
   */
  private parseSimpleFunctionType(typeText: string): ParsedType {
    // 파라미터 추출: (data: Type) => void
    const match = typeText.match(/\(([^)]*)\)\s*=>\s*(\w+)/);
    if (!match) {
      return { name: 'Function', kind: 'function', raw: typeText };
    }

    const paramsText = match[1];
    const returnTypeText = match[2];
    const functionParams: ParsedType[] = [];

    if (paramsText.trim()) {
      // 파라미터 파싱: data: Type 또는 err: unknown
      const paramMatch = paramsText.match(/(\w+)\s*:\s*(.+)/);
      if (paramMatch) {
        const paramType = paramMatch[2].trim();
        functionParams.push(this.parseSimpleType(paramType));
      }
    }

    return {
      name: 'Function',
      kind: 'function',
      functionParams,
      raw: typeText,
    };
  }

  /**
   * 간단한 타입 파싱 (재귀 없이)
   */
  private parseSimpleType(typeText: string): ParsedType {
    const primitives = ['string', 'number', 'boolean', 'void', 'any', 'unknown', 'object'];

    if (primitives.includes(typeText)) {
      return { name: typeText, kind: 'primitive', raw: typeText };
    }

    if (typeText.endsWith('[]')) {
      const elementType = typeText.slice(0, -2);
      return {
        name: 'Array',
        kind: 'array',
        elementType: { name: elementType, kind: 'object', raw: elementType },
        raw: typeText,
      };
    }

    return { name: typeText, kind: 'object', raw: typeText };
  }

  /**
   * 타입 멤버들을 파싱하여 프로퍼티 배열로 변환
   */
  private parseTypeMembers(members: any[]): any[] {
    return members.map(member => {
      if (member.getKind() === SyntaxKind.PropertySignature) {
        const propSig = member.asKind(SyntaxKind.PropertySignature);
        if (propSig) {
          const propName = propSig.getName();
          const propTypeNode = propSig.getTypeNode();
          const propTypeText = propTypeNode?.getText() || 'any';
          const isOptional = propSig.hasQuestionToken();

          // 단순 타입 파싱 (재귀 방지)
          let parsedType: ParsedType;
          if (['string', 'number', 'boolean', 'void', 'any'].includes(propTypeText)) {
            parsedType = { name: propTypeText, kind: 'primitive', raw: propTypeText };
          } else if (propTypeText.endsWith('[]')) {
            const elementTypeName = propTypeText.slice(0, -2);
            parsedType = {
              name: 'Array',
              kind: 'array',
              elementType: { name: elementTypeName, kind: 'object', raw: elementTypeName },
              raw: propTypeText,
            };
          } else if (propTypeText.includes('=>')) {
            // 함수 타입: (param: Type) => ReturnType 또는 () => void
            // 파라미터 추출: (param: Type, param2: Type2) => ...
            const paramsMatch = propTypeText.match(/^\(([^)]*)\)\s*=>/);
            const functionParams: ParsedType[] = [];
            if (paramsMatch && paramsMatch[1].trim()) {
              // 파라미터 목록 파싱 (간단한 버전: 콤마로 분리)
              const paramStrings = paramsMatch[1].split(',').map((s: string) => s.trim());
              for (const paramStr of paramStrings) {
                // "name: Type" 또는 "name?: Type" 형식
                const colonIdx = paramStr.indexOf(':');
                if (colonIdx > 0) {
                  const paramType = paramStr.slice(colonIdx + 1).trim();
                  // 간단한 타입 파싱 (재귀 방지)
                  if (['string', 'number', 'boolean', 'void', 'any'].includes(paramType)) {
                    functionParams.push({ name: paramType, kind: 'primitive', raw: paramType });
                  } else {
                    functionParams.push({ name: paramType, kind: 'object', raw: paramType });
                  }
                }
              }
            }
            parsedType = {
              name: propTypeText,
              kind: 'function',
              functionParams,
              raw: propTypeText,
            };
          } else if (propTypeText.startsWith('Record<')) {
            // Record<K, V> 타입
            const recordMatch = propTypeText.match(/^Record<([^,]+),\s*([^>]+)>/);
            if (recordMatch) {
              const keyTypeText = recordMatch[1].trim();
              const valueTypeText = recordMatch[2].trim();
              parsedType = {
                name: 'Record',
                kind: 'record',
                keyType: { name: keyTypeText, kind: 'primitive', raw: keyTypeText },
                valueType: { name: valueTypeText, kind: 'primitive', raw: valueTypeText },
                raw: propTypeText,
              };
            } else {
              parsedType = { name: propTypeText, kind: 'object', raw: propTypeText };
            }
          } else if (propTypeText.includes('|') && propTypeText.includes("'")) {
            // 문자열 리터럴 union: 'light' | 'dark'
            // C#에서는 string으로 매핑 (enum은 상위 레벨에서 처리)
            parsedType = { name: 'string', kind: 'primitive', raw: propTypeText };
          } else {
            parsedType = { name: propTypeText, kind: 'object', raw: propTypeText };
          }

          return {
            name: propName,
            type: parsedType,
            optional: isOptional,
            description: undefined,
          };
        }
      }
      return null;
    }).filter(p => p !== null);
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
        const namespaceAPIs = this.parseNamespaceObjects(sourceFile);
        apis.push(...namespaceAPIs);
        continue;
      }

      // index.d.cts, bridge.d.ts, types.d.ts 파일은 스킵 (전체 re-export 파일)
      if (fileName === 'index.d.cts' || fileName === 'types.d.ts' || fileName === 'bridge.d.ts') {
        continue;
      }

      const fileAPIs = this.parseSourceFile(sourceFile);
      apis.push(...fileAPIs);
    }

    // @apps-in-toss/framework에서 추가 API 파싱 (web-framework에서 re-export되지 않는 API)
    if (frameworkApiNames && frameworkApiNames.length > 0) {
      const frameworkAPIs = this.parseFrameworkAPIs(frameworkApiNames);
      apis.push(...frameworkAPIs);
    }

    return apis;
  }

  // ============================================================
  // 동적 감지 메서드 (타입 주석 텍스트 기반)
  // getTypeNode().getText()를 사용하여 타입 해석 없이 패턴 매칭
  // 스택 오버플로우 없이 완전 자동 감지
  // ============================================================

  /**
   * VariableDeclaration에서 타입 주석 텍스트만 추출 (JSDoc 제외)
   * getType()을 사용하지 않으므로 스택 오버플로우 위험 없음
   */
  private getTypeAnnotationText(varDecl: any): string {
    try {
      // 타입 노드가 있으면 직접 텍스트 추출 (JSDoc 제외)
      const typeNode = varDecl.getTypeNode?.();
      if (typeNode) {
        return typeNode.getText();
      }
      // 타입 노드가 없으면 빈 문자열 반환 (JSDoc 포함 방지)
      return '';
    } catch {
      return '';
    }
  }

  /**
   * 선언이 현재 소스 파일에 정의되어 있는지 확인 (re-export 제외)
   */
  private isDefinedInFile(decl: any, sourceFile: SourceFile): boolean {
    try {
      const declFile = decl.getSourceFile?.();
      if (!declFile) return false;
      return declFile.getFilePath() === sourceFile.getFilePath();
    } catch {
      return false;
    }
  }

  /**
   * 이벤트 네임스페이스 감지 (타입에 addEventListener 속성 포함)
   * 패턴: const xxxEvent: { addEventListener: <K extends keyof ...> ... }
   */
  private detectEventNamespaces(sourceFile: SourceFile): Set<string> {
    const eventNamespaces = new Set<string>();
    const exportedDeclarations = sourceFile.getExportedDeclarations();

    for (const [name, declarations] of exportedDeclarations) {
      for (const decl of declarations) {
        if (decl.getKind() !== SyntaxKind.VariableDeclaration) continue;
        // 이 파일에 정의된 선언만 처리 (re-export 제외)
        if (!this.isDefinedInFile(decl, sourceFile)) continue;

        const typeText = this.getTypeAnnotationText(decl);
        // addEventListener가 속성으로 정의되어 있는지 확인
        // 패턴: addEventListener: 또는 addEventListener< (JSDoc 내 언급 제외)
        if (/addEventListener\s*[:<]/.test(typeText)) {
          eventNamespaces.add(name);
        }
      }
    }

    return eventNamespaces;
  }

  /**
   * 선언이 deprecated인지 확인 (JSDoc @deprecated 태그)
   */
  private isDeprecatedDeclaration(decl: any): boolean {
    try {
      const jsDocs = decl.getJsDocs?.() || [];
      for (const jsDoc of jsDocs) {
        const tags = jsDoc.getTags?.() || [];
        if (tags.some((tag: any) => tag.getTagName?.() === 'deprecated')) {
          return true;
        }
      }
      return false;
    } catch {
      return false;
    }
  }

  /**
   * 글로벌 함수 감지 (이 파일에 정의된 FunctionDeclaration 또는 단순 화살표 함수)
   * 패턴: declare function NAME(...) 또는 declare const NAME: () => ...
   * deprecated 함수는 제외
   */
  private detectGlobalFunctions(sourceFile: SourceFile): Set<string> {
    const globalFunctions = new Set<string>();
    const exportedDeclarations = sourceFile.getExportedDeclarations();

    for (const [name, declarations] of exportedDeclarations) {
      for (const decl of declarations) {
        // 이 파일에 정의된 선언만 처리 (re-export 제외)
        if (!this.isDefinedInFile(decl, sourceFile)) continue;

        // deprecated 선언은 제외
        if (this.isDeprecatedDeclaration(decl)) continue;

        // Case 1: function declaration (declare function isMinVersionSupported(...))
        if (decl.getKind() === SyntaxKind.FunctionDeclaration) {
          globalFunctions.add(name);
          continue;
        }

        // Case 2: const with simple arrow function type (const getAppsInTossGlobals: () => ...)
        if (decl.getKind() === SyntaxKind.VariableDeclaration) {
          const typeText = this.getTypeAnnotationText(decl);
          // 단순 화살표 함수 타입: () => ... 형태이면서 객체 리터럴이 아닌 경우
          if (/^\s*\(\s*\)\s*=>\s*\w/.test(typeText) && !typeText.includes('{')) {
            globalFunctions.add(name);
          }
        }
      }
    }

    return globalFunctions;
  }

  /**
   * 네임스페이스 객체 감지 (메서드들의 모음인 순수 객체)
   * 패턴: declare const NAME: { method1: (...) => ..., ... } 또는 { method1: typeof fn, ... }
   * 호출 가능한 객체(callable)는 제외 (예: startUpdateLocation)
   */
  private detectNamespaceObjects(
    sourceFile: SourceFile,
    eventNamespaces: Set<string>,
    globalFunctions: Set<string>
  ): Set<string> {
    const namespaceObjects = new Set<string>();
    const exportedDeclarations = sourceFile.getExportedDeclarations();

    for (const [name, declarations] of exportedDeclarations) {
      // 이미 이벤트 네임스페이스거나 글로벌 함수면 스킵
      if (eventNamespaces.has(name) || globalFunctions.has(name)) continue;

      for (const decl of declarations) {
        if (decl.getKind() !== SyntaxKind.VariableDeclaration) continue;
        // 이 파일에 정의된 선언만 처리 (re-export 제외)
        if (!this.isDefinedInFile(decl, sourceFile)) continue;

        const typeText = this.getTypeAnnotationText(decl);

        // 객체 리터럴 타입인지 확인 ({ ... } 형태)
        if (!typeText.startsWith('{')) continue;

        // 호출 가능한 객체는 제외 (타입이 (...)로 시작하면 callable)
        // 예: { (params: Foo): Bar; getPermission(): ... }
        if (/^\{\s*\(/.test(typeText)) continue;

        // 메서드가 있는지 확인:
        // 패턴 1: => (화살표 함수)
        // 패턴 2: typeof (함수 참조)
        const hasArrowMethods = typeText.includes('=>');
        const hasTypeofMethods = typeText.includes('typeof ');

        if (!hasArrowMethods && !hasTypeofMethods) continue;

        namespaceObjects.add(name);
      }
    }

    return namespaceObjects;
  }

  /**
   * index.d.ts에서 네임스페이스 객체, 이벤트 네임스페이스, 글로벌 함수 파싱
   * 모든 항목을 동적으로 감지하여 파싱
   */
  private parseNamespaceObjects(sourceFile: SourceFile): ParsedAPI[] {
    const apis: ParsedAPI[] = [];
    const exportedDeclarations = sourceFile.getExportedDeclarations();

    // 1. 이벤트 네임스페이스 감지 (addEventListener 패턴)
    const eventNamespaces = this.detectEventNamespaces(sourceFile);

    // 2. 글로벌 함수 감지 (FunctionDeclaration)
    const globalFunctions = this.detectGlobalFunctions(sourceFile);

    // 3. 네임스페이스 객체 감지 (나머지 중 메서드만 있는 객체)
    const namespaceObjects = this.detectNamespaceObjects(sourceFile, eventNamespaces, globalFunctions);

    // 감지된 항목 로깅 (디버깅용)
    // console.log('Detected event namespaces:', [...eventNamespaces]);
    // console.log('Detected global functions:', [...globalFunctions]);
    // console.log('Detected namespace objects:', [...namespaceObjects]);

    for (const [name, declarations] of exportedDeclarations) {
      // 네임스페이스 객체 파싱 (동적 감지됨)
      if (namespaceObjects.has(name)) {
        for (const declaration of declarations) {
          if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
            const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
            if (varDecl) {
              const namespaceAPIs = this.parseNamespaceObject(name, varDecl, sourceFile);
              apis.push(...namespaceAPIs);
            }
          }
        }
      }

      // 이벤트 네임스페이스 파싱 (동적 감지됨)
      if (eventNamespaces.has(name)) {
        const eventAPIs = this.parseEventNamespace(name, sourceFile);
        apis.push(...eventAPIs);
      }

      // 글로벌 함수 파싱 (동적 감지됨)
      if (globalFunctions.has(name)) {
        for (const declaration of declarations) {
          if (declaration.getKind() === SyntaxKind.FunctionDeclaration) {
            const func = declaration as FunctionDeclaration;
            const api = this.parseFunctionDeclaration(func, sourceFile);
            if (api) {
              // 글로벌 함수는 Environment 카테고리로 설정
              api.category = 'Environment';
              apis.push(api);
            }
          }
          // 변수 선언 형태의 함수도 처리
          if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
            const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
            if (varDecl) {
              const type = varDecl.getType();
              const callSignatures = type.getCallSignatures();
              if (callSignatures.length > 0) {
                const api = this.parseVariableFunction(name, varDecl, sourceFile);
                if (api) {
                  api.category = 'Environment';
                  apis.push(api);
                }
              }
            }
          }
        }
      }
    }

    return apis;
  }

  /**
   * 네임스페이스 객체의 메서드들을 파싱
   * 예: IAP.getProductItemList, Storage.getItem 등
   */
  private parseNamespaceObject(
    namespaceName: string,
    varDecl: any,
    sourceFile: SourceFile
  ): ParsedAPI[] {
    const apis: ParsedAPI[] = [];
    const type = varDecl.getType();
    const properties = type.getProperties?.() || [];

    // JSDoc에서 각 프로퍼티의 설명 추출을 위해 원본 텍스트도 확인
    const varDeclText = varDecl.getText?.() || '';

    for (const prop of properties) {
      const methodName = prop.getName();
      const propType = prop.getTypeAtLocation?.(varDecl) || prop.getDeclaredType?.();

      if (!propType) continue;

      // 메서드인지 확인 (함수 타입)
      const callSignatures = propType.getCallSignatures?.() || [];
      if (callSignatures.length === 0) continue;

      const signature = callSignatures[0];

      // JSDoc에서 deprecated 확인
      const jsDocComment = this.extractJsDocForProperty(varDeclText, methodName);
      const isDeprecated = jsDocComment.includes('@deprecated');
      const deprecatedMatch = jsDocComment.match(/@deprecated\s+(.+?)(?=\n\s*\*\s*@|\n\s*\*\/|$)/s);
      const deprecatedMessage = deprecatedMatch ? deprecatedMatch[1].trim() : undefined;

      // 설명 추출
      const descriptionMatch = jsDocComment.match(/@description\s+(.+?)(?=\n\s*\*\s*@|\n\s*\*\/|$)/s);
      const description = descriptionMatch ? descriptionMatch[1].trim() : undefined;

      // 파라미터 파싱
      const parameters = signature.getParameters?.().map((param: any, index: number) => {
        let paramName = param.getName();
        if (paramName.includes('{') || paramName.includes('}') || paramName.includes(',')) {
          paramName = `options${index > 0 ? index : ''}`;
        }
        const valueDecl = param.getValueDeclaration?.();
        const paramType = valueDecl?.getType?.() || param.getDeclaredType?.();
        // optional 확인: isOptional() 또는 hasQuestionToken() (typeof 참조 시 isOptional이 false 반환할 수 있음)
        const isOptional = param.isOptional?.() || valueDecl?.hasQuestionToken?.() || false;
        return {
          name: paramName,
          type: paramType ? this.parseType(paramType) : { name: 'any', kind: 'primitive' as const, raw: 'any' },
          optional: isOptional,
          description: undefined,
        };
      }) || [];

      const returnType = this.parseType(signature.getReturnType());
      const isAsync = returnType.kind === 'promise';

      // C# 메서드 이름: 네임스페이스 + PascalCase 메서드명
      // 예: IAP.getProductItemList → IAPGetProductItemList
      const pascalMethodName = this.toPascalCase(methodName);
      const fullName = `${namespaceName}${pascalMethodName}`;

      // 네임스페이스를 카테고리로 사용
      const category = this.getNamespaceCategory(namespaceName);

      // 중첩 콜백 감지 (options 파라미터 내부의 함수 타입)
      const nestedCallbacks = this.detectNestedCallbacks(parameters);

      // 콜백 기반 API 감지 (onEvent/onError 패턴)
      const isCallbackBased = this.detectCallbackBasedPattern(parameters, returnType);

      apis.push({
        name: fullName,
        pascalName: this.toPascalCase(fullName),
        originalName: methodName,
        category,
        file: sourceFile.getFilePath(),
        description,
        parameters,
        returnType,
        isAsync: isCallbackBased ? false : isAsync, // 콜백 기반 API는 동기로 처리
        hasPermission: false,
        namespace: namespaceName,
        isDeprecated,
        deprecatedMessage,
        nestedCallbacks: nestedCallbacks.length > 0 ? nestedCallbacks : undefined,
        isCallbackBased,
      });
    }

    return apis;
  }

  /**
   * 콜백 기반 API 패턴 감지
   *
   * 조건:
   * 1. 파라미터가 1개 (args 객체)
   * 2. args 객체에 onEvent, onError 함수 프로퍼티 존재
   * 3. 반환 타입이 () => void (cleanup/dispose 함수)
   *
   * 예: GoogleAdMob.loadAppsInTossAdMob, loadFullScreenAd 등
   */
  private detectCallbackBasedPattern(
    parameters: ParsedParameter[],
    returnType: ParsedType
  ): boolean {
    // 조건 1: 파라미터가 1개
    if (parameters.length !== 1) return false;

    const argsType = parameters[0].type;

    // 조건 2: args가 object 타입이고 properties가 있어야 함
    if (argsType.kind !== 'object' || !argsType.properties) return false;

    // onEvent와 onError 함수 프로퍼티 존재 확인
    const hasOnEvent = argsType.properties.some(
      (p) => p.name === 'onEvent' && p.type.kind === 'function'
    );
    const hasOnError = argsType.properties.some(
      (p) => p.name === 'onError' && p.type.kind === 'function'
    );

    if (!hasOnEvent || !hasOnError) return false;

    // 조건 3: 반환 타입이 () => void (cleanup 함수)
    // returnType.kind가 'function'이고 반환값이 void인 경우
    const isCleanupReturn =
      returnType.kind === 'function' &&
      (returnType.raw?.includes('() => void') ||
        returnType.functionReturnType?.name === 'void');

    return isCleanupReturn;
  }

  /**
   * 파라미터에서 중첩 콜백 감지
   *
   * 구조 기반 감지:
   * - 최상위 함수 프로퍼티 (param.onEvent, param.onError): 이벤트 콜백 시스템 사용
   * - object 타입 프로퍼티 내부의 함수 (param.options.processProductGrant): 중첩 콜백
   *
   * 이름이 아닌 구조(depth)로 구분하므로, 어떤 이름의 콜백도 올바르게 처리됨
   */
  private detectNestedCallbacks(parameters: ParsedParameter[]): NestedCallback[] {
    const nestedCallbacks: NestedCallback[] = [];
    const seenCallbacks = new Set<string>(); // 중복 방지

    // object 타입 내부에서 중첩 콜백을 추출하는 재귀 헬퍼 함수
    // depth가 1 이상인 함수 프로퍼티만 중첩 콜백으로 취급
    const extractNestedCallbacksFromType = (
      type: ParsedType,
      path: string[],
      depth: number  // 0 = 최상위 파라미터 레벨, 1+ = 중첩
    ): void => {
      // object 타입: properties 확인
      if (type.kind === 'object' && type.properties) {
        for (const prop of type.properties) {
          if (prop.type.kind === 'function') {
            // depth가 1 이상이면 중첩 콜백 (options.xyz, nested.abc 등)
            if (depth >= 1) {
              const callbackKey = [...path, prop.name].join('.');
              if (!seenCallbacks.has(callbackKey)) {
                seenCallbacks.add(callbackKey);
                nestedCallbacks.push({
                  name: prop.name,
                  path: [...path, prop.name],
                  parameterType: prop.type.functionParams?.[0],
                  returnType: prop.type.functionReturnType,
                });
              }
            }
            // depth 0의 함수 프로퍼티는 최상위 콜백이므로 무시
          } else if (prop.type.kind === 'object' || prop.type.kind === 'union') {
            // object 또는 union 타입이면 재귀 탐색 (depth 증가)
            extractNestedCallbacksFromType(
              prop.type,
              [...path, prop.name],
              depth + 1
            );
          }
        }
      }

      // union 타입: 각 멤버 확인 (인터섹션 타입이 잘못 파싱된 경우 포함)
      if (type.kind === 'union' && type.unionTypes) {
        for (const unionMember of type.unionTypes) {
          extractNestedCallbacksFromType(unionMember, path, depth);
        }
      }
    };

    for (const param of parameters) {
      // object 타입 파라미터 확인
      if (param.type.kind !== 'object' || !param.type.properties) {
        continue;
      }

      // 파라미터의 각 프로퍼티 확인 (depth 0에서 시작)
      for (const prop of param.type.properties) {
        if (prop.type.kind === 'object' || prop.type.kind === 'union') {
          // object/union 타입 프로퍼티 내부 탐색 (depth 1부터 중첩 콜백)
          extractNestedCallbacksFromType(
            prop.type,
            [prop.name],
            1  // 이 레벨의 함수부터 중첩 콜백
          );
        }
        // depth 0의 함수 타입 프로퍼티는 최상위 콜백이므로 여기서 무시
      }
    }

    return nestedCallbacks;
  }

  /**
   * 변수 선언의 텍스트에서 특정 프로퍼티의 JSDoc 추출
   */
  private extractJsDocForProperty(varDeclText: string, propertyName: string): string {
    // JSDoc 주석 패턴: /** ... */ propertyName:
    const regex = new RegExp(`(\\/\\*\\*[\\s\\S]*?\\*\\/)\\s*(?:\\[?"?)?${propertyName}(?:"?\\])?\\s*:`, 'm');
    const match = varDeclText.match(regex);
    return match ? match[1] : '';
  }

  /**
   * 네임스페이스 → 카테고리 특수 매핑 (기본값: 네임스페이스명을 PascalCase로)
   * 새 네임스페이스가 추가되어도 자동으로 처리됨
   */
  private static readonly NAMESPACE_CATEGORY_OVERRIDES: Record<string, string> = {
    GoogleAdMob: 'Advertising',
    SafeAreaInsets: 'SafeArea',
    env: 'Environment',
  };

  /**
   * 네임스페이스 이름을 카테고리로 변환
   * - 특수 매핑이 있으면 사용
   * - 없으면 PascalCase로 변환하여 카테고리로 사용
   */
  private getNamespaceCategory(namespaceName: string): string {
    // 특수 매핑 확인
    if (TypeScriptParser.NAMESPACE_CATEGORY_OVERRIDES[namespaceName]) {
      return TypeScriptParser.NAMESPACE_CATEGORY_OVERRIDES[namespaceName];
    }
    // 기본값: PascalCase로 변환 (partner → Partner, IAP → IAP)
    return this.toPascalCase(namespaceName);
  }

  /**
   * 이벤트 타입 정의에서 이벤트 목록 동적 파싱
   * 예: TdsEvent, GraniteEvent, AppsInTossEvent 타입 별칭에서 이벤트 추출
   */
  private parseEventTypeDefinition(
    typeName: string,
    sourceFile: SourceFile
  ): { eventName: string; hasData: boolean; dataType?: string }[] {
    const events: { eventName: string; hasData: boolean; dataType?: string }[] = [];

    // 타입 별칭 찾기
    const exportedDeclarations = sourceFile.getExportedDeclarations();
    const typeDeclarations = exportedDeclarations.get(typeName);

    if (!typeDeclarations || typeDeclarations.length === 0) {
      return events;
    }

    const typeAlias = typeDeclarations[0];
    if (typeAlias.getKind() !== SyntaxKind.TypeAliasDeclaration) {
      return events;
    }

    // 타입의 프로퍼티 추출 (이벤트 이름들)
    const type = typeAlias.getType();
    const properties = type.getProperties();

    for (const prop of properties) {
      const eventName = prop.getName();
      const propType = prop.getTypeAtLocation(typeAlias);

      // onEvent 프로퍼티 찾기
      const onEventProp = propType.getProperty('onEvent');
      if (!onEventProp) continue;

      const onEventType = onEventProp.getTypeAtLocation(typeAlias);
      const callSignatures = onEventType.getCallSignatures();

      if (callSignatures.length > 0) {
        const params = callSignatures[0].getParameters();
        const hasData = params.length > 0;

        let dataType: string | undefined;
        if (hasData && params[0]) {
          const paramType = params[0].getTypeAtLocation(typeAlias);
          // 인라인 객체 타입인 경우 이벤트명 기반으로 타입 이름 생성
          const typeText = paramType.getText();
          if (typeText.startsWith('{')) {
            // 인라인 객체: TdsNavigationAccessoryEventData 형태로 생성 (네임스페이스 접두사 포함)
            // typeName에서 'Event' 접미사를 제거하여 네임스페이스 접두사 추출 (TdsEvent → Tds)
            const namespacePrefix = typeName.replace(/Event$/, '');
            dataType = `${namespacePrefix}${this.toPascalCase(eventName)}Data`;
          } else {
            // 명명된 타입 사용
            dataType = typeText;
          }
        }

        events.push({ eventName, hasData, dataType });
      }
    }

    return events;
  }

  /**
   * 이벤트 네임스페이스 파싱 (addEventListener 패턴이 있는 객체)
   * 각 이벤트를 별도의 Subscribe 메서드로 생성
   */
  private parseEventNamespace(namespaceName: string, sourceFile: SourceFile): ParsedAPI[] {
    const apis: ParsedAPI[] = [];
    // 이벤트 타입 이름을 동적으로 결정: tdsEvent → TdsEvent (첫 글자 대문자화)
    const typeName = this.toPascalCase(namespaceName);

    // 동적으로 이벤트 정의 파싱
    const eventDefs = this.parseEventTypeDefinition(typeName, sourceFile);

    for (const eventDef of eventDefs) {
      // C# 메서드 이름: 네임스페이스 + Subscribe + PascalCase 이벤트명
      // 예: tdsEvent.addEventListener('navigationAccessoryEvent', ...)
      //     → TdsEventSubscribeNavigationAccessoryEvent
      const pascalNamespace = this.toPascalCase(namespaceName);
      const pascalEventName = this.toPascalCase(eventDef.eventName);
      const fullName = `${pascalNamespace}Subscribe${pascalEventName}`;

      // 이벤트 데이터 타입
      let eventDataType: ParsedType | undefined;
      if (eventDef.hasData && eventDef.dataType) {
        eventDataType = {
          name: eventDef.dataType,
          kind: 'object',
          raw: eventDef.dataType,
        };
      }

      // 반환 타입은 Action (구독 해제 함수)
      const returnType: ParsedType = {
        name: 'Action',
        kind: 'function',
        raw: '() => void',
      };

      apis.push({
        name: fullName,
        pascalName: fullName,
        originalName: 'addEventListener',
        category: 'AppEvents',
        file: sourceFile.getFilePath(),
        description: `${namespaceName}.${eventDef.eventName} 이벤트를 구독합니다.`,
        parameters: [], // 이벤트 API는 콜백 파라미터를 C# 템플릿에서 직접 생성
        returnType,
        isAsync: false, // 이벤트 구독은 동기적
        hasPermission: false,
        namespace: namespaceName,
        // 이벤트 API 전용 필드
        isEventSubscription: true,
        eventName: eventDef.eventName,
        eventDataType,
      });
    }

    return apis;
  }

  /**
   * 단일 소스 파일 파싱
   */
  private parseSourceFile(sourceFile: SourceFile): ParsedAPI[] {
    const apis: ParsedAPI[] = [];

    // export된 함수 찾기
    const exportedDeclarations = sourceFile.getExportedDeclarations();

    for (const [name, declarations] of exportedDeclarations) {
      for (const declaration of declarations) {
        // 함수 선언
        if (declaration.getKind() === SyntaxKind.FunctionDeclaration) {
          const func = declaration as FunctionDeclaration;
          const api = this.parseFunctionDeclaration(func, sourceFile);
          if (api) {
            apis.push(api);
          }
        }

        // 변수 선언 (const openCamera = ..., const getClipboardText: PermissionFunctionWithDialog<...>)
        if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
          const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
          if (varDecl) {
            const initializer = varDecl.getInitializer();
            // Arrow function (const fn = () => {})
            if (initializer && initializer.getKind() === SyntaxKind.ArrowFunction) {
              const api = this.parseVariableFunction(name, varDecl, sourceFile);
              if (api) {
                apis.push(api);
              }
            }
            // Const with function type (const fn: FnType = ...)
            // Arrow function이 없어도 타입이 함수면 파싱
            else {
              const type = varDecl.getType();
              const callSignatures = type.getCallSignatures();
              if (callSignatures.length > 0) {
                const api = this.parseVariableFunction(name, varDecl, sourceFile);
                if (api) {
                  apis.push(api);
                }
              }
            }
          }
        }
      }
    }

    return apis;
  }

  /**
   * 함수 선언 파싱
   */
  private parseFunctionDeclaration(
    func: FunctionDeclaration,
    sourceFile: SourceFile
  ): ParsedAPI | null {
    const name = func.getName();
    if (!name) return null;

    const jsDoc = func.getJsDocs()[0];
    const description = jsDoc?.getDescription().trim();

    // JSDoc에서 추가 정보 추출
    const paramDescriptions = this.extractParamDescriptions(jsDoc);
    const returnDescription = this.extractReturnsDescription(jsDoc);
    const examples = this.extractExamples(jsDoc);

    // 파라미터 파싱 (JSDoc 설명 포함)
    const parameters = func.getParameters().map((param, index) => {
      let paramName = param.getName();
      // Destructuring parameter ({ foo }) 처리: 간단한 이름으로 변경
      if (paramName.includes('{') || paramName.includes('}') || paramName.includes(',')) {
        paramName = `options${index > 0 ? index : ''}`;
      }
      // optional 확인: isOptional() 또는 hasQuestionToken()
      const isOptional = param.isOptional() || param.hasQuestionToken?.() || false;
      return {
        name: paramName,
        type: this.parseType(param.getType()),
        optional: isOptional,
        description: paramDescriptions.get(param.getName()), // 원본 이름으로 설명 찾기
      };
    });

    const returnType = this.parseType(func.getReturnType());

    // returnType이 Promise인지 확인하여 동기/비동기 구분
    const isAsync = returnType.kind === 'promise';
    const hasPermission = this.checkPermissionSupport(func);

    // 항상 .d.ts 파일명에서 카테고리 추출 (@category JSDoc 태그 무시)
    // Emscripten이 한글 파일명을 처리하지 못하므로, 영어 파일명 기반 카테고리 사용
    return {
      name,
      pascalName: this.toPascalCase(name),
      originalName: name,
      category: this.getCategoryFromPath(sourceFile.getFilePath()),
      file: sourceFile.getFilePath(),
      description,
      returnDescription,
      examples,
      parameters,
      returnType,
      isAsync,
      hasPermission,
    };
  }

  /**
   * 변수 함수 파싱 (const fn = () => {})
   */
  private parseVariableFunction(
    name: string,
    varDecl: any,
    sourceFile: SourceFile
  ): ParsedAPI | null {
    const type = varDecl.getType();
    const signatures = type.getCallSignatures();

    if (signatures.length === 0) return null;

    const signature = signatures[0];
    // getJsDocs()가 있으면 사용, 없으면 빈 배열
    const jsDocs = typeof varDecl.getJsDocs === 'function' ? varDecl.getJsDocs() : [];
    const jsDoc = jsDocs[0];
    const description = jsDoc?.getDescription?.()?.trim();

    // JSDoc에서 추가 정보 추출
    const paramDescriptions = this.extractParamDescriptions(jsDoc);
    const returnDescription = this.extractReturnsDescription(jsDoc);
    const examples = this.extractExamples(jsDoc);

    // 파라미터 파싱 (JSDoc 설명 포함)
    const parameters = signature.getParameters().map((param: any, index: number) => {
      let paramName = param.getName();
      // Destructuring parameter ({ foo }) 처리: 간단한 이름으로 변경
      if (paramName.includes('{') || paramName.includes('}') || paramName.includes(',')) {
        paramName = `options${index > 0 ? index : ''}`;
      }
      const valueDecl = param.getValueDeclaration?.();
      const paramType = valueDecl?.getType?.() || param.getDeclaredType?.();
      // optional 확인: isOptional() 또는 hasQuestionToken() (typeof 참조 시 isOptional이 false 반환할 수 있음)
      const isOptional = param.isOptional?.() || valueDecl?.hasQuestionToken?.() || false;
      return {
        name: paramName,
        type: paramType ? this.parseType(paramType) : { name: 'any', kind: 'primitive', raw: 'any' },
        optional: isOptional,
        description: paramDescriptions.get(param.getName()), // 원본 이름으로 설명 찾기
      };
    });

    const returnType = this.parseType(signature.getReturnType());
    // returnType이 Promise인지 확인하여 동기/비동기 구분
    const isAsync = returnType.kind === 'promise';
    const hasPermission = false; // TODO: 검증 로직 추가

    // 항상 .d.ts 파일명에서 카테고리 추출 (@category JSDoc 태그 무시)
    // Emscripten이 한글 파일명을 처리하지 못하므로, 영어 파일명 기반 카테고리 사용
    return {
      name,
      pascalName: this.toPascalCase(name),
      originalName: name,
      category: this.getCategoryFromPath(sourceFile.getFilePath()),
      file: sourceFile.getFilePath(),
      description,
      returnDescription,
      examples,
      parameters,
      returnType,
      isAsync,
      hasPermission,
    };
  }

  /**
   * DOM/브라우저 전용 타입 목록
   * 이 타입들은 Unity에서 사용할 수 없으므로 'object'로 처리
   * 순환 참조가 많아 스택 오버플로우를 유발할 수 있음
   */
  private static readonly DOM_TYPES = new Set([
    'HTMLElement',
    'Element',
    'Node',
    'Document',
    'Window',
    'Event',
    'EventTarget',
    'HTMLDivElement',
    'HTMLSpanElement',
    'HTMLInputElement',
    'HTMLButtonElement',
    'HTMLAnchorElement',
    'HTMLImageElement',
    'HTMLCanvasElement',
    'HTMLVideoElement',
    'HTMLAudioElement',
    'HTMLFormElement',
    'HTMLSelectElement',
    'HTMLTextAreaElement',
    'HTMLTableElement',
    'HTMLIFrameElement',
    'SVGElement',
    'SVGSVGElement',
    'DocumentFragment',
    'ShadowRoot',
    'Text',
    'Comment',
    'Attr',
    'NamedNodeMap',
    'NodeList',
    'HTMLCollection',
    'DOMTokenList',
    'CSSStyleDeclaration',
    'DOMRect',
    'DOMRectReadOnly',
    'TouchEvent',
    'MouseEvent',
    'KeyboardEvent',
    'PointerEvent',
    'FocusEvent',
    'WheelEvent',
    'DragEvent',
    'ClipboardEvent',
    'AnimationEvent',
    'TransitionEvent',
  ]);

  /**
   * 타입 파싱 (ts-morph Type 객체 사용)
   */
  private parseType(typeNode: any): ParsedType {
    const typeText = typeNode.getText?.() || typeNode.toString();

    // DOM/브라우저 전용 타입 감지 (순환 참조로 인한 스택 오버플로우 방지)
    // Unity에서는 DOM 타입을 사용할 수 없으므로 'object'로 처리
    if (TypeScriptParser.DOM_TYPES.has(typeText)) {
      return {
        name: 'object',
        kind: 'primitive',
        raw: typeText,
      };
    }

    // Primitive 타입 (string literal도 포함)
    if (['string', 'number', 'boolean', 'void', 'any', 'unknown', 'undefined', 'null', 'never'].includes(typeText)) {
      return {
        name: typeText,
        kind: 'primitive',
        raw: typeText,
      };
    }

    // 텍스트 기반 nullable 패턴 감지: "SomeType | undefined" 또는 "SomeType | null"
    // ts-morph가 isUnion()을 false로 반환하는 경우가 있어서 텍스트 기반 체크 추가
    // 단순 패턴만 처리 (단일 타입 | null/undefined)
    const nullableSimpleMatch = typeText.match(/^([^|]+)\s*\|\s*(undefined|null)$/);
    if (nullableSimpleMatch) {
      const baseTypeText = nullableSimpleMatch[1].trim();
      // 기본 타입이 추가 | 를 포함하지 않는 경우만 처리 (복잡한 union 제외)
      if (!baseTypeText.includes('|')) {
        // 재귀적으로 기본 타입 파싱 (새로운 객체로 생성하여 무한 루프 방지)
        const baseType = this.parseType({ getText: () => baseTypeText });
        return {
          ...baseType,
          isNullable: true,
          raw: typeText,
        };
      }
    }

    // String literal 타입 ("foo", 'bar')
    if (typeText.startsWith('"') || typeText.startsWith("'")) {
      return {
        name: 'string',
        kind: 'primitive',
        raw: typeText,
      };
    }

    // Template literal 타입 (`${number}.${number}.${number}`)
    // 이런 타입은 결국 string이므로 string으로 처리
    if (typeText.startsWith('`')) {
      return {
        name: 'string',
        kind: 'primitive',
        raw: typeText,
      };
    }

    // Number literal 타입 (123, 0.5)
    if (/^-?\d+(\.\d+)?$/.test(typeText)) {
      return {
        name: 'number',
        kind: 'primitive',
        raw: typeText,
      };
    }

    // ts-morph Type 메서드 활용
    const isArray = typeNode.isArray?.();
    const isObject = typeNode.isObject?.();
    const isUnion = typeNode.isUnion?.();
    const symbol = typeNode.getSymbol?.();

    // Enum 타입 감지: typeNode가 enum이면 symbol에서 타입 이름 추출
    // typeText가 enum value (예: "Lowest")일 경우, symbol에서 enum 타입 이름을 가져옴
    if (symbol) {
      const declarations = symbol.getDeclarations();
      if (declarations && declarations.length > 0) {
        for (const decl of declarations) {
          // EnumMember인 경우, 부모 Enum의 이름을 가져옴
          if (decl.getKind() === SyntaxKind.EnumMember) {
            const parentEnum = decl.getParent();
            if (parentEnum && parentEnum.getKind() === SyntaxKind.EnumDeclaration) {
              const enumName = parentEnum.asKind(SyntaxKind.EnumDeclaration)?.getName();
              if (enumName) {
                return {
                  name: enumName,
                  kind: 'object', // enum은 object 취급
                  raw: enumName,
                };
              }
            }
          }
        }
      }
    }

    // Array 타입
    if (isArray || typeText.endsWith('[]')) {
      const elementType = typeNode.getArrayElementType?.();
      return {
        name: 'Array',
        kind: 'array',
        elementType: elementType
          ? this.parseType(elementType)
          : this.parseType({ getText: () => typeText.slice(0, -2) }),
        raw: typeText,
      };
    }

    // Promise 타입 (제네릭 타입 체크)
    if (typeText.startsWith('Promise<')) {
      const typeArgs = typeNode.getTypeArguments?.();
      if (typeArgs && typeArgs.length > 0) {
        return {
          name: 'Promise',
          kind: 'promise',
          promiseType: this.parseType(typeArgs[0]),
          raw: typeText,
        };
      }
      // Fallback: 문자열 파싱
      const innerType = typeText.slice(8, -1);
      return {
        name: 'Promise',
        kind: 'promise',
        promiseType: this.parseType({ getText: () => innerType }),
        raw: typeText,
      };
    }

    // Record 타입 (Record<K, V> -> Dictionary<K, V>)
    if (typeText.startsWith('Record<')) {
      const typeArgs = typeNode.getTypeArguments?.();
      if (typeArgs && typeArgs.length >= 2) {
        return {
          name: 'Record',
          kind: 'record',
          keyType: this.parseType(typeArgs[0]),
          valueType: this.parseType(typeArgs[1]),
          raw: typeText,
        };
      }
      // Fallback: 기본 Dictionary<string, object>
      return {
        name: 'Record',
        kind: 'record',
        keyType: { name: 'string', kind: 'primitive', raw: 'string' },
        valueType: { name: 'object', kind: 'primitive', raw: 'object' },
        raw: typeText,
      };
    }

    // Union 타입
    if (isUnion) {
      const unionTypes = typeNode.getUnionTypes?.() || [];
      const parsedUnionTypes = unionTypes.map((t: any) => this.parseType(t));

      // Nullable 패턴 감지: T | null 또는 T | undefined
      const nullTypes = parsedUnionTypes.filter((t: ParsedType) =>
        t.name === 'null' || t.name === 'undefined'
      );
      const nonNullTypes = parsedUnionTypes.filter((t: ParsedType) =>
        t.name !== 'null' && t.name !== 'undefined'
      );

      // 단일 타입 + null/undefined = nullable 타입
      if (nullTypes.length > 0 && nonNullTypes.length === 1) {
        return {
          ...nonNullTypes[0],
          isNullable: true,
          raw: typeText,
        };
      }

      // Discriminated Union 감지: 객체 1개 + 문자열 리터럴 N개
      // undefined가 포함된 경우 제외하고 검사 (T | "error1" | "error2" | undefined)
      const objectTypes = nonNullTypes.filter((t: ParsedType) => t.kind === 'object');
      const stringLiterals = nonNullTypes.filter((t: ParsedType) =>
        t.kind === 'primitive' && t.name === 'string' && t.raw.startsWith('"')
      );

      const isDiscriminatedUnion = objectTypes.length === 1 && stringLiterals.length > 0;

      if (isDiscriminatedUnion) {
        const result: ParsedType = {
          name: typeText,
          kind: 'union',
          unionTypes: nonNullTypes, // undefined 제외
          raw: typeText,
          isDiscriminatedUnion: true,
          successType: objectTypes[0],
          errorCodes: stringLiterals.map((t: ParsedType) => t.raw.replace(/['"]/g, '')),
        };
        // undefined가 포함된 경우 nullable 플래그 설정
        if (nullTypes.length > 0) {
          result.isNullable = true;
        }
        return result;
      }

      return {
        name: typeText,
        kind: 'union',
        unionTypes: parsedUnionTypes,
        raw: typeText,
      };
    }

    // Intersection 타입 (예: Sku & { processProductGrant: ... })
    const isIntersection = typeNode.isIntersection?.();
    if (isIntersection) {
      const intersectionTypes = typeNode.getIntersectionTypes?.() || [];

      // 모든 intersection 멤버의 프로퍼티를 병합
      const mergedProperties: ParsedProperty[] = [];
      const seenProps = new Set<string>();

      for (const intersectType of intersectionTypes) {
        const parsed = this.parseType(intersectType);

        // Union 타입의 경우 각 멤버에서 프로퍼티 수집
        if (parsed.kind === 'union' && parsed.unionTypes) {
          for (const unionMember of parsed.unionTypes) {
            if (unionMember.properties) {
              for (const prop of unionMember.properties) {
                if (!seenProps.has(prop.name)) {
                  seenProps.add(prop.name);
                  mergedProperties.push(prop);
                }
              }
            }
          }
        }
        // Object 타입의 경우 프로퍼티 직접 수집
        else if (parsed.properties) {
          for (const prop of parsed.properties) {
            if (!seenProps.has(prop.name)) {
              seenProps.add(prop.name);
              mergedProperties.push(prop);
            }
          }
        }
        // 인라인 객체인 경우 프로퍼티 파싱
        else if (intersectType.getProperties) {
          const props = intersectType.getProperties?.() || [];
          for (const prop of props) {
            const propName = prop.getName();
            if (!seenProps.has(propName)) {
              seenProps.add(propName);
              const propDecl = prop.getValueDeclaration?.();
              const propType = propDecl?.getType?.() || prop.getDeclaredType?.();
              const parsedPropType: ParsedType = propType ? this.parseType(propType) : { name: 'any', kind: 'primitive' as const, raw: 'any' };

              mergedProperties.push({
                name: propName,
                type: parsedPropType,
                optional: propDecl?.hasQuestionToken?.() || false,
                description: undefined,
              });
            }
          }
        }
      }

      // Intersection 타입 이름 생성
      // named type이 있으면 사용, 없으면 익명 타입(__type)으로 처리
      let intersectionName = '__type';
      for (const t of intersectionTypes) {
        const symbol = t.getSymbol?.();
        if (symbol) {
          const symbolName = symbol.getName();
          // __type이 아닌 실제 이름이면 사용
          if (symbolName && symbolName !== '__type' && !symbolName.startsWith('__')) {
            intersectionName = symbolName;
            break;
          }
        }
      }

      return {
        name: intersectionName,
        kind: 'object',
        properties: mergedProperties,
        raw: typeText,
        isIntersection: true,
      };
    }

    // 함수 타입 (예: () => void, (event: T) => void)
    // 실제로 호출 가능한 타입인지 확인 (call signature가 있어야 함)
    // 중요: typeText.includes('=>')를 사용하면 함수 속성을 가진 객체 타입도 함수로 분류됨
    // 예: Sku & { processProductGrant: (...) => ... } 같은 intersection 타입은 객체이지만 => 포함
    if (typeNode.getCallSignatures && typeNode.getCallSignatures().length > 0) {
      const callSignatures = typeNode.getCallSignatures?.() || [];
      let functionParams: any[] = [];
      let functionReturnType: any = { name: 'void', kind: 'primitive', raw: 'void' };

      if (callSignatures.length > 0) {
        const signature = callSignatures[0];
        // 파라미터 타입 파싱
        const params = signature.getParameters?.() || [];
        functionParams = params.map((param: any) => {
          const paramDecl = param.getValueDeclaration?.();
          const paramType = paramDecl?.getType?.() || param.getDeclaredType?.();
          return paramType ? this.parseType(paramType) : { name: 'any', kind: 'primitive', raw: 'any' };
        });
        // 반환 타입 파싱
        const returnType = signature.getReturnType?.();
        if (returnType) {
          functionReturnType = this.parseType(returnType);
        }
      }

      return {
        name: 'Function',
        kind: 'function',
        functionParams,
        functionReturnType,
        raw: typeText,
      };
    }

    // Object 타입 (인터페이스, 타입 별칭, 익명 객체)
    if (isObject || typeText.startsWith('{')) {
      const properties = typeNode.getProperties?.() || [];

      // 익명 객체의 경우 의미 있는 이름 생성
      let objectName = symbol?.getName() || typeText;
      objectName = cleanTypeName(objectName); // $1, $2 등 접미사 제거
      if (objectName.startsWith('{')) {
        // 익명 객체는 'AnonymousObject'로 명명
        objectName = 'object';
      }

      return {
        name: objectName,
        kind: 'object',
        properties: properties.map((prop: any) => {
          const propName = prop.getName();
          const valueDecl = prop.getValueDeclaration?.();
          const propType = valueDecl ? prop.getTypeAtLocation?.(valueDecl) : prop.getDeclaredType?.();

          return {
            name: propName,
            type: propType ? this.parseType(propType) : { name: 'any', kind: 'primitive', raw: 'any' },
            optional: prop.isOptional?.() || false,
            description: undefined,
          };
        }),
        raw: typeText,
      };
    }

    // Named type (인터페이스/타입 별칭 참조)
    if (symbol) {
      const declarations = symbol.getDeclarations();
      if (declarations && declarations.length > 0) {
        const decl = declarations[0];

        // 인터페이스인 경우
        if (decl.getKind() === SyntaxKind.InterfaceDeclaration) {
          const properties = typeNode.getProperties?.() || [];
          return {
            name: cleanTypeName(symbol.getName()),
            kind: 'object',
            properties: properties.map((prop: any) => {
              const propName = prop.getName();
              const propDecl = prop.getValueDeclaration?.();
              const propType = propDecl?.getType?.() || prop.getDeclaredType?.();

              return {
                name: propName,
                type: propType ? this.parseType(propType) : { name: 'any', kind: 'primitive', raw: 'any' },
                optional: prop.isOptional?.() || false,
                description: undefined,
              };
            }),
            raw: typeText,
          };
        }

        // 타입 별칭인 경우
        if (decl.getKind() === SyntaxKind.TypeAliasDeclaration) {
          // 타입 별칭의 실제 타입을 재귀 파싱
          const aliasedType = typeNode.getAliasedType?.();
          if (aliasedType) {
            const parsed = this.parseType(aliasedType);
            // 이름은 별칭 이름으로 유지
            return {
              ...parsed,
              name: cleanTypeName(symbol.getName()),
            };
          }
        }
      }

      // 선언을 찾았지만 구체적인 타입을 모르는 경우
      // 하지만 properties를 읽을 수 있으면 시도
      const properties = typeNode.getProperties?.() || [];
      return {
        name: cleanTypeName(symbol.getName()),
        kind: 'object', // Named type은 일단 object로 처리
        properties: properties.map((prop: any) => {
          const propName = prop.getName();
          const propDecl = prop.getValueDeclaration?.();
          const propType = propDecl?.getType?.() || prop.getDeclaredType?.();

          return {
            name: propName,
            type: propType ? this.parseType(propType) : { name: 'any', kind: 'primitive', raw: 'any' },
            optional: prop.isOptional?.() || false,
            description: undefined,
          };
        }),
        raw: typeText,
      };
    }

    // 기타 (정말 알 수 없는 타입)
    return {
      name: typeText,
      kind: 'unknown',
      raw: typeText,
    };
  }

  /**
   * camelCase를 PascalCase로 변환
   * 예: appLogin -> AppLogin, startUpdateLocation -> StartUpdateLocation
   */
  private toPascalCase(str: string): string {
    if (!str) return str;
    return str.charAt(0).toUpperCase() + str.slice(1);
  }

  /**
   * JSDoc에서 파라미터 설명 추출
   */
  private extractParamDescriptions(jsDoc: any): Map<string, string> {
    const paramMap = new Map<string, string>();
    if (!jsDoc) return paramMap;

    const tags = jsDoc.getTags();
    for (const tag of tags) {
      if (tag.getTagName() === 'param') {
        const comment = tag.getCommentText();
        const name = tag.getName?.();
        if (name && comment) {
          paramMap.set(name, comment.trim());
        }
      }
    }
    return paramMap;
  }

  /**
   * JSDoc에서 @returns 태그 설명 추출
   */
  private extractReturnsDescription(jsDoc: any): string | undefined {
    if (!jsDoc) return undefined;

    const tags = jsDoc.getTags();
    const returnsTag = tags.find((tag: any) => tag.getTagName() === 'returns');
    if (returnsTag) {
      const comment = returnsTag.getCommentText();
      return comment ? comment.trim() : undefined;
    }
    return undefined;
  }

  /**
   * JSDoc에서 @example 태그들 추출
   */
  private extractExamples(jsDoc: any): string[] {
    if (!jsDoc) return [];

    const examples: string[] = [];
    const tags = jsDoc.getTags();
    for (const tag of tags) {
      if (tag.getTagName() === 'example') {
        const comment = tag.getCommentText();
        if (comment) {
          examples.push(comment.trim());
        }
      }
    }
    return examples;
  }

  /**
   * 파일 경로에서 카테고리 추출
   * .d.ts 파일명을 PascalCase로 변환하여 카테고리로 사용
   * 예: appLogin.d.ts -> AppLogin
   */
  private getCategoryFromPath(filePath: string): string {
    const fileName = path.basename(filePath, '.d.ts');
    return fileName.charAt(0).toUpperCase() + fileName.slice(1);
  }

  /**
   * Permission 지원 여부 확인
   */
  private checkPermissionSupport(func: FunctionDeclaration): boolean {
    const type = func.getReturnType();
    const properties = type.getProperties();

    return properties.some(
      prop => prop.getName() === 'getPermission' || prop.getName() === 'openPermissionDialog'
    );
  }

  /**
   * 모든 타입 정의 파싱 (enum, interface)
   */
  async parseTypeDefinitions(): Promise<ParsedTypeDefinition[]> {
    const typeDefinitions: ParsedTypeDefinition[] = [];
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

      try {
        // 먼저 declare enum 등 파일 내 모든 enum을 찾음 (export 여부 무관)
        const allEnums = sourceFile.getEnums();
        for (const enumDecl of allEnums) {
          const name = enumDecl.getName();
          const members = enumDecl.getMembers();
          const enumValues = members.map(member => {
            const memberName = member.getName();
            const initializer = member.getInitializer();
            // 숫자 초기화 값이 있으면 { name, value } 형태로 반환
            if (initializer) {
              const initText = initializer.getText();
              const numValue = Number(initText);
              if (!isNaN(numValue)) {
                return { name: memberName, value: numValue };
              }
            }
            return memberName;
          });

          // JSDoc에서 description 추출
          const jsDocs = enumDecl.getJsDocs();
          const description = jsDocs.length > 0
            ? jsDocs[0].getDescription().trim()
            : undefined;

          // 중복 체크: 같은 이름의 enum이 이미 있으면 스킵
          if (!typeMap.has(name)) {
            typeMap.set(name, {
              name,
              kind: 'enum',
              file: fileName,
              description,
              enumValues,
            });
          }
        }

        // non-exported type alias도 처리 (PermissionName$1 같은 로컬 타입)
        const allTypeAliases = sourceFile.getTypeAliases();
        for (const typeAlias of allTypeAliases) {
          const rawName = typeAlias.getName();
          // TypeScript 빌드 시 생성되는 $1, $2 등의 접미사 제거
          const name = rawName.replace(/\$\d+$/, '');

          const typeNode = typeAlias.getTypeNode();
          if (typeNode && typeNode.getKind() === SyntaxKind.UnionType) {
            const unionType = typeNode.asKind(SyntaxKind.UnionType);
            if (unionType) {
              const members = unionType.getTypeNodes();
              const allStringLiterals = members.every(
                m => m.getKind() === SyntaxKind.LiteralType
              );

              if (allStringLiterals) {
                const enumValues = members.map(m => {
                  const literalType = m.asKind(SyntaxKind.LiteralType);
                  if (literalType) {
                    const literal = literalType.getLiteral();
                    return literal.getText().replace(/['"]/g, '');
                  }
                  return '';
                }).filter(v => v !== '');

                // JSDoc에서 description 추출
                const jsDocs = typeAlias.getJsDocs();
                const description = jsDocs.length > 0
                  ? jsDocs[0].getDescription().trim()
                  : undefined;

                // 중복 체크: 같은 이름의 enum이 이미 있으면 스킵
                if (!typeMap.has(name) && enumValues.length > 0) {
                  typeMap.set(name, {
                    name,
                    kind: 'enum',
                    file: fileName,
                    description,
                    enumValues,
                  });
                }
              }
            }
          }
        }

        // native-modules의 비-export 인터페이스(InterstitialAd 등)는
        // parseNativeModulesType()으로 별도 처리 (순환 참조 방지)

        // exported 선언들 처리
        const exportedDeclarations = sourceFile.getExportedDeclarations();

        for (const [name, declarations] of exportedDeclarations) {
          for (const declaration of declarations) {
          // Type Alias (export type Foo = ...)
          if (declaration.getKind() === SyntaxKind.TypeAliasDeclaration) {
            const typeAlias = declaration.asKind(SyntaxKind.TypeAliasDeclaration);
            if (typeAlias) {
              const typeNode = typeAlias.getTypeNode();
              if (typeNode && typeNode.getKind() === SyntaxKind.UnionType) {
                // Union 타입인 경우, 모든 멤버가 문자열 리터럴이면 enum으로 변환
                const unionType = typeNode.asKind(SyntaxKind.UnionType);
                if (unionType) {
                  const members = unionType.getTypeNodes();
                  const allStringLiterals = members.every(
                    m => m.getKind() === SyntaxKind.LiteralType
                  );

                  if (allStringLiterals) {
                    const enumValues = members.map(m => {
                      const literalType = m.asKind(SyntaxKind.LiteralType);
                      if (literalType) {
                        const literal = literalType.getLiteral();
                        return literal.getText().replace(/['"]/g, ''); // 따옴표 제거
                      }
                      return '';
                    }).filter(v => v !== '');

                    // JSDoc에서 description 추출
                    const jsDocs = typeAlias.getJsDocs();
                    const description = jsDocs.length > 0
                      ? jsDocs[0].getDescription().trim()
                      : undefined;

                    // 중복 체크: 같은 이름의 enum이 이미 있으면 스킵
                    if (!typeMap.has(name)) {
                      typeMap.set(name, {
                        name,
                        kind: 'enum',
                        file: fileName,
                        description,
                        enumValues,
                      });
                    }
                  } else {
                    // Discriminated Union: 모든 멤버가 객체 리터럴이고 공통 discriminator 필드를 가진 경우
                    // 예: { statusCode: "SUCCESS"; ... } | { statusCode: "PROFILE_NOT_FOUND" }
                    const allTypeLiterals = members.every(
                      m => m.getKind() === SyntaxKind.TypeLiteral
                    );

                    if (allTypeLiterals) {
                      // 각 멤버의 첫 번째 프로퍼티를 추출하여 discriminator 후보로 사용
                      const memberProperties: ParsedProperty[][] = [];
                      let discriminatorField: string | null = null;

                      for (const member of members) {
                        const typeLiteral = member.asKind(SyntaxKind.TypeLiteral);
                        if (typeLiteral) {
                          const props: ParsedProperty[] = [];
                          const memberProps = typeLiteral.getMembers();
                          for (const prop of memberProps) {
                            if (prop.getKind() === SyntaxKind.PropertySignature) {
                              const propSig = prop.asKind(SyntaxKind.PropertySignature);
                              if (propSig) {
                                const propName = propSig.getName();
                                const propType = propSig.getType();
                                const isOptional = propSig.hasQuestionToken();

                                // 첫 번째 프로퍼티가 문자열 리터럴이면 discriminator 후보
                                if (!discriminatorField && propType.isStringLiteral()) {
                                  discriminatorField = propName;
                                }

                                props.push({
                                  name: propName,
                                  type: this.parseType(propType),
                                  optional: isOptional,
                                });
                              }
                            }
                          }
                          memberProperties.push(props);
                        }
                      }

                      // discriminator 필드가 있으면 interface로 처리
                      if (discriminatorField && memberProperties.length > 0) {
                        // 모든 프로퍼티를 합쳐서 하나의 interface로 생성
                        // 공통되지 않은 프로퍼티는 optional로 표시
                        const allPropertyNames = new Set<string>();
                        const propertyOccurrences = new Map<string, number>();

                        for (const props of memberProperties) {
                          for (const prop of props) {
                            allPropertyNames.add(prop.name);
                            propertyOccurrences.set(
                              prop.name,
                              (propertyOccurrences.get(prop.name) || 0) + 1
                            );
                          }
                        }

                        const mergedProperties: ParsedProperty[] = [];
                        for (const propName of allPropertyNames) {
                          // 모든 멤버에 있는 프로퍼티만 필수, 나머지는 optional
                          const isInAll = propertyOccurrences.get(propName) === memberProperties.length;
                          // 첫 번째로 발견된 타입 정보 사용
                          let propType: ParsedType = { name: 'string', kind: 'primitive', raw: 'string' };
                          let propDescription: string | undefined;

                          for (const props of memberProperties) {
                            const found = props.find(p => p.name === propName);
                            if (found) {
                              // discriminator 필드는 string으로 처리 (리터럴 타입이므로)
                              if (propName === discriminatorField) {
                                propType = { name: 'string', kind: 'primitive', raw: 'string' };
                              } else {
                                propType = found.type;
                              }
                              propDescription = found.description;
                              break;
                            }
                          }

                          mergedProperties.push({
                            name: propName,
                            type: propType,
                            optional: !isInAll,
                            description: propDescription,
                          });
                        }

                        // JSDoc에서 description 추출
                        const jsDocs = typeAlias.getJsDocs();
                        const description = jsDocs.length > 0
                          ? jsDocs[0].getDescription().trim()
                          : undefined;

                        if (!typeMap.has(name)) {
                          typeMap.set(name, {
                            name,
                            kind: 'interface',
                            file: fileName,
                            description,
                            properties: mergedProperties,
                          });
                        }
                      }
                    }
                  }
                }
              }
            }
          }

          // Enum Declaration (declare enum Foo { ... } 또는 export enum Foo { ... })
          if (declaration.getKind() === SyntaxKind.EnumDeclaration) {
            const enumDecl = declaration.asKind(SyntaxKind.EnumDeclaration);
            if (enumDecl) {
              const members = enumDecl.getMembers();
              const enumValues = members.map(member => {
                return member.getName();
              });

              // JSDoc에서 description 추출
              const jsDocs = enumDecl.getJsDocs();
              const description = jsDocs.length > 0
                ? jsDocs[0].getDescription().trim()
                : undefined;

              // 중복 체크: 같은 이름의 enum이 이미 있으면 스킵
              if (!typeMap.has(name)) {
                typeMap.set(name, {
                  name,
                  kind: 'enum',
                  file: fileName,
                  description,
                  enumValues,
                });
              }
            }
          }

          // Interface (export interface Foo { ... })
          if (declaration.getKind() === SyntaxKind.InterfaceDeclaration) {
            const interfaceDecl = declaration.asKind(SyntaxKind.InterfaceDeclaration);
            if (interfaceDecl) {
              const members = interfaceDecl.getMembers();
              const properties = members.map(member => {
                if (member.getKind() === SyntaxKind.PropertySignature) {
                  const propSig = member.asKind(SyntaxKind.PropertySignature);
                  if (propSig) {
                    const propName = propSig.getName();
                    const propType = propSig.getType();
                    const isOptional = propSig.hasQuestionToken();

                    // JSDoc에서 description 추출
                    const jsDocs = propSig.getJsDocs();
                    const propDescription = jsDocs.length > 0
                      ? jsDocs[0].getDescription().trim()
                      : undefined;

                    return {
                      name: propName,
                      type: this.parseType(propType),
                      optional: isOptional,
                      description: propDescription,
                    };
                  }
                }
                return null;
              }).filter(p => p !== null) as any[];

              // JSDoc에서 description 추출
              const jsDocs = interfaceDecl.getJsDocs();
              const description = jsDocs.length > 0
                ? jsDocs[0].getDescription().trim()
                : undefined;

              // 중복 체크: 같은 이름의 interface가 이미 있으면 스킵
              if (!typeMap.has(name)) {
                typeMap.set(name, {
                  name,
                  kind: 'interface',
                  file: fileName,
                  description,
                  properties,
                });
              }
            }
          }
        }
      }
      } catch (error) {
        // 파일 파싱 실패 시 스킵하고 계속 진행
        // 생성 단계에서는 경고 없이 조용히 스킵
        continue;
      }
    }

    // Map에서 중복이 제거된 타입 정의들을 반환
    return Array.from(typeMap.values());
  }
}
