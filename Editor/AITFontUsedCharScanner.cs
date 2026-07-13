// -----------------------------------------------------------------------
// <copyright file="AITFontUsedCharScanner.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font subset Auto 모드 사용 문자 스캐너
// </copyright>
// -----------------------------------------------------------------------
//
// 폰트 subset Auto 모드의 "어떤 문자가 게임에 등장하는가"를 전 프로젝트에서 스캔한다(Editor 전용).
//
// 철학(zero-config): 개발자가 보존 범위를 손으로 명시하지 않아도, 프로젝트에 실제로 등장하는
//   문자체계를 스캔해 그 블록 전체를 보존한다(블록 완성 규칙은 AITFontUnicodeBlocks). 따라서
//   "가" 한 글자만 써도 한글 음절 전체가 살아남아 동적 닉네임/채팅이 □ 가 되지 않는다.
//
// 스캔 대상(보수적 — 누락보다 과보존을 택함):
//   - 씬/프리팹/ScriptableObject(.unity/.prefab/.asset): YAML 라인에서 비ASCII 문자 전부 + 따옴표 문자열
//   - C# 소스(.cs, Editor 포함): 문자열/문자 리터럴(단순화: 비ASCII + 따옴표 안 문자)
//   - StreamingAssets/ 및 Localization 텍스트(.json/.csv/.txt/.xml/.yaml): 비ASCII 문자 전부
//
// ★ 원시 비ASCII 문자뿐 아니라 백슬래시 이스케이프(\xXX/\uXXXX/\UXXXXXXXX)도 디코드한다. Unity YAML·
//   C#·JSON 은 비ASCII 문자열을 ASCII 이스케이프로 직렬화하는 경우가 흔하다(예: I2Languages.asset 은
//   로컬라이제이션 DB 전체를 "\uXXXX..." 형태로 저장). 원시 문자만 봤다면 실사용 CJK·가나 수천 자를
//   통째로 놓쳐 서브셋 결과가 대량 □(tofu)가 된다.
//
// 비용: 수천 파일에서도 수 초 수준이 되도록 파일을 라인 스트리밍으로 처리한다(전체 read 금지).
//   파일별 try/catch — 한 파일 실패가 전체 스캔을 막지 않는다.
//
// 한자(Han) 처리: 블록이 2만+자라 블록 완성을 적용하지 않는다. 대신 감지된 한자 자체 + KS X 1001
//   상용 한자 4,888자 패드를 보존한다. KS X 1001 한자는 EUC-KR(Code Page 949)로 한자 영역
//   (행 0xCA~0xFD)을 디코드해 도출한다(실패 시 감지된 한자만 + 경고).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 전 프로젝트 사용 문자(코드포인트)를 스캔하는 Editor 전용 헬퍼.
    /// 결과는 <see cref="HashSet{Int32}"/>(코드포인트). 서로게이트 쌍은 UTF-32 로 합쳐 수집한다.
    /// </summary>
    public static class AITFontUsedCharScanner
    {
        /// <summary>스캔 대상 확장자(소문자, '.' 포함).</summary>
        private static readonly HashSet<string> ScanExtensions = new HashSet<string>
        {
            ".unity", ".prefab", ".asset",          // 씬/프리팹/ScriptableObject(YAML)
            ".cs",                                   // C# 소스
            ".json", ".csv", ".txt", ".xml", ".yaml", ".yml", // 로컬라이제이션/StreamingAssets 텍스트
        };

        private static List<int> _ksx1001HanCache;

        /// <summary>
        /// 항시 포함 베이스라인 범위(스캔 결과와 무관하게 항상 보존).
        /// ASCII + Latin-1 + 한글 음절/자모 + CJK 기호 + 전각. 기존 수동 기본값과 동일.
        /// </summary>
        public const string BaselineRanges =
            "U+0020-007E,U+00A0-00FF,U+AC00-D7A3,U+1100-11FF,U+3000-303F,U+FF00-FFEF";

        /// <summary>
        /// 스캔으로 감지한 코드포인트 + 블록 완성 + Han 패드 + 베이스라인을 합쳐
        /// 최종 보존 범위 문자열(fontTools 표기)을 만든다. 부수 효과 없음 → 단위 테스트 대상.
        /// </summary>
        /// <param name="detected">스캔으로 감지한 코드포인트(비ASCII 위주).</param>
        /// <param name="hanPad">KS X 1001 등 한자 패드(없으면 빈 목록).</param>
        /// <param name="preservedBlocks">리포트용: 블록 완성으로 보존된 블록 목록.</param>
        /// <returns>baseline 을 항상 포함하는 범위 문자열.</returns>
        public static string BuildPreservedRanges(
            IEnumerable<int> detected,
            IEnumerable<int> hanPad,
            out List<AITFontUnicodeBlocks.Block> preservedBlocks)
        {
            var detectedSet = new HashSet<int>();
            if (detected != null)
            {
                foreach (var cp in detected)
                {
                    if (cp >= 0)
                    {
                        detectedSet.Add(cp);
                    }
                }
            }

            // 1) 비한자 코드포인트 → 속한 블록 전체 보존(블록 완성).
            preservedBlocks = AITFontUnicodeBlocks.ExpandToBlocks(detectedSet);

            var codepoints = new HashSet<int>();

            // 2) 보존 블록 전체 코드포인트 펼치기.
            foreach (var block in preservedBlocks)
            {
                for (int cp = block.Start; cp <= block.End; cp++)
                {
                    codepoints.Add(cp);
                }
            }

            // 3) ★ 감지된 코드포인트 자체는 블록 등재 여부·한자 여부와 무관하게 전부 보존한다(드롭 방지).
            //   블록 완성(1~2)은 '테이블에 등재된' 비한자 문자체계만 블록 전체를 살리지만, 테이블에
            //   없는 문자체계(예: Bengali·Georgian·Tibetan·Khmer·국기 이모지 Regional Indicator 등)의
            //   감지 글자가 조용히 누락되면 그 글자가 □(tofu)로 깨진다. 또한 한자는 블록 완성 제외
            //   대상이지만 감지된 글자 자체는 살려야 한다. 따라서 감지된 코드포인트를 무조건 보존해
            //   "프로젝트에 실제 등장한 글자는 어떤 문자체계든 절대 □ 가 되지 않는다"를 보장한다.
            //   (미등재 문자체계의 '동적' 텍스트 블록 완성은 fontSubsetExtraRanges 로 보강한다.)
            foreach (var cp in detectedSet)
            {
                codepoints.Add(cp);
            }

            // 4) Han 패드(KS X 1001 상용 한자) 보존.
            if (hanPad != null)
            {
                foreach (var cp in hanPad)
                {
                    codepoints.Add(cp);
                }
            }

            // 5) 항시 베이스라인 — 스캔 결과와 무관하게 항상 포함.
            foreach (var cp in EnumerateBaseline())
            {
                codepoints.Add(cp);
            }

            return AITFontUnicodeBlocks.FormatRanges(codepoints);
        }

        /// <summary>베이스라인 범위 문자열을 코드포인트로 펼친다.</summary>
        internal static IEnumerable<int> EnumerateBaseline()
        {
            foreach (var part in BaselineRanges.Split(','))
            {
                string token = part.Trim();
                if (token.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring(2);
                }

                if (token.Length == 0)
                {
                    continue;
                }

                int dash = token.IndexOf('-');
                if (dash < 0)
                {
                    if (int.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out int single))
                    {
                        yield return single;
                    }

                    continue;
                }

                if (int.TryParse(token.Substring(0, dash), System.Globalization.NumberStyles.HexNumber, null, out int lo)
                    && int.TryParse(token.Substring(dash + 1), System.Globalization.NumberStyles.HexNumber, null, out int hi)
                    && hi >= lo)
                {
                    for (int cp = lo; cp <= hi; cp++)
                    {
                        yield return cp;
                    }
                }
            }
        }

        /// <summary>
        /// Assets/ 트리 전체를 스캔해 등장한 코드포인트 집합을 반환한다.
        /// 실패/예외는 파일 단위로 흡수하며, 치명적 예외 시 지금까지 수집분을 반환한다.
        /// </summary>
        public static HashSet<int> ScanProject()
        {
            var codepoints = new HashSet<int>();
            int scannedFiles = 0;
            try
            {
                string assetsPath = Application.dataPath;
                string[] files;
                try
                {
                    files = Directory.GetFiles(assetsPath, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-FontSubset] 프로젝트 파일 열거 실패 → 스캔 생략: {e.Message}");
                    return codepoints;
                }

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!ScanExtensions.Contains(ext))
                    {
                        continue;
                    }

                    if (ScanFile(file, codepoints))
                    {
                        scannedFiles++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] 문자 스캔 중 예외(수집분으로 계속): {e.Message}");
            }

            Debug.Log($"[AIT-FontSubset] 사용 문자 스캔: {scannedFiles}개 파일에서 고유 코드포인트 {codepoints.Count}개 감지.");
            return codepoints;
        }

        /// <summary>
        /// 파일 하나를 라인 스트리밍으로 읽어 비ASCII 코드포인트(서로게이트 합성)를 수집한다.
        /// ASCII 영역은 항시 베이스라인에 포함되므로 여기서는 비ASCII만 모아 비용을 줄인다.
        /// </summary>
        /// <returns>실제로 읽었으면 true(예외 시 false).</returns>
        private static bool ScanFile(string path, HashSet<int> sink)
        {
            try
            {
                using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        CollectNonAscii(line, sink);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset]   파일 스캔 실패(건너뜀): {Path.GetFileName(path)} — {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 문자열에서 비ASCII 코드포인트를 수집한다. 서로게이트 쌍은 <see cref="char.ConvertToUtf32(char, char)"/>
        /// 로 합쳐 단일 코드포인트로 다룬다(이모지/확장 한자 대응).
        ///
        /// ★ 원시(raw) 문자뿐 아니라 백슬래시 이스케이프(<c>\xXX</c>·<c>\uXXXX</c>·<c>\UXXXXXXXX</c>)도
        /// 디코드한다. Unity YAML(.asset/.prefab/.unity)·C# 소스·JSON 은 비ASCII 문자열을 원시 바이트가
        /// 아니라 ASCII 이스케이프로 직렬화하는 경우가 흔하다. 예: I2 Localization 의 I2Languages.asset 은
        /// 전 로컬라이제이션 DB 를 <c>"抽抽防..."</c> 형태(CJK/가나/키릴)로 저장한다.
        /// 이를 놓치면(=원시 문자만 수집) 서브셋이 실사용 CJK·가나 수천 자를 통째로 누락해 대량 tofu(□)
        /// 가 된다. 과보존이 누락보다 안전하다는 스캐너 철학에 따라 이스케이프를 적극 디코드한다
        /// (오검출된 코드포인트는 글리프 몇 개를 더 살릴 뿐이나, 누락은 글자가 □ 로 깨진다).
        /// </summary>
        internal static void CollectNonAscii(string s, HashSet<int> sink)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            int pendingHighSurrogate = -1; // \uXXXX 상위 서로게이트가 하위 짝을 기다리는 중.

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                // 1) 백슬래시 이스케이프 디코드(\xXX / \uXXXX / \UXXXXXXXX).
                if (c == '\\' && i + 1 < s.Length)
                {
                    // 이스케이프된 백슬래시(\\)는 다음 문자를 이스케이프 시작으로 오인하지 않도록 함께 소비.
                    if (s[i + 1] == '\\')
                    {
                        pendingHighSurrogate = -1;
                        i++;
                        continue;
                    }

                    int cp = TryReadUnicodeEscape(s, i, out int consumed);
                    if (cp >= 0)
                    {
                        if (cp >= 0xD800 && cp <= 0xDBFF)
                        {
                            // \uXXXX 상위 서로게이트 — 다음 하위 서로게이트와 합쳐 astral 코드포인트로.
                            pendingHighSurrogate = cp;
                        }
                        else if (cp >= 0xDC00 && cp <= 0xDFFF && pendingHighSurrogate >= 0)
                        {
                            sink.Add(char.ConvertToUtf32((char)pendingHighSurrogate, (char)cp));
                            pendingHighSurrogate = -1;
                        }
                        else
                        {
                            pendingHighSurrogate = -1;

                            // ASCII 영역은 베이스라인이 책임지므로 비ASCII(0x80 이상)만 수집.
                            if (cp >= 0x80)
                            {
                                sink.Add(cp);
                            }
                        }

                        i += consumed - 1; // for 루프의 i++ 를 상쇄해 이스케이프 전체를 건너뜀.
                        continue;
                    }
                }

                pendingHighSurrogate = -1;

                // 2) 원시(raw) 서로게이트 쌍 → UTF-32 로 합쳐 수집(이모지/확장 한자).
                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    sink.Add(char.ConvertToUtf32(c, s[i + 1]));
                    i++;
                    continue;
                }

                // 3) 원시(raw) 비ASCII(0x80 이상)만 수집.
                if (c >= 0x80)
                {
                    sink.Add(c);
                }
            }
        }

        /// <summary>
        /// 위치 <paramref name="backslashIndex"/>('\')에서 시작하는 유니코드 이스케이프를 해석한다.
        /// 지원 형식: <c>\xXX</c>(2 hex)·<c>\uXXXX</c>(4 hex)·<c>\UXXXXXXXX</c>(8 hex).
        /// 성공 시 코드포인트를, 형식 불일치/hex 부족/범위 초과 시 -1 을 반환한다. 서로게이트 값
        /// (0xD800-0xDFFF)은 유효로 간주해 반환하며, 쌍 합성은 호출부가 담당한다.
        /// </summary>
        /// <param name="consumed">소비한 문자 수('\' 포함). 실패 시 0.</param>
        private static int TryReadUnicodeEscape(string s, int backslashIndex, out int consumed)
        {
            consumed = 0;

            int hexLen;
            switch (s[backslashIndex + 1])
            {
                case 'x': hexLen = 2; break;
                case 'u': hexLen = 4; break;
                case 'U': hexLen = 8; break;
                default: return -1;
            }

            int start = backslashIndex + 2;
            if (start + hexLen > s.Length)
            {
                return -1;
            }

            int value = 0;
            for (int k = 0; k < hexLen; k++)
            {
                int d = HexDigit(s[start + k]);
                if (d < 0)
                {
                    return -1;
                }

                value = (value << 4) | d;
            }

            if (value > 0x10FFFF)
            {
                return -1;
            }

            consumed = 2 + hexLen;
            return value;
        }

        /// <summary>16진 숫자 하나를 값(0-15)으로. 16진이 아니면 -1.</summary>
        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return (c - 'a') + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return (c - 'A') + 10;
            }

            return -1;
        }

        /// <summary>
        /// KS X 1001 상용 한자 4,888자의 코드포인트 목록을 반환한다(EUC-KR 디코딩으로 도출, 캐시).
        /// EUC-KR(Code Page 949)에서 한자 영역은 lead byte 0xCA~0xFD × trail byte 0xA1~0xFE 범위.
        /// 디코딩 실패(인코딩 미등록 등) 시 빈 목록을 반환하고 경고한다(호출부가 감지된 한자만 사용).
        /// </summary>
        public static List<int> GetKsx1001Han()
        {
            if (_ksx1001HanCache != null)
            {
                return _ksx1001HanCache;
            }

            var result = new List<int>();
            try
            {
                Encoding eucKr = Encoding.GetEncoding(949);
                var bytes = new byte[2];
                for (int lead = 0xCA; lead <= 0xFD; lead++)
                {
                    for (int trail = 0xA1; trail <= 0xFE; trail++)
                    {
                        bytes[0] = (byte)lead;
                        bytes[1] = (byte)trail;
                        string decoded;
                        try
                        {
                            decoded = eucKr.GetString(bytes);
                        }
                        catch
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(decoded) || decoded.Length != 1)
                        {
                            continue;
                        }

                        int cp = decoded[0];
                        // 한자 영역(CJK Unified Ideographs)만 채택 — 미매핑/기호 디코드는 제외.
                        if (AITFontUnicodeBlocks.IsHan(cp))
                        {
                            result.Add(cp);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] KS X 1001 한자 도출 실패(감지된 한자만 보존): {e.Message}");
                result.Clear();
            }

            _ksx1001HanCache = result;
            return result;
        }
    }
}
