using System.Collections.Generic;

namespace AppsInToss
{
    /// <summary>
    /// 간단한 JSON 파서/직렬화 유틸리티
    /// package.json의 dependencies 머지에 사용
    /// </summary>
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            int index = 0;
            return ParseValue(json, ref index);
        }

        public static string Serialize(object obj)
        {
            var sb = new System.Text.StringBuilder();
            SerializeValue(obj, sb, 0);
            return sb.ToString();
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            if (char.IsDigit(c) || c == '-') return ParseNumber(json, ref index);

            return null;
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var obj = new Dictionary<string, object>();
            index++; // skip '{'
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != '}')
            {
                SkipWhitespace(json, ref index);
                if (json[index] == '}') break;

                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                index++; // skip ':'
                SkipWhitespace(json, ref index);
                object value = ParseValue(json, ref index);
                obj[key] = value;

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }

            if (index < json.Length) index++; // skip '}'
            return obj;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var arr = new List<object>();
            index++; // skip '['
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != ']')
            {
                arr.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
                SkipWhitespace(json, ref index);
            }

            if (index < json.Length) index++; // skip ']'
            return arr;
        }

        private static string ParseString(string json, ref int index)
        {
            index++; // skip opening '"'
            var sb = new System.Text.StringBuilder();

            while (index < json.Length && json[index] != '"')
            {
                if (json[index] == '\\' && index + 1 < json.Length)
                {
                    index++;
                    char escaped = json[index];
                    if (escaped == 'n') sb.Append('\n');
                    else if (escaped == 't') sb.Append('\t');
                    else if (escaped == 'r') sb.Append('\r');
                    else sb.Append(escaped);
                }
                else
                {
                    sb.Append(json[index]);
                }
                index++;
            }

            if (index < json.Length) index++; // skip closing '"'
            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == '-' || json[index] == 'e' || json[index] == 'E' || json[index] == '+'))
            {
                index++;
            }
            string numStr = json.Substring(start, index - start);
            if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
            {
                double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d);
                return d;
            }
            long.TryParse(numStr, out long l);
            return l;
        }

        private static bool ParseBool(string json, ref int index)
        {
            if (json.Substring(index, 4) == "true") { index += 4; return true; }
            if (json.Substring(index, 5) == "false") { index += 5; return false; }
            return false;
        }

        private static object ParseNull(string json, ref int index)
        {
            index += 4; // "null"
            return null;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private static void SerializeValue(object value, System.Text.StringBuilder sb, int indent)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string str)
            {
                sb.Append('"');
                sb.Append(str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t"));
                sb.Append('"');
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is Dictionary<string, object> dict)
            {
                sb.AppendLine("{");
                int i = 0;
                foreach (var kvp in dict)
                {
                    sb.Append(new string(' ', (indent + 1) * 2));
                    sb.Append('"');
                    sb.Append(kvp.Key);
                    sb.Append("\": ");
                    SerializeValue(kvp.Value, sb, indent + 1);
                    if (i < dict.Count - 1) sb.Append(',');
                    sb.AppendLine();
                    i++;
                }
                sb.Append(new string(' ', indent * 2));
                sb.Append('}');
            }
            else if (value is List<object> list)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    SerializeValue(list[i], sb, indent);
                    if (i < list.Count - 1) sb.Append(", ");
                }
                sb.Append(']');
            }
            else if (value is long || value is int)
            {
                sb.Append(value.ToString());
            }
            else if (value is double || value is float)
            {
                sb.Append(((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append('"');
                sb.Append(value.ToString());
                sb.Append('"');
            }
        }
    }
}
