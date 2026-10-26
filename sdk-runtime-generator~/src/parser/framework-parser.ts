import { Project, SourceFile, SyntaxKind } from 'ts-morph';
import * as path from 'path';
import * as fs from 'fs';
import { ParsedAPI, ParsedParameter, ParsedProperty, ParsedType, ParsedTypeDefinition } from '../types.js';
import { getCategory } from '../categories.js';
import { toPascalCase } from './utils.js';
import { parseType, parseSimpleType, parseSimpleFunctionType, parseFrameworkSimpleType, parseTypeMembers } from './type-parser.js';

/**
 * web-framework 번들 dist/index.d.ts 경로 찾기
 *
 * loadFullScreenAd/showFullScreenAd 등 FRAMEWORK_APIS는 web-framework 3.x가 자체
 * dist/index.d.ts에 직접 선언한다 (별도 @apps-in-toss/framework 패키지 불필요).
 *
 * @param webFrameworkPath web-framework 패키지의 실제 경로
 */
export function findFrameworkPath(webFrameworkPath?: string): string | null {
  if (!webFrameworkPath) return null;
  try {
    const bundlePath = path.join(fs.realpathSync(webFrameworkPath), 'dist', 'index.d.ts');
    return fs.existsSync(bundlePath) ? bundlePath : null;
  } catch (e) {
    console.debug(`[findFrameworkPath] 실패: ${e}`);
    return null;
  }
}

/**
 * FRAMEWORK_APIS 파싱용 ts-morph 프로젝트 생성
 */
function createFrameworkProject(frameworkPath: string): { project: Project; sourceFile: SourceFile } {
  const tempProject = new Project({
    skipAddingFilesFromTsConfig: true,
    compilerOptions: {
      strictNullChecks: true,
    },
  });

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
 * web-framework 번들에서 특정 API 파싱 (loadFullScreenAd, showFullScreenAd 등)
 * 이 API들은 web-bridge 파일별 .d.ts에 없고 번들 index.d.ts에만 선언되므로 직접 파싱
 *
 * @param apiNames 파싱할 API 이름 목록
 * @param frameworkDtsPath web-framework 번들 index.d.ts 경로 (지정 시 findFrameworkPath 호출 생략)
 * @param webFrameworkPath web-framework 패키지 경로 (findFrameworkPath에 사용)
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
 * web-framework 번들에서 특정 API 관련 타입 정의 파싱
 * (LoadFullScreenAdEvent, ShowFullScreenAdEvent, Options 등)
 *
 * @param apiNames 파싱할 API 이름 목록
 * @param frameworkDtsPath web-framework 번들 index.d.ts 경로 (지정 시 findFrameworkPath 호출 생략)
 * @param webFrameworkPath web-framework 패키지 경로 (findFrameworkPath에 사용)
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
 * web-framework 번들에서 인터페이스 타입 정의 파싱
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
 * 개별 FRAMEWORK_APIS 파싱
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

  // 설명은 비운다: 번들 JSDoc의 `isSupported()` 백틱 줄이 C# XML 주석에 그대로 들어가
  // 생성 코드가 바뀐다 (이전 framework 1.6.0 패키지에는 JSDoc이 없었음).
  const description: string | undefined = undefined;

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
    // web-framework에서 직접 export됨 (AppsInToss 네임스페이스 없음)
    isTopLevelExport: true,
  };
}

/**
 * web-framework 번들에서 특정 타입 정의 파싱 (on-demand)
 * 순환 참조로 인한 스택 오버플로우를 방지하기 위해 별도 메서드로 분리
 */
export function parseNativeModulesType(typeName: string, webFrameworkPath?: string): ParsedTypeDefinition | null {
  // TossAdsAttachOptions -> AttachOptions 변환 (export alias 처리)
  const typeNameMappings: Record<string, string> = {
    'TossAdsAttachOptions': 'AttachOptions',
    'TossAdsInitializeOptions': 'InitializeOptions',
    'TossAdsBannerSlotCallbacks': 'BannerSlotCallbacks',
    'TossAdsAttachBannerOptions': 'AttachBannerOptions',
  };
  const actualTypeName = typeNameMappings[typeName] || typeName;

  // web-framework 번들 index.d.ts에서 찾기 (3.x는 모든 공개 타입을 자체 선언)
  const bundlePath = findFrameworkPath(webFrameworkPath);
  if (!bundlePath) {
    return null;
  }
  return parseTypeFromFile(bundlePath, actualTypeName, typeName);
}

/**
 * 특정 파일에서 타입 정의 파싱 (interface 또는 type alias)
 */
function parseTypeFromFile(
  filePath: string,
  searchTypeName: string,
  outputTypeName: string
): ParsedTypeDefinition | null {
  // 별도 프로젝트로 파싱 (메인 프로젝트 오염 방지, 동일 파일은 캐시 재사용)
  const { sourceFile } = getFrameworkProject(filePath);
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
        // TypeReference 또는 객체 리터럴 variant만 병합 대상 (예: Offer = { type: 'A'; ... } | { type: 'B'; ... })
        const allTypeRefs = memberNodes.every(
          m => m.getKind() === SyntaxKind.TypeReference || m.getKind() === SyntaxKind.TypeLiteral,
        );

        if (allTypeRefs && memberNodes.length > 0) {
          // 각 TypeReference의 interface/type을 찾아 프로퍼티를 merge
          const mergedProperties = new Map<string, ParsedProperty>();

          for (const memberNode of memberNodes) {
            const literalVariant = memberNode.asKind(SyntaxKind.TypeLiteral);
            if (literalVariant) {
              for (const prop of parseTypeMembers(literalVariant.getMembers())) {
                if (!mergedProperties.has(prop.name)) {
                  mergedProperties.set(prop.name, prop);
                }
              }
              continue;
            }

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
                  if (existing.type.name !== prop.type.name) {
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
              const literalVariant = memberNode.asKind(SyntaxKind.TypeLiteral);
              if (literalVariant) {
                if (literalVariant.getMembers().some(m => m.asKind(SyntaxKind.PropertySignature)?.getName() === prop.name)) {
                  presentCount++;
                }
                continue;
              }
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
