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
}
