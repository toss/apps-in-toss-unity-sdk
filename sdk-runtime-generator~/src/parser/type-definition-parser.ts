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
          } else {
            // Non-exported TypeReference union (예: IapProductListItem = A | B | C)
            parseDiscriminatedUnion(typeAlias, members, name, fileName, typeMap, sourceFile);
          }
        }
      } else if (typeNode && typeNode.getKind() === SyntaxKind.IndexedAccessType) {
        // `type PermissionName = PermissionParams["name"]` 처럼 인덱스 접근으로 정의된 문자열 리터럴 union.
        // 해석된 타입이 문자열 리터럴만으로 이뤄졌으면 enum으로 만든다.
        const resolved = typeAlias.getType();
        const resolvedMembers = resolved.isUnion() ? resolved.getUnionTypes() : [resolved];
        if (resolvedMembers.length > 0 && resolvedMembers.every((m: any) => m.isStringLiteral())) {
          const enumValues: string[] = resolvedMembers.map((m: any) => String(m.getLiteralValue()));
          const jsDocs = typeAlias.getJsDocs();
          const description = jsDocs.length > 0 ? jsDocs[0].getDescription().trim() : undefined;
          if (!typeMap.has(name)) {
            typeMap.set(name, { name, kind: 'enum', file: fileName, description, enumValues });
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
                  // 또는 TypeReference union (예: IapProductListItem = A | B | C)
                  parseDiscriminatedUnion(typeAlias, members, name, fileName, typeMap, sourceFile);
                }
              }
            }
          }
        }

        // 객체 리터럴 별칭 (export type Foo = { ... }). interface와 같은 클래스로 만든다.
        // (LoadFullScreenAdEvent = { type: "loaded" }, AdUserEarnedReward = { type, data } 등)
        if (declaration.getKind() === SyntaxKind.TypeAliasDeclaration) {
          const literalAlias = declaration.asKind(SyntaxKind.TypeAliasDeclaration);
          const literalNode = literalAlias?.getTypeNode()?.asKind(SyntaxKind.TypeLiteral);
          if (literalAlias && literalNode && literalAlias.getTypeParameters().length === 0 && !typeMap.has(name)) {
            const properties = literalNode
              .getMembers()
              .map(member => propertyFromMember(member))
              .filter((p): p is ParsedProperty => p !== null);
            if (properties.length > 0) {
              const jsDocs = literalAlias.getJsDocs();
              typeMap.set(name, {
                name,
                kind: 'interface',
                file: fileName,
                description: jsDocs.length > 0 ? jsDocs[0].getDescription().trim() || undefined : undefined,
                properties,
              });
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
          // 타입 매개변수가 있는 제네릭 인터페이스(ApiResponse<T>, AdMobHandlerParams<Options, Event> 등)는
          // 인수가 정해지기 전에는 필드 타입이 확정되지 않아 C# 클래스로 만들 수 없다.
          // 실제로 쓰이는 구체 인스턴스는 API 시그니처 경로가 각자 이름 있는 클래스로 만든다.
          if (interfaceDecl && interfaceDecl.getTypeParameters().length === 0) {
            const members = interfaceDecl.getMembers();
            // extends가 있으면(Omit<..>, 다른 interface 등) 상속된 프로퍼티까지 타입 체커로 모은다
            const inherited = interfaceDecl.getExtends().length > 0
              ? propertiesFromChecker(interfaceDecl)
              : null;
            const properties = inherited ?? members.map(member => {
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
  typeMap: Map<string, ParsedTypeDefinition>,
  sourceFile?: SourceFile
): void {
  const allTypeLiterals = members.every(
    m => m.getKind() === SyntaxKind.TypeLiteral
  );

  if (!allTypeLiterals) {
    // TypeReference 멤버 union 처리 (예: IapProductListItem = A | B | C)
    parseTypeReferenceUnion(typeAlias, members, name, fileName, typeMap, sourceFile);
    return;
  }

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

/**
 * TypeReference 멤버 union 파싱
 * 예: type IapProductListItem = ConsumableProductListItem | NonConsumableProductListItem | SubscriptionProductListItem
 * 각 멤버가 TypeReference인 경우, sourceFile에서 interface/type을 찾아 프로퍼티를 병합
 */
function parseTypeReferenceUnion(
  typeAlias: any,
  members: any[],
  name: string,
  fileName: string,
  typeMap: Map<string, ParsedTypeDefinition>,
  sourceFile?: SourceFile
): void {
  if (!sourceFile) return;

  // 모든 멤버가 TypeReference인지 확인
  const allTypeReferences = members.every(
    m => m.getKind() === SyntaxKind.TypeReference
  );
  if (!allTypeReferences) return;

  // 각 TypeReference에서 이름 추출 후 interface/type 찾기
  const allPropertyNames = new Set<string>();
  const propertyOccurrences = new Map<string, number>();
  const memberProperties: ParsedProperty[][] = [];

  for (const member of members) {
    const typeRef = member.asKind(SyntaxKind.TypeReference);
    if (!typeRef) continue;

    const refName = typeRef.getTypeName().getText();
    const props = resolveInterfaceProperties(refName, sourceFile);
    memberProperties.push(props);

    for (const prop of props) {
      allPropertyNames.add(prop.name);
      propertyOccurrences.set(
        prop.name,
        (propertyOccurrences.get(prop.name) || 0) + 1
      );
    }
  }

  if (memberProperties.length === 0 || allPropertyNames.size === 0) return;

  // 프로퍼티 병합: 모든 멤버에 있으면 필수, 아니면 optional
  const mergedProperties: ParsedProperty[] = [];
  for (const propName of allPropertyNames) {
    const isInAll = propertyOccurrences.get(propName) === memberProperties.length;

    // 첫 번째로 발견된 타입 정보 사용
    let propType: ParsedType = { name: 'string', kind: 'primitive', raw: 'string' };
    let propDescription: string | undefined;
    let isOptional = false;

    for (const props of memberProperties) {
      const found = props.find(p => p.name === propName);
      if (found) {
        // 문자열 리터럴 union 감지: 각 멤버에서 같은 필드가 다른 리터럴 값이면 string으로 처리
        const allLiteralValues: string[] = [];
        for (const mp of memberProperties) {
          const f = mp.find(p => p.name === propName);
          if (f && f.type.kind === 'primitive' && f.type.name === 'string' && f.type.raw.startsWith('"') && !f.type.raw.includes('|')) {
            allLiteralValues.push(f.type.raw);
          }
        }
        if (allLiteralValues.length > 0) {
          propType = { name: 'string', kind: 'primitive', raw: 'string' };
        } else {
          propType = found.type;
          // 멤버마다 같은 필드가 서로 다른 이름 있는 객체 타입이면 (예: ContactsViralEvent의 data)
          // 첫 멤버 타입으로 뭉개지 말고 필드를 합친 클래스(`<Union><Field>`)로 만든다
          const mergedRef = mergeDivergentObjectProperty(name, propName, memberProperties, fileName, typeMap, sourceFile);
          if (mergedRef) propType = mergedRef;
        }
        propDescription = found.description;
        isOptional = found.optional;
        break;
      }
    }

    mergedProperties.push({
      name: propName,
      type: propType,
      optional: !isInAll || isOptional,
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

/**
 * interface 이름에서 프로퍼티를 재귀적으로 resolve (extends 체인 포함)
 */
function resolveInterfaceProperties(
  name: string,
  sourceFile: SourceFile
): ParsedProperty[] {
  const properties: ParsedProperty[] = [];
  const seenProps = new Set<string>();

  // sourceFile에서 interface 찾기
  const interfaceDecl = sourceFile.getInterface(name);
  if (interfaceDecl) {
    // extends 체인의 base interface 프로퍼티 먼저 수집
    const baseTypes = interfaceDecl.getExtends();
    for (const baseType of baseTypes) {
      const baseName = baseType.getExpression().getText();
      const baseProps = resolveInterfaceProperties(baseName, sourceFile);
      for (const prop of baseProps) {
        if (!seenProps.has(prop.name)) {
          seenProps.add(prop.name);
          properties.push(prop);
        }
      }
    }

    // 이 interface의 프로퍼티 수집
    const members = interfaceDecl.getMembers();
    for (const member of members) {
      if (member.getKind() === SyntaxKind.PropertySignature) {
        const propSig = member.asKind(SyntaxKind.PropertySignature);
        if (propSig) {
          const propName = propSig.getName();
          if (!seenProps.has(propName)) {
            seenProps.add(propName);
            const propType = propSig.getType();
            const isOptional = propSig.hasQuestionToken();

            // JSDoc에서 description 추출
            const jsDocs = propSig.getJsDocs();
            const propDescription = jsDocs.length > 0
              ? jsDocs[0].getDescription().trim()
              : undefined;

            properties.push({
              name: propName,
              type: parseType(propType),
              optional: isOptional,
              description: propDescription,
            });
          }
        }
      }
    }
    return properties;
  }

  // type alias 찾기 (interface가 아닌 경우)
  const typeAliasDecl = sourceFile.getTypeAlias(name);
  if (typeAliasDecl) {
    const typeNode = typeAliasDecl.getTypeNode();
    if (typeNode && typeNode.getKind() === SyntaxKind.TypeLiteral) {
      const typeLiteral = typeNode.asKind(SyntaxKind.TypeLiteral);
      if (typeLiteral) {
        const members = typeLiteral.getMembers();
        for (const member of members) {
          if (member.getKind() === SyntaxKind.PropertySignature) {
            const propSig = member.asKind(SyntaxKind.PropertySignature);
            if (propSig) {
              const propName = propSig.getName();
              if (!seenProps.has(propName)) {
                seenProps.add(propName);
                const propType = propSig.getType();
                const isOptional = propSig.hasQuestionToken();
                properties.push({
                  name: propName,
                  type: parseType(propType),
                  optional: isOptional,
                });
              }
            }
          }
        }
      }
    }
  }

  return properties;
}

/** 인터페이스/타입 리터럴 멤버 하나를 프로퍼티로 옮긴다 (프로퍼티 시그니처만; 메서드 시그니처는 건너뛴다) */
function propertyFromMember(member: any): ParsedProperty | null {
  if (member.getKind() !== SyntaxKind.PropertySignature) return null;
  const propSig = member.asKind(SyntaxKind.PropertySignature);
  if (!propSig) return null;
  const jsDocs = propSig.getJsDocs();
  return {
    name: propSig.getName(),
    type: parseType(propSig.getType()),
    optional: propSig.hasQuestionToken(),
    description: jsDocs.length > 0 ? jsDocs[0].getDescription().trim() || undefined : undefined,
  };
}

/**
 * 타입 체커로 interface의 전체 프로퍼티(상속·Omit 결과 포함)를 모은다.
 * 선언 위치의 JSDoc/optional을 쓰고, 타입은 이 interface 기준으로 해석한 값을 쓴다.
 */
function propertiesFromChecker(interfaceDecl: any): ParsedProperty[] {
  const props: ParsedProperty[] = [];
  for (const symbol of interfaceDecl.getType().getProperties()) {
    const decl = symbol.getDeclarations()[0];
    const propSig = decl?.asKind?.(SyntaxKind.PropertySignature);
    if (!propSig) continue;
    const jsDocs = propSig.getJsDocs();
    props.push({
      name: symbol.getName(),
      type: parseType(symbol.getTypeAtLocation(interfaceDecl)),
      optional: propSig.hasQuestionToken(),
      description: jsDocs.length > 0 ? jsDocs[0].getDescription().trim() || undefined : undefined,
    });
  }
  return props;
}

/**
 * union 멤버들의 같은 필드가 서로 다른 이름 있는 객체 타입을 가리킬 때, 그 필드들을 합친 클래스를 만든다.
 * 공통이 아닌 필드는 optional이다. 합칠 대상이 아니면 undefined.
 */
function mergeDivergentObjectProperty(
  unionName: string,
  propName: string,
  memberProperties: ParsedProperty[][],
  fileName: string,
  typeMap: Map<string, ParsedTypeDefinition>,
  sourceFile?: SourceFile
): ParsedType | undefined {
  if (!sourceFile) return undefined;
  const typeNames: string[] = [];
  for (const props of memberProperties) {
    const found = props.find(p => p.name === propName);
    if (!found) continue;
    if (found.type.kind !== 'object' || !/^[A-Za-z_][A-Za-z0-9_$]*$/.test(found.type.name)) return undefined;
    if (!typeNames.includes(found.type.name)) typeNames.push(found.type.name);
  }
  if (typeNames.length < 2) return undefined;

  const perType = typeNames.map(n => resolveInterfaceProperties(n, sourceFile));
  if (perType.some(props => props.length === 0)) return undefined;

  const occurrences = new Map<string, number>();
  const merged: ParsedProperty[] = [];
  for (const props of perType) {
    for (const prop of props) {
      occurrences.set(prop.name, (occurrences.get(prop.name) || 0) + 1);
      const existing = merged.find(m => m.name === prop.name);
      if (!existing) merged.push({ ...prop });
      else if (prop.optional) existing.optional = true;
    }
  }
  for (const prop of merged) {
    if (occurrences.get(prop.name) !== perType.length) prop.optional = true;
    // 값 타입(double, bool)은 optional이면 nullable로 둬서 "없음"과 0/false를 구분한다
    if (prop.optional && prop.type.kind === 'primitive' && ['number', 'boolean'].includes(prop.type.name)) {
      prop.type = { ...prop.type, isNullable: true };
    }
  }

  const mergedName = `${unionName}${propName.charAt(0).toUpperCase()}${propName.slice(1)}`;
  if (!typeMap.has(mergedName)) {
    typeMap.set(mergedName, {
      name: mergedName,
      kind: 'interface',
      file: fileName,
      description: `${typeNames.join(' | ')}의 필드를 합친 값. 이벤트 종류에 따라 일부 필드만 채워진다.`,
      properties: merged,
    });
  }
  return { name: mergedName, kind: 'object', raw: mergedName };
}
