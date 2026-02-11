/**
 * Firebase 타입 정의 생성기
 *
 * Firebase API에서 사용하는 C# 타입 정의를 생성합니다.
 * (FirebaseUser 등)
 */

import type { FirebaseTypeDefinition } from '../../parser/firebase-parser.js';

/**
 * Firebase 타입 정의 C# 파일 생성
 */
export function generateFirebaseTypes(types: FirebaseTypeDefinition[]): string {
  const typeDefinitions = types.map(t => {
    if (t.kind === 'class') {
      return generateClassType(t);
    }
    return generateEnumType(t);
  }).join('\n\n');

  return `// -----------------------------------------------------------------------
// <copyright file="AITFirebase.Types.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace AppsInToss.Firebase
{
${typeDefinitions}
}
`;
}

/**
 * C# class 타입 생성
 */
function generateClassType(type: FirebaseTypeDefinition): string {
  const description = type.description
    ? `    /// <summary>${type.description}</summary>\n`
    : '';

  const properties = (type.properties || []).map(prop => {
    const propDesc = prop.description
      ? `        /// <summary>${prop.description}</summary>\n`
      : '';
    const nullableType = prop.nullable && prop.csharpType !== 'string' && prop.csharpType !== 'bool'
      ? `${prop.csharpType}?`
      : prop.csharpType;

    return `${propDesc}        [Preserve]
        [JsonProperty("${prop.jsonName}")]
        public ${nullableType} ${prop.name} { get; set; }`;
  }).join('\n\n');

  return `${description}    [Serializable]
    [Preserve]
    public class ${type.name}
    {
        [Preserve]
        public ${type.name}() { }

${properties}
    }`;
}

/**
 * C# enum 타입 생성
 */
function generateEnumType(type: FirebaseTypeDefinition): string {
  const description = type.description
    ? `    /// <summary>${type.description}</summary>\n`
    : '';

  const values = (type.enumValues || []).map(v => `        ${v}`).join(',\n');

  return `${description}    [Preserve]
    public enum ${type.name}
    {
${values}
    }`;
}
