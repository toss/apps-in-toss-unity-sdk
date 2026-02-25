import { Project, SourceFile, SyntaxKind } from 'ts-morph';
import * as path from 'path';
import * as fs from 'fs';
import { ParsedAPI, ParsedParameter, ParsedProperty, ParsedType, ParsedTypeDefinition } from '../types.js';
import { getCategory } from '../categories.js';
import { toPascalCase } from './utils.js';
import { parseType, parseSimpleType, parseSimpleFunctionType, parseFrameworkSimpleType, parseTypeMembers } from './type-parser.js';

/**
 * pnpm virtual store에서 framework 패키지 경로 찾기
 *
 * @param webFrameworkPath web-framework 패키지의 실제 경로 (sibling 기반 해결에 사용)
 */
export function findFrameworkPath(webFrameworkPath?: string): string | null {
  // 전략 1: webFrameworkPath가 주어지면 sibling에서 찾기 (가장 정확)
  if (webFrameworkPath) {
    try {
      const realPath = fs.realpathSync(webFrameworkPath);
      const siblingFramework = path.join(path.dirname(realPath), 'framework', 'dist', 'index.d.cts');
      if (fs.existsSync(siblingFramework)) {
        return siblingFramework;
      }
    } catch (e) {
      console.debug(`[findFrameworkPath] 전략 1 실패 (sibling 해결): ${e}`);
    }
  }

  // 전략 2: package.json 버전으로 정확 매칭
  const pkgJsonPath = path.join(process.cwd(), 'package.json');
  if (fs.existsSync(pkgJsonPath)) {
    try {
      const pkgJson = JSON.parse(fs.readFileSync(pkgJsonPath, 'utf-8'));
      const frameworkVersion = pkgJson.dependencies?.['@apps-in-toss/framework'];
      if (frameworkVersion) {
        const pnpmPath = path.join(process.cwd(), 'node_modules', '.pnpm');
        if (fs.existsSync(pnpmPath)) {
          const prefix = `@apps-in-toss+framework@${frameworkVersion}`;
          const dirs = fs.readdirSync(pnpmPath).filter(d => d.startsWith(prefix));
          for (const dir of dirs) {
            const indexPath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'framework', 'dist', 'index.d.cts');
            if (fs.existsSync(indexPath)) {
              return indexPath;
            }
          }
        }
      }
    } catch (e) {
      console.debug(`[findFrameworkPath] 전략 2 실패 (package.json 매칭): ${e}`);
    }
  }

  // 전략 1, 2 모두 실패 시 null 반환 (잘못된 버전 선택 방지)
  // 기존에는 pnpm store에서 첫 번째 매치를 반환했으나, 파일시스템 정렬 순서에
  // 의존하여 잘못된 버전을 선택하는 버그가 있었음
  return null;
}

/**
 * framework 패키지의 @apps-in-toss/types 의존성 .d.ts 경로 찾기
 * v1.10.0+에서 LoadFullScreenAdParams 등이 @apps-in-toss/types로 이동됨
 */
function findFrameworkTypesPath(frameworkDtsPath: string): string | null {
  try {
    // framework index.d.cts의 실제 경로에서 sibling types 패키지 찾기
    const realFrameworkDir = path.dirname(fs.realpathSync(frameworkDtsPath));
    // realFrameworkDir = .../framework/dist → 상위 = .../framework → 상위 = .../@apps-in-toss
    const appsInTossDir = path.dirname(path.dirname(realFrameworkDir));
    const typesBase = path.join(appsInTossDir, 'types', 'dist');

    for (const file of ['index.d.cts', 'index.d.ts']) {
      const candidate = path.join(typesBase, file);
      if (fs.existsSync(candidate)) {
        return candidate;
      }
    }
  } catch (e) {
    console.debug(`[findFrameworkTypesPath] 실패: ${e}`);
  }

  return null;
}

/**
 * framework 파싱용 ts-morph 프로젝트 생성
 * @apps-in-toss/types 의존성도 자동으로 추가
 */
function createFrameworkProject(frameworkPath: string): { project: Project; sourceFile: SourceFile } {
  const tempProject = new Project({
    skipAddingFilesFromTsConfig: true,
    compilerOptions: {
      strictNullChecks: true,
    },
  });

  // @apps-in-toss/types .d.ts도 추가 (v1.10.0+에서 필요)
  const typesPath = findFrameworkTypesPath(frameworkPath);
  if (typesPath) {
    tempProject.addSourceFileAtPath(typesPath);
  }

  const sourceFile = tempProject.addSourceFileAtPath(frameworkPath);
  return { project: tempProject, sourceFile };
}

// 동일 frameworkPath에 대한 중복 Project 생성 방지 캐시
let _cachedFrameworkProject: { path: string; project: Project; sourceFile: SourceFile } | null = null;

function getFrameworkProject(frameworkPath: string): { project: Project; sourceFile: SourceFile } {
  if (_cachedFrameworkProject && _cachedFrameworkProject.path === frameworkPath) {
    return { project: _cachedFrameworkProject.project, sourceFile: _cachedFrameworkProject.sourceFile };
  }
  const result = createFrameworkProject(frameworkPath);
  _cachedFrameworkProject = { path: frameworkPath, ...result };
  return result;
}

/**
 * @apps-in-toss/framework에서 특정 API 파싱 (loadFullScreenAd, showFullScreenAd 등)
 * 이 API들은 web-framework에서 re-export되지 않으므로 직접 파싱
 *
 * @param apiNames 파싱할 API 이름 목록
 * @param frameworkDtsPath framework index.d.cts 경로 (지정 시 findFrameworkPath 호출 생략)
 * @param webFrameworkPath web-framework 경로 (findFrameworkPath의 sibling 해결에 사용)
 */
export function parseFrameworkAPIs(apiNames: string[], frameworkDtsPath?: string, webFrameworkPath?: string): ParsedAPI[] {
  const frameworkPath = frameworkDtsPath ?? findFrameworkPath(webFrameworkPath);

  if (!frameworkPath) {
    return [];
  }

  const { project: tempProject, sourceFile } = getFrameworkProject(frameworkPath);

  // 모든 소스 파일에서 API 파싱 (import된 타입도 해결 가능)
  const allSourceFiles = tempProject.getSourceFiles();
  const apis: ParsedAPI[] = [];

  for (const apiName of apiNames) {
    const api = parseFrameworkAPI(sourceFile, apiName, allSourceFiles);
    if (api) {
      apis.push(api);
    }
  }

  return apis;
}

/**
 * @apps-in-toss/framework에서 특정 API 관련 타입 정의 파싱
 * (LoadFullScreenAdEvent, ShowFullScreenAdEvent, Options 등)
 *
 * @param apiNames 파싱할 API 이름 목록
 * @param frameworkDtsPath framework index.d.cts 경로 (지정 시 findFrameworkPath 호출 생략)
 * @param webFrameworkPath web-framework 경로 (findFrameworkPath의 sibling 해결에 사용)
 */
export function parseFrameworkTypeDefinitions(apiNames: string[], frameworkDtsPath?: string, webFrameworkPath?: string): ParsedTypeDefinition[] {
  const frameworkPath = frameworkDtsPath ?? findFrameworkPath(webFrameworkPath);

  if (!frameworkPath) {
    return [];
  }

  const { project: tempProject, sourceFile } = getFrameworkProject(frameworkPath);
  const allSourceFiles = tempProject.getSourceFiles();
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
      // 모든 소스 파일에서 검색 (v1.10.0+: @apps-in-toss/types에 정의된 경우)
      let typeDef: ParsedTypeDefinition | null = null;
      for (const sf of allSourceFiles) {
        typeDef = parseFrameworkInterface(sf, typeName);
        if (typeDef) break;
      }
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
      let showEventTypeDef: ParsedTypeDefinition | null = null;
      for (const sf of allSourceFiles) {
        showEventTypeDef = parseFrameworkUnionEventType(sf, 'ShowFullScreenAdEvent');
        if (showEventTypeDef) break;
      }
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
function parseFrameworkInterface(sourceFile: SourceFile, typeName: string): ParsedTypeDefinition | null {
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
          type: parseFrameworkSimpleType(propTypeText),
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
              type: parseFrameworkSimpleType(propTypeText),
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
 * ShowFullScreenAdEvent Union 타입 파싱
 * type ShowFullScreenAdEvent = AdMobFullScreenEvent | AdUserEarnedReward | { type: 'requested' }
 */
function parseFrameworkUnionEventType(sourceFile: SourceFile, typeName: string): ParsedTypeDefinition | null {
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
 * 여러 소스 파일에서 인터페이스를 검색
 * (v1.10.0+에서 @apps-in-toss/types로 분리된 타입 해결용)
 */
function findInterfaceAcrossFiles(files: SourceFile[], name: string) {
  for (const file of files) {
    const iface = file.getInterface(name);
    if (iface) return iface;
  }
  return undefined;
}

/**
 * 개별 framework API 파싱
 * @param allSourceFiles 프로젝트 내 모든 소스 파일 (import된 타입 해결용)
 */
function parseFrameworkAPI(sourceFile: SourceFile, apiName: string, allSourceFiles?: SourceFile[]): ParsedAPI | null {
  const searchFiles = allSourceFiles ?? [sourceFile];

  // 함수 또는 변수 선언 찾기
  const funcDecl = sourceFile.getFunction(apiName);
  const varDecl = sourceFile.getVariableDeclaration(apiName);

  if (!funcDecl && !varDecl) {
    return null;
  }

  // 파라미터 타입 인터페이스 이름 추론 (예: loadFullScreenAd -> LoadFullScreenAdParams)
  const paramsTypeName = apiName.charAt(0).toUpperCase() + apiName.slice(1) + 'Params';
  const paramsInterface = findInterfaceAcrossFiles(searchFiles, paramsTypeName);

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
        const parsedType = parseSimpleFunctionType(propTypeText);
        parameters.push({
          name: propName,
          type: parsedType,
          optional: isOptional,
          description: propName === 'onEvent' ? '이벤트 콜백' : '에러 콜백',
        });
      } else if (propName === 'options') {
        // options 객체 내부 프로퍼티를 풀어서 파라미터로 추가
        const optionsTypeName = propTypeText;
        const optionsInterface = findInterfaceAcrossFiles(searchFiles, optionsTypeName);

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
                type: parseSimpleType(optPropTypeText),
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
  const pascalName = toPascalCase(apiName);

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
 * native-modules에서 특정 타입 정의 파싱 (on-demand)
 * 순환 참조로 인한 스택 오버플로우를 방지하기 위해 별도 메서드로 분리
 */
export function parseNativeModulesType(typeName: string): ParsedTypeDefinition | null {
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
    const result = parseTypeFromFile(nativeModulesPath, actualTypeName, typeName);
    if (result) return result;
  }

  // 2. web-bridge에서도 찾기 (TossAds 관련 타입 등)
  // v1.8.0+: dist/, v1.5.0~v1.7.x: built/
  let webBridgePath: string | null = null;
  for (const dir of pnpmDirs) {
    if (dir.startsWith('@apps-in-toss+web-bridge')) {
      const basePath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'web-bridge');
      // dist 우선, built 폴백
      for (const subdir of ['dist', 'built']) {
        const indexPath = path.join(basePath, subdir, 'index.d.ts');
        if (fs.existsSync(indexPath)) {
          webBridgePath = indexPath;
          break;
        }
      }
      if (webBridgePath) break;
    }
  }

  if (webBridgePath) {
    const result = parseTypeFromFile(webBridgePath, actualTypeName, typeName);
    if (result) return result;
  }

  // 3. @apps-in-toss/types에서도 찾기 (SDK 하위호환성: web-framework에서 제거된 타입)
  let typesPath: string | null = null;
  for (const dir of pnpmDirs) {
    if (dir.startsWith('@apps-in-toss+types')) {
      const basePath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'types', 'dist');
      const indexPath = path.join(basePath, 'index.d.ts');
      if (fs.existsSync(indexPath)) {
        typesPath = indexPath;
        break;
      }
    }
  }

  if (typesPath) {
    const result = parseTypeFromFile(typesPath, actualTypeName, typeName);
    if (result) return result;
  }

  return null;
}

/**
 * 특정 파일에서 타입 정의 파싱 (interface 또는 type alias)
 */
function parseTypeFromFile(
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
    const properties = parseTypeMembers(members);

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

    // 2a. TypeLiteral: type Foo = { ... }
    if (typeNode && typeNode.getKind() === SyntaxKind.TypeLiteral) {
      const typeLiteral = typeNode.asKind(SyntaxKind.TypeLiteral);
      if (typeLiteral) {
        const members = typeLiteral.getMembers();
        const properties = parseTypeMembers(members);

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

    // 2b. UnionType: type Foo = A | B | C (TypeReference union → merge properties)
    if (typeNode && typeNode.getKind() === SyntaxKind.UnionType) {
      const unionType = typeNode.asKind(SyntaxKind.UnionType);
      if (unionType) {
        const memberNodes = unionType.getTypeNodes();
        const allTypeRefs = memberNodes.every(m => m.getKind() === SyntaxKind.TypeReference);

        if (allTypeRefs && memberNodes.length > 0) {
          // 각 TypeReference의 interface/type을 찾아 프로퍼티를 merge
          const mergedProperties = new Map<string, ParsedProperty>();

          for (const memberNode of memberNodes) {
            const typeRef = memberNode.asKind(SyntaxKind.TypeReference);
            if (!typeRef) continue;

            const refName = typeRef.getTypeName().getText();
            // 같은 소스 파일에서 해당 interface 찾기
            const refInterface = interfaces.find(i => i.getName() === refName);
            if (refInterface) {
              const props = parseTypeMembers(refInterface.getMembers());
              for (const prop of props) {
                if (!mergedProperties.has(prop.name)) {
                  mergedProperties.set(prop.name, prop);
                } else {
                  // 이미 있으면 optional로 마킹 (다른 variant에만 있을 수 있으므로)
                  const existing = mergedProperties.get(prop.name)!;
                  if (existing.type !== prop.type) {
                    existing.optional = true;
                  }
                }
              }
              continue;
            }

            // interface 못 찾으면 type alias에서 찾기 (extends 포함)
            const refTypeAlias = typeAliases.find(t => t.getName() === refName);
            if (refTypeAlias) {
              const refTypeNode = refTypeAlias.getTypeNode();
              if (refTypeNode && refTypeNode.getKind() === SyntaxKind.TypeLiteral) {
                const lit = refTypeNode.asKind(SyntaxKind.TypeLiteral);
                if (lit) {
                  const props = parseTypeMembers(lit.getMembers());
                  for (const prop of props) {
                    if (!mergedProperties.has(prop.name)) {
                      mergedProperties.set(prop.name, prop);
                    }
                  }
                }
              }
            }
          }

          // 멤버 interface의 extends도 탐색 (BasicProductListItem 등)
          for (const memberNode of memberNodes) {
            const typeRef = memberNode.asKind(SyntaxKind.TypeReference);
            if (!typeRef) continue;

            const refName = typeRef.getTypeName().getText();
            const refInterface = interfaces.find(i => i.getName() === refName);
            if (refInterface) {
              // extends 처리
              for (const ext of refInterface.getExtends()) {
                const baseName = ext.getExpression().getText();
                const baseInterface = interfaces.find(i => i.getName() === baseName);
                if (baseInterface) {
                  const baseProps = parseTypeMembers(baseInterface.getMembers());
                  for (const prop of baseProps) {
                    if (!mergedProperties.has(prop.name)) {
                      mergedProperties.set(prop.name, prop);
                    }
                  }
                }
              }
            }
          }

          const properties = Array.from(mergedProperties.values());

          if (properties.length === 0) {
            return null;
          }

          // union의 각 variant에만 있는 프로퍼티는 optional로 마킹
          for (const prop of properties) {
            let presentCount = 0;
            for (const memberNode of memberNodes) {
              const typeRef = memberNode.asKind(SyntaxKind.TypeReference);
              if (!typeRef) continue;
              const refName = typeRef.getTypeName().getText();
              const refInterface = interfaces.find(i => i.getName() === refName);
              if (refInterface) {
                const allMembers = [
                  ...refInterface.getMembers(),
                  ...refInterface.getExtends().flatMap(ext => {
                    const baseName = ext.getExpression().getText();
                    const baseIf = interfaces.find(i => i.getName() === baseName);
                    return baseIf ? baseIf.getMembers() : [];
                  }),
                ];
                if (allMembers.some(m => {
                  const propSig = m.asKind(SyntaxKind.PropertySignature);
                  return propSig && propSig.getName() === prop.name;
                })) {
                  presentCount++;
                }
              }
            }
            if (presentCount < memberNodes.length) {
              prop.optional = true;
            }
          }

          return {
            name: outputTypeName,
            kind: 'interface',
            file: fileName,
            description: undefined,
            properties,
          };
        }
      }
    }
  }

  return null;
}
