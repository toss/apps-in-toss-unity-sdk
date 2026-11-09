/**
 * JSON 코덱 생성 단계 (Newtonsoft 없이 SDK 타입을 읽고 쓰는 코드)
 *
 * 앞 단계들(type-collector, compat, type-name-pins, union-result 템플릿, AITCore 템플릿)은 필드의
 * 와이어 정보를 다음 "표시"로 남긴다. 이 단계가 그 표시를 읽어 타입별 읽기·쓰기 코드를 만들고,
 * 출력 C#에서는 표시를 지운다. 출력은 Newtonsoft를 참조하지 않는다.
 *
 *   [JsonProperty("wire")]                     와이어 이름 (없으면 C# 멤버 이름 그대로)
 *   [JsonProperty("wire", NullValueHandling = NullValueHandling.Ignore)]
 *                                              null이면 쓰지 않고, JSON null은 읽지 않는다
 *   [JsonIgnore]                               와이어에 없는 멤버
 *   [Newtonsoft.Json.JsonExtensionData] + IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData
 *                                              타입에 없는 키를 모으는 필드 (출력에서는 IDictionary<string, JsonValue>)
 *   `XSpecified` bool 멤버                      X가 와이어에 왔는지 (읽을 때 채우고, 쓸 때 false면 X를 뺀다)
 *
 * 와이어 규칙은 이전 Newtonsoft 설정(기본 JsonSerializerSettings + SmartEnumConverter)과 같다:
 * - 쓰기: 필드(선언 순서) 다음 getter가 있는 프로퍼티, 마지막에 _extensionData. null도 `null`로 쓴다.
 * - 읽기: 이름이 정확히 같은 멤버를 먼저, 없으면 대소문자를 무시하고 찾는다. 없으면 _extensionData로.
 * - enum: [EnumMember]가 하나라도 있으면 문자열, 아니면 숫자.
 *
 * 생성 코드는 리플렉션을 쓰지 않는다(IL2CPP가 쓰이지 않는 타입의 코드를 지울 수 있게).
 * 쓰기는 각 타입의 `IAITJsonWritable` 구현, 읽기는 `AITJsonCodec.R_{타입}` 정적 메서드다.
 *
 * 템플릿에서 타입별 읽기 코드가 필요하면 `AITJsonCodec.Deserialize<T>(jsonString)` 매크로를 쓴다.
 * 이 단계가 T에 맞는 읽기 식으로 바꾼다 (런타임에 그런 제네릭 메서드는 없다).
 */

// ---------------------------------------------------------------------------
// C# 선언 모델
// ---------------------------------------------------------------------------

export interface CsMember {
  name: string;
  /** JSON 키 */
  wire: string;
  type: string;
  isProperty: boolean;
  /** 와이어에서 읽어 대입할 수 있는가 (필드, setter 있는 프로퍼티) */
  canRead: boolean;
  /** 와이어로 쓸 수 있는가 (필드, getter 있는 프로퍼티) */
  canWrite: boolean;
  ignored: boolean;
  nullIgnore: boolean;
  isNew: boolean;
}

export interface CsClass {
  name: string;
  /** 바깥 클래스 경로 (예: ['AITCore']) */
  outer: string[];
  base?: string;
  members: CsMember[];
  hasExtensionData: boolean;
  serializable: boolean;
  file: string;
}

export interface CsEnumMember {
  name: string;
  wire?: string;
  value: number;
}

export interface CsEnum {
  name: string;
  outer: string[];
  members: CsEnumMember[];
  file: string;
}

export interface CsModel {
  classes: CsClass[];
  enums: CsEnum[];
}

const EXT_FIELD = '_extensionData';
const IR_EXT_TYPE = 'System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken>';
const OUT_EXT_TYPE = 'System.Collections.Generic.IDictionary<string, JsonValue>';

/** 줄에서 문자열·문자 리터럴과 // 주석을 지운다 (중괄호 세기용). */
function codeOnly(line: string): string {
  let out = '';
  let i = 0;
  while (i < line.length) {
    const c = line[i];
    if (c === '/' && line[i + 1] === '/') break;
    if (c === '"' || c === '\'') {
      const quote = c;
      const verbatim = c === '"' && line[i - 1] === '@';
      i++;
      while (i < line.length) {
        if (!verbatim && line[i] === '\\') { i += 2; continue; }
        if (line[i] === quote) {
          if (verbatim && line[i + 1] === '"') { i += 2; continue; }
          break;
        }
        i++;
      }
      i++;
      out += quote + quote;
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

interface Scope {
  kind: 'class' | 'enum' | 'other';
  bodyDepth: number;
  cls?: CsClass;
  en?: CsEnum;
}

const DECL_RE = /^(?:(?:public|internal|private|protected|static|sealed|abstract|partial)\s+)*(class|enum|struct|interface)\s+(\w+)(?:\s*:\s*([\w.]+))?/;

function parseAttribute(attrs: string[], member: Partial<CsMember>): void {
  for (const a of attrs) {
    const jp = /^JsonProperty\("([^"]*)"(.*)\)$/.exec(a);
    if (jp) {
      member.wire = jp[1];
      const rest = jp[2].trim();
      if (rest === ', NullValueHandling = NullValueHandling.Ignore') member.nullIgnore = true;
      else if (rest !== '') throw new Error(`json-codec: 지원하지 않는 JsonProperty 인자: [${a}]`);
      continue;
    }
    if (a === 'JsonIgnore') { member.ignored = true; continue; }
    if (/^(Newtonsoft\.Json\.)?Json/.test(a) && a !== 'Newtonsoft.Json.JsonExtensionData') {
      throw new Error(`json-codec: 알 수 없는 JSON 표시: [${a}]`);
    }
  }
}

/**
 * 생성된 C# 파일에서 클래스·enum 선언과 멤버를 읽는다.
 * 생성기가 내는 형식(한 줄에 선언 하나, 4칸 들여쓰기)만 다룬다.
 */
export function parseCSharpDeclarations(code: string, file = ''): CsModel {
  const model: CsModel = { classes: [], enums: [] };
  const lines = code.split('\n');
  const scopes: Scope[] = [];
  let depth = 0;
  let pendingAttrs: string[] = [];
  let pendingDecl: Scope | null = null;

  const current = () => scopes[scopes.length - 1];
  const outerNames = () => scopes.filter(s => s.kind === 'class').map(s => s.cls!.name);

  for (let li = 0; li < lines.length; li++) {
    const raw = lines[li];
    const trimmed = raw.trim();
    const code = codeOnly(raw).trim();
    const scope = current();
    const atBody = scope && depth === scope.bodyDepth;

    if (trimmed.startsWith('//') || trimmed === '') {
      // 주석·빈 줄: 속성 누적은 유지
    } else if (/^\[.*\]$/.test(trimmed) && !trimmed.startsWith('[assembly')) {
      pendingAttrs.push(trimmed.slice(1, -1).trim());
    } else {
      const decl = DECL_RE.exec(code);
      if (decl) {
        const outer = outerNames();
        if (decl[1] === 'class' || decl[1] === 'struct') {
          const cls: CsClass = {
            name: decl[2],
            outer,
            base: decl[3],
            members: [],
            hasExtensionData: false,
            serializable: pendingAttrs.includes('Serializable') && decl[1] === 'class',
            file,
          };
          model.classes.push(cls);
          pendingDecl = { kind: 'class', bodyDepth: -1, cls };
        } else if (decl[1] === 'enum') {
          const en: CsEnum = { name: decl[2], outer, members: [], file };
          model.enums.push(en);
          pendingDecl = { kind: 'enum', bodyDepth: -1, en };
        } else {
          pendingDecl = { kind: 'other', bodyDepth: -1 };
        }
        pendingAttrs = [];
      } else if (atBody && scope.kind === 'class') {
        const m = parseMemberLine(code, pendingAttrs);
        if (m) {
          if (m.name === EXT_FIELD) scope.cls!.hasExtensionData = true;
          else scope.cls!.members.push(m);
        }
        pendingAttrs = [];
      } else if (atBody && scope.kind === 'enum') {
        const em = /^(\w+)\s*(?:=\s*(-?\d+))?\s*,?$/.exec(code);
        if (em) {
          const en = scope.en!;
          const prev = en.members[en.members.length - 1];
          const value = em[2] !== undefined ? parseInt(em[2], 10) : prev ? prev.value + 1 : 0;
          let wire: string | undefined;
          for (const a of pendingAttrs) {
            const mm = /^EnumMember\(Value = "([^"]*)"\)$/.exec(a);
            if (mm) wire = mm[1];
          }
          en.members.push({ name: em[1], wire, value });
        } else if (code !== '{' && code !== '}') {
          throw new Error(`json-codec: enum 멤버를 읽을 수 없습니다 (${file}:${li + 1}): ${trimmed}`);
        }
        pendingAttrs = [];
      } else if (!trimmed.startsWith('[')) {
        pendingAttrs = [];
      }
    }

    // 중괄호 세기 (스코프 열고 닫기)
    for (const ch of code) {
      if (ch === '{') {
        depth++;
        if (pendingDecl) {
          pendingDecl.bodyDepth = depth;
          scopes.push(pendingDecl);
          pendingDecl = null;
        }
      } else if (ch === '}') {
        if (current() && current().bodyDepth === depth) scopes.pop();
        depth--;
      }
    }
  }
  return model;
}

function parseMemberLine(code: string, attrs: string[]): CsMember | null {
  const mod = /^((?:(?:public|internal|private|protected|static|readonly|const|new|override|virtual|abstract|sealed|event|unsafe|volatile|extern)\s+)+)(.*)$/.exec(code);
  if (!mod) return null;
  const modifiers = mod[1].split(/\s+/).filter(Boolean);
  if (!modifiers.includes('public')) return null;
  if (modifiers.some(m => m === 'static' || m === 'const' || m === 'event')) return null;
  const rest = mod[2];
  const member: Partial<CsMember> = { ignored: false, nullIgnore: false, isNew: modifiers.includes('new') };
  parseAttribute(attrs, member);

  const arrow = rest.indexOf('=>');
  const paren = rest.indexOf('(');
  const eq = rest.search(/=(?!>)/);
  const brace = rest.indexOf('{');
  const firstStop = Math.min(...[arrow, eq, brace].filter(i => i >= 0), rest.length);
  if (paren >= 0 && paren < firstStop) return null; // 메서드·생성자

  let typeAndName: string;
  let isProperty: boolean;
  let canRead: boolean;
  let canWrite: boolean;
  let blockProperty = false;
  if (arrow >= 0 && (eq < 0 || arrow < eq)) {
    typeAndName = rest.slice(0, arrow);
    isProperty = true;
    canRead = false;
    canWrite = true;
  } else if (brace >= 0 && (eq < 0 || brace < eq)) {
    typeAndName = rest.slice(0, brace);
    const accessors = rest.slice(brace);
    isProperty = true;
    canWrite = /\bget\b/.test(accessors);
    canRead = /\bset\b/.test(accessors);
  } else if (rest.trimEnd().endsWith(';')) {
    typeAndName = eq >= 0 ? rest.slice(0, eq) : rest.trimEnd().slice(0, -1);
    isProperty = false;
    canRead = true;
    canWrite = true;
  } else {
    // 다음 줄에 { get ... set ... } 블록이 오는 프로퍼티
    typeAndName = rest;
    isProperty = true;
    canRead = false;
    canWrite = false;
    blockProperty = true;
  }
  const tn = /^(.+?)\s+([A-Za-z_]\w*)\s*$/.exec(typeAndName.trim());
  if (!tn) throw new Error(`json-codec: 멤버 선언을 읽을 수 없습니다: ${code}`);
  member.type = tn[1].trim();
  member.name = tn[2];
  if (blockProperty && !member.ignored) {
    throw new Error(`json-codec: 본문이 있는 프로퍼티는 [JsonIgnore]여야 합니다: ${code}`);
  }
  if (!member.ignored && /^System\.(Action|Func)\b/.test(member.type)) {
    throw new Error(`json-codec: 델리게이트 필드는 [JsonIgnore]여야 합니다: ${code}`);
  }
  return {
    name: member.name,
    wire: member.wire ?? member.name,
    type: member.type,
    isProperty,
    canRead,
    canWrite,
    ignored: member.ignored!,
    nullIgnore: member.nullIgnore!,
    isNew: member.isNew!,
  };
}

// ---------------------------------------------------------------------------
// 타입 식
// ---------------------------------------------------------------------------

export type TypeNode =
  | { kind: 'name'; name: string }
  | { kind: 'nullable'; inner: TypeNode }
  | { kind: 'array'; inner: TypeNode }
  | { kind: 'generic'; name: string; args: TypeNode[] };

export function parseTypeExpr(text: string): TypeNode {
  const t = text.trim();
  if (t.endsWith('?')) return { kind: 'nullable', inner: parseTypeExpr(t.slice(0, -1)) };
  if (t.endsWith('[]')) return { kind: 'array', inner: parseTypeExpr(t.slice(0, -2)) };
  const lt = t.indexOf('<');
  if (lt >= 0) {
    if (!t.endsWith('>')) throw new Error(`json-codec: 타입을 읽을 수 없습니다: ${text}`);
    const name = t.slice(0, lt).trim();
    const inner = t.slice(lt + 1, -1);
    const args: string[] = [];
    let level = 0;
    let start = 0;
    for (let i = 0; i < inner.length; i++) {
      const c = inner[i];
      if (c === '<') level++;
      else if (c === '>') level--;
      else if (c === ',' && level === 0) {
        args.push(inner.slice(start, i));
        start = i + 1;
      }
    }
    args.push(inner.slice(start));
    return { kind: 'generic', name, args: args.map(parseTypeExpr) };
  }
  return { kind: 'name', name: t };
}

function renderType(t: TypeNode): string {
  switch (t.kind) {
    case 'name': return t.name === 'JToken' || t.name === 'Newtonsoft.Json.Linq.JToken' ? 'JsonValue' : t.name;
    case 'nullable': return `${renderType(t.inner)}?`;
    case 'array': return `${renderType(t.inner)}[]`;
    case 'generic': return `${t.name}<${t.args.map(renderType).join(', ')}>`;
  }
}

const PRIMITIVE_READ: Record<string, [string, string]> = {
  string: ['Str', 'Str'],
  double: ['Dbl', 'DblN'],
  bool: ['Bool', 'BoolN'],
  long: ['Long', 'LongN'],
  int: ['Int', 'IntN'],
};
const VALUE_PRIMITIVES = new Set(['double', 'bool', 'long', 'int']);

function stripGenericNs(name: string): string {
  return name.replace(/^System\.Collections\.Generic\./, '');
}

export class CodecTypes {
  private readonly classes = new Map<string, CsClass>();
  private readonly enums = new Map<string, CsEnum>();
  /** 딕셔너리 키로 쓰인 enum (키 변환 도우미가 필요) */
  readonly enumKeys = new Set<string>();

  constructor(model: CsModel) {
    for (const c of model.classes) if (c.serializable) this.classes.set(c.name, c);
    for (const e of model.enums) this.enums.set(e.name, e);
  }

  classOf(name: string): CsClass | undefined {
    return this.classes.get(name) ?? this.classes.get(name.split('.').pop()!);
  }

  enumOf(name: string): CsEnum | undefined {
    return this.enums.get(name);
  }

  isValueType(t: TypeNode): boolean {
    return t.kind === 'name' && (VALUE_PRIMITIVES.has(t.name) || this.enums.has(t.name));
  }

  /** JsonValue 식 `jv`를 타입 t의 값으로 읽는 C# 식. */
  read(t: TypeNode, jv: string, depth = 0): string {
    const n = depth + 1;
    switch (t.kind) {
      case 'name': {
        const p = PRIMITIVE_READ[t.name];
        if (p) return `AITJson.${p[0]}(${jv})`;
        if (t.name === 'object') return `AITJson.Any(${jv})`;
        if (t.name === 'JsonValue' || t.name === 'JToken' || t.name === 'Newtonsoft.Json.Linq.JToken') return jv;
        if (this.enums.has(t.name)) return `AITJsonCodec.RE_${t.name}(${jv}).GetValueOrDefault()`;
        const c = this.classOf(t.name);
        if (c) return `AITJsonCodec.R_${codecId(c)}(${jv})`;
        throw new Error(`json-codec: 읽기 코드를 만들 수 없는 타입: ${t.name}`);
      }
      case 'nullable': {
        if (t.inner.kind === 'name') {
          const p = PRIMITIVE_READ[t.inner.name];
          if (p && VALUE_PRIMITIVES.has(t.inner.name)) return `AITJson.${p[1]}(${jv})`;
          if (this.enums.has(t.inner.name)) return `AITJsonCodec.RE_${t.inner.name}(${jv})`;
        }
        throw new Error(`json-codec: 읽기 코드를 만들 수 없는 nullable 타입: ${renderType(t)}`);
      }
      case 'array': {
        if (t.inner.kind === 'name' && t.inner.name === 'byte') return `AITJson.Bytes(${jv})`;
        return `AITJson.Arr<${renderType(t.inner)}>(${jv}, j${n} => ${this.read(t.inner, `j${n}`, n)})`;
      }
      case 'generic': {
        const name = stripGenericNs(t.name);
        if ((name === 'List' || name === 'IList' || name === 'IReadOnlyList' || name === 'IEnumerable') && t.args.length === 1) {
          return `AITJson.Lst<${renderType(t.args[0])}>(${jv}, j${n} => ${this.read(t.args[0], `j${n}`, n)})`;
        }
        if ((name === 'Dictionary' || name === 'IDictionary') && t.args.length === 2) {
          return `AITJson.Dict<${renderType(t.args[0])}, ${renderType(t.args[1])}>(${jv}, ${this.keyParser(t.args[0])}, j${n} => ${this.read(t.args[1], `j${n}`, n)})`;
        }
        throw new Error(`json-codec: 읽기 코드를 만들 수 없는 제네릭 타입: ${renderType(t)}`);
      }
    }
  }

  /** 값 식 `v`를 쓰는 C# 식 (문장 끝 `;` 없음). */
  write(t: TypeNode, w: string, v: string, depth = 0): string {
    const n = depth + 1;
    switch (t.kind) {
      case 'name': {
        if (PRIMITIVE_READ[t.name]) return `${w}.Value(${v})`;
        if (t.name === 'object') return `AITJson.Any(${w}, ${v})`;
        if (t.name === 'JsonValue' || t.name === 'JToken' || t.name === 'Newtonsoft.Json.Linq.JToken') return `AITJson.Val(${w}, ${v})`;
        if (this.enums.has(t.name)) return `AITJsonCodec.WE_${t.name}(${w}, ${v})`;
        if (this.classOf(t.name)) return `AITJson.Obj(${w}, ${v})`;
        throw new Error(`json-codec: 쓰기 코드를 만들 수 없는 타입: ${t.name}`);
      }
      case 'nullable': {
        if (t.inner.kind === 'name' && (VALUE_PRIMITIVES.has(t.inner.name) || this.enums.has(t.inner.name))) {
          return this.write(t.inner, w, v, depth);
        }
        throw new Error(`json-codec: 쓰기 코드를 만들 수 없는 nullable 타입: ${renderType(t)}`);
      }
      case 'array': {
        if (t.inner.kind === 'name' && t.inner.name === 'byte') return `AITJson.Bytes(${w}, ${v})`;
        return `AITJson.Arr<${renderType(t.inner)}>(${w}, ${v}, (w${n}, x${n}) => ${this.write(t.inner, `w${n}`, `x${n}`, n)})`;
      }
      case 'generic': {
        const name = stripGenericNs(t.name);
        if ((name === 'List' || name === 'IList' || name === 'IReadOnlyList' || name === 'IEnumerable') && t.args.length === 1) {
          return `AITJson.Arr<${renderType(t.args[0])}>(${w}, ${v}, (w${n}, x${n}) => ${this.write(t.args[0], `w${n}`, `x${n}`, n)})`;
        }
        if ((name === 'Dictionary' || name === 'IDictionary') && t.args.length === 2) {
          return `AITJson.Dict<${renderType(t.args[0])}, ${renderType(t.args[1])}>(${w}, ${v}, ${this.keyWriter(t.args[0])}, (w${n}, x${n}) => ${this.write(t.args[1], `w${n}`, `x${n}`, n)})`;
        }
        throw new Error(`json-codec: 쓰기 코드를 만들 수 없는 제네릭 타입: ${renderType(t)}`);
      }
    }
  }

  private keyParser(k: TypeNode): string {
    if (k.kind === 'name' && k.name === 'string') return 'null';
    if (k.kind === 'name' && this.enums.has(k.name)) {
      this.enumKeys.add(k.name);
      return `AITJsonCodec.KE_${k.name}`;
    }
    throw new Error(`json-codec: 딕셔너리 키로 쓸 수 없는 타입: ${renderType(k)}`);
  }

  private keyWriter(k: TypeNode): string {
    if (k.kind === 'name' && k.name === 'string') return 'null';
    if (k.kind === 'name' && this.enums.has(k.name)) {
      this.enumKeys.add(k.name);
      return `AITJsonCodec.SE_${k.name}`;
    }
    throw new Error(`json-codec: 딕셔너리 키로 쓸 수 없는 타입: ${renderType(k)}`);
  }
}

function codecId(c: CsClass): string {
  return [...c.outer, c.name].join('_');
}

function qualifiedName(c: { outer: string[]; name: string }): string {
  return [...c.outer, c.name].join('.');
}

function csString(s: string): string {
  return JSON.stringify(s);
}

// ---------------------------------------------------------------------------
// 코덱 생성
// ---------------------------------------------------------------------------

/** 상속을 펼친 와이어 멤버 (자기 멤버 먼저, 그다음 숨겨지지 않은 부모 멤버) */
function effectiveMembers(c: CsClass, types: CodecTypes): { members: CsMember[]; hasExt: boolean; all: CsMember[] } {
  const own = c.members;
  let members = own.filter(m => !m.ignored);
  let all = [...own];
  let hasExt = c.hasExtensionData;
  if (c.base) {
    const base = types.classOf(c.base);
    if (base) {
      const inherited = effectiveMembers(base, types);
      const ownNames = new Set(own.map(m => m.name));
      members = [...members, ...inherited.members.filter(m => !ownNames.has(m.name))];
      all = [...all, ...inherited.all.filter(m => !ownNames.has(m.name))];
      hasExt = hasExt || inherited.hasExt;
    }
  }
  return { members, hasExt, all };
}

function specifiedOf(m: CsMember, all: CsMember[]): CsMember | undefined {
  return all.find(x => x.name === `${m.name}Specified` && x.type === 'bool');
}

function generateClassCodec(c: CsClass, types: CodecTypes): { reader: string; writer: string } {
  const { members, hasExt, all } = effectiveMembers(c, types);
  const id = codecId(c);
  const qn = qualifiedName(c);

  // 읽기
  const readable = members.filter(m => m.canRead);
  const names = readable.map(m => csString(m.wire)).join(', ');
  const cases = readable.map((m, i) => {
    const t = parseTypeExpr(m.type);
    const spec = specifiedOf(m, all);
    const assign = [`o.${m.name} = ${types.read(t, 'v')};`];
    if (spec) assign.push(`o.${spec.name} = true;`);
    const guard = types.isValueType(t) || m.nullIgnore;
    const body = guard
      ? `if (!AITJson.IsNull(v)) { ${assign.join(' ')} }`
      : assign.join(' ');
    return `                    case ${i}: ${body} break;`;
  });
  const fallback = hasExt ? 'AITJson.Ext(ref o._extensionData, p); break;' : 'break;';
  const reader = [
    `        private static readonly string[] N_${id} = { ${names} };`,
    `        internal static ${qn} R_${id}(JsonValue j)`,
    `        {`,
    `            if (j == null || j.Kind != JsonValueKind.Object) return null;`,
    `            var o = new ${qn}();`,
    `            foreach (var p in j._members)`,
    `            {`,
    `                var v = p.Value;`,
    `                switch (AITJson.Match(p.Key, N_${id}))`,
    `                {`,
    ...cases,
    `                    default: ${fallback}`,
    `                }`,
    `            }`,
    `            return o;`,
    `        }`,
  ].join('\n');

  // 쓰기 (Newtonsoft 순서: 필드 → 프로퍼티 → _extensionData)
  const writable = [...members.filter(m => m.canWrite && !m.isProperty), ...members.filter(m => m.canWrite && m.isProperty)];
  const indent = '            ';
  const stmts = writable.map(m => {
    const t = parseTypeExpr(m.type);
    const spec = specifiedOf(m, all);
    const stmt = `w.Name(${csString(m.wire)}); ${types.write(t, 'w', `this.${m.name}`)};`;
    if (spec) return `${indent}if (this.${spec.name}) { ${stmt} }`;
    if (m.nullIgnore && !types.isValueType(t)) return `${indent}if (this.${m.name} != null) { ${stmt} }`;
    return `${indent}${stmt}`;
  });
  const body = [
    `        void IAITJsonWritable.WriteJson(AITJsonWriter w)`,
    `        {`,
    `${indent}w.BeginObject();`,
    ...stmts,
    ...(hasExt ? [`${indent}AITJson.Ext(w, this._extensionData);`] : []),
    `${indent}w.EndObject();`,
    `        }`,
  ];
  let writer = [`    partial class ${c.name} : IAITJsonWritable`, `    {`, ...body, `    }`].join('\n');
  for (const outer of [...c.outer].reverse()) {
    writer = `    partial class ${outer}\n    {\n${writer.split('\n').map(l => '    ' + l).join('\n')}\n    }`;
  }
  return { reader, writer };
}

function generateEnumCodec(e: CsEnum, types: CodecTypes): string {
  const names = e.members.map(m => m.name);
  const isString = e.members.some(m => m.wire !== undefined);
  const wire = e.members.map(m => m.wire ?? m.name);
  const sequential = e.members.every((m, i) => m.value === i);
  const sameNames = wire.every((w, i) => w === names[i]);
  const lines: string[] = [];
  lines.push(`        private static readonly string[] EN_${e.name} = { ${names.map(csString).join(', ')} };`);
  const wireArr = isString && !sameNames ? `EW_${e.name}` : `EN_${e.name}`;
  if (isString && !sameNames) {
    lines.push(`        private static readonly string[] EW_${e.name} = { ${wire.map(csString).join(', ')} };`);
  }
  const values = sequential ? 'null' : `EV_${e.name}`;
  if (!sequential) {
    lines.push(`        private static readonly int[] EV_${e.name} = { ${e.members.map(m => m.value).join(', ')} };`);
  }
  const writeWire = isString ? wireArr : 'null';
  lines.push(
    `        internal static ${e.name}? RE_${e.name}(JsonValue j) => AITJson.Enum(j, ${wireArr}, EN_${e.name}, ${values}, out var v) ? (${e.name})v : (${e.name}?)null;`,
    `        internal static void WE_${e.name}(AITJsonWriter w, ${e.name}? v) { if (v.HasValue) AITJson.Enum(w, (int)v.Value, ${writeWire}, ${values}); else w.Null(); }`,
  );
  if (types.enumKeys.has(e.name)) {
    lines.push(
      `        internal static bool KE_${e.name}(string s, out ${e.name} k) { bool ok = AITJson.EnumText(s, ${wireArr}, EN_${e.name}, ${values}, out var v); k = (${e.name})v; return ok; }`,
      `        internal static string SE_${e.name}(${e.name} k) => AITJson.EnumWire((int)k, ${wireArr}, ${values}) ?? ((int)k).ToString(CultureInfo.InvariantCulture);`,
    );
  }
  return lines.join('\n');
}

/** `ReadBoxed`: Type으로 고르는 읽기 (공개 API `AITJsonSettings.Deserialize<T>`, 콜백 인자용). */
function generateBoxedReader(
  methodName: string,
  classes: CsClass[],
  enums: CsEnum[],
  arrays: boolean,
): string {
  const lines = [
    `        internal static object ${methodName}(Type t, JsonValue j)`,
    `        {`,
    `            if (t == typeof(string)) return AITJson.Str(j);`,
    `            if (t == typeof(double)) return AITJson.Dbl(j);`,
    `            if (t == typeof(double?)) return AITJson.DblN(j);`,
    `            if (t == typeof(bool)) return AITJson.Bool(j);`,
    `            if (t == typeof(bool?)) return AITJson.BoolN(j);`,
    `            if (t == typeof(long)) return AITJson.Long(j);`,
    `            if (t == typeof(long?)) return AITJson.LongN(j);`,
    `            if (t == typeof(int)) return AITJson.Int(j);`,
    `            if (t == typeof(int?)) return AITJson.IntN(j);`,
    `            if (t == typeof(object)) return AITJson.Any(j);`,
    `            if (t == typeof(JsonValue)) return j;`,
  ];
  for (const c of classes) {
    lines.push(`            if (t == typeof(${qualifiedName(c)})) return R_${codecId(c)}(j);`);
  }
  for (const e of enums) {
    lines.push(`            if (t == typeof(${e.name}) || t == typeof(${e.name}?)) return RE_${e.name}(j);`);
  }
  if (arrays) {
    lines.push(
      `            if (t.IsArray && t.GetArrayRank() == 1)`,
      `            {`,
      `                if (j == null || j.Kind != JsonValueKind.Array) return null;`,
      `                var elementType = t.GetElementType();`,
      `                var array = Array.CreateInstance(elementType, j.Count);`,
      `                for (int i = 0; i < j.Count; i++) array.SetValue(${methodName}(elementType, j._items[i]), i);`,
      `                return array;`,
      `            }`,
    );
  }
  lines.push(
    `            throw new NotSupportedException("JSON: " + t.FullName + " is not an Apps in Toss SDK type");`,
    `        }`,
  );
  return lines.join('\n');
}

export interface JsonCodecResult {
  /** 표시를 지우고 매크로를 푼 파일들 (입력과 같은 키) */
  files: Map<string, string>;
  /** 생성된 AITJsonCodec.cs */
  codecFile: string;
  model: CsModel;
}

const CODEC_FILE_HEADER = `// -----------------------------------------------------------------------
// <copyright file="AITJsonCodec.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Generated JSON read/write code
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;

// [Obsolete] 타입·멤버도 와이어 형식은 그대로 읽고 쓴다.
#pragma warning disable 0612, 0618

namespace AppsInToss
{
`;

/**
 * 표시(IR)를 지운다: Newtonsoft 속성 줄과 using을 없애고, _extensionData 타입을 JsonValue로 바꾸고,
 * 코덱이 붙을 클래스를 partial로 만든다.
 */
export function stripJsonMarkers(code: string, partialClasses: Set<string>): string {
  const out: string[] = [];
  for (const line of code.split('\n')) {
    const t = line.trim();
    if (/^\[JsonProperty\(.*\)\]$/.test(t) || t === '[JsonIgnore]' || t === '[Newtonsoft.Json.JsonExtensionData]') continue;
    if (/^using Newtonsoft\.Json(\.\w+)*;$/.test(t)) continue;
    let l = line.split(IR_EXT_TYPE).join(OUT_EXT_TYPE);
    const decl = /^(\s*)public class (\w+)\b/.exec(l);
    if (decl && partialClasses.has(decl[2])) l = l.replace('public class ', 'public partial class ');
    out.push(l);
  }
  return out.join('\n');
}

/** `AITJsonCodec.Deserialize<T>(json)` 매크로를 T에 맞는 읽기 식으로 바꾼다. */
export function expandCodecMacros(code: string, types: CodecTypes): string {
  const MACRO = 'AITJsonCodec.Deserialize<';
  let result = '';
  let pos = 0;
  while (true) {
    const at = code.indexOf(MACRO, pos);
    if (at < 0) break;
    result += code.slice(pos, at);
    let i = at + MACRO.length;
    let level = 1;
    while (i < code.length && level > 0) {
      if (code[i] === '<') level++;
      else if (code[i] === '>') level--;
      i++;
    }
    const typeText = code.slice(at + MACRO.length, i - 1);
    if (code[i] !== '(') throw new Error(`json-codec: 매크로 형식 오류: ${code.slice(at, i + 20)}`);
    let parens = 1;
    let j = i + 1;
    while (j < code.length && parens > 0) {
      const c = code[j];
      if (c === '"') {
        j++;
        while (j < code.length && code[j] !== '"') j += code[j] === '\\' ? 2 : 1;
      } else if (c === '(') parens++;
      else if (c === ')') parens--;
      j++;
    }
    const arg = code.slice(i + 1, j - 1);
    result += types.read(parseTypeExpr(typeText), `AITJson.Parse(${arg})`);
    pos = j;
  }
  return result + code.slice(pos);
}

/**
 * 생성된 C# 파일들(타입 파일, AITCore, 카테고리 파일)을 받아 코덱 파일을 만들고 각 파일의 표시를 지운다.
 */
export function applyJsonCodec(files: Map<string, string>): JsonCodecResult {
  const model: CsModel = { classes: [], enums: [] };
  for (const [name, code] of files) {
    const m = parseCSharpDeclarations(code, name);
    model.classes.push(...m.classes);
    model.enums.push(...m.enums);
  }
  const seen = new Set<string>();
  for (const c of model.classes.filter(c => c.serializable)) {
    const q = qualifiedName(c);
    if (seen.has(q)) throw new Error(`json-codec: 같은 이름의 직렬화 클래스가 둘 있습니다: ${q}`);
    seen.add(q);
  }
  const types = new CodecTypes(model);
  const serializable = model.classes.filter(c => c.serializable);

  // 매크로를 먼저 풀어야 딕셔너리 키 enum 목록이 채워진다
  const expanded = new Map<string, string>();
  for (const [name, code] of files) expanded.set(name, expandCodecMacros(code, types));

  const classCodecs = serializable.map(c => ({ c, ...generateClassCodec(c, types) }));

  // 콜백 인자로만 쓰이는 타입 (InvokeCallbackBag<T>, RegisterNestedCallback<TParam>)
  const callbackArgNames = new Set<string>();
  for (const c of model.classes) {
    for (const m of c.members) {
      const t = parseTypeExpr(m.type);
      if (t.kind === 'generic' && (t.name === 'System.Action' || t.name === 'System.Func' || t.name === 'Action' || t.name === 'Func')) {
        const arg = t.args[0];
        if (arg.kind === 'name') callbackArgNames.add(arg.name);
      }
    }
  }
  const callbackClasses = serializable.filter(c => c.outer.length === 0 && callbackArgNames.has(c.name));
  const callbackEnums = model.enums.filter(e => callbackArgNames.has(e.name));

  const enumCodecs = model.enums.filter(e => e.outer.length === 0).map(e => generateEnumCodec(e, types));

  const codecClass = [
    `    /// <summary>생성된 타입별 JSON 읽기 코드. 쓰기는 각 타입의 IAITJsonWritable 구현에 있다.</summary>`,
    `    internal static partial class AITJsonCodec`,
    `    {`,
    generateBoxedReader('ReadBoxed', serializable, model.enums.filter(e => e.outer.length === 0), true),
    ``,
    generateBoxedReader('ReadCallbackArg', callbackClasses, callbackEnums, false),
    ``,
    ...enumCodecs.flatMap(e => [e, '']),
    ...classCodecs.flatMap(cc => [cc.reader, '']),
  ];
  codecClass.pop();
  codecClass.push(`    }`);

  const codecFile =
    CODEC_FILE_HEADER +
    codecClass.join('\n') +
    '\n\n' +
    classCodecs.map(cc => cc.writer).join('\n\n') +
    '\n}\n';

  const partialNames = new Set(serializable.map(c => c.name));
  const out = new Map<string, string>();
  for (const [name, code] of expanded) {
    const stripped = stripJsonMarkers(code, partialNames);
    if (stripped.includes('Newtonsoft')) {
      const line = stripped.split('\n').find(l => l.includes('Newtonsoft'));
      throw new Error(`json-codec: ${name}에 Newtonsoft 참조가 남았습니다: ${line?.trim()}`);
    }
    out.set(name, stripped);
  }
  return { files: out, codecFile, model };
}
