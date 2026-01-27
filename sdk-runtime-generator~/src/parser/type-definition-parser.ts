import { SourceFile, SyntaxKind } from 'ts-morph';
import { ParsedTypeDefinition, ParsedProperty, ParsedType } from '../types.js';
import { parseType } from './type-parser.js';
import * as path from 'path';

/**
 * 소스 파일에서 모든 타입 정의 파싱 (enum, interface)
 */
export function parseTypeDefinitionsFromFile(
  sourceFile: SourceFile,
  typeMap: Map<string, ParsedTypeDefinition>
): void {
  const filePath = sourceFile.getFilePath();
  const fileName = path.basename(filePath);

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
                  parseDiscriminatedUnion(typeAlias, members, name, fileName, typeMap);
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
                    type: parseType(propType),
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
  }
}

/**
 * Discriminated Union 파싱
 * 예: { statusCode: "SUCCESS"; ... } | { statusCode: "PROFILE_NOT_FOUND" }
 */
function parseDiscriminatedUnion(
  typeAlias: any,
  members: any[],
  name: string,
  fileName: string,
  typeMap: Map<string, ParsedTypeDefinition>
): void {
  const allTypeLiterals = members.every(
    m => m.getKind() === SyntaxKind.TypeLiteral
  );

  if (!allTypeLiterals) return;

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
              type: parseType(propType),
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
