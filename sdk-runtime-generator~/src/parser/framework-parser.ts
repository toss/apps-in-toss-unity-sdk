import { Project, SourceFile, SyntaxKind } from 'ts-morph';
import * as path from 'path';
import * as fs from 'fs';
import { ParsedAPI, ParsedParameter, ParsedProperty, ParsedType, ParsedTypeDefinition } from '../types.js';
import { getCategory } from '../categories.js';
import { toPascalCase } from './utils.js';
import { parseType, parseSimpleType, parseSimpleFunctionType, parseFrameworkSimpleType, parseTypeMembers } from './type-parser.js';

/**
 * pnpm virtual store에서 framework 패키지 경로 찾기
 */
export function findFrameworkPath(): string | null {
  const nmPath = path.join(process.cwd(), 'node_modules');
  const pnpmPath = path.join(nmPath, '.pnpm');

  if (!fs.existsSync(pnpmPath)) {
    return null;
  }

  const pnpmDirs = fs.readdirSync(pnpmPath);

  for (const dir of pnpmDirs) {
    if (dir.startsWith('@apps-in-toss+framework@')) {
      const indexPath = path.join(pnpmPath, dir, 'node_modules', '@apps-in-toss', 'framework', 'dist', 'index.d.cts');
      if (fs.existsSync(indexPath)) {
        return indexPath;
      }
    }
  }

  return null;
}

/**
 * @apps-in-toss/framework에서 특정 API 파싱 (loadFullScreenAd, showFullScreenAd 등)
 * 이 API들은 web-framework에서 re-export되지 않으므로 직접 파싱
 */
export function parseFrameworkAPIs(apiNames: string[]): ParsedAPI[] {
  const frameworkPath = findFrameworkPath();

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
    const api = parseFrameworkAPI(sourceFile, apiName);
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
export function parseFrameworkTypeDefinitions(apiNames: string[]): ParsedTypeDefinition[] {
  const frameworkPath = findFrameworkPath();

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
      const typeDef = parseFrameworkInterface(sourceFile, typeName);
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
      const showEventTypeDef = parseFrameworkUnionEventType(sourceFile, 'ShowFullScreenAdEvent');
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
 * 개별 framework API 파싱
 */
function parseFrameworkAPI(sourceFile: SourceFile, apiName: string): ParsedAPI | null {
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
  }

  return null;
}
