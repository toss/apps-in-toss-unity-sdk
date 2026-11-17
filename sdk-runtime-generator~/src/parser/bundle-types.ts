import { SourceFile, SyntaxKind } from 'ts-morph';
import * as path from 'path';
import { ParsedProperty, ParsedTypeDefinition } from '../types.js';
import { parseTypeMembers } from './type-parser.js';

/**
 * C#이 합성한 `<Api><Param>` 형태의 공개 별칭 -> 번들에 실제로 선언된 타입 이름
 * (번들은 `AttachOptions`를 `TossAdsAttachOptions`라는 이름으로 내보내지 않는다)
 */
const BUNDLE_TYPE_ALIASES: Record<string, string> = {
  TossAdsAttachOptions: 'AttachOptions',
  TossAdsInitializeOptions: 'InitializeOptions',
  TossAdsBannerSlotCallbacks: 'BannerSlotCallbacks',
  TossAdsAttachBannerOptions: 'AttachBannerOptions',
};

/**
 * 번들에서 이름으로 타입(interface 또는 type alias)을 찾아 정의로 파싱한다 (on-demand).
 * 순환 참조로 인한 스택 오버플로우를 피하려고 필요한 타입만 요청 시점에 파싱한다.
 */
export function parseBundleTypeByName(sourceFile: SourceFile, typeName: string): ParsedTypeDefinition | null {
  return parseTypeFromBundle(sourceFile, BUNDLE_TYPE_ALIASES[typeName] ?? typeName, typeName);
}

/**
 * 특정 파일에서 타입 정의 파싱 (interface 또는 type alias)
 */
export function parseTypeFromBundle(
  sourceFile: SourceFile,
  searchTypeName: string,
  outputTypeName: string
): ParsedTypeDefinition | null {
  const fileName = path.basename(sourceFile.getFilePath());

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
