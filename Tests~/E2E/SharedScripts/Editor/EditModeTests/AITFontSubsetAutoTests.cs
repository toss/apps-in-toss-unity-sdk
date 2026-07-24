// -----------------------------------------------------------------------
// AITFontSubsetAutoTests.cs - 폰트 subset Auto 모드 순수 로직 검증
// Level 0: 블록 완성 매핑 / 범위 포맷팅 / 베이스라인 항시 포함 / Han 패드 처리
//
// 핵심 불변식 회귀 방지: "어떤 문자체계가 한 글자라도 등장하면 그 블록 전체가 보존된다."
// (동적 텍스트가 같은 문자체계라면 절대 □ 가 되지 않게.)
// -----------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITFontSubsetAutoTests
{
    // 범위 문자열 → 코드포인트 집합으로 펼치는 헬퍼(테스트 검증용).
    private static HashSet<int> Expand(string ranges)
    {
        var set = new HashSet<int>();
        foreach (var part in ranges.Split(','))
        {
            string token = part.Trim();
            if (token.StartsWith("U+") || token.StartsWith("u+"))
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
                set.Add(System.Convert.ToInt32(token, 16));
                continue;
            }

            int lo = System.Convert.ToInt32(token.Substring(0, dash), 16);
            int hi = System.Convert.ToInt32(token.Substring(dash + 1), 16);
            for (int c = lo; c <= hi; c++)
            {
                set.Add(c);
            }
        }

        return set;
    }

    // =====================================================
    // 블록 완성 규칙: 한 글자 → 블록 전체
    // =====================================================

    [Test]
    public void Hangul_SingleChar_Preserves_WholeSyllableBlock()
    {
        // "가"(U+AC00) 한 글자 → 한글 음절 블록(AC00-D7A3) 전체 보존
        var blocks = AITFontUnicodeBlocks.ExpandToBlocks(new[] { 0xAC00 });

        var match = blocks.Find(b => b.Start == 0xAC00 && b.End == 0xD7A3);
        Assert.AreEqual("Hangul Syllables", match.Name,
            "한글 1자 → 한글 음절 블록 전체가 보존되어야 함(동적 닉네임/채팅 보호)");
    }

    [Test]
    public void LatinExtended_SingleChar_Preserves_WholeBlock()
    {
        // "ā"(U+0101) 한 글자 → Latin Extended-A(0100-017F) 전체 보존
        var blocks = AITFontUnicodeBlocks.ExpandToBlocks(new[] { 0x0101 });

        var match = blocks.Find(b => b.Start == 0x0100 && b.End == 0x017F);
        Assert.AreEqual("Latin Extended-A", match.Name,
            "라틴 확장 1자 → 해당 블록 전체가 보존되어야 함");
    }

    [Test]
    public void Hiragana_SingleChar_Preserves_WholeBlock()
    {
        var blocks = AITFontUnicodeBlocks.ExpandToBlocks(new[] { 0x3042 }); // あ
        var match = blocks.Find(b => b.Start == 0x3040 && b.End == 0x309F);
        Assert.AreEqual("Hiragana", match.Name);
    }

    // =====================================================
    // Han 예외: 한자는 블록 완성 미적용
    // =====================================================

    [Test]
    public void Han_SingleChar_DoesNotExpand_ToWholeBlock()
    {
        // "韓"(U+97D3) → CJK Unified Ideographs 블록 전체는 보존하지 않음
        var blocks = AITFontUnicodeBlocks.ExpandToBlocks(new[] { 0x97D3 });
        Assert.IsFalse(blocks.Exists(b => b.IsHan),
            "한자는 블록 전체(2만+자)가 아니라 글자 자체 + KS X 1001 패드만 보존되어야 함");
    }

    [Test]
    public void IsHan_Identifies_CjkUnifiedIdeographs()
    {
        Assert.IsTrue(AITFontUnicodeBlocks.IsHan(0x4E00));
        Assert.IsTrue(AITFontUnicodeBlocks.IsHan(0x9FFF));
        Assert.IsFalse(AITFontUnicodeBlocks.IsHan(0xAC00)); // 한글은 한자 아님
    }

    // =====================================================
    // 범위 문자열 포맷팅
    // =====================================================

    [Test]
    public void FormatRanges_Compresses_ContiguousRuns()
    {
        // {0x41,0x42,0x43,0x61} → "U+0041-0043,U+0061"
        string s = AITFontUnicodeBlocks.FormatRanges(new[] { 0x41, 0x42, 0x43, 0x61 });
        Assert.AreEqual("U+0041-0043,U+0061", s);
    }

    [Test]
    public void FormatRanges_Dedups_And_Sorts()
    {
        string s = AITFontUnicodeBlocks.FormatRanges(new[] { 0x61, 0x41, 0x41, 0x42 });
        Assert.AreEqual("U+0041-0042,U+0061", s);
    }

    [Test]
    public void FormatRanges_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AITFontUnicodeBlocks.FormatRanges(new int[0]));
    }

    [Test]
    public void FormatCodepoint_PadsToFourHexDigits()
    {
        Assert.AreEqual("U+0041", AITFontUnicodeBlocks.FormatCodepoint(0x41));
        Assert.AreEqual("U+AC00", AITFontUnicodeBlocks.FormatCodepoint(0xAC00));
        Assert.AreEqual("U+1F600", AITFontUnicodeBlocks.FormatCodepoint(0x1F600));
    }

    // =====================================================
    // 베이스라인 항시 포함
    // =====================================================

    [Test]
    public void BuildPreservedRanges_AlwaysIncludes_Baseline_EvenWithNoDetection()
    {
        // 감지 문자가 전혀 없어도 베이스라인(ASCII/한글 음절/CJK 기호/전각)은 항상 포함
        string ranges = AITFontUsedCharScanner.BuildPreservedRanges(
            new int[0], new int[0], out _);
        var cps = Expand(ranges);

        Assert.IsTrue(cps.Contains(0x0041), "ASCII 'A' 는 항상 포함");
        Assert.IsTrue(cps.Contains(0xAC00), "한글 '가' 는 베이스라인으로 항상 포함");
        Assert.IsTrue(cps.Contains(0x3000), "CJK 기호 베이스라인 항상 포함");
        Assert.IsTrue(cps.Contains(0xFF01), "전각 베이스라인 항상 포함");
    }

    [Test]
    public void BuildPreservedRanges_DetectedScript_ExpandsToWholeBlock()
    {
        // 키릴 한 글자(U+0410) 감지 → 키릴 블록 전체(0400-04FF) 보존
        string ranges = AITFontUsedCharScanner.BuildPreservedRanges(
            new[] { 0x0410 }, new int[0], out var blocks);
        var cps = Expand(ranges);

        Assert.IsTrue(cps.Contains(0x0400), "키릴 1자 감지 → 블록 시작(0400) 보존");
        Assert.IsTrue(cps.Contains(0x04FF), "키릴 1자 감지 → 블록 끝(04FF) 보존");
        Assert.IsTrue(blocks.Exists(b => b.Name == "Cyrillic"), "리포트에 Cyrillic 블록 포함");
    }

    [Test]
    public void BuildPreservedRanges_IncludesDetectedHan_And_HanPad()
    {
        // 감지된 한자 자체 + Han 패드는 보존하되 블록 전체로 펼치지 않음
        string ranges = AITFontUsedCharScanner.BuildPreservedRanges(
            new[] { 0x97D3 }, new[] { 0x4E00, 0x4E01 }, out var blocks);
        var cps = Expand(ranges);

        Assert.IsTrue(cps.Contains(0x97D3), "감지된 한자 자체는 보존");
        Assert.IsTrue(cps.Contains(0x4E00), "Han 패드 한자 보존");
        Assert.IsTrue(cps.Contains(0x4E01), "Han 패드 한자 보존");
        Assert.IsFalse(cps.Contains(0x5000), "패드/감지 외 한자는 보존하지 않음(블록 전체 미적용)");
        Assert.IsFalse(blocks.Exists(b => b.IsHan), "한자 블록은 리포트에 블록 완성으로 포함되지 않음");
    }

    // =====================================================
    // KS X 1001 Han 패드 도출
    // =====================================================

    [Test]
    public void Ksx1001Han_WhenAvailable_ContainsCommonHanja()
    {
        var han = AITFontUsedCharScanner.GetKsx1001Han();

        // EUC-KR(949) 인코딩이 등록된 환경이면 약 4,888자가 도출됨.
        // 미등록(.NET 환경 차이)이면 빈 목록으로 graceful degrade — 그 경우는 검증을 건너뛴다.
        if (han.Count == 0)
        {
            Assert.Pass("EUC-KR(949) 미등록 환경 — 감지된 한자만 보존(graceful degrade). 검증 생략.");
            return;
        }

        Assert.GreaterOrEqual(han.Count, 4000,
            "KS X 1001 상용 한자는 약 4,888자여야 함");
        foreach (var cp in han)
        {
            Assert.IsTrue(AITFontUnicodeBlocks.IsHan(cp),
                $"KS X 1001 패드의 모든 코드포인트는 한자 영역이어야 함: U+{cp:X4}");
        }
    }

    // =====================================================
    // 사용 문자 스캐너: 비ASCII 수집 / 서로게이트 처리
    // =====================================================

    [Test]
    public void CollectNonAscii_Collects_OnlyNonAscii()
    {
        var sink = new HashSet<int>();
        AITFontUsedCharScanner.CollectNonAscii("Hello 가나다!", sink);

        Assert.IsFalse(sink.Contains('H'), "ASCII 는 베이스라인이 책임지므로 수집 제외");
        Assert.IsTrue(sink.Contains(0xAC00), "'가' 수집");
        Assert.IsTrue(sink.Contains(0xB098), "'나' 수집");
        Assert.IsTrue(sink.Contains(0xB2E4), "'다' 수집");
    }

    [Test]
    public void CollectNonAscii_Handles_SurrogatePairs()
    {
        var sink = new HashSet<int>();
        // U+1F600 (😀) 은 서로게이트 쌍 — UTF-32 단일 코드포인트로 합쳐 수집되어야 함
        AITFontUsedCharScanner.CollectNonAscii("emoji \U0001F600 here", sink);

        Assert.IsTrue(sink.Contains(0x1F600), "서로게이트 쌍을 합친 코드포인트로 수집");
    }

    // =====================================================
    // 백슬래시 이스케이프 디코드: Unity YAML/C#/JSON 은 비ASCII 를 \uXXXX 로 직렬화
    // (회귀 방지: I2Languages.asset 처럼 CJK 를 \uXXXX 로 저장하면 원시 문자만 봐선 전부 누락됨)
    // =====================================================

    [Test]
    public void CollectNonAscii_Decodes_UnicodeEscape_uXXXX()
    {
        var sink = new HashSet<int>();
        // verbatim(@) 이라 백슬래시가 그대로 — 런타임 문자열은 "一"(6문자) → 一(U+4E00) 수집
        AITFontUsedCharScanner.CollectNonAscii(@"prefix \u4E00 suffix", sink);

        Assert.IsTrue(sink.Contains(0x4E00), "\\uXXXX 이스케이프를 코드포인트로 디코드");
    }

    [Test]
    public void CollectNonAscii_Decodes_MultipleEscapes_InQuotedString()
    {
        var sink = new HashSet<int>();
        // I2Languages.asset 의 실제 저장 형태: "抽抽防御" (抽抽防御)
        AITFontUsedCharScanner.CollectNonAscii("      - \"\\u62BD\\u62BD\\u9632\\u5FA1\"", sink);

        Assert.IsTrue(sink.Contains(0x62BD), "연속 이스케이프 첫 글자(抽) 수집");
        Assert.IsTrue(sink.Contains(0x9632), "연속 이스케이프 셋째 글자(防) 수집");
        Assert.IsTrue(sink.Contains(0x5FA1), "연속 이스케이프 넷째 글자(御) 수집");
    }

    [Test]
    public void CollectNonAscii_Decodes_HexEscape_xXX()
    {
        var sink = new HashSet<int>();
        // \xE9 = é(U+00E9) — 프랑스어 텍스트가 이 형태로 저장됨(YAML double-quoted 스칼라)
        AITFontUsedCharScanner.CollectNonAscii(@"r\xE9clamer", sink);

        Assert.IsTrue(sink.Contains(0xE9), "\\xXX(2 hex) 이스케이프를 디코드");
    }

    [Test]
    public void CollectNonAscii_Decodes_SurrogatePair_FromTwoEscapes()
    {
        var sink = new HashSet<int>();
        // 😀 (상위+하위 서로게이트 이스케이프) → 😀(U+1F600) 로 합성
        AITFontUsedCharScanner.CollectNonAscii(@"emoji \uD83D\uDE00 end", sink);

        Assert.IsTrue(sink.Contains(0x1F600), "두 \\uXXXX 서로게이트를 astral 코드포인트로 합성");
        Assert.IsFalse(sink.Contains(0xD83D), "낱개 상위 서로게이트는 수집하지 않음");
        Assert.IsFalse(sink.Contains(0xDE00), "낱개 하위 서로게이트는 수집하지 않음");
    }

    [Test]
    public void CollectNonAscii_Decodes_EightDigitEscape_UXXXXXXXX()
    {
        var sink = new HashSet<int>();
        // \U0001F600 (8 hex) → 😀(U+1F600)
        AITFontUsedCharScanner.CollectNonAscii(@"emoji \U0001F600 end", sink);

        Assert.IsTrue(sink.Contains(0x1F600), "\\UXXXXXXXX(8 hex) 이스케이프를 디코드");
    }

    [Test]
    public void CollectNonAscii_IgnoresEscapedBackslash_NotAnEscape()
    {
        var sink = new HashSet<int>();
        // 리터럴 "\\u4E00" = 이스케이프된 백슬래시 + "u4E00" 텍스트 → 一 을 수집하면 안 됨
        AITFontUsedCharScanner.CollectNonAscii(@"path\\u4E00text", sink);

        Assert.IsFalse(sink.Contains(0x4E00), "이스케이프된 백슬래시(\\\\) 뒤 텍스트는 이스케이프로 오인하지 않음");
    }

    [Test]
    public void CollectNonAscii_Malformed_Escape_DoesNotThrow_OrOverCollect()
    {
        var sink = new HashSet<int>();
        // hex 부족/비hex — 예외 없이 무시(원시 ASCII 로 취급되어 아무것도 수집 안 함)
        Assert.DoesNotThrow(() => AITFontUsedCharScanner.CollectNonAscii(@"bad \u4E end \uZZZZ \x", sink));
        Assert.IsFalse(sink.Contains(0x4E00), "불완전한 \\u4E 는 디코드하지 않음");
    }

    [Test]
    public void CollectNonAscii_StillCollects_RawNonAscii_AlongsideEscapes()
    {
        var sink = new HashSet<int>();
        // 원시 CJK 와 이스케이프가 섞여도 둘 다 수집(기존 동작 회귀 방지)
        AITFontUsedCharScanner.CollectNonAscii("가나 \\u4E00 " + "あ", sink);

        Assert.IsTrue(sink.Contains(0xAC00), "원시 '가' 수집(기존 동작 유지)");
        Assert.IsTrue(sink.Contains(0x4E00), "이스케이프 一 수집");
        Assert.IsTrue(sink.Contains(0x3042), "원시 'あ' 수집");
    }

    // =====================================================
    // ★ 드롭 버그 회귀 방지: 감지된 코드포인트는 블록 등재 여부와 무관하게 항상 보존
    // (테이블에 없는 문자체계라도 '감지된 글자'는 절대 드롭되지 않아야 함 — tofu 방지 최저선)
    // =====================================================

    [Test]
    public void BuildPreservedRanges_PreservesDetectedCodepoint_EvenWhenBlockNotTabled()
    {
        // U+16A0 (Runic) 은 블록 테이블에 없음 → 블록 완성은 안 되지만 감지된 글자 자체는 보존되어야 함.
        const int runic = 0x16A0;
        Assert.IsFalse(AITFontUnicodeBlocks.TryFindBlock(runic, out _),
            "전제: Runic 은 블록 테이블에 없어야 이 테스트가 드롭 경로를 검증함");

        string ranges = AITFontUsedCharScanner.BuildPreservedRanges(
            new[] { runic }, new int[0], out var blocks);
        var cps = Expand(ranges);

        Assert.IsTrue(cps.Contains(runic),
            "미등재 블록의 감지 글자도 raw 로 보존되어야 함(드롭 버그 회귀 방지)");
        Assert.IsFalse(blocks.Exists(b => b.Contains(runic)),
            "미등재 블록은 블록 완성 리포트에 포함되지 않음(글자 자체만 보존)");
    }

    [Test]
    public void ExpandToBlocks_UntabledCodepoint_ReturnsNoBlock_ButCallerPreservesRaw()
    {
        // 계약 분리 검증: ExpandToBlocks 는 미등재 코드포인트에 블록을 만들지 않지만(블록 완성 미적용),
        // BuildPreservedRanges 는 그 글자를 raw 로 무조건 보존한다.
        const int pua = 0xE000; // Private Use Area — 어떤 블록에도 없음
        var blocks = AITFontUnicodeBlocks.ExpandToBlocks(new[] { pua });
        Assert.IsEmpty(blocks, "ExpandToBlocks 는 미등재 코드포인트에 블록을 만들지 않아야 함");

        var cps = Expand(AITFontUsedCharScanner.BuildPreservedRanges(new[] { pua }, new int[0], out _));
        Assert.IsTrue(cps.Contains(pua), "호출부(BuildPreservedRanges)가 미등재 감지 글자를 raw 로 보존");
    }

    [Test]
    public void BuildPreservedRanges_MixedTabledAndUntabled_BothPreserved()
    {
        // 등재(키릴 U+0410) + 미등재(Runic U+16A0) 혼재 → 키릴은 블록 완성, Runic 은 raw 보존.
        string ranges = AITFontUsedCharScanner.BuildPreservedRanges(
            new[] { 0x0410, 0x16A0 }, new int[0], out _);
        var cps = Expand(ranges);

        Assert.IsTrue(cps.Contains(0x0400) && cps.Contains(0x04FF), "키릴은 블록 전체 보존");
        Assert.IsTrue(cps.Contains(0x16A0), "미등재 Runic 은 감지 글자 raw 보존");
    }

    // =====================================================
    // 블록 테이블 확장: 주요 생존 문자체계 + 국기/게임 이모지의 동적 텍스트 커버
    // =====================================================

    [Test]
    public void ExpandedTable_Covers_MajorLivingScripts()
    {
        AssertBlockCompletes(0x0995, "Bengali");   // ক
        AssertBlockCompletes(0x0B95, "Tamil");     // க
        AssertBlockCompletes(0x10D0, "Georgian");  // ა
        AssertBlockCompletes(0x0E01, "Thai");      // ก (기존 유지 확인)
        AssertBlockCompletes(0x1780, "Khmer");     // ក
        AssertBlockCompletes(0x0F40, "Tibetan");   // ཀ
        AssertBlockCompletes(0x1000, "Myanmar");   // က
    }

    [Test]
    public void ExpandedTable_Covers_FlagAndGameEmoji()
    {
        // 국기 이모지 Regional Indicator(U+1F1E6-1F1FF) → Enclosed Alphanumeric Supplement 블록.
        AssertBlockCompletes(0x1F1F0, "Enclosed Alphanumeric Supplement");
        AssertBlockCompletes(0x1F0A1, "Playing Cards");
        AssertBlockCompletes(0x1F004, "Mahjong Tiles");
    }

    private static void AssertBlockCompletes(int detectedCp, string expectedBlockName)
    {
        var blocks = AITFontUnicodeBlocks.ExpandToBlocks(new[] { detectedCp });
        Assert.IsTrue(blocks.Exists(b => b.Name == expectedBlockName && b.Contains(detectedCp)),
            $"U+{detectedCp:X4} 감지 → '{expectedBlockName}' 블록 전체가 보존되어야 함(동적 텍스트 커버)");
    }
}
