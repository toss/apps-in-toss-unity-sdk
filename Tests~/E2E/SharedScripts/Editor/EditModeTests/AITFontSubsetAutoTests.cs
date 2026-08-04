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
    public void BuildPreservedRanges_AlwaysIncludes_HangulCompatibilityJamo()
    {
        // 호환 자모(U+3130-318F): ㅋㅋ/ㅠㅠ/ㅇㅇ 같은 낱자모 전용 동적 텍스트가 자동 모드에서도
        // 스캔 결과와 무관하게 항상 보존되어야 함(회귀 방지: 서버발 동적 텍스트).
        string ranges = AITFontUsedCharScanner.BuildPreservedRanges(
            new int[0], new int[0], out _);
        var cps = Expand(ranges);

        Assert.IsTrue(cps.Contains(0x314B), "ㅋ(U+314B, 호환 자모) 는 베이스라인으로 항상 포함");
        Assert.IsTrue(cps.Contains(0x3160), "ㅠ(U+3160, 호환 자모) 는 베이스라인으로 항상 포함");
        Assert.IsTrue(cps.Contains(0x3147), "ㅇ(U+3147, 호환 자모) 는 베이스라인으로 항상 포함");
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

    // =====================================================
    // AITFontSubsetLanguages: 동적 텍스트 언어 선택 → 유니코드 범위
    // =====================================================

    [Test]
    public void LanguageTable_Japanese_CoversHiraganaKatakanaHan()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("ja"));
        Assert.IsTrue(cps.Contains(0x3041), "ja 선택 → ぁ(U+3041, 히라가나) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x30A2), "ja 선택 → ア(U+30A2, 가타카나) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x4E00), "ja 선택 → 一(U+4E00, 한자) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x3001), "ja 선택 → CJK 문장부호(U+3001, 、) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0xFF21), "ja 선택 → 전각(U+FF21, Ａ) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_Japanese_ExcludesCjkExtensionA()
    {
        // 일본어 상용에 사실상 불필요한 CJK Ext-A(U+3400-4DBF)는 절감 우선으로 제외됨.
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("ja"));
        Assert.IsFalse(cps.Contains(0x3400), "ja 선택 → CJK Ext-A(U+3400)는 포함되지 않아야 함");
    }

    [Test]
    public void LanguageTable_Thai_CoversThaiBlock()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("th"));
        Assert.IsTrue(cps.Contains(0x0E01), "th 선택 → ก(U+0E01) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_Emoji_CoversEmoticonBlock()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("emoji"));
        Assert.IsTrue(cps.Contains(0x1F600), "emoji 선택 → U+1F600(웃는 얼굴) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_Emoji_CoversExtendedSymbolsAndDingbats()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("emoji"));
        Assert.IsTrue(cps.Contains(0x2122), "emoji 선택 → ™(U+2122) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x20E3), "emoji 선택 → 키캡(U+20E3) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x2190), "emoji 선택 → 화살표(U+2190) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x231A), "emoji 선택 → ⌚(U+231A) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x25B6), "emoji 선택 → ▶(U+25B6) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x2B50), "emoji 선택 → ⭐(U+2B50) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x1F191), "emoji 선택 → 알파벳 사각(U+1F191) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x1F7E0), "emoji 선택 → 원형 기호(U+1F7E0) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_ChineseSimplified_CoversHan()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("zh-Hans"));
        Assert.IsTrue(cps.Contains(0x4E00), "zh-Hans 선택 → 一(U+4E00) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_ChineseTraditional_CoversBopomofoAndHan()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("zh-Hant"));
        Assert.IsTrue(cps.Contains(0x3105), "zh-Hant 선택 → ㄅ(U+3105, 주음부호) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x4E00), "zh-Hant 선택 → 一(U+4E00) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_Russian_CoversCyrillicBlock()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("ru"));
        Assert.IsTrue(cps.Contains(0x0410), "ru 선택 → А(U+0410) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_LatinExt_CoversEuropeanAndVietnamese()
    {
        // latin-ext 는 유럽 닉네임(ł/š/ő 등, Latin Extended-A/B)과 베트남어 성조 부호(Latin Extended
        // Additional)를 하나의 태그로 커버한다(vi 태그는 제거되고 latin-ext 로 흡수됨).
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("latin-ext"));
        Assert.IsTrue(cps.Contains(0x0142), "latin-ext 선택 → ł(U+0142, Latin Extended-A) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x1EA0), "latin-ext 선택 → 베트남어 성조(U+1EA0) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x20AB), "latin-ext 선택 → 동화 기호(U+20AB) 보존되어야 함");
    }

    [Test]
    public void LanguageTable_Vietnamese_TagRemoved_TreatedAsUnknown()
    {
        // vi 태그는 제거되었으므로 미지 태그로 취급되어 무시된다(BuildRanges_UnknownTag 계약과 동일).
        Assert.AreEqual(string.Empty, AITFontSubsetLanguages.BuildRanges("vi"),
            "vi 태그는 제거되어 미지 태그로 무시되어야 함");
    }

    [Test]
    public void LanguageTable_Arabic_CoversArabicBlock()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("ar"));
        Assert.IsTrue(cps.Contains(0x0627), "ar 선택 → ا(U+0627) 보존되어야 함");
        Assert.IsTrue(cps.Contains(0x200C), "ar 선택 → ZWNJ(U+200C) 보존되어야 함");
    }

    [Test]
    public void BuildRanges_MultipleTags_UnionsAllSelected()
    {
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("ja,th"));
        Assert.IsTrue(cps.Contains(0x3041), "다중 태그 결합 → 일본어 범위 포함");
        Assert.IsTrue(cps.Contains(0x0E01), "다중 태그 결합 → 태국어 범위 포함");
    }

    [Test]
    public void BuildRanges_AlwaysIncludedTags_AreIgnored()
    {
        string ranges = AITFontSubsetLanguages.BuildRanges("ko,la");
        Assert.AreEqual(string.Empty, ranges,
            "ko/la 는 AlwaysIncluded(베이스라인이 이미 보존) → BuildRanges 결과에 포함되지 않아야 함");
    }

    [Test]
    public void BuildRanges_UnknownTag_IsIgnoredWithoutAffectingResult()
    {
        string ranges = AITFontSubsetLanguages.BuildRanges("xx-unknown,th");
        var cps = Expand(ranges);
        Assert.IsTrue(cps.Contains(0x0E01), "미지 태그는 무시하되 나머지 태그는 정상 반영되어야 함");
    }

    [Test]
    public void BuildRanges_EmptyInput_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, AITFontSubsetLanguages.BuildRanges(""));
        Assert.AreEqual(string.Empty, AITFontSubsetLanguages.BuildRanges(null));
    }

    [Test]
    public void BuildRanges_DuplicateTags_AreAllowed()
    {
        // 중복 태그가 있어도 예외 없이 동일한 union 결과를 반환해야 함.
        var cps = Expand(AITFontSubsetLanguages.BuildRanges("th,th,th"));
        Assert.IsTrue(cps.Contains(0x0E01), "중복 태그 → 예외 없이 정상 union 결과 반환");
    }

    [Test]
    public void BuildRanges_JaAndZhHans_DedupsSharedRangeToken_OutputsOnce()
    {
        // ja 와 zh-Hans 는 U+4E00-9FFF·U+3000-303F·U+FF00-FFEF 토큰을 공유한다.
        // 태그 중복 제거뿐 아니라 범위 토큰 자체도 중복 없이 1회만 출력되어야 한다.
        string ranges = AITFontSubsetLanguages.BuildRanges("ja,zh-Hans");
        var tokens = ranges.Split(',');

        int hanCount = 0;
        int punctCount = 0;
        foreach (var token in tokens)
        {
            if (token == "U+4E00-9FFF") hanCount++;
            if (token == "U+3000-303F") punctCount++;
        }

        Assert.AreEqual(1, hanCount, "ja+zh-Hans 동시 선택 시 공유 한자 범위(U+4E00-9FFF)는 1회만 출력되어야 함");
        Assert.AreEqual(1, punctCount, "ja+zh-Hans 동시 선택 시 공유 CJK 문장부호 범위(U+3000-303F)는 1회만 출력되어야 함");

        var cps = Expand(ranges);
        Assert.IsTrue(cps.Contains(0x4E00), "한자 범위는 여전히 보존됨");
        Assert.IsTrue(cps.Contains(0x3041), "ja 고유 범위(히라가나)도 보존됨");
    }

    // =====================================================
    // AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection: 진리표
    // =====================================================

    [Test]
    public void ShouldSkipAuto_AllEmptyAndAutoMode_ReturnsTrue()
    {
        Assert.IsTrue(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "", "", "", "", ""),
            "자동 모드에서 언어·범위·대상·제외경로가 전부 비어 있으면 인지된 선택이 없으므로 건너뛰어야 함");
    }

    [Test]
    public void ShouldSkipAuto_LanguagesOnly_ReturnsFalse()
    {
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "ja", "", "", "", ""),
            "언어가 선택되었으면 인지된 활성화이므로 건너뛰지 않아야 함");
    }

    [Test]
    public void ShouldSkipAuto_ExtraRangesOnly_ReturnsFalse()
    {
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "", "", "U+0E00-0E7F", "", ""),
            "추가 보존 범위가 지정되었으면 건너뛰지 않아야 함");
    }

    [Test]
    public void ShouldSkipAuto_TargetPathsOnly_ReturnsFalse()
    {
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "", "", "", "Assets/Fonts/A.ttf", ""),
            "대상 폰트 경로가 지정되었으면 건너뛰지 않아야 함");
    }

    [Test]
    public void ShouldSkipAuto_ManualUnicodeRangesOnly_ReturnsFalse()
    {
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "", "U+0E00-0E7F", "", "", ""),
            "수동 보존 범위가 지정되었으면 건너뛰지 않아야 함");
    }

    [Test]
    public void ShouldSkipAuto_ExcludeTargetPathsOnly_ReturnsFalse()
    {
        // 제외 경로만 설정한 기존 사용자도 subset 의 존재와 위험을 인지·조정한 것이므로 게이팅하지 않는다.
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "", "", "", "", "Assets/Fonts/Fallback.ttf"),
            "제외 대상 경로가 지정되었으면 인지된 활성화로 보아 건너뛰지 않아야 함");
    }

    [Test]
    public void ShouldSkipAuto_ExplicitOn_AllEmpty_ReturnsFalse()
    {
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(1, "", "", "", "", ""),
            "fontSubset==1(명시 활성)이면 전부 비어 있어도 기존 동작(스캔 단독 실행)을 허용해야 함");
    }

    [Test]
    public void ShouldSkipAuto_Disabled_ReturnsFalse()
    {
        // fontSubset==0 은 호출부(ApplyForBuild)에서 이미 no-op 처리되어 게이팅과 무관하지만,
        // 헬퍼 자체는 -1 이 아니면 항상 false 를 반환해야 함(안전한 순수 함수 계약).
        Assert.IsFalse(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(0, "", "", "", "", ""),
            "fontSubset==0 은 -1 이 아니므로 게이팅 대상이 아니어야 함(호출부에서 이미 비활성 처리)");
    }

    [Test]
    public void ShouldSkipAuto_WhitespaceOnlyFields_TreatedAsEmpty()
    {
        Assert.IsTrue(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(-1, "   ", " ", "\t", "", "  "),
            "공백만 있는 필드는 trim 후 빈 값으로 취급되어야 함");
    }

    // =====================================================
    // AITFontSubsetLanguages.LazyEligible: AlwaysIncluded(ko/la) 제외 전부 true
    // =====================================================

    [Test]
    public void LazyEligible_AlwaysIncludedEntries_AreFalse()
    {
        foreach (var entry in AITFontSubsetLanguages.Table)
        {
            if (entry.AlwaysIncluded)
            {
                Assert.IsFalse(entry.LazyEligible,
                    $"'{entry.Tag}' 는 AlwaysIncluded(부트 폰트 필수 포함)이므로 LazyEligible 이 false 여야 함");
            }
        }
    }

    [Test]
    public void LazyEligible_NonAlwaysIncludedEntries_AreAllTrue()
    {
        foreach (var entry in AITFontSubsetLanguages.Table)
        {
            if (!entry.AlwaysIncluded)
            {
                Assert.IsTrue(entry.LazyEligible,
                    $"'{entry.Tag}' 는 AlwaysIncluded 가 아니므로 LazyEligible 이 true 여야 함(계약: " +
                    "AlwaysIncluded 를 제외한 모든 태그가 true)");
            }
        }
    }

    [Test]
    public void LazyEligible_KoAndLa_AreFalse()
    {
        // 계약 문구를 태그 단위로도 직접 고정: ko/la 는 항상 부트 폰트에 포함되어야 하므로 lazy 대상이 아니다.
        Assert.IsTrue(AITFontSubsetLanguages.TryFindEntry("ko", out var ko));
        Assert.IsTrue(AITFontSubsetLanguages.TryFindEntry("la", out var la));
        Assert.IsFalse(ko.LazyEligible, "ko 는 항상 부트 폰트에 포함되어야 하므로 LazyEligible=false");
        Assert.IsFalse(la.LazyEligible, "la 는 항상 부트 폰트에 포함되어야 하므로 LazyEligible=false");
    }

    [Test]
    public void LazyEligible_KnownEligibleTags_AreTrue()
    {
        // 계약이 열거한 8개 태그를 명시적으로 고정(테이블에 항목이 추가/누락되면 이 테스트가 즉시 감지).
        foreach (var tag in new[] { "ja", "zh-Hans", "zh-Hant", "ru", "th", "latin-ext", "ar", "emoji" })
        {
            Assert.IsTrue(AITFontSubsetLanguages.TryFindEntry(tag, out var entry), $"'{tag}' 가 테이블에 있어야 함");
            Assert.IsTrue(entry.LazyEligible, $"'{tag}' 는 LazyEligible=true 여야 함");
        }
    }

    // =====================================================
    // AITFontLazyExtensionBuilder.SplitLazyAndBootTags: lazySet/bootTags 분할
    // =====================================================

    [Test]
    public void SplitLazyAndBootTags_MixedSelection_SplitsByLazyEligible()
    {
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags(
            "ja,ru,th", out List<string> lazyTags, out List<string> bootTags);

        CollectionAssert.AreEquivalent(new[] { "ja", "ru", "th" }, lazyTags,
            "ja/ru/th 는 전부 LazyEligible 이므로 lazyTags 로 분류되어야 함");
        Assert.AreEqual(0, bootTags.Count, "LazyEligible 아닌 태그가 없으므로 bootTags 는 비어야 함");
    }

    [Test]
    public void SplitLazyAndBootTags_KoLa_AlwaysGoesToBootTags()
    {
        // 계약: bootTags = 선택 언어 − lazySet. ko/la 는 LazyEligible=false 이므로 선택되어도 항상 bootTags.
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags(
            "ko,la,ja", out List<string> lazyTags, out List<string> bootTags);

        CollectionAssert.AreEquivalent(new[] { "ja" }, lazyTags);
        CollectionAssert.AreEquivalent(new[] { "ko", "la" }, bootTags);
    }

    [Test]
    public void SplitLazyAndBootTags_UnknownTag_SafelyGoesToBootTags()
    {
        // 미지 태그는 lazy 시도 대상이 될 수 없으므로(테이블에 범위가 없음) 안전하게 boot 로 분류된다
        // (boot union 에 있어도 BuildRanges 가 미지 태그를 무시하므로 무해 — 기존 관용구와 동일).
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags(
            "ja,unknown-tag", out List<string> lazyTags, out List<string> bootTags);

        CollectionAssert.AreEquivalent(new[] { "ja" }, lazyTags);
        CollectionAssert.AreEquivalent(new[] { "unknown-tag" }, bootTags);
    }

    [Test]
    public void SplitLazyAndBootTags_DuplicateTags_DedupsToSingleEntry()
    {
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags(
            "ja,ja,ko,ko", out List<string> lazyTags, out List<string> bootTags);

        Assert.AreEqual(1, lazyTags.Count, "중복 태그는 첫 등장만 반영되어야 함(ja)");
        Assert.AreEqual(1, bootTags.Count, "중복 태그는 첫 등장만 반영되어야 함(ko)");
    }

    [Test]
    public void SplitLazyAndBootTags_EmptyOrNullCsv_ReturnsBothEmpty()
    {
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags(string.Empty, out List<string> lazyTagsEmpty, out List<string> bootTagsEmpty);
        Assert.AreEqual(0, lazyTagsEmpty.Count);
        Assert.AreEqual(0, bootTagsEmpty.Count);

        AITFontLazyExtensionBuilder.SplitLazyAndBootTags(null, out List<string> lazyTagsNull, out List<string> bootTagsNull);
        Assert.AreEqual(0, lazyTagsNull.Count);
        Assert.AreEqual(0, bootTagsNull.Count);
    }

    // =====================================================
    // AITFontLazyExtensionBuilder.JoinInTableOrder + 폴백(fallback-to-boot) 시뮬레이션
    // =====================================================

    [Test]
    public void JoinInTableOrder_OrdersByTableRegardlessOfInputOrder()
    {
        // Table 순서: ko, la, ja, zh-Hans, zh-Hant, ru, th, latin-ext, ar, emoji.
        string result = AITFontLazyExtensionBuilder.JoinInTableOrder(new[] { "th", "ja", "ko" });
        Assert.AreEqual("ko,ja,th", result, "입력 순서와 무관하게 Table 순서로 결정적 직렬화되어야 함");
    }

    [Test]
    public void JoinInTableOrder_DedupsDuplicateTags()
    {
        string result = AITFontLazyExtensionBuilder.JoinInTableOrder(new[] { "ja", "ja", "ko" });
        Assert.AreEqual("ko,ja", result, "중복 태그는 1회만 출력되어야 함");
    }

    [Test]
    public void JoinInTableOrder_EmptyInput_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, AITFontLazyExtensionBuilder.JoinInTableOrder(new List<string>()));
        Assert.AreEqual(string.Empty, AITFontLazyExtensionBuilder.JoinInTableOrder(null));
    }

    [Test]
    public void FallbackToBoot_FailedLazyTag_RestoredIntoBootUnion()
    {
        // 안전 불변식 시뮬레이션: "ja,ru" 선택 중 'ru' 의 lazy 확장이 실패했다고 가정하면, 호출부
        // (ApplyLazyExtensions)는 실패한 태그를 bootTags 로 되돌린 뒤 JoinInTableOrder 로 최종 boot
        // union CSV 를 만든다. 이 테스트는 그 재계산 로직이 실제 union 값에 반영됨을 순수 함수 조합으로 검증한다
        // (로그만 남기고 범위가 빠지는 구조가 아님을 보장).
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags("ja,ru", out List<string> lazyTags, out List<string> bootTags);
        Assert.AreEqual(2, lazyTags.Count, "사전조건: ja/ru 모두 처음엔 lazy 시도 대상");

        // 'ru' 의 lazy 확장이 실패했다고 가정 → 호출부와 동일하게 bootTags 로 되돌린다.
        bootTags.Add("ru");

        string bootUnionCsv = AITFontLazyExtensionBuilder.JoinInTableOrder(bootTags);
        Assert.AreEqual("ru", bootUnionCsv,
            "실패한 'ru' 는 boot union CSV 에 나타나야 하고, 성공한 'ja' 는 나타나지 않아야 함(lazy 로 분리됨)");

        var cps = Expand(AITFontSubsetLanguages.BuildRanges(bootUnionCsv));
        Assert.IsTrue(cps.SetEquals(Expand(AITFontSubsetLanguages.BuildRanges("ru"))),
            "폴백된 'ru' 의 boot union 범위는 'ru' 단독 BuildRanges 결과와 동일해야 함(1단계와 동등한 보존)");
    }

    [Test]
    public void FallbackToBoot_AllLazyTagsFail_BootUnionEqualsOriginalSelection()
    {
        // 전부 실패하는 극단 케이스: bootTags 가 원래 선택 전체와 동일해져야 한다(1단계와 완전히 동등).
        AITFontLazyExtensionBuilder.SplitLazyAndBootTags("ja,zh-Hans,ko", out List<string> lazyTags, out List<string> bootTags);
        foreach (var tag in lazyTags)
        {
            bootTags.Add(tag); // 전부 실패 가정.
        }

        string bootUnionCsv = AITFontLazyExtensionBuilder.JoinInTableOrder(bootTags);
        Assert.AreEqual("ko,ja,zh-Hans", bootUnionCsv,
            "전 lazy 태그가 실패하면 boot union 은 원래 선택(순서만 Table 기준으로 정규화)과 동일해야 함");
    }

    // =====================================================
    // AITFontLazyExtensionBuilder 매니페스트 병합(read-merge-write) 순수 로직
    // =====================================================

    [Test]
    public void ParseManifestJson_EmptyOrNullInput_ReturnsEmptyEntries()
    {
        var dtoEmpty = AITFontLazyExtensionBuilder.ParseManifestJson(string.Empty);
        Assert.IsNotNull(dtoEmpty.entries);
        Assert.AreEqual(0, dtoEmpty.entries.Length);

        var dtoNull = AITFontLazyExtensionBuilder.ParseManifestJson(null);
        Assert.IsNotNull(dtoNull.entries);
        Assert.AreEqual(0, dtoNull.entries.Length);
    }

    [Test]
    public void ParseManifestJson_MalformedInput_ReturnsEmptyEntriesWithoutThrowing()
    {
        Assert.DoesNotThrow(() =>
        {
            var dto = AITFontLazyExtensionBuilder.ParseManifestJson("{not valid json!!");
            Assert.IsNotNull(dto.entries);
        });
    }

    [Test]
    public void BuildEntryJson_EagerEntry_OmitsLazyFields()
    {
        var entry = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "abc123",
            bundle = "font-abc123.bundle",
            encoding = "br",
            fonts = new[] { "NotoSansKR SDF" },
            lazyTag = string.Empty,
            lazyRanges = string.Empty,
        };

        string json = AITFontLazyExtensionBuilder.BuildEntryJson(entry);
        StringAssert.DoesNotContain("lazyTag", json, "빈 lazyTag(기존 eager 엔트리)는 필드 자체가 생략되어야 함");
        StringAssert.DoesNotContain("lazyRanges", json);
    }

    [Test]
    public void BuildEntryJson_LazyEntry_IncludesLazyTagAndRanges()
    {
        Assert.IsTrue(AITFontSubsetLanguages.TryFindEntry("ja", out var jaEntry));
        var entry = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "lazy-ja",
            bundle = "lazy-ja-deadbeef.bundle.br",
            encoding = "br",
            fonts = new[] { "lazy_ja SDF" },
            lazyTag = "ja",
            lazyRanges = jaEntry.Ranges,
        };

        string json = AITFontLazyExtensionBuilder.BuildEntryJson(entry);
        StringAssert.Contains("\"lazyTag\":\"ja\"", json);
        StringAssert.Contains($"\"lazyRanges\":\"{jaEntry.Ranges}\"", json,
            "lazyRanges 는 언어 테이블 값 그대로 직렬화되어야 함(계약)");
    }

    [Test]
    public void BuildEntryJson_ThenParseManifestJson_RoundTripsLazyFields()
    {
        Assert.IsTrue(AITFontSubsetLanguages.TryFindEntry("th", out var thEntry));
        var entry = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "lazy-th",
            bundle = "lazy-th-cafebabe.bundle",
            encoding = string.Empty,
            fonts = new[] { "lazy_th SDF" },
            lazyTag = "th",
            lazyRanges = thEntry.Ranges,
        };

        string entryJson = AITFontLazyExtensionBuilder.BuildEntryJson(entry);
        string manifestJson = "{\"maxConcurrent\":2,\"entries\":[" + entryJson + "]}";

        var dto = AITFontLazyExtensionBuilder.ParseManifestJson(manifestJson);
        Assert.AreEqual(1, dto.entries.Length);
        Assert.AreEqual("th", dto.entries[0].lazyTag);
        Assert.AreEqual(thEntry.Ranges, dto.entries[0].lazyRanges,
            "라운드트립 후에도 lazyRanges 가 테이블 값과 정확히 일치해야 함");
    }

    [Test]
    public void MergeLazyEntries_PreservesEagerEntries_RegardlessOfNewLazyEntries()
    {
        var eager = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "eager-1", bundle = "font-eager.bundle", fonts = new[] { "Eager SDF" },
            lazyTag = string.Empty, lazyRanges = string.Empty,
        };
        var newLazy = new List<AITFontLazyExtensionBuilder.ManifestEntryDto>
        {
            new AITFontLazyExtensionBuilder.ManifestEntryDto
            {
                guid = "lazy-ja", bundle = "lazy-ja-x.bundle", fonts = new[] { "lazy_ja SDF" },
                lazyTag = "ja", lazyRanges = "U+3040-309F",
            },
        };

        var merged = AITFontLazyExtensionBuilder.MergeLazyEntries(new[] { eager }, newLazy);

        Assert.AreEqual(2, merged.Count);
        Assert.IsTrue(merged.Exists(e => e.guid == "eager-1"), "eager 엔트리는 항상 보존되어야 함");
        Assert.IsTrue(merged.Exists(e => e.lazyTag == "ja"), "새 lazy 엔트리가 추가되어야 함");
    }

    [Test]
    public void MergeLazyEntries_ReplacesSameTagLazyEntry_PreservesOtherTagLazyEntries()
    {
        var existingJa = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "lazy-ja", bundle = "lazy-ja-OLD.bundle", fonts = new[] { "lazy_ja SDF" },
            lazyTag = "ja", lazyRanges = "OLD_RANGE",
        };
        var existingRu = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "lazy-ru", bundle = "lazy-ru-x.bundle", fonts = new[] { "lazy_ru SDF" },
            lazyTag = "ru", lazyRanges = "U+0400-04FF",
        };
        var newJa = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "lazy-ja", bundle = "lazy-ja-NEW.bundle", fonts = new[] { "lazy_ja SDF" },
            lazyTag = "ja", lazyRanges = "NEW_RANGE",
        };

        var merged = AITFontLazyExtensionBuilder.MergeLazyEntries(
            new[] { existingJa, existingRu },
            new List<AITFontLazyExtensionBuilder.ManifestEntryDto> { newJa });

        Assert.AreEqual(2, merged.Count, "같은 태그('ja')는 교체되고 다른 태그('ru')는 보존되어 총 2건이어야 함");
        var mergedJa = merged.Find(e => e.lazyTag == "ja");
        Assert.AreEqual("lazy-ja-NEW.bundle", mergedJa.bundle, "'ja' 엔트리는 새 값으로 교체되어야 함");
        Assert.IsTrue(merged.Exists(e => e.lazyTag == "ru" && e.bundle == "lazy-ru-x.bundle"),
            "'ru' 는 이번 빌드에서 다시 만들지 않았으므로 기존 값 그대로 보존되어야 함");
    }

    [Test]
    public void MergeLazyEntries_NoNewLazyEntries_KeepsAllExisting()
    {
        var eager = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "eager-1", bundle = "font-eager.bundle", fonts = new[] { "Eager SDF" },
            lazyTag = string.Empty, lazyRanges = string.Empty,
        };
        var existingLazy = new AITFontLazyExtensionBuilder.ManifestEntryDto
        {
            guid = "lazy-ja", bundle = "lazy-ja-x.bundle", fonts = new[] { "lazy_ja SDF" },
            lazyTag = "ja", lazyRanges = "U+3040-309F",
        };

        var merged = AITFontLazyExtensionBuilder.MergeLazyEntries(new[] { eager, existingLazy }, null);

        Assert.AreEqual(2, merged.Count, "새 lazy 엔트리가 없으면 기존 전부(eager+lazy)가 보존되어야 함");
    }

    [Test]
    public void HasLazyArtifacts_NonExistentDirectory_ReturnsFalse()
    {
        Assert.IsFalse(AITFontLazyExtensionBuilder.HasLazyArtifacts(
            "Assets/__nonexistent_ait_lazy_probe_dir__"));
    }
}
