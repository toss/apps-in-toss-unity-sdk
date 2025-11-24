import { Project, SourceFile, FunctionDeclaration, TypeNode, SyntaxKind } from 'ts-morph';
import { ParsedAPI, ParsedParameter, ParsedType, ParsedTypeDefinition } from './types.js';
import * as path from 'path';

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
    ];

    let tsConfigPath: string | undefined;
    for (const configPath of possibleConfigs) {
      try {
        if (require('fs').existsSync(configPath)) {
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
    });

    // sourceDir의 .d.ts 파일들만 추가
    this.project.addSourceFilesAtPaths(path.join(sourceDir, '**', '*.d.ts'));
  }

  /**
   * 모든 API 파싱
   */
  async parseAPIs(): Promise<ParsedAPI[]> {
    const apis: ParsedAPI[] = [];
    const sourceFiles = this.project.getSourceFiles();

    for (const sourceFile of sourceFiles) {
      // index.d.ts, bridge.d.ts, types.d.ts 파일은 스킵 (전체 re-export 파일)
      const filePath = sourceFile.getFilePath();
      const fileName = path.basename(filePath);
      if (fileName === 'index.d.ts' || fileName === 'index.d.cts' || fileName === 'types.d.ts' || fileName === 'bridge.d.ts') {
        continue;
      }

      const fileAPIs = this.parseSourceFile(sourceFile);
      apis.push(...fileAPIs);
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

        // 변수 선언 (const openCamera = ...)
        if (declaration.getKind() === SyntaxKind.VariableDeclaration) {
          const varDecl = declaration.asKind(SyntaxKind.VariableDeclaration);
          if (varDecl) {
            const initializer = varDecl.getInitializer();
            if (initializer && initializer.getKind() === SyntaxKind.ArrowFunction) {
              const api = this.parseVariableFunction(name, varDecl, sourceFile);
              if (api) {
                apis.push(api);
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
    const category = this.extractCategory(jsDoc?.getTags() || []);

    // JSDoc에서 추가 정보 추출
    const paramDescriptions = this.extractParamDescriptions(jsDoc);
    const returnDescription = this.extractReturnsDescription(jsDoc);
    const examples = this.extractExamples(jsDoc);

    // 파라미터 파싱 (JSDoc 설명 포함)
    const parameters = func.getParameters().map(param => {
      const paramName = param.getName();
      return {
        name: paramName,
        type: this.parseType(param.getType()),
        optional: param.isOptional(),
        description: paramDescriptions.get(paramName),
      };
    });

    const returnType = this.parseType(func.getReturnType());

    const isAsync = returnType.kind === 'promise';
    const hasPermission = this.checkPermissionSupport(func);

    return {
      name,
      pascalName: this.toPascalCase(name),
      originalName: name,
      category: category || this.getCategoryFromPath(sourceFile.getFilePath()),
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
    const jsDoc = varDecl.getJsDocs()[0];
    const description = jsDoc?.getDescription().trim();
    const category = this.extractCategory(jsDoc?.getTags() || []);

    // JSDoc에서 추가 정보 추출
    const paramDescriptions = this.extractParamDescriptions(jsDoc);
    const returnDescription = this.extractReturnsDescription(jsDoc);
    const examples = this.extractExamples(jsDoc);

    // 파라미터 파싱 (JSDoc 설명 포함)
    const parameters = signature.getParameters().map((param: any) => {
      const paramName = param.getName();
      return {
        name: paramName,
        type: this.parseType(param.getValueDeclaration()?.getType()),
        optional: param.isOptional(),
        description: paramDescriptions.get(paramName),
      };
    });

    const returnType = this.parseType(signature.getReturnType());
    const isAsync = returnType.kind === 'promise';
    const hasPermission = false; // TODO: 검증 로직 추가

    return {
      name,
      pascalName: this.toPascalCase(name),
      originalName: name,
      category: category || this.getCategoryFromPath(sourceFile.getFilePath()),
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
   * 타입 파싱 (ts-morph Type 객체 사용)
   */
  private parseType(typeNode: any): ParsedType {
    const typeText = typeNode.getText?.() || typeNode.toString();

    // Primitive 타입 (string literal도 포함)
    if (['string', 'number', 'boolean', 'void', 'any', 'unknown', 'undefined', 'null'].includes(typeText)) {
      return {
        name: typeText,
        kind: 'primitive',
        raw: typeText,
      };
    }

    // String literal 타입 ("foo", 'bar')
    if (typeText.startsWith('"') || typeText.startsWith("'")) {
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

    // Union 타입
    if (isUnion) {
      const unionTypes = typeNode.getUnionTypes?.() || [];
      const parsedUnionTypes = unionTypes.map((t: any) => this.parseType(t));

      // Discriminated Union 감지: 객체 1개 + 문자열 리터럴 N개
      const objectTypes = parsedUnionTypes.filter((t: ParsedType) => t.kind === 'object');
      const stringLiterals = parsedUnionTypes.filter((t: ParsedType) =>
        t.kind === 'primitive' && t.name === 'string' && t.raw.startsWith('"')
      );

      const isDiscriminatedUnion = objectTypes.length === 1 && stringLiterals.length > 0;

      if (isDiscriminatedUnion) {
        return {
          name: typeText,
          kind: 'union',
          unionTypes: parsedUnionTypes,
          raw: typeText,
          isDiscriminatedUnion: true,
          successType: objectTypes[0],
          errorCodes: stringLiterals.map((t: ParsedType) => t.raw.replace(/['"]/g, '')),
        };
      }

      return {
        name: typeText,
        kind: 'union',
        unionTypes: parsedUnionTypes,
        raw: typeText,
      };
    }

    // 함수 타입 (예: () => void)
    if (typeText.includes('=>') || (typeNode.getCallSignatures && typeNode.getCallSignatures().length > 0)) {
      return {
        name: 'Function',
        kind: 'function',
        raw: typeText,
      };
    }

    // Object 타입 (인터페이스, 타입 별칭, 익명 객체)
    if (isObject || typeText.startsWith('{')) {
      const properties = typeNode.getProperties?.() || [];

      // 익명 객체의 경우 의미 있는 이름 생성
      let objectName = symbol?.getName() || typeText;
      if (objectName.startsWith('{')) {
        // 익명 객체는 'AnonymousObject'로 명명
        objectName = 'object';
      }

      return {
        name: objectName,
        kind: 'object',
        properties: properties.map((prop: any) => {
          const propName = prop.getName();
          const propType = prop.getTypeAtLocation?.(prop.getValueDeclaration());

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
            name: symbol.getName(),
            kind: 'object',
            properties: properties.map((prop: any) => {
              const propName = prop.getName();
              const propDecl = prop.getValueDeclaration();
              const propType = propDecl ? propDecl.getType() : undefined;

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
              name: symbol.getName(),
            };
          }
        }
      }

      // 선언을 찾았지만 구체적인 타입을 모르는 경우
      // 하지만 properties를 읽을 수 있으면 시도
      const properties = typeNode.getProperties?.() || [];
      return {
        name: symbol.getName(),
        kind: 'object', // Named type은 일단 object로 처리
        properties: properties.map((prop: any) => {
          const propName = prop.getName();
          const propDecl = prop.getValueDeclaration();
          const propType = propDecl ? propDecl.getType() : undefined;

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
   * JSDoc에서 카테고리 추출
   */
  private extractCategory(tags: any[]): string | undefined {
    const categoryTag = tags.find(tag => tag.getTagName() === 'category');
    return categoryTag?.getCommentText();
  }

  /**
   * 파일 경로에서 카테고리 추출
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

      // index, bridge, types 파일은 스킵
      if (fileName === 'index.d.ts' || fileName === 'index.d.cts' || fileName === 'types.d.ts' || fileName === 'bridge.d.ts') {
        continue;
      }

      try {
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
                  }
                }
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
        console.warn(`Warning: Failed to parse type definitions in ${fileName}: ${error}`);
        continue;
      }
    }

    // Map에서 중복이 제거된 타입 정의들을 반환
    return Array.from(typeMap.values());
  }
}
