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
}
