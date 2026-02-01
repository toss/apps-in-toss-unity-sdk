import { SyntaxKind } from 'ts-morph';
import { ParsedType, ParsedProperty } from '../types.js';
import { DOM_TYPES } from './constants.js';
import { cleanTypeName } from './utils.js';

/**
 * 타입 파싱 (ts-morph Type 객체 사용)
 */
export function parseType(typeNode: any): ParsedType {
  const typeText = typeNode.getText?.() || typeNode.toString();

  // DOM/브라우저 전용 타입 감지 (순환 참조로 인한 스택 오버플로우 방지)
  // Unity에서는 DOM 타입을 사용할 수 없으므로 'object'로 처리
  if (DOM_TYPES.has(typeText)) {
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
      const baseType = parseType({ getText: () => baseTypeText });
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
        ? parseType(elementType)
        : parseType({ getText: () => typeText.slice(0, -2) }),
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
        promiseType: parseType(typeArgs[0]),
        raw: typeText,
      };
    }
    // Fallback: 문자열 파싱
    const innerType = typeText.slice(8, -1);
    return {
      name: 'Promise',
      kind: 'promise',
      promiseType: parseType({ getText: () => innerType }),
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
        keyType: parseType(typeArgs[0]),
        valueType: parseType(typeArgs[1]),
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
    const parsedUnionTypes = unionTypes.map((t: any) => parseType(t));

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
      const parsed = parseType(intersectType);

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
            const parsedPropType: ParsedType = propType ? parseType(propType) : { name: 'any', kind: 'primitive' as const, raw: 'any' };

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
      const typeSymbol = t.getSymbol?.();
      if (typeSymbol) {
        const symbolName = typeSymbol.getName();
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
        return paramType ? parseType(paramType) : { name: 'any', kind: 'primitive', raw: 'any' };
      });
      // 반환 타입 파싱
      const returnType = signature.getReturnType?.();
      if (returnType) {
        functionReturnType = parseType(returnType);
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
          type: propType ? parseType(propType) : { name: 'any', kind: 'primitive', raw: 'any' },
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
              type: propType ? parseType(propType) : { name: 'any', kind: 'primitive', raw: 'any' },
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
          const parsed = parseType(aliasedType);
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
          type: propType ? parseType(propType) : { name: 'any', kind: 'primitive', raw: 'any' },
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
 * 간단한 타입 파싱 (재귀 없이)
 */
export function parseSimpleType(typeText: string): ParsedType {
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
 * 간단한 함수 타입 파싱 (예: (data: SomeType) => void)
 */
export function parseSimpleFunctionType(typeText: string): ParsedType {
  // 파라미터 추출: (data: Type) => void
  const match = typeText.match(/\(([^)]*)\)\s*=>\s*(\w+)/);
  if (!match) {
    return { name: 'Function', kind: 'function', raw: typeText };
  }

  const paramsText = match[1];
  const functionParams: ParsedType[] = [];

  if (paramsText.trim()) {
    // 파라미터 파싱: data: Type 또는 err: unknown
    const paramMatch = paramsText.match(/(\w+)\s*:\s*(.+)/);
    if (paramMatch) {
      const paramType = paramMatch[2].trim();
      functionParams.push(parseSimpleType(paramType));
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
 * Framework 타입 파싱 (리터럴 타입 처리 포함)
 * 'loaded' -> string, 'userEarnedReward' -> string 등
 */
export function parseFrameworkSimpleType(typeText: string): ParsedType {
  // 문자열 리터럴 타입 ('loaded', 'userEarnedReward' 등)은 string으로 변환
  if (typeText.startsWith("'") || typeText.startsWith('"')) {
    return { name: 'string', kind: 'primitive', raw: typeText };
  }

  // 함수 타입은 무시 (Params의 콜백 등)
  if (typeText.includes('=>')) {
    return { name: 'object', kind: 'primitive', raw: typeText };
  }

  // 일반 타입은 parseSimpleType 사용
  return parseSimpleType(typeText);
}

/**
 * 타입 멤버들을 파싱하여 프로퍼티 배열로 변환
 */
export function parseTypeMembers(members: any[]): any[] {
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
