/**
 * JSON 코덱 생성 단계(json-codec.ts) 검증.
 * 생성기 출력 모양만 본다. 실제 읽기·쓰기 동작은 EditMode 테스트(SDKJsonCodecTests.cs)가 확인한다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  applyJsonCodec,
  parseCSharpDeclarations,
  parseTypeExpr,
  CodecTypes,
  expandCodecMacros,
  stripJsonMarkers,
} from '../../src/generators/csharp/json-codec.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

const SAMPLE = `namespace AppsInToss
{
    public enum Mode
    {
        [EnumMember(Value = "on-air")]
        OnAir,
        [EnumMember(Value = "off")]
        Off
    }

    public enum Level
    {
        Low = 1,
        High = 3
    }

    [Serializable]
    [Preserve]
    public class Inner
    {
        [Preserve]
        [JsonProperty("id")]
        public string Id;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class Outer
    {
        [Preserve]
        [JsonProperty("count")]
        public double Count;
        [Preserve]
        [JsonProperty("maybe")]
        public double? Maybe; // optional
        [Preserve]
        [JsonProperty("mode")]
        public Mode? Mode;
        [Preserve]
        [JsonProperty("level")]
        public Level Level;
        [Preserve]
        [JsonProperty("items")]
        public Inner[] Items;
        [Preserve]
        [JsonProperty("meta")]
        public Dictionary<string, object> Meta;
        [Preserve]
        [JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("Use AmountTyped")]
        public double Amount;
        [JsonIgnore]
        public bool AmountSpecified { get; set; }
        [JsonIgnore]
        public double? AmountTyped
        {
            get => AmountSpecified ? Amount : (double?)null;
            set { Amount = value ?? 0; AmountSpecified = value.HasValue; }
        }
        [JsonIgnore]
        public System.Action<Inner> OnInner;
        public bool IsBig => Count > 10;

        public void Helper(
            int x
        )
        {
        }

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
`;

describe('C# 선언 읽기', () => {
  const model = parseCSharpDeclarations(SAMPLE);

  test('클래스·enum과 멤버의 와이어 정보를 읽는다', () => {
    expect(model.enums.map(e => e.name)).toEqual(['Mode', 'Level']);
    expect(model.enums[0].members).toEqual([
      { name: 'OnAir', wire: 'on-air', value: 0 },
      { name: 'Off', wire: 'off', value: 1 },
    ]);
    expect(model.enums[1].members.map(m => m.value)).toEqual([1, 3]);

    const outer = model.classes.find(c => c.name === 'Outer')!;
    expect(outer.serializable).toBe(true);
    expect(outer.hasExtensionData).toBe(true);
    const byName = Object.fromEntries(outer.members.map(m => [m.name, m]));
    expect(byName.Count).toMatchObject({ wire: 'count', type: 'double', canRead: true, canWrite: true, ignored: false });
    expect(byName.Amount).toMatchObject({ wire: 'amount', nullIgnore: true });
    expect(byName.AmountSpecified).toMatchObject({ ignored: true, isProperty: true });
    expect(byName.OnInner).toMatchObject({ ignored: true });
    expect(byName.IsBig).toMatchObject({ isProperty: true, canRead: false, canWrite: true, wire: 'IsBig' });
    expect(byName.Helper).toBeUndefined();
  });

  test('[JsonIgnore] 없는 델리게이트 필드나 본문 프로퍼티는 거부한다', () => {
    expect(() => parseCSharpDeclarations(`    [Serializable]\n    public class A\n    {\n        public System.Action<int> OnX;\n    }\n`))
      .toThrow(/델리게이트/);
    expect(() => parseCSharpDeclarations(`    public class A\n    {\n        public string X\n        {\n            get => "";\n        }\n    }\n`))
      .toThrow(/본문이 있는 프로퍼티/);
  });
});

describe('코덱 생성', () => {
  const result = applyJsonCodec(new Map([['AIT.Types.Sample.cs', SAMPLE]]));
  const codec = result.codecFile;

  test('읽기: 이름 표, 읽을 수 없는 값은 대입하지 않고, 모르는 키는 _extensionData로', () => {
    expect(codec).toContain('private static readonly string[] N_Outer = { "count", "maybe", "mode", "level", "items", "meta", "amount", "IsBig" };');
    expect(codec).toContain('case 0: if (AITJson.TryDbl(v, out double x0)) { o.Count = x0; } break;');
    expect(codec).toContain('case 1: if (AITJson.TryDblN(v, out double? x1)) { o.Maybe = x1; } break;');
    expect(codec).toContain('case 2: if (AITJsonCodec.TEN_Mode(v, out Mode? x2)) { o.Mode = x2; } break;');
    expect(codec).toContain('case 3: if (AITJsonCodec.TE_Level(v, out Level x3)) { o.Level = x3; } break;');
    expect(codec).toContain('case 4: if (AITJson.TryArr<Inner>(v, (JsonValue j1, out Inner y1) => AITJson.TryObj<Inner>(j1, AITJsonCodec.R_Inner, out y1), out Inner[] x4)) { o.Items = x4; } break;');
    expect(codec).toContain('case 5: if (AITJson.TryDict<string, object>(v, null, AITJson.TryAny, out Dictionary<string, object> x5)) { o.Meta = x5; } break;');
    // NullValueHandling.Ignore: JSON null은 읽지 않는다. Specified는 대입할 때만 true
    expect(codec).toContain('case 6: if (!AITJson.IsNull(v) && AITJson.TryDbl(v, out double x6)) { o.Amount = x6; o.AmountSpecified = true; } break;');
    // getter만 있는 bool 멤버의 키는 bool로 바꿔 _extensionData에 담는다
    expect(codec).toContain('case 7: if (AITJson.TryBoolToken(v, out var x7)) AITJson.Ext(ref o._extensionData, p.Key, x7); break;');
    expect(codec).toContain('default: AITJson.Ext(ref o._extensionData, p); break;');
  });

  test('쓰기: 필드 다음 프로퍼티, Specified 조건, 마지막에 _extensionData', () => {
    const start = codec.indexOf('partial class Outer : IAITJsonWritable');
    const body = codec.slice(start, codec.indexOf('\n    }', start));
    const order = ['"count"', '"maybe"', '"mode"', '"level"', '"items"', '"meta"', '"amount"', '"IsBig"', 'AITJson.Ext(w'];
    const positions = order.map(s => body.indexOf(s));
    expect(positions.every(p => p >= 0)).toBe(true);
    expect([...positions].sort((a, b) => a - b)).toEqual(positions);
    expect(body).toContain('if (this.AmountSpecified) { w.Name("amount"); w.Value(this.Amount); }');
    expect(body).toContain('w.Name("mode"); AITJsonCodec.WE_Mode(w, this.Mode);');
    expect(body).not.toContain('OnInner');
    expect(body).not.toContain('AmountTyped');
  });

  test('enum: 문자열 enum은 와이어 표, 숫자 enum은 값 표와 숫자 쓰기', () => {
    expect(codec).toContain('private static readonly string[] EW_Mode = { "on-air", "off" };');
    expect(codec).toContain('AITJson.Enum(w, (int)v.Value, EW_Mode, null)');
    expect(codec).toContain('private static readonly int[] EV_Level = { 1, 3 };');
    expect(codec).toContain('AITJson.Enum(w, (int)v.Value, null, EV_Level)');
  });

  test('콜백 인자 타입(Action<T>/Func<T,..>)만 ReadCallbackArg에 들어간다', () => {
    const start = codec.indexOf('internal static object ReadCallbackArg');
    const body = codec.slice(start, codec.indexOf('throw new NotSupportedException', start));
    expect(body).toContain('typeof(Inner)');
    expect(body).not.toContain('typeof(Outer)');
  });

  test('출력 파일에서 표시가 지워지고 클래스는 partial이 된다', () => {
    const out = result.files.get('AIT.Types.Sample.cs')!;
    expect(out).not.toMatch(/JsonProperty|JsonIgnore|Newtonsoft/);
    expect(out).toContain('public partial class Outer');
    expect(out).toContain('public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;');
  });
});

describe('매크로와 타입 식', () => {
  const types = new CodecTypes(parseCSharpDeclarations(SAMPLE));

  test('Deserialize<T> 매크로를 타입별 읽기 식으로 바꾼다', () => {
    expect(expandCodecMacros('var a = AITJsonCodec.Deserialize<Outer>(json);', types))
      .toBe('var a = AITJsonCodec.R_Outer(AITJson.Parse(json));');
    expect(expandCodecMacros('x(AITJsonCodec.Deserialize<Dictionary<Mode, string>>(s.Trim()));', types))
      .toBe('x(AITJson.Dict<Mode, string>(AITJson.Parse(s.Trim()), AITJsonCodec.KE_Mode, j1 => AITJson.Str(j1)));');
    expect(expandCodecMacros('AITJsonCodec.Deserialize<Mode?>(t)', types)).toBe('AITJsonCodec.RE_Mode(AITJson.Parse(t))');
    expect(expandCodecMacros('AITJsonCodec.Deserialize<Inner[]>(t)', types))
      .toBe('AITJson.Arr<Inner>(AITJson.Parse(t), j1 => AITJsonCodec.R_Inner(j1))');
  });

  test('모르는 타입은 생성 단계에서 실패한다', () => {
    expect(() => types.read(parseTypeExpr('UnknownThing'), 'v')).toThrow(/읽기 코드/);
    expect(() => types.write(parseTypeExpr('float?'), 'w', 'v')).toThrow(/쓰기 코드/);
  });

  test('stripJsonMarkers는 Newtonsoft using도 지운다', () => {
    expect(stripJsonMarkers('using Newtonsoft.Json;\nusing System;\n', new Set())).toBe('using System;\n');
  });
});

describe('생성된 Runtime/SDK', () => {
  const files = fs.readdirSync(SDK_DIR).filter(f => f.endsWith('.cs'));

  test('어떤 C# 파일도 Newtonsoft를 참조하지 않는다', () => {
    const offenders = files.filter(f => fs.readFileSync(path.join(SDK_DIR, f), 'utf-8').includes('Newtonsoft'));
    expect(offenders).toEqual([]);
  });

  test('AITJson.cs와 AITJsonCodec.cs가 있고 .meta가 있다', () => {
    for (const f of ['AITJson.cs', 'AITJsonCodec.cs']) {
      expect(files).toContain(f);
      expect(fs.existsSync(path.join(SDK_DIR, `${f}.meta`))).toBe(true);
    }
  });

  test('콜백 결과 타입 읽기는 매크로가 풀려 있다', () => {
    const core = fs.readFileSync(path.join(SDK_DIR, 'AITCore.cs'), 'utf-8');
    expect(core).not.toContain('AITJsonCodec.Deserialize<');
    expect(core).toContain('AITJsonCodec.R_APIResponse(AITJson.Parse(resultJson))');
  });
});
