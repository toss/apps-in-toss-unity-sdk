/**
 * 테스트 하네스용 API 카탈로그 생성기
 *
 * E2E 하네스(Tests~/E2E/SharedScripts)가 필요로 하는 두 가지 사실을 생성기가 직접 내보낸다.
 *  - API 이름 -> 카테고리 (UI 그룹핑)
 *  - enum 타입 -> 멤버 이름 (IL2CPP에서 리플렉션 없이 값을 고르기 위한 표)
 *
 * 하네스가 손으로 같은 표를 따로 유지하면 생성기의 분류와 어긋난다. 그래서 이 모듈은 분류를
 * 다시 계산하지 않고, 생성기가 실제로 쓰는 C# 본문(AIT.{Category}.cs, AIT.Types.{Category}.cs)을
 * 읽어서 표를 만든다. "어느 파일에 들어갔는가"가 곧 카테고리이므로 새 API나 새 enum 값이 생겨도
 * 표가 빠질 수 없다.
 */

import { CATEGORY_ORDER, DEFAULT_CATEGORY } from '../categories.js';

/** 하네스 카탈로그가 놓이는 위치 (저장소 루트 기준) */
export const API_CATALOG_RELATIVE_PATH = 'Tests~/E2E/SharedScripts/Runtime/AITApiCatalog.g.cs';

/** 생성된 카탈로그가 노출하는 C# 타입 이름 */
export const API_CATALOG_CLASS_NAME = 'AITApiCatalog';

export interface ApiCatalogEnum {
  name: string;
  members: string[];
}

export interface ApiCatalogInput {
  /** AIT.{Category}.cs 파일명 -> 본문. API 메서드의 카테고리는 파일이 정한다 */
  categoryFiles: Map<string, string>;
  /** 카테고리 파일 밖에서 AIT partial class에 메서드를 더하는 수동 파일. 파일명 -> { category, content } */
  manualFiles?: Map<string, { category: string; content: string }>;
  /** AIT.Types.{Category}.cs 파일명 -> 본문 */
  typeFiles: Map<string, string>;
}

/**
 * 하네스(APIParameterInspector.GetAllAPIMethods)가 API로 치는 반환 타입.
 * 리플렉션의 ReturnType.Name이 Task/Awaitable/Action으로 시작하는 메서드만 API다.
 */
const API_RETURN_TYPE_PREFIXES = ['Task', 'Awaitable', 'Action'];

/** 한 줄짜리 `public static [async] <반환 타입> <이름>(` 선언 */
const PUBLIC_STATIC_METHOD_RE = /^\s*public\s+static\s+(?:async\s+)?(.+?)\s+(\w+)\s*\(/;

/** C# 본문에서 하네스가 API로 보는 public static 메서드 이름을 모은다 */
export function extractApiMethodNames(source: string): string[] {
  const names = new Set<string>();
  for (const line of source.split('\n')) {
    if (/^\s*\/\//.test(line)) continue;
    const match = PUBLIC_STATIC_METHOD_RE.exec(line);
    if (!match) continue;
    // 네임스페이스 접두(System.Action 등)는 리플렉션 Type.Name에 없으므로 뗀다
    const returnType = match[1].replace(/^(?:\w+\.)+/, '');
    if (!API_RETURN_TYPE_PREFIXES.some(prefix => returnType.startsWith(prefix))) continue;
    names.add(match[2]);
  }
  return Array.from(names);
}

/** `[Attr(...)]` 형태의 어트리뷰트 (문자열 안의 `]`는 무시) */
const ATTRIBUTE_RE = /\[(?:[^\]"]|"(?:[^"\\]|\\.)*")*\]/g;

/** 네임스페이스 바로 아래(들여쓰기 4칸)에 선언된 public enum의 멤버 이름을 선언 순서대로 모은다 */
export function extractEnums(source: string): ApiCatalogEnum[] {
  const enums: ApiCatalogEnum[] = [];
  const declRe = /^ {4}public\s+enum\s+(\w+)\b[^{]*\{/gm;
  let match: RegExpExecArray | null;
  while ((match = declRe.exec(source)) !== null) {
    const bodyStart = match.index + match[0].length;
    const bodyEnd = source.indexOf('\n    }', bodyStart);
    if (bodyEnd < 0) throw new Error(`enum ${match[1]}의 닫는 중괄호를 찾지 못했습니다`);
    const body = source
      .slice(bodyStart, bodyEnd)
      .split('\n')
      .filter(line => !/^\s*(?:\/\/|#)/.test(line))
      .map(line => line.replace(/\s\/\/.*$/, ''))
      .join('\n')
      .replace(ATTRIBUTE_RE, '');
    const members = body
      .split(',')
      .map(part => part.trim().split('=')[0].trim().replace(/^@/, ''))
      .filter(part => part.length > 0);
    enums.push({ name: match[1], members });
  }
  return enums;
}

/** 카테고리 순서: CATEGORY_ORDER 우선, 그 밖은 알파벳 순 (generateCategoryFiles와 같은 규약) */
export function orderCategories(categories: Iterable<string>): string[] {
  const present = new Set(categories);
  const ordered = CATEGORY_ORDER.filter(category => present.has(category));
  const rest = Array.from(present)
    .filter(category => !CATEGORY_ORDER.includes(category))
    .sort();
  return [...ordered, ...rest];
}

function categoryFromFileName(fileName: string): string {
  const match = /^AIT\.(.+)\.cs$/.exec(fileName);
  if (!match) throw new Error(`카테고리 파일 이름 규약(AIT.{Category}.cs)이 아닙니다: ${fileName}`);
  return match[1];
}

function csString(value: string): string {
  return `"${value.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
}

/** 카탈로그 입력에서 API 이름 -> 카테고리 표를 만든다. 같은 이름이 두 카테고리에 있으면 실패한다 */
export function collectApiCategories(input: ApiCatalogInput): Map<string, string> {
  const result = new Map<string, string>();
  const add = (name: string, category: string, origin: string) => {
    const existing = result.get(name);
    if (existing !== undefined && existing !== category) {
      throw new Error(`API '${name}'이 두 카테고리에 있습니다: ${existing}, ${category} (${origin})`);
    }
    result.set(name, category);
  };
  for (const [fileName, content] of input.categoryFiles) {
    const category = categoryFromFileName(fileName);
    for (const name of extractApiMethodNames(content)) add(name, category, fileName);
  }
  for (const [fileName, manual] of input.manualFiles ?? []) {
    for (const name of extractApiMethodNames(manual.content)) add(name, manual.category || DEFAULT_CATEGORY, fileName);
  }
  return result;
}

/** 카탈로그 입력에서 enum 표를 만든다. 이름순으로 고정해 출력이 안정적이다 */
export function collectEnums(input: ApiCatalogInput): ApiCatalogEnum[] {
  const byName = new Map<string, ApiCatalogEnum>();
  for (const [fileName, content] of input.typeFiles) {
    for (const e of extractEnums(content)) {
      if (byName.has(e.name)) throw new Error(`enum '${e.name}'이 둘 이상 선언됩니다 (${fileName})`);
      byName.set(e.name, e);
    }
  }
  return Array.from(byName.values()).sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
}

/** 하네스 카탈로그 C# 소스를 만든다 */
export function generateApiCatalog(input: ApiCatalogInput): string {
  const apiCategories = collectApiCategories(input);
  const categoryOrder = orderCategories(apiCategories.values());
  const enums = collectEnums(input);

  const apiLines = Array.from(apiCategories.entries())
    .sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0))
    .map(([name, category]) => `            { ${csString(name)}, ${csString(category)} },`);
  const enumLines = enums.map(e => {
    const members = e.members.map(csString).join(', ');
    return `            { typeof(AppsInToss.${e.name}), new[] { ${members} } },`;
  });

  return `// -----------------------------------------------------------------------
// <auto-generated>
//     이 파일은 sdk-runtime-generator가 생성합니다. 직접 수정하지 마세요.
//     'cd sdk-runtime-generator~ && pnpm generate'가 다시 씁니다.
// </auto-generated>
// -----------------------------------------------------------------------
//
// E2E 하네스가 쓰는 API 카탈로그. 카테고리와 enum 멤버를 생성기가 이미 정한 대로 옮겨 둔다.
// IL2CPP에서 리플렉션이나 어트리뷰트 조회에 기대지 않으려고 정적 표로 둔다.

using System;
using System.Collections.Generic;

/// <summary>
/// 생성기가 내보낸 API 이름 -> 카테고리, 카테고리 순서, enum 멤버 이름 표
/// </summary>
public static class ${API_CATALOG_CLASS_NAME}
{
    /// <summary>
    /// 카테고리 표시 순서 (실제로 쓰이는 카테고리만)
    /// </summary>
    public static readonly string[] CategoryOrder =
    {
${categoryOrder.map(category => `        ${csString(category)},`).join('\n')}
    };

    /// <summary>
    /// API 메서드 이름 -> 카테고리
    /// </summary>
    public static readonly Dictionary<string, string> ApiCategories = new Dictionary<string, string>
    {
${apiLines.join('\n')}
    };

    /// <summary>
    /// enum 타입 -> 멤버 이름 (선언 순서, Enum.Parse 입력용)
    /// </summary>
    public static readonly Dictionary<Type, string[]> EnumNames = new Dictionary<Type, string[]>
    {
${enumLines.join('\n')}
    };
}
`;
}
