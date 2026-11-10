// -----------------------------------------------------------------------
// <copyright file="AITJson.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - JSON runtime
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary><see cref="JsonValue"/>가 담은 JSON 값의 종류.</summary>
    public enum JsonValueKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    /// <summary>
    /// SDK가 쓰는 작은 JSON 값. SDK 타입에 없는 필드(<c>_extensionData</c>)와
    /// 콜백 묶음 이벤트의 인자(<see cref="AITCallbackBagEvent.Payload"/>)를 담는다.
    /// 숫자는 받은 텍스트를 그대로 들고 있어 정밀도를 잃지 않는다.
    /// </summary>
    [Preserve]
    public sealed class JsonValue
    {
        /// <summary>JSON <c>null</c>.</summary>
        public static readonly JsonValue Null = new JsonValue(JsonValueKind.Null, null);
        /// <summary>JSON <c>true</c>.</summary>
        public static readonly JsonValue True = new JsonValue(JsonValueKind.Bool, "true");
        /// <summary>JSON <c>false</c>.</summary>
        public static readonly JsonValue False = new JsonValue(JsonValueKind.Bool, "false");

        private static readonly JsonValue[] NoItems = new JsonValue[0];
        private static readonly KeyValuePair<string, JsonValue>[] NoMembers = new KeyValuePair<string, JsonValue>[0];

        /// <summary>값의 종류.</summary>
        public JsonValueKind Kind { get; }

        // String: 문자열 값, Number: 숫자 원문, Bool: "true"/"false"
        internal readonly string _text;
        internal readonly List<JsonValue> _items;
        internal readonly List<KeyValuePair<string, JsonValue>> _members;

        private JsonValue(JsonValueKind kind, string text)
        {
            Kind = kind;
            _text = text;
        }

        internal JsonValue(List<JsonValue> items)
        {
            Kind = JsonValueKind.Array;
            _items = items;
        }

        internal JsonValue(List<KeyValuePair<string, JsonValue>> members)
        {
            Kind = JsonValueKind.Object;
            _members = members;
        }

        /// <summary>JSON <c>null</c>이면 true.</summary>
        public bool IsNull => Kind == JsonValueKind.Null;

        /// <summary>배열의 원소 수 또는 객체의 키 수. 그 밖에는 0.</summary>
        public int Count => _items != null ? _items.Count : _members != null ? _members.Count : 0;

        /// <summary>배열 원소. 배열이 아니면 빈 목록.</summary>
        public IReadOnlyList<JsonValue> Items => (IReadOnlyList<JsonValue>)_items ?? NoItems;

        /// <summary>객체의 키와 값(받은 순서). 객체가 아니면 빈 목록.</summary>
        public IReadOnlyList<KeyValuePair<string, JsonValue>> Properties =>
            (IReadOnlyList<KeyValuePair<string, JsonValue>>)_members ?? NoMembers;

        /// <summary>객체의 키 값. 객체가 아니거나 키가 없으면 null(C# null).</summary>
        public JsonValue this[string key]
        {
            get
            {
                TryGetValue(key, out var value);
                return value;
            }
        }

        /// <summary>배열 원소. 배열이 아니거나 범위 밖이면 null(C# null).</summary>
        public JsonValue this[int index] =>
            _items != null && index >= 0 && index < _items.Count ? _items[index] : null;

        /// <summary>객체에서 키를 찾는다. 같은 키가 여러 번 오면 마지막 값.</summary>
        public bool TryGetValue(string key, out JsonValue value)
        {
            if (_members != null)
            {
                for (int i = _members.Count - 1; i >= 0; i--)
                {
                    if (_members[i].Key == key)
                    {
                        value = _members[i].Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        /// <summary>문자열은 그 값, 숫자·불리언은 JSON 텍스트, null은 null, 배열·객체는 JSON.</summary>
        public string AsString()
        {
            switch (Kind)
            {
                case JsonValueKind.Null: return null;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.Bool: return _text;
                default: return ToString();
            }
        }

        /// <summary>숫자(또는 숫자 문자열)를 double로. 바꿀 수 없으면 0.</summary>
        public double AsDouble() => AITJson.Dbl(this);

        /// <summary>숫자(또는 숫자 문자열)를 long으로. 바꿀 수 없으면 0.</summary>
        public long AsLong() => AITJson.Long(this);

        /// <summary>불리언(또는 "true"/"false" 문자열)을 bool로. 바꿀 수 없으면 false.</summary>
        public bool AsBool() => AITJson.Bool(this);

        /// <summary>SDK 타입(또는 string·double·bool·long·int·object·enum)으로 읽는다.</summary>
        public T ToObject<T>() => AITJsonSettings.FromJsonValue<T>(this);

        /// <summary>JSON 텍스트(공백 없음).</summary>
        public override string ToString()
        {
            var w = new AITJsonWriter();
            AITJson.Val(w, this);
            return w.ToString();
        }

        public static JsonValue FromString(string value) => value == null ? Null : new JsonValue(JsonValueKind.String, value);
        public static JsonValue FromBool(bool value) => value ? True : False;
        public static JsonValue FromNumber(long value) => new JsonValue(JsonValueKind.Number, value.ToString(CultureInfo.InvariantCulture));
        public static JsonValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return FromString(AITJson.NonFinite(value));
            return new JsonValue(JsonValueKind.Number, AITJson.FormatDouble(value));
        }

        /// <summary>배열 값을 만든다.</summary>
        public static JsonValue CreateArray(IEnumerable<JsonValue> items)
        {
            var list = new List<JsonValue>();
            if (items != null) foreach (var item in items) list.Add(item ?? Null);
            return new JsonValue(list);
        }

        /// <summary>객체 값을 만든다(키 순서 유지).</summary>
        public static JsonValue CreateObject(IEnumerable<KeyValuePair<string, JsonValue>> members)
        {
            var list = new List<KeyValuePair<string, JsonValue>>();
            if (members != null)
                foreach (var m in members) list.Add(new KeyValuePair<string, JsonValue>(m.Key, m.Value ?? Null));
            return new JsonValue(list);
        }

        public static implicit operator JsonValue(string value) => FromString(value);
        public static implicit operator JsonValue(bool value) => FromBool(value);
        public static implicit operator JsonValue(long value) => FromNumber(value);
        public static implicit operator JsonValue(int value) => FromNumber(value);
        public static implicit operator JsonValue(double value) => FromNumber(value);

        internal static JsonValue Number(string raw) => new JsonValue(JsonValueKind.Number, raw);

        /// <summary>JSON 텍스트를 읽는다. 형식이 틀리면 <see cref="FormatException"/>.</summary>
        public static JsonValue Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWs();
            var v = p.ReadValue(0);
            p.SkipWs();
            if (p.Pos != json.Length) throw p.Error("Additional text after JSON value");
            return v;
        }

        private struct Parser
        {
            private const int MaxDepth = 64;
            private readonly string _s;
            public int Pos;

            public Parser(string s)
            {
                _s = s;
                Pos = 0;
            }

            public FormatException Error(string message) =>
                new FormatException($"JSON: {message} (position {Pos})");

            public void SkipWs()
            {
                while (Pos < _s.Length)
                {
                    char c = _s[Pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') Pos++;
                    else break;
                }
            }

            public JsonValue ReadValue(int depth)
            {
                if (Pos >= _s.Length) throw Error("Unexpected end");
                char c = _s[Pos];
                switch (c)
                {
                    case '{': return ReadObject(depth + 1);
                    case '[': return ReadArray(depth + 1);
                    case '"': return FromString(ReadString());
                    case 't': Expect("true"); return True;
                    case 'f': Expect("false"); return False;
                    case 'n': Expect("null"); return Null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, Pos, word, 0, word.Length) != 0) throw Error($"Expected '{word}'");
                Pos += word.Length;
            }

            private JsonValue ReadObject(int depth)
            {
                if (depth > MaxDepth) throw Error("Too deep");
                Pos++;
                var members = new List<KeyValuePair<string, JsonValue>>();
                SkipWs();
                if (Pos < _s.Length && _s[Pos] == '}')
                {
                    Pos++;
                    return new JsonValue(members);
                }
                while (true)
                {
                    SkipWs();
                    if (Pos >= _s.Length || _s[Pos] != '"') throw Error("Expected property name");
                    string key = ReadString();
                    SkipWs();
                    if (Pos >= _s.Length || _s[Pos] != ':') throw Error("Expected ':'");
                    Pos++;
                    SkipWs();
                    members.Add(new KeyValuePair<string, JsonValue>(key, ReadValue(depth)));
                    SkipWs();
                    if (Pos >= _s.Length) throw Error("Unexpected end");
                    char c = _s[Pos++];
                    if (c == '}') return new JsonValue(members);
                    if (c != ',') throw Error("Expected ',' or '}'");
                }
            }

            private JsonValue ReadArray(int depth)
            {
                if (depth > MaxDepth) throw Error("Too deep");
                Pos++;
                var items = new List<JsonValue>();
                SkipWs();
                if (Pos < _s.Length && _s[Pos] == ']')
                {
                    Pos++;
                    return new JsonValue(items);
                }
                while (true)
                {
                    SkipWs();
                    items.Add(ReadValue(depth));
                    SkipWs();
                    if (Pos >= _s.Length) throw Error("Unexpected end");
                    char c = _s[Pos++];
                    if (c == ']') return new JsonValue(items);
                    if (c != ',') throw Error("Expected ',' or ']'");
                }
            }

            private JsonValue ReadNumber()
            {
                int start = Pos;
                if (_s[Pos] == '-') Pos++;
                int digits = Pos;
                while (Pos < _s.Length && _s[Pos] >= '0' && _s[Pos] <= '9') Pos++;
                if (Pos == digits) throw Error("Invalid number");
                if (Pos < _s.Length && _s[Pos] == '.')
                {
                    Pos++;
                    int frac = Pos;
                    while (Pos < _s.Length && _s[Pos] >= '0' && _s[Pos] <= '9') Pos++;
                    if (Pos == frac) throw Error("Invalid number");
                }
                if (Pos < _s.Length && (_s[Pos] == 'e' || _s[Pos] == 'E'))
                {
                    Pos++;
                    if (Pos < _s.Length && (_s[Pos] == '+' || _s[Pos] == '-')) Pos++;
                    int exp = Pos;
                    while (Pos < _s.Length && _s[Pos] >= '0' && _s[Pos] <= '9') Pos++;
                    if (Pos == exp) throw Error("Invalid number");
                }
                return Number(_s.Substring(start, Pos - start));
            }

            private string ReadString()
            {
                Pos++; // opening quote
                int start = Pos;
                // 이스케이프가 없는 흔한 경우는 Substring 한 번으로 끝낸다.
                while (Pos < _s.Length)
                {
                    char c = _s[Pos];
                    if (c == '"')
                    {
                        string plain = _s.Substring(start, Pos - start);
                        Pos++;
                        return plain;
                    }
                    if (c == '\\') break;
                    Pos++;
                }
                var sb = new StringBuilder(_s, start, Pos - start, Pos - start + 16);
                while (Pos < _s.Length)
                {
                    char c = _s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (Pos >= _s.Length) break;
                    char e = _s[Pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (Pos + 4 > _s.Length) throw Error("Invalid unicode escape");
                            int code = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                char h = _s[Pos++];
                                int d = h >= '0' && h <= '9' ? h - '0'
                                    : h >= 'a' && h <= 'f' ? h - 'a' + 10
                                    : h >= 'A' && h <= 'F' ? h - 'A' + 10
                                    : -1;
                                if (d < 0) throw Error("Invalid unicode escape");
                                code = (code << 4) | d;
                            }
                            sb.Append((char)code);
                            break;
                        default:
                            throw Error($"Invalid escape '\\{e}'");
                    }
                }
                throw Error("Unterminated string");
            }
        }
    }

    /// <summary>생성된 SDK 타입이 자기 자신을 JSON으로 쓰는 통로 (리플렉션 없이).</summary>
    internal interface IAITJsonWritable
    {
        void WriteJson(AITJsonWriter w);
    }

    /// <summary>JSON 텍스트를 차례로 쓰는 작성기. 쉼표는 알아서 넣는다.</summary>
    internal sealed class AITJsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder(128);
        private bool _comma;
        internal int Depth;

        private void Sep()
        {
            if (_comma) _sb.Append(',');
            _comma = true;
        }

        public void BeginObject()
        {
            Sep();
            _sb.Append('{');
            _comma = false;
            if (++Depth > 64) throw new InvalidOperationException("JSON: object graph is too deep (self-referencing loop?)");
        }

        public void EndObject()
        {
            _sb.Append('}');
            _comma = true;
            Depth--;
        }

        public void BeginArray()
        {
            Sep();
            _sb.Append('[');
            _comma = false;
            if (++Depth > 64) throw new InvalidOperationException("JSON: object graph is too deep (self-referencing loop?)");
        }

        public void EndArray()
        {
            _sb.Append(']');
            _comma = true;
            Depth--;
        }

        public void Name(string name)
        {
            Sep();
            AITJson.Quote(_sb, name);
            _sb.Append(':');
            _comma = false;
        }

        public void Null()
        {
            Sep();
            _sb.Append("null");
        }

        /// <summary>이미 JSON인 조각(숫자 원문 등)을 그대로 쓴다.</summary>
        public void Raw(string json)
        {
            Sep();
            _sb.Append(json);
        }

        public void Value(string value)
        {
            if (value == null)
            {
                Null();
                return;
            }
            Sep();
            AITJson.Quote(_sb, value);
        }

        public void Value(bool value) => Raw(value ? "true" : "false");

        public void Value(bool? value)
        {
            if (value.HasValue) Value(value.Value);
            else Null();
        }

        public void Value(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) Value(AITJson.NonFinite(value));
            else Raw(AITJson.FormatDouble(value));
        }

        public void Value(double? value)
        {
            if (value.HasValue) Value(value.Value);
            else Null();
        }

        public void Value(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) Value(AITJson.NonFinite(value));
            else Raw(AITJson.EnsureDecimalPlace(value.ToString("R", CultureInfo.InvariantCulture)));
        }

        public void Value(long value) => Raw(value.ToString(CultureInfo.InvariantCulture));

        public void Value(long? value)
        {
            if (value.HasValue) Value(value.Value);
            else Null();
        }

        public void Value(int value) => Raw(value.ToString(CultureInfo.InvariantCulture));

        public void Value(int? value)
        {
            if (value.HasValue) Value(value.Value);
            else Null();
        }

        public override string ToString() => _sb.ToString();
    }

    /// <summary>생성된 직렬화 코드가 함께 쓰는 읽기·쓰기 도우미.</summary>
    internal static class AITJson
    {
        internal delegate bool KeyParser<TKey>(string text, out TKey key);

        // ---------------------------------------------------------------
        // 텍스트
        // ---------------------------------------------------------------

        /// <summary>null·빈 문자열·공백이면 null(값 없음), 아니면 파싱한다.</summary>
        internal static JsonValue Parse(string json)
        {
            if (json == null) return null;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r') return JsonValue.Parse(json);
            }
            return null;
        }

        internal static string FormatDouble(double value) =>
            EnsureDecimalPlace(value.ToString("R", CultureInfo.InvariantCulture));

        internal static string EnsureDecimalPlace(string text)
        {
            if (text.IndexOf('.') >= 0 || text.IndexOf('E') >= 0 || text.IndexOf('e') >= 0) return text;
            return text + ".0";
        }

        internal static string NonFinite(double value) =>
            double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";

        internal static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                string esc;
                switch (c)
                {
                    case '"': esc = "\\\""; break;
                    case '\\': esc = "\\\\"; break;
                    case '\n': esc = "\\n"; break;
                    case '\r': esc = "\\r"; break;
                    case '\t': esc = "\\t"; break;
                    case '\b': esc = "\\b"; break;
                    case '\f': esc = "\\f"; break;
                    case '\u0085': esc = "\\u0085"; break;
                    case (char)0x2028: esc = "\\u2028"; break;
                    case (char)0x2029: esc = "\\u2029"; break;
                    default:
                        if (c < ' ') esc = "\\u00" + "0123456789abcdef"[c >> 4] + "0123456789abcdef"[c & 0xF];
                        else esc = null;
                        break;
                }
                if (esc == null)
                {
                    run++;
                    continue;
                }
                if (run > 0) sb.Append(s, i - run, run);
                run = 0;
                sb.Append(esc);
            }
            if (run > 0) sb.Append(s, s.Length - run, run);
            sb.Append('"');
        }

        // ---------------------------------------------------------------
        // 읽기 (null(C#)은 키가 없던 것, JsonValue.Null은 JSON null)
        // ---------------------------------------------------------------

        internal static bool IsNull(JsonValue v) => v == null || v.Kind == JsonValueKind.Null;

        /// <summary>숫자·불리언·enum 자리에서 값 없음으로 보는 값: 키 없음, JSON null, 빈 문자열.</summary>
        internal static bool IsNullOrEmpty(JsonValue v) => IsNull(v) || (v.Kind == JsonValueKind.String && v._text.Length == 0);

        internal static string Str(JsonValue v)
        {
            if (v == null) return null;
            return v.AsString();
        }

        private static bool TryNumber(JsonValue v, out double result)
        {
            if (v != null && (v.Kind == JsonValueKind.Number || v.Kind == JsonValueKind.String))
                return double.TryParse(v._text, v.Kind == JsonValueKind.String ? NumberStyles.Float | NumberStyles.AllowThousands : NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            if (v != null && v.Kind == JsonValueKind.Bool)
            {
                result = v == JsonValue.True ? 1 : 0;
                return true;
            }
            result = 0;
            return false;
        }

        internal static double Dbl(JsonValue v)
        {
            if (v != null && v.Kind == JsonValueKind.String)
            {
                if (v._text == "NaN") return double.NaN;
                if (v._text == "Infinity") return double.PositiveInfinity;
                if (v._text == "-Infinity") return double.NegativeInfinity;
            }
            TryNumber(v, out var d);
            return d;
        }

        internal static double? DblN(JsonValue v) => IsNullOrEmpty(v) ? (double?)null : Dbl(v);

        internal static long Long(JsonValue v)
        {
            if (v != null && (v.Kind == JsonValueKind.Number || v.Kind == JsonValueKind.String) &&
                long.TryParse(v._text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                return l;
            // 소수는 가장 가까운 정수로(반올림 중간값은 짝수 쪽), Convert.ToInt64(double)과 같다
            TryNumber(v, out var d);
            return (long)Math.Round(d);
        }

        internal static long? LongN(JsonValue v) => IsNullOrEmpty(v) ? (long?)null : Long(v);

        internal static int Int(JsonValue v) => (int)Long(v);

        internal static int? IntN(JsonValue v) => IsNullOrEmpty(v) ? (int?)null : Int(v);

        internal static bool Bool(JsonValue v)
        {
            if (v == null) return false;
            switch (v.Kind)
            {
                case JsonValueKind.Bool: return v == JsonValue.True;
                case JsonValueKind.String: return bool.TryParse(v._text, out var b) && b;
                case JsonValueKind.Number: return TryNumber(v, out var d) && d != 0;
                default: return false;
            }
        }

        internal static bool? BoolN(JsonValue v) => IsNullOrEmpty(v) ? (bool?)null : Bool(v);

        /// <summary><c>object</c> 필드: 문자열·불리언·long·double, long 범위를 넘는 정수와 배열·객체는 <see cref="JsonValue"/>.</summary>
        internal static object Any(JsonValue v)
        {
            if (v == null) return null;
            switch (v.Kind)
            {
                case JsonValueKind.Null: return null;
                case JsonValueKind.Bool: return v == JsonValue.True;
                case JsonValueKind.String: return v._text;
                case JsonValueKind.Number:
                    if (v._text.IndexOf('.') < 0 && v._text.IndexOf('e') < 0 && v._text.IndexOf('E') < 0 &&
                        long.TryParse(v._text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        return l;
                    // long 범위를 넘는 정수는 원문을 그대로 들고 있는 JsonValue로 둔다(쓸 때 같은 텍스트로 나간다).
                    if (IsIntegerText(v._text)) return v;
                    return Dbl(v);
                default: return v;
            }
        }

        private static bool IsIntegerText(string s)
        {
            int i = s.Length > 0 && s[0] == '-' ? 1 : 0;
            if (i >= s.Length) return false;
            for (; i < s.Length; i++) if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        internal static byte[] Bytes(JsonValue v)
        {
            if (IsNull(v)) return null;
            if (v.Kind == JsonValueKind.String)
            {
                if (v._text.Length == 0) return new byte[0];
                try { return Convert.FromBase64String(v._text); }
                catch (FormatException) { return null; }
            }
            if (v.Kind != JsonValueKind.Array) return null;
            var bytes = new byte[v._items.Count];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)Long(v._items[i]);
            return bytes;
        }

        internal static T[] Arr<T>(JsonValue v, Func<JsonValue, T> read)
        {
            if (v == null || v.Kind != JsonValueKind.Array) return null;
            var result = new T[v._items.Count];
            for (int i = 0; i < result.Length; i++) result[i] = read(v._items[i]);
            return result;
        }

        internal static List<T> Lst<T>(JsonValue v, Func<JsonValue, T> read)
        {
            if (v == null || v.Kind != JsonValueKind.Array) return null;
            var result = new List<T>(v._items.Count);
            for (int i = 0; i < v._items.Count; i++) result.Add(read(v._items[i]));
            return result;
        }

        /// <summary>키 파서가 null이면 문자열 키. 해석할 수 없는 키는 건너뛴다.</summary>
        internal static Dictionary<TKey, TValue> Dict<TKey, TValue>(JsonValue v, KeyParser<TKey> key, Func<JsonValue, TValue> read)
        {
            if (v == null || v.Kind != JsonValueKind.Object) return null;
            var result = new Dictionary<TKey, TValue>();
            foreach (var m in v._members)
            {
                TKey k;
                if (key == null) k = (TKey)(object)m.Key;
                else if (!key(m.Key, out k)) continue;
                result[k] = read(m.Value);
            }
            return result;
        }

        /// <summary>bool 자리로 읽은 값을 JSON 값으로: 빈 문자열·null은 null, 그 밖에는 true/false.</summary>
        internal static JsonValue BoolToken(JsonValue v) => IsNullOrEmpty(v) ? JsonValue.Null : JsonValue.FromBool(Bool(v));

        internal static void Ext(ref IDictionary<string, JsonValue> ext, string key, JsonValue value)
        {
            if (ext == null) ext = new Dictionary<string, JsonValue>();
            ext[key] = value;
        }

        /// <summary>타입에 없는 키를 <c>_extensionData</c>에 모은다.</summary>
        internal static void Ext(ref IDictionary<string, JsonValue> ext, KeyValuePair<string, JsonValue> member)
        {
            if (ext == null) ext = new Dictionary<string, JsonValue>();
            ext[member.Key] = member.Value;
        }

        /// <summary>
        /// 키에 맞는 멤버 번호. 대소문자까지 같은 이름을 먼저 찾고, 없으면 대소문자를 무시하고 찾는다. 없으면 -1.
        /// </summary>
        internal static int Match(string key, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(key, names[i], StringComparison.Ordinal)) return i;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(key, names[i], StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>
        /// enum 값을 읽는다. 와이어 이름(EnumMember) → 숫자 → C# 이름 순서로, 대소문자 일치를 먼저 본다.
        /// 정의에 없는 값이면 false.
        /// </summary>
        internal static bool Enum(JsonValue v, string[] wire, string[] names, int[] values, out int value)
        {
            value = 0;
            if (v == null) return false;
            if (v.Kind == JsonValueKind.Number)
            {
                if (!long.TryParse(v._text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return false;
                value = (int)n;
                return true;
            }
            if (v.Kind != JsonValueKind.String) return false;
            return EnumText(v._text, wire, names, values, out value);
        }

        internal static bool EnumText(string s, string[] wire, string[] names, int[] values, out int value)
        {
            value = 0;
            if (s == null) return false;
            int i = IndexOf(s, wire, StringComparison.Ordinal);
            if (i < 0)
            {
                string t = s.Trim();
                if (t.Length > 0 && (char.IsDigit(t[0]) || t[0] == '-' || t[0] == '+') &&
                    int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    value = n;
                    return true;
                }
                i = IndexOf(t, wire, StringComparison.Ordinal);
                if (i < 0) i = IndexOf(t, names, StringComparison.Ordinal);
                if (i < 0) i = IndexOf(t, wire, StringComparison.OrdinalIgnoreCase);
                if (i < 0) i = IndexOf(t, names, StringComparison.OrdinalIgnoreCase);
                if (i < 0) i = IndexOf(s, wire, StringComparison.OrdinalIgnoreCase);
                if (i < 0) return false;
            }
            value = values != null ? values[i] : i;
            return true;
        }

        private static int IndexOf(string s, string[] list, StringComparison comparison)
        {
            for (int i = 0; i < list.Length; i++)
                if (string.Equals(s, list[i], comparison)) return i;
            return -1;
        }

        // ---------------------------------------------------------------
        // 쓰기
        // ---------------------------------------------------------------

        internal static void Obj(AITJsonWriter w, IAITJsonWritable value)
        {
            if (value == null) w.Null();
            else value.WriteJson(w);
        }

        internal static void Arr<T>(AITJsonWriter w, IEnumerable<T> items, Action<AITJsonWriter, T> write)
        {
            if (items == null)
            {
                w.Null();
                return;
            }
            w.BeginArray();
            foreach (var item in items) write(w, item);
            w.EndArray();
        }

        /// <summary>키 변환이 null이면 키를 문자열로 바꾼다(invariant).</summary>
        internal static void Dict<TKey, TValue>(AITJsonWriter w, IEnumerable<KeyValuePair<TKey, TValue>> dict, Func<TKey, string> key, Action<AITJsonWriter, TValue> write)
        {
            if (dict == null)
            {
                w.Null();
                return;
            }
            w.BeginObject();
            foreach (var kv in dict)
            {
                w.Name(key != null ? key(kv.Key) : KeyText(kv.Key));
                write(w, kv.Value);
            }
            w.EndObject();
        }

        /// <summary><c>_extensionData</c>를 객체 끝에 이어 쓴다.</summary>
        internal static void Ext(AITJsonWriter w, IDictionary<string, JsonValue> ext)
        {
            if (ext == null) return;
            foreach (var kv in ext)
            {
                w.Name(kv.Key);
                Val(w, kv.Value);
            }
        }

        internal static void Val(AITJsonWriter w, JsonValue v)
        {
            if (v == null)
            {
                w.Null();
                return;
            }
            switch (v.Kind)
            {
                case JsonValueKind.Null: w.Null(); break;
                case JsonValueKind.Bool:
                case JsonValueKind.Number: w.Raw(v._text); break;
                case JsonValueKind.String: w.Value(v._text); break;
                case JsonValueKind.Array:
                    w.BeginArray();
                    foreach (var item in v._items) Val(w, item);
                    w.EndArray();
                    break;
                default:
                    w.BeginObject();
                    foreach (var m in v._members)
                    {
                        w.Name(m.Key);
                        Val(w, m.Value);
                    }
                    w.EndObject();
                    break;
            }
        }

        internal static void Bytes(AITJsonWriter w, byte[] bytes)
        {
            if (bytes == null) w.Null();
            else w.Value(Convert.ToBase64String(bytes));
        }

        internal static void Enum(AITJsonWriter w, int value, string[] wire, int[] values)
        {
            if (wire == null)
            {
                w.Value(value);
                return;
            }
            string text = EnumWire(value, wire, values);
            if (text != null) w.Value(text);
            else w.Value(value);
        }

        internal static string EnumWire(int value, string[] wire, int[] values)
        {
            if (values == null) return value >= 0 && value < wire.Length ? wire[value] : null;
            for (int i = 0; i < values.Length; i++)
                if (values[i] == value) return wire[i];
            return null;
        }

        private static string KeyText(object key)
        {
            switch (key)
            {
                case null: return "";
                case string s: return s;
                case System.Enum e: return EnumText(e) ?? Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return key.ToString();
            }
        }

        /// <summary>
        /// 타입을 미리 알 수 없는 값(<c>object</c> 필드, <see cref="AITJsonSettings.Serialize"/> 인자)을 쓴다.
        /// SDK 타입은 생성된 코드로, 그 밖의 객체는 공개 필드·프로퍼티를 리플렉션으로 쓴다.
        /// </summary>
        internal static void Any(AITJsonWriter w, object value)
        {
            switch (value)
            {
                case null: w.Null(); return;
                case string s: w.Value(s); return;
                case bool b: w.Value(b); return;
                case JsonValue jv: Val(w, jv); return;
                case IAITJsonWritable x: x.WriteJson(w); return;
                case double d: w.Value(d); return;
                case float f: w.Value(f); return;
                case int i: w.Value(i); return;
                case long l: w.Value(l); return;
                case decimal m: w.Raw(EnsureDecimalPlace(m.ToString(CultureInfo.InvariantCulture))); return;
                case short or byte or sbyte or ushort or uint or ulong:
                    w.Raw(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                    return;
                case char c: w.Value(c.ToString()); return;
                case System.Enum e:
                    {
                        string text = EnumText(e);
                        if (text != null) w.Value(text);
                        else w.Raw(Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                        return;
                    }
                case DateTime dt: w.Value(dt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture)); return;
                case DateTimeOffset dto: w.Value(dto.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture)); return;
                case Guid g: w.Value(g.ToString("D")); return;
                case TimeSpan ts: w.Value(ts.ToString()); return;
                case byte[] bytes: Bytes(w, bytes); return;
                case IDictionary dict:
                    w.BeginObject();
                    foreach (DictionaryEntry kv in dict)
                    {
                        w.Name(KeyText(kv.Key));
                        Any(w, kv.Value);
                    }
                    w.EndObject();
                    return;
                case IEnumerable seq:
                    w.BeginArray();
                    foreach (var item in seq) Any(w, item);
                    w.EndArray();
                    return;
                default:
                    Reflect(w, value);
                    return;
            }
        }

        private static readonly Dictionary<Type, Dictionary<long, string>> EnumWireCache = new Dictionary<Type, Dictionary<long, string>>();

        /// <summary>
        /// 멤버 중 하나라도 [EnumMember]가 있으면 문자열 enum: 그 값(없으면 C# 이름)을 돌려준다.
        /// 숫자 enum이거나 정의에 없는 값이면 null(숫자로 쓴다).
        /// </summary>
        private static string EnumText(System.Enum e)
        {
            var type = e.GetType();
            Dictionary<long, string> map;
            lock (EnumWireCache)
            {
                if (!EnumWireCache.TryGetValue(type, out map))
                {
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                    bool hasMember = false;
                    foreach (var f in fields)
                        if (f.GetCustomAttribute<EnumMemberAttribute>() != null) hasMember = true;
                    if (hasMember)
                    {
                        map = new Dictionary<long, string>();
                        foreach (var f in fields)
                        {
                            var attr = f.GetCustomAttribute<EnumMemberAttribute>();
                            long key = Convert.ToInt64(f.GetValue(null), CultureInfo.InvariantCulture);
                            if (!map.ContainsKey(key)) map[key] = attr != null && attr.Value != null ? attr.Value : f.Name;
                        }
                    }
                    EnumWireCache[type] = map;
                }
            }
            if (map == null) return null;
            return map.TryGetValue(Convert.ToInt64(e, CultureInfo.InvariantCulture), out var text) ? text : null;
        }

        /// <summary>
        /// SDK 밖의 객체(익명 타입, 게임의 클래스): 공개 인스턴스 필드 다음 공개 프로퍼티 순서로 쓴다.
        /// 멤버의 [JsonIgnore]·[JsonProperty("이름")] 속성은 그 어셈블리를 참조하지 않고 속성 이름으로 알아본다.
        /// </summary>
        private static void Reflect(AITJsonWriter w, object value)
        {
            var type = value.GetType();
            w.BeginObject();
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                string name = MemberName(f);
                if (name == null || typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                w.Name(name);
                Any(w, f.GetValue(value));
            }
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0 || p.GetGetMethod() == null) continue;
                string name = MemberName(p);
                if (name == null || typeof(Delegate).IsAssignableFrom(p.PropertyType)) continue;
                w.Name(name);
                Any(w, p.GetValue(value, null));
            }
            w.EndObject();
        }

        private static string MemberName(MemberInfo member)
        {
            string name = member.Name;
            foreach (var attr in member.GetCustomAttributes(true))
            {
                var attrType = attr.GetType();
                if (attrType.Name == "JsonIgnoreAttribute") return null;
                if (attrType.Name == "JsonPropertyAttribute")
                {
                    var prop = attrType.GetProperty("PropertyName");
                    if (prop != null && prop.GetValue(attr, null) is string renamed && renamed.Length > 0) name = renamed;
                }
            }
            return name;
        }
    }

    /// <summary>
    /// SDK 타입의 JSON 직렬화 진입점. 와이어 형식(필드 이름, enum 문자열, null 처리)은
    /// d.ts 이름 그대로, null도 null로 쓰고, enum은 문자열 값으로 쓴다.
    /// </summary>
    public static class AITJsonSettings
    {
        /// <summary>값을 JSON으로 쓴다. SDK 타입은 생성된 코드로 쓰고, 그 밖의 객체는 공개 필드·프로퍼티를 쓴다.</summary>
        public static string Serialize(object obj)
        {
            var w = new AITJsonWriter();
            AITJson.Any(w, obj);
            return w.ToString();
        }

        /// <summary>
        /// JSON을 SDK 타입(또는 string·double·bool·long·int·object·<see cref="JsonValue"/>·SDK enum, 그 배열)으로 읽는다.
        /// 빈 문자열이나 null이면 기본값.
        /// </summary>
        public static T Deserialize<T>(string json) => FromJsonValue<T>(AITJson.Parse(json));

        /// <summary><see cref="Deserialize{T}(string)"/>의 Type 인자 버전.</summary>
        public static object Deserialize(string json, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return AITJsonCodec.ReadBoxed(type, AITJson.Parse(json));
        }

        /// <summary>이미 읽은 <see cref="JsonValue"/>를 SDK 타입으로 바꾼다.</summary>
        public static T FromJsonValue<T>(JsonValue value)
        {
            object result = AITJsonCodec.ReadBoxed(typeof(T), value);
            return result is T typed ? typed : default(T);
        }
    }
}
