// -----------------------------------------------------------------------
// AITMemoryBudgetReport.cs - 빌드 시점 iOS 메모리 예산 추정 리포트 (P1-4)
//
// 저메모리 단말(iPhone 11, Toss 상주 약 1GB)에서 WebContent 가 jetsam 으로 죽는 것은 SDK 밖(게임 콘텐츠)의
// 소비자가 큰 경우가 많다. 개발자가 "무엇을 줄여야 하는지" 빌드 직후에 알 수 있도록, 산출물에서 바로 읽히는 값
// (wasm code 섹션, .data 해제 크기, 외부화된 오디오·폰트)으로 iOS WebContent footprint 를 추정한다.
//
// 계수 출처: iOS 26.5 Simulator 실측(Unity 6000.0 heavy, wasm code 32.6MB, data 30.7MB).
//   지속(steady) ≈ 47(빈 미니앱) + 7.4 × codeMB(컴파일 구조 210 + 인스턴스 31 = 241MB / 32.6)
//                  + dataRawMB(Gigacage 에 상주하는 .data 버퍼) + heapMB(wasm 선형 메모리 추정)
//                  + streamingHeldMB(외부화 오디오·폰트가 힙에 보유되는 바이트)
//                  + otherMB(JS 객체·글루·DOM 약 80 + JIT 약 15, 차감 추정)
//   피크(peak)   ≈ 지속 + 260 × (codeMB / 32.6)   — wasm 컴파일·인스턴스화 중 1초 안팎의 일시 상승
// 시뮬레이터 값이라 실기기(압축 메모리 포함 dirty footprint, H2)와 다를 수 있다 → 수치 대신 등급(여유/주의/초과)을
// 앞세우고, 예산은 계획 가정(피크 450MB / 지속 350MB)이다.
//
// 훅: IPostprocessBuildWithReport 구현이라 Unity 가 빌드 후 자동 호출한다(AITDataBreakdownReport 와 같은 방식).
// 순수 진단이다 — 아무것도 바꾸지 않고, 실패해도 경고만 남기며 빌드는 계속된다(예외를 밖으로 던지지 않음).
// symbols 파일(Build/*.symbols.json)이 있으면 wasm 함수 본문 크기를 심볼 이름으로 묶어 Top 20 을 보여 준다.
// 계산·파싱은 순수 함수로 분리해 합성 입력으로 EditMode 에서 검증한다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AppsInToss.Editor
{
    internal class AITMemoryBudgetReport : IPostprocessBuildWithReport
    {
        // AITDataBreakdownReport(10000) 뒤. 다른 후처리기와 순서 의존성은 없다.
        public int callbackOrder => 10010;

        // ─────────────────────────── 계수(B 실측) ───────────────────────────

        /// <summary>빈 미니앱 WebContent footprint(MB). 프로세스 이미지·런타임 포함.</summary>
        internal const double BaseMB = 47.0;

        /// <summary>wasm code 섹션 1MB 당 iOS 지속 footprint(MB). (컴파일 구조 210 + 인스턴스 31) / 32.6.</summary>
        internal const double SteadyMBPerCodeMB = 7.4;

        /// <summary>JS 객체·Unity/Emscripten 글루·DOM(약 80) + JIT(약 15). 직접 측정이 아니라 차감 추정이다.</summary>
        internal const double OtherMB = 95.0;

        /// <summary>wasm 선형 메모리 추정의 기본분(MB). 실측 heavy 힙 107~138MB 에서 data·스트리밍 몫을 뺀 값.</summary>
        internal const double HeapBaseMB = 64.0;

        /// <summary>지속 대비 피크 상승분(MB), 코드 크기 기준값(<see cref="PeakReferenceCodeMB"/>)일 때.</summary>
        internal const double PeakExtraMB = 260.0;

        /// <summary>피크 상승분 실측 시 wasm code 크기(MB).</summary>
        internal const double PeakReferenceCodeMB = 32.6;

        /// <summary>계획 가정 예산(MB): 지속 350, 피크 450. H1(실기기 jetsam 한계)로 확정되기 전의 값이다.</summary>
        internal const double SteadyBudgetMB = 350.0;
        internal const double PeakBudgetMB = 450.0;

        /// <summary>예산의 이 비율을 넘으면 "주의".</summary>
        internal const double CautionRatio = 0.85;

        internal const int TopGroupCount = 20;

        private const double MB = 1024.0 * 1024.0;
        private const int NodeTimeoutMs = 180000;

        internal enum Grade
        {
            /// <summary>예산 이내(여유).</summary>
            Comfortable = 0,

            /// <summary>예산의 85% 초과(주의).</summary>
            Caution = 1,

            /// <summary>예산 초과.</summary>
            Over = 2,
        }

        internal static string GradeLabel(Grade g)
        {
            switch (g)
            {
                case Grade.Comfortable: return "여유";
                case Grade.Caution: return "주의";
                default: return "초과";
            }
        }

        internal struct Inputs
        {
            /// <summary>wasm code 섹션 크기(MB).</summary>
            public double codeMB;

            /// <summary>.data 압축 해제 크기(MB).</summary>
            public double dataRawMB;

            /// <summary>외부화 오디오·폰트 파일 합계(MB) — 힙에 보유되는 바이트의 하한 추정.</summary>
            public double streamingHeldMB;
        }

        internal struct Estimate
        {
            public double codeMB;
            public double codeFootprintMB;
            public double dataRawMB;
            public double heapMB;
            public double streamingHeldMB;
            public double steadyMB;
            public double peakMB;
            public Grade grade;
        }

        // ─────────────────────────── 순수 계산(유닛 테스트 대상) ───────────────────────────

        /// <summary>
        /// 입력에서 iOS 지속·피크 추정과 등급을 계산한다. 음수 입력은 0 으로 취급한다.
        /// 힙 추정 = <see cref="HeapBaseMB"/> + dataRawMB(데이터가 클수록 초기화 중 힙 사용이 커진다).
        /// </summary>
        internal static Estimate Compute(Inputs input)
        {
            double code = Math.Max(0.0, input.codeMB);
            double data = Math.Max(0.0, input.dataRawMB);
            double streaming = Math.Max(0.0, input.streamingHeldMB);

            double codeFootprint = SteadyMBPerCodeMB * code;
            double heap = HeapBaseMB + data;
            double steady = BaseMB + codeFootprint + data + heap + streaming + OtherMB;
            double peak = steady + PeakExtraMB * (code / PeakReferenceCodeMB);

            return new Estimate
            {
                codeMB = code,
                codeFootprintMB = codeFootprint,
                dataRawMB = data,
                heapMB = heap,
                streamingHeldMB = streaming,
                steadyMB = steady,
                peakMB = peak,
                grade = GradeOf(steady, peak),
            };
        }

        /// <summary>지속·피크 중 하나라도 예산을 넘으면 초과, 예산의 85% 를 넘으면 주의, 아니면 여유.</summary>
        internal static Grade GradeOf(double steadyMB, double peakMB)
        {
            if (steadyMB > SteadyBudgetMB || peakMB > PeakBudgetMB) return Grade.Over;
            if (steadyMB > SteadyBudgetMB * CautionRatio || peakMB > PeakBudgetMB * CautionRatio) return Grade.Caution;
            return Grade.Comfortable;
        }

        /// <summary>
        /// 2021.3 이면 6000.x 전환 시 이득 안내를 돌려준다(그 외 null).
        /// 근거: wasm code 2021.3 44.7MB → 6000.0 32.6MB / 6000.3 27.1MB(코드 −12~17MB) × 7.4 ≈ iOS 지속 −90~125MB.
        /// </summary>
        internal static string LegacyEngineHint(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion) || !unityVersion.StartsWith("2021.3", StringComparison.Ordinal))
                return null;
            return "6000.x 로 전환하면 wasm 코드가 약 12~17MB 줄고 iOS 지속 메모리가 약 90~125MB 줄어듭니다" +
                   "(SDK 자체 heavy 픽스처 실측: 2021.3 44.7MB, 6000.0 32.6MB, 6000.3 27.1MB).";
        }

        private static string F0(double v) => v.ToString("F0", CultureInfo.InvariantCulture);
        private static string F1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

        /// <summary>한 줄 요약(등급 우선, 수치는 근사임을 표기).</summary>
        internal static string FormatSummary(Estimate e)
        {
            var sb = new StringBuilder();
            sb.Append("[AIT-MemBudget] iOS 메모리 예산 추정: 등급 ").Append(GradeLabel(e.grade));
            sb.Append(" (지속 약 ").Append(F0(e.steadyMB)).Append("MB/예산 ").Append(F0(SteadyBudgetMB));
            sb.Append(", 피크 약 ").Append(F0(e.peakMB)).Append("MB/예산 ").Append(F0(PeakBudgetMB)).Append(')');
            sb.Append("\n  구성(지속): 기본 ").Append(F0(BaseMB));
            sb.Append(" + wasm 코드 ").Append(F1(e.codeMB)).Append("MB×").Append(F1(SteadyMBPerCodeMB)).Append('=').Append(F0(e.codeFootprintMB));
            sb.Append(" + data ").Append(F0(e.dataRawMB));
            sb.Append(" + 힙 ").Append(F0(e.heapMB));
            sb.Append(" + 스트리밍 보유 ").Append(F0(e.streamingHeldMB));
            sb.Append(" + 기타 ").Append(F0(OtherMB));
            sb.Append("\n  시뮬레이터 실측 계수의 근사치입니다(실기기와 다를 수 있음).");
            return sb.ToString();
        }

        /// <summary>가장 큰 항목에 맞춘 줄이기 힌트(초과·주의일 때만 의미가 있다). 없으면 null.</summary>
        internal static string DominantHint(Estimate e)
        {
            if (e.grade == Grade.Comfortable) return null;
            double code = e.codeFootprintMB;
            double dataHeap = e.dataRawMB + e.heapMB + e.streamingHeldMB;
            if (code >= dataHeap)
                return "가장 큰 항목은 wasm 코드입니다 — 사용하지 않는 패키지·어셈블리 제거, Managed Stripping, IL2CPP Code Generation, " +
                       "예외 모드 축소가 코드 1MB 당 iOS 약 7.4MB 를 줄입니다.";
            return "가장 큰 항목은 데이터·힙입니다 — 큰 텍스처·오디오·폰트 축소와 외부화(스트리밍)가 효과적입니다.";
        }

        // ─────────────────────────── wasm 함수 본문 크기 파싱(순수) ───────────────────────────

        /// <summary>
        /// wasm 의 import 섹션에서 함수 import 개수를, code 섹션에서 함수 본문(페이로드) 바이트를 읽는다.
        /// 함수 인덱스 공간은 import 함수가 먼저이므로 i 번째 본문의 함수 인덱스는 importFuncCount + i 다.
        /// 매직·섹션 길이가 어긋나거나 지원하지 않는 import 형태(memory64)면 false.
        /// </summary>
        internal static bool TryReadFunctionBodySizes(byte[] wasm, out List<int> bodySizes, out int importFuncCount)
        {
            bodySizes = new List<int>();
            importFuncCount = 0;
            if (wasm == null || wasm.Length < 8) return false;
            if (wasm[0] != 0x00 || wasm[1] != 0x61 || wasm[2] != 0x73 || wasm[3] != 0x6D) return false;
            if (wasm[4] != 0x01 || wasm[5] != 0x00 || wasm[6] != 0x00 || wasm[7] != 0x00) return false;

            int pos = 8;
            while (pos < wasm.Length)
            {
                byte id = wasm[pos++];
                if (!AITWasmSections.TryReadU32(wasm, ref pos, out uint size)) return false;
                if ((long)size > wasm.Length - pos) return false;
                int end = pos + (int)size;

                if (id == 2)
                {
                    int p = pos;
                    if (!TryCountImportedFunctions(wasm, ref p, end, out importFuncCount)) return false;
                }
                else if (id == AITWasmSections.SectionCode)
                {
                    int p = pos;
                    if (!AITWasmSections.TryReadU32(wasm, ref p, out uint count)) return false;
                    for (uint i = 0; i < count; i++)
                    {
                        if (!AITWasmSections.TryReadU32(wasm, ref p, out uint bodySize)) return false;
                        if ((long)bodySize > end - p) return false;
                        bodySizes.Add((int)bodySize);
                        p += (int)bodySize;
                    }
                }

                pos = end;
            }

            return true;
        }

        private static bool TryCountImportedFunctions(byte[] d, ref int p, int end, out int funcCount)
        {
            funcCount = 0;
            if (!AITWasmSections.TryReadU32(d, ref p, out uint count)) return false;
            for (uint i = 0; i < count; i++)
            {
                // module 이름, field 이름(길이 + 바이트)
                for (int n = 0; n < 2; n++)
                {
                    if (!AITWasmSections.TryReadU32(d, ref p, out uint len)) return false;
                    if ((long)len > end - p) return false;
                    p += (int)len;
                }

                if (p >= end) return false;
                byte kind = d[p++];
                switch (kind)
                {
                    case 0: // func: typeidx
                        if (!AITWasmSections.TryReadU32(d, ref p, out _)) return false;
                        funcCount++;
                        break;
                    case 1: // table: reftype + limits
                        if (p >= end) return false;
                        p++;
                        if (!TrySkipLimits(d, ref p, end)) return false;
                        break;
                    case 2: // memory: limits
                        if (!TrySkipLimits(d, ref p, end)) return false;
                        break;
                    case 3: // global: valtype + mutability
                        if (p + 2 > end) return false;
                        p += 2;
                        break;
                    case 4: // tag: attribute + typeidx
                        if (p >= end) return false;
                        p++;
                        if (!AITWasmSections.TryReadU32(d, ref p, out _)) return false;
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        private static bool TrySkipLimits(byte[] d, ref int p, int end)
        {
            if (p >= end) return false;
            byte flags = d[p++];
            if ((flags & 0x04) != 0) return false; // memory64: u64 한계 — 이 파서는 다루지 않는다
            if (!AITWasmSections.TryReadU32(d, ref p, out _)) return false;
            if ((flags & 0x01) != 0 && !AITWasmSections.TryReadU32(d, ref p, out _)) return false;
            return true;
        }

        // ─────────────────────────── symbols 파싱·그룹 집계(순수) ───────────────────────────

        /// <summary>
        /// symbols 텍스트를 함수 인덱스 → 이름으로 읽는다. 지원 형태:
        ///  · JSON 객체 {"12":"name", ...}(인덱스→이름) 또는 {"name":12, ...}(이름→인덱스)
        ///  · JSON 문자열 배열 ["name0","name1", ...](위치가 인덱스)
        ///  · 줄 단위 "12:name"(emscripten --emit-symbol-map)
        /// 하나도 못 읽으면 false.
        /// </summary>
        internal static bool TryParseSymbols(string text, out Dictionary<int, string> names)
        {
            names = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(text)) return false;

            int i = 0;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) return false;

            if (text[i] == '{') ParseJsonObject(text, i + 1, names);
            else if (text[i] == '[') ParseJsonArray(text, i + 1, names);
            else ParseSymbolLines(text, names);

            return names.Count > 0;
        }

        private static void ParseJsonObject(string s, int i, Dictionary<int, string> names)
        {
            while (i < s.Length)
            {
                SkipJsonSeparators(s, ref i);
                if (i >= s.Length || s[i] == '}') return;
                if (s[i] != '"') return;
                if (!TryReadJsonString(s, ref i, out string key)) return;

                while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ':')) i++;
                if (i >= s.Length) return;

                string value;
                if (s[i] == '"')
                {
                    if (!TryReadJsonString(s, ref i, out value)) return;
                    if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int idx))
                        names[idx] = value;
                }
                else
                {
                    int start = i;
                    while (i < s.Length && s[i] != ',' && s[i] != '}' && !char.IsWhiteSpace(s[i])) i++;
                    if (int.TryParse(s.Substring(start, i - start), NumberStyles.None, CultureInfo.InvariantCulture, out int idx2))
                        names[idx2] = key;
                }
            }
        }

        private static void ParseJsonArray(string s, int i, Dictionary<int, string> names)
        {
            int index = 0;
            while (i < s.Length)
            {
                SkipJsonSeparators(s, ref i);
                if (i >= s.Length || s[i] == ']') return;
                if (s[i] != '"') return;
                if (!TryReadJsonString(s, ref i, out string value)) return;
                names[index++] = value;
            }
        }

        private static void SkipJsonSeparators(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
        }

        // s[i] 가 여는 따옴표인 위치에서 JSON 문자열을 읽고 닫는 따옴표 다음으로 i 를 옮긴다.
        // 이스케이프는 \" \\ \/ \n \t \r \uXXXX 만 해석하고 나머지는 그대로 둔다(심볼 이름에는 충분하다).
        private static bool TryReadJsonString(string s, ref int i, out string value)
        {
            value = null;
            if (i >= s.Length || s[i] != '"') return false;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i >= s.Length) return false;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'u':
                        if (i + 4 > s.Length) return false;
                        if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            sb.Append((char)code);
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }

            return false;
        }

        private static void ParseSymbolLines(string text, Dictionary<int, string> names)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0 || colon == line.Length - 1) continue;
                if (int.TryParse(line.Substring(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out int idx))
                    names[idx] = line.Substring(colon + 1);
            }
        }

        internal const string UnnamedGroup = "(이름 없음)";
        internal const string NativeGroup = "(네이티브·런타임)";
        internal const string GenericGroup = "(제네릭 타입)";
        internal const string CompilerGeneratedGroup = "(컴파일러 생성)";

        // IL2CPP 가 만든 C# 메서드 이름의 해시 접미사: <타입>_<메서드>_m<대문자 16진수>.
        private static readonly Regex Il2CppMethodHash = new Regex("_m[0-9A-F]{8,}", RegexOptions.Compiled);

        /// <summary>
        /// 심볼 이름을 그룹 이름으로 분류한다. IL2CPP 가 생성한 C# 메서드(<c>Ns_Type_Method_m해시</c>)는 첫 토큰(루트 네임스페이스,
        /// 네임스페이스가 없는 타입이면 그 타입 이름)으로 묶는다. 어셈블리 경계는 심볼에 없으므로 어셈블리 단위가 아니라
        /// 근사다. 해시 접미사가 없는 이름(libil2cpp·libc·emscripten 등)은 네이티브·런타임으로 묶는다.
        /// </summary>
        internal static string ClassifySymbol(string name)
        {
            if (string.IsNullOrEmpty(name)) return UnnamedGroup;
            if (name.StartsWith("U3C", StringComparison.Ordinal)) return CompilerGeneratedGroup;

            Match m = Il2CppMethodHash.Match(name);
            if (!m.Success || m.Index == 0) return NativeGroup;

            string head = name.Substring(0, m.Index);
            string[] tokens = head.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return NativeGroup;

            // 제네릭 타입(List_1_..., Dictionary_2_...)은 두 번째 토큰이 항 수(숫자)다.
            if (tokens.Length >= 2 && IsAllDigits(tokens[1])) return GenericGroup;
            return tokens[0];
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        internal struct GroupEntry
        {
            public string name;
            public long bytes;
            public int functions;
        }

        /// <summary>
        /// 함수 본문 크기를 심볼 이름의 그룹으로 합산해 바이트 내림차순으로 돌려준다.
        /// 함수 인덱스 = importFuncCount + 본문 순번. 심볼이 없는 함수는 <see cref="UnnamedGroup"/>.
        /// </summary>
        internal static List<GroupEntry> GroupCodeBytes(IList<int> bodySizes, int importFuncCount, IDictionary<int, string> names)
        {
            var byGroup = new Dictionary<string, GroupEntry>(StringComparer.Ordinal);
            if (bodySizes != null)
            {
                for (int i = 0; i < bodySizes.Count; i++)
                {
                    string name = null;
                    if (names != null) names.TryGetValue(importFuncCount + i, out name);
                    string group = ClassifySymbol(name);
                    byGroup.TryGetValue(group, out GroupEntry e);
                    e.name = group;
                    e.bytes += bodySizes[i];
                    e.functions++;
                    byGroup[group] = e;
                }
            }

            var list = new List<GroupEntry>(byGroup.Values);
            list.Sort((a, b) =>
            {
                int c = b.bytes.CompareTo(a.bytes);
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            return list;
        }

        /// <summary>그룹 목록에서 상위 <paramref name="top"/> 개를 표로 만든다. 나머지는 한 줄로 합친다.</summary>
        internal static string FormatTopGroups(List<GroupEntry> groups, int top)
        {
            long total = 0;
            foreach (var g in groups) total += g.bytes;
            if (total <= 0) return null;

            var sb = new StringBuilder();
            sb.Append("[AIT-MemBudget] wasm 코드 바이트 Top ").Append(Math.Min(top, groups.Count))
              .Append(" (루트 네임스페이스 근사, 함수 본문 합 ").Append(F1(total / MB)).Append("MB)");
            long shown = 0;
            for (int i = 0; i < groups.Count && i < top; i++)
            {
                var g = groups[i];
                shown += g.bytes;
                sb.Append("\n  ").Append(F1(g.bytes / MB).PadLeft(6)).Append("MB ")
                  .Append((g.bytes * 100.0 / total).ToString("F1", CultureInfo.InvariantCulture).PadLeft(5)).Append("%  ")
                  .Append(g.name).Append(" (함수 ").Append(g.functions.ToString(CultureInfo.InvariantCulture)).Append("개)");
            }

            if (groups.Count > top)
            {
                sb.Append("\n  ").Append(F1((total - shown) / MB).PadLeft(6)).Append("MB       나머지 ")
                  .Append((groups.Count - top).ToString(CultureInfo.InvariantCulture)).Append("개 그룹");
            }

            return sb.ToString();
        }

        // ─────────────────────────── 어댑터(빌드 산출물 I/O) ───────────────────────────

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                if (report == null) return;

                // SDK 는 UPM 패키지로 파트너 프로젝트 전체에 임포트되므로 다른 플랫폼 빌드에서도 호출된다 → WebGL 만.
                if (report.summary.platform != BuildTarget.WebGL) return;

                string disableEnv = Environment.GetEnvironmentVariable("AIT_MEM_BUDGET_DISABLE");
                if (string.Equals(disableEnv, "true", StringComparison.OrdinalIgnoreCase))
                {
                    AITLog.Info("[AIT-MemBudget] AIT_MEM_BUDGET_DISABLE=true — 진단을 건너뜁니다.");
                    return;
                }

                Run(report.summary.outputPath);
            }
            catch (Exception e)
            {
                AITLog.Warning(
                    $"[AIT-MemBudget] 진단 실행 중 예외 발생(무시 — 빌드 산출물에는 영향 없음): {e.Message}",
                    sentryCapture: false);
            }
        }

        private static void Run(string outputPath)
        {
            string buildDir = string.IsNullOrEmpty(outputPath) ? null : Path.Combine(outputPath, "Build");
            if (buildDir == null || !Directory.Exists(buildDir))
            {
                AITLog.Info("[AIT-MemBudget] WebGL Build 폴더를 찾지 못해 진단을 건너뜁니다.");
                return;
            }

            string wasmPath = FindFirstMatch(buildDir, new[] { ".wasm", ".wasm.br", ".wasm.gz", ".wasm.unityweb" });
            if (wasmPath == null)
            {
                AITLog.Info("[AIT-MemBudget] wasm 파일을 찾지 못해 진단을 건너뜁니다.");
                return;
            }

            if (!TryReadMaybeCompressed(wasmPath, out byte[] wasm, out string wasmError))
            {
                AITLog.Warning($"[AIT-MemBudget] wasm 읽기 실패로 진단을 건너뜁니다: {wasmError}", sentryCapture: false);
                return;
            }

            if (!AITWasmSections.TryParse(wasm, out AITWasmSections.Sizes sizes))
            {
                AITLog.Warning("[AIT-MemBudget] wasm 형식이 아니어서 진단을 건너뜁니다.", sentryCapture: false);
                return;
            }

            long dataRaw = -1;
            string dataPath = FindFirstMatch(buildDir, new[] { ".data", ".data.br", ".data.gz", ".data.unityweb" });
            if (dataPath != null) dataRaw = AITDataRawSize.Measure(dataPath);
            if (dataRaw < 0 && dataPath != null)
            {
                // .unityweb 이거나 측정 실패: 압축 파일 크기를 하한으로 쓴다.
                try { dataRaw = new FileInfo(dataPath).Length; }
                catch (Exception) { dataRaw = 0; }
            }

            long streamingBytes = SumStreamingBytes(Path.Combine(outputPath, "StreamingAssets", "ait-stream-audio"))
                                  + SumStreamingBytes(Path.Combine(outputPath, "StreamingAssets", "ait-stream-font"));

            Estimate est = Compute(new Inputs
            {
                codeMB = sizes.codeBytes / MB,
                dataRawMB = Math.Max(0, dataRaw) / MB,
                streamingHeldMB = streamingBytes / MB,
            });

            string summary = FormatSummary(est);
            if (est.grade == Grade.Over) AITLog.Warning(summary, sentryCapture: false);
            else AITLog.Info(summary);

            string hint = DominantHint(est);
            if (hint != null) AITLog.Info("[AIT-MemBudget] " + hint);

            string legacy = LegacyEngineHint(Application.unityVersion);
            if (legacy != null) AITLog.Info("[AIT-MemBudget] " + legacy);

            LogTopGroups(buildDir, wasm);
        }

        private static void LogTopGroups(string buildDir, byte[] wasm)
        {
            string symbolsPath = FindSymbolsFile(buildDir);
            if (symbolsPath == null)
            {
                AITLog.Info("[AIT-MemBudget] symbols 파일(Build/*.symbols.json)이 없어 코드 바이트 Top 20 분해는 생략합니다 " +
                            "(WebGL Debug Symbols 를 External 로 두면 출력됩니다).");
                return;
            }

            if (!TryReadMaybeCompressed(symbolsPath, out byte[] symBytes, out string symError))
            {
                AITLog.Info($"[AIT-MemBudget] symbols 읽기 실패로 Top 20 분해를 생략합니다: {symError}");
                return;
            }

            if (!TryParseSymbols(Encoding.UTF8.GetString(symBytes), out Dictionary<int, string> names))
            {
                AITLog.Info("[AIT-MemBudget] symbols 형식을 해석하지 못해 Top 20 분해를 생략합니다.");
                return;
            }

            if (!TryReadFunctionBodySizes(wasm, out List<int> bodies, out int importFuncCount))
            {
                AITLog.Info("[AIT-MemBudget] wasm 함수 본문을 읽지 못해 Top 20 분해를 생략합니다.");
                return;
            }

            string table = FormatTopGroups(GroupCodeBytes(bodies, importFuncCount, names), TopGroupCount);
            if (table != null) AITLog.Info(table);
        }

        // 접미사 우선순위대로 Build 폴더에서 첫 파일을 찾는다. .symbols/.map 같은 부속 파일은 제외한다.
        private static string FindFirstMatch(string dir, string[] suffixesInPriorityOrder)
        {
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch (Exception) { return null; }

            foreach (string suffix in suffixesInPriorityOrder)
            {
                foreach (string f in files)
                {
                    string name = Path.GetFileName(f);
                    if (name.IndexOf(".symbols", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return f;
                }
            }

            return null;
        }

        private static string FindSymbolsFile(string dir)
        {
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch (Exception) { return null; }

            string[] suffixes = { ".symbols.json", ".symbols.json.br", ".symbols.json.gz", ".symbols.json.unityweb" };
            foreach (string suffix in suffixes)
                foreach (string f in files)
                    if (f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        private static long SumStreamingBytes(string dir)
        {
            long total = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    total += new FileInfo(f).Length;
                }
            }
            catch (Exception)
            {
                // 진단 보조 — 못 읽으면 0 으로 둔다.
            }

            return total;
        }

        // 무압축(.wasm/.json)은 그대로, 그 외(.br/.gz/.unityweb)는 내장 Node(zlib)로 풀어 읽는다.
        // Editor mono 에는 brotli 디코더가 없다(AITBrotliCompressor·AITWasmSections 와 같은 사유).
        private const string DecompressScript =
            "const fs=require('fs'),z=require('zlib');" +
            "const b=fs.readFileSync(process.argv[1]);let o;" +
            "if(b.length>=2&&b[0]===0x1f&&b[1]===0x8b)o=z.gunzipSync(b);" +
            "else o=z.brotliDecompressSync(b);" +
            "fs.writeFileSync(process.argv[2],o);";

        private static bool TryReadMaybeCompressed(string path, out byte[] bytes, out string error)
        {
            bytes = null;
            error = null;
            string temp = null;
            try
            {
                if (path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    bytes = File.ReadAllBytes(path);
                    return true;
                }

                if (!AITBrotliCompressor.TryResolveNode(out string node))
                {
                    error = "내장 Node 를 찾을 수 없음";
                    return false;
                }

                temp = Path.Combine(Path.GetTempPath(),
                    "ait-membudget-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N") + ".bin");
                var psi = new ProcessStartInfo
                {
                    FileName = node,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath(),
                };
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add(DecompressScript);
                psi.ArgumentList.Add(path);
                psi.ArgumentList.Add(temp);

                using (var p = new Process { StartInfo = psi })
                {
                    var err = new StringBuilder();
                    p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) err.AppendLine(ev.Data); };
                    p.Start();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(NodeTimeoutMs))
                    {
                        try { p.Kill(); } catch (Exception) { /* 이미 종료됨 */ }
                        error = "node 해제 시간 초과";
                        return false;
                    }

                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        string msg = err.ToString().Trim();
                        error = "node 해제 실패(exit " + p.ExitCode + "): " + (msg.Length > 200 ? msg.Substring(0, 200) : msg);
                        return false;
                    }
                }

                if (!File.Exists(temp))
                {
                    error = "node 해제 산출물 없음";
                    return false;
                }

                bytes = File.ReadAllBytes(temp);
                return true;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
            finally
            {
                if (temp != null)
                {
                    try { File.Delete(temp); } catch (Exception) { /* 임시 파일 정리 실패는 무시 */ }
                }
            }
        }
    }
}
