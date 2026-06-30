// -----------------------------------------------------------------------
// <copyright file="AITFontUnicodeBlocks.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font subset 유니코드 블록 테이블 / 블록 완성 규칙
// </copyright>
// -----------------------------------------------------------------------
//
// 폰트 subset Auto 모드의 핵심 불변식을 책임지는 순수 로직 모듈(부수 효과 없음 → 단위 테스트 대상).
//
//   ★ 불변식: "게임에 어떤 문자체계가 한 글자라도 있으면 그 문자체계의 유니코드 블록 전체가
//             폰트에 살아남는다." (동적 텍스트가 같은 문자체계라면 절대 □ 가 되지 않게.)
//
// 동작: 스캐너가 수집한 코드포인트 집합을 받아, 각 코드포인트가 속한 유니코드 블록 전체를
//   보존 범위에 추가한다. 즉 한글 "가" 한 글자만 써도 한글 음절 블록(AC00-D7A3) 전체가 살아남아
//   런타임 동적 닉네임/채팅의 임의 한글이 □ 가 되지 않는다.
//
//   ⚠ Han 예외: CJK Unified Ideographs(U+4E00-9FFF 및 확장 A~)는 2만+자라 블록 전체를
//     보존하면 subset 의 의미가 사라진다. 따라서 한자만은 블록 완성 규칙에서 제외하고,
//     [감지된 한자들 자체] + [KS X 1001 한자 4,888자 패드](AITFontUsedCharScanner 가 도출)를
//     합쳐 보존한다. 한국어 게임의 동적 한자 표시 범위(상용 한자)를 보수적으로 커버한다.

using System.Collections.Generic;
using System.Text;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 유니코드 블록 테이블과 "블록 완성" 규칙을 제공하는 순수 정적 헬퍼.
    /// 부수 효과가 없어 EditMode 단위 테스트에서 직접 검증한다.
    /// </summary>
    public static class AITFontUnicodeBlocks
    {
        /// <summary>CJK Unified Ideographs 기본 블록(한자) 시작.</summary>
        public const int HanBlockStart = 0x4E00;

        /// <summary>CJK Unified Ideographs 기본 블록(한자) 끝.</summary>
        public const int HanBlockEnd = 0x9FFF;

        /// <summary>유니코드 블록 항목(start, end 포함, name).</summary>
        public readonly struct Block
        {
            /// <summary>블록 시작 코드포인트(포함).</summary>
            public readonly int Start;

            /// <summary>블록 끝 코드포인트(포함).</summary>
            public readonly int End;

            /// <summary>블록 이름(드롭 리포트 표기용).</summary>
            public readonly string Name;

            /// <summary>한자 계열(CJK Unified Ideographs)이라 블록 전체 보존 대상에서 제외되는지.</summary>
            public readonly bool IsHan;

            public Block(int start, int end, string name, bool isHan = false)
            {
                Start = start;
                End = end;
                Name = name;
                IsHan = isHan;
            }

            /// <summary>코드포인트가 이 블록에 속하는지.</summary>
            public bool Contains(int cp) => cp >= Start && cp <= End;
        }

        /// <summary>
        /// 주요 유니코드 블록 테이블(start, end, name). 게임에서 실사용되는 문자체계 위주로
        /// 내장한다. 여기 없는 코드포인트는 "감지된 글자 자체"만 보존된다(블록 완성 미적용).
        ///
        /// Han 예외: CJK Unified Ideographs(및 확장 A)는 IsHan=true 로 표시되어 블록 완성에서
        ///   제외된다(AITFontUsedCharScanner 가 KS X 1001 패드로 별도 처리).
        /// </summary>
        public static readonly Block[] Blocks =
        {
            // ── Latin 계열 ──
            new Block(0x0000, 0x007F, "Basic Latin"),
            new Block(0x0080, 0x00FF, "Latin-1 Supplement"),
            new Block(0x0100, 0x017F, "Latin Extended-A"),
            new Block(0x0180, 0x024F, "Latin Extended-B"),
            new Block(0x0250, 0x02AF, "IPA Extensions"),
            new Block(0x02B0, 0x02FF, "Spacing Modifier Letters"),
            new Block(0x1E00, 0x1EFF, "Latin Extended Additional"),

            // ── 결합 발음 부호 ──
            new Block(0x0300, 0x036F, "Combining Diacritical Marks"),

            // ── Greek / Cyrillic ──
            new Block(0x0370, 0x03FF, "Greek and Coptic"),
            new Block(0x0400, 0x04FF, "Cyrillic"),
            new Block(0x0500, 0x052F, "Cyrillic Supplement"),

            // ── 기타 표기 체계 ──
            new Block(0x0530, 0x058F, "Armenian"),
            new Block(0x0590, 0x05FF, "Hebrew"),
            new Block(0x0600, 0x06FF, "Arabic"),
            new Block(0x0E00, 0x0E7F, "Thai"),
            new Block(0x0900, 0x097F, "Devanagari"),

            // ── 한글 ──
            new Block(0x1100, 0x11FF, "Hangul Jamo"),
            new Block(0x3130, 0x318F, "Hangul Compatibility Jamo"),
            new Block(0xA960, 0xA97F, "Hangul Jamo Extended-A"),
            new Block(0xAC00, 0xD7A3, "Hangul Syllables"),
            new Block(0xD7B0, 0xD7FF, "Hangul Jamo Extended-B"),

            // ── 일본어 가나 ──
            new Block(0x3040, 0x309F, "Hiragana"),
            new Block(0x30A0, 0x30FF, "Katakana"),
            new Block(0x31F0, 0x31FF, "Katakana Phonetic Extensions"),

            // ── CJK 기호 / 구두점 / 전각 ──
            new Block(0x2000, 0x206F, "General Punctuation"),
            new Block(0x20A0, 0x20CF, "Currency Symbols"),
            new Block(0x2100, 0x214F, "Letterlike Symbols"),
            new Block(0x2150, 0x218F, "Number Forms"),
            new Block(0x2190, 0x21FF, "Arrows"),
            new Block(0x2200, 0x22FF, "Mathematical Operators"),
            new Block(0x2460, 0x24FF, "Enclosed Alphanumerics"),
            new Block(0x2500, 0x257F, "Box Drawing"),
            new Block(0x25A0, 0x25FF, "Geometric Shapes"),
            new Block(0x2600, 0x26FF, "Miscellaneous Symbols"),
            new Block(0x2700, 0x27BF, "Dingbats"),
            new Block(0x3000, 0x303F, "CJK Symbols and Punctuation"),
            new Block(0x3200, 0x32FF, "Enclosed CJK Letters and Months"),
            new Block(0x3300, 0x33FF, "CJK Compatibility"),
            new Block(0xFE30, 0xFE4F, "CJK Compatibility Forms"),
            new Block(0xFF00, 0xFFEF, "Halfwidth and Fullwidth Forms"),

            // ── 이모지 관련(BMP 외 일부는 코드포인트 자체만 보존) ──
            new Block(0x1F300, 0x1F5FF, "Miscellaneous Symbols and Pictographs"),
            new Block(0x1F600, 0x1F64F, "Emoticons"),
            new Block(0x1F680, 0x1F6FF, "Transport and Map Symbols"),
            new Block(0x1F900, 0x1F9FF, "Supplemental Symbols and Pictographs"),
            new Block(0x2B00, 0x2BFF, "Miscellaneous Symbols and Arrows"),

            // ── 한자(Han 예외: 블록 완성 미적용) ──
            new Block(HanBlockStart, HanBlockEnd, "CJK Unified Ideographs", isHan: true),
            new Block(0x3400, 0x4DBF, "CJK Unified Ideographs Extension A", isHan: true),
            new Block(0xF900, 0xFAFF, "CJK Compatibility Ideographs", isHan: true),
        };

        /// <summary>
        /// 코드포인트가 한자 계열(블록 완성에서 제외할 영역)인지 판정한다.
        /// </summary>
        public static bool IsHan(int cp)
        {
            foreach (var b in Blocks)
            {
                if (b.IsHan && b.Contains(cp))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 코드포인트가 속한 블록을 찾는다. 못 찾으면 false.
        /// </summary>
        public static bool TryFindBlock(int cp, out Block block)
        {
            foreach (var b in Blocks)
            {
                if (b.Contains(cp))
                {
                    block = b;
                    return true;
                }
            }

            block = default;
            return false;
        }

        /// <summary>
        /// 감지된 코드포인트 집합 → "보존할 블록" 집합으로 확장한다(블록 완성 규칙).
        ///
        /// - 한자(IsHan) 블록은 전체 보존하지 않으므로 결과에 포함하지 않는다(호출부가 별도 처리).
        /// - 테이블에 없는 코드포인트는 블록이 없으므로 무시된다(호출부가 코드포인트 자체를 별도 보존).
        /// </summary>
        /// <param name="codepoints">스캔으로 감지한 코드포인트.</param>
        /// <returns>보존 대상 블록 목록(start 오름차순, 중복 제거).</returns>
        public static List<Block> ExpandToBlocks(IEnumerable<int> codepoints)
        {
            var seen = new HashSet<string>();
            var result = new List<Block>();
            foreach (var cp in codepoints)
            {
                if (IsHan(cp))
                {
                    continue; // Han 예외: 블록 완성 미적용
                }

                if (TryFindBlock(cp, out var block) && seen.Add($"{block.Start}-{block.End}"))
                {
                    result.Add(block);
                }
            }

            result.Sort((a, b) => a.Start.CompareTo(b.Start));
            return result;
        }

        /// <summary>
        /// 코드포인트 집합을 fontTools 표기 범위 문자열로 직렬화한다.
        /// 연속 구간을 "U+XXXX-YYYY", 단일은 "U+XXXX" 로 압축한다.
        /// 예) {0x41,0x42,0x43,0x61} → "U+0041-0043,U+0061".
        /// </summary>
        public static string FormatRanges(IEnumerable<int> codepoints)
        {
            var sorted = new List<int>();
            var dedup = new HashSet<int>();
            foreach (var cp in codepoints)
            {
                if (cp >= 0 && dedup.Add(cp))
                {
                    sorted.Add(cp);
                }
            }

            sorted.Sort();
            if (sorted.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            int runStart = sorted[0];
            int prev = sorted[0];
            for (int i = 1; i <= sorted.Count; i++)
            {
                bool contiguous = i < sorted.Count && sorted[i] == prev + 1;
                if (contiguous)
                {
                    prev = sorted[i];
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                if (runStart == prev)
                {
                    sb.Append(FormatCodepoint(runStart));
                }
                else
                {
                    sb.Append(FormatCodepoint(runStart)).Append('-').Append(HexOnly(prev));
                }

                if (i < sorted.Count)
                {
                    runStart = sorted[i];
                    prev = sorted[i];
                }
            }

            return sb.ToString();
        }

        /// <summary>단일 코드포인트를 "U+XXXX" 표기로(최소 4자리 0패딩).</summary>
        public static string FormatCodepoint(int cp) => "U+" + HexOnly(cp);

        private static string HexOnly(int cp)
        {
            string h = cp.ToString("X");
            return h.Length < 4 ? h.PadLeft(4, '0') : h;
        }
    }
}
