// -----------------------------------------------------------------------
// AITStreamingFontTests.cs - 런타임 폰트 lazy 확장 재수화(AITStreamingFont) 순수 로직 검증
// Level 0: 언어별 폰트를 lazy 로 로드하기 위한 결정 로직 회귀 테스트.
//   - ParseRanges      : "U+XXXX-YYYY,U+ZZZZ" lazyRanges 문자열 → (start,end) 구간 배열 파서
//                        (잘못된 토큰 = 그 언어가 영원히 감지 안 됨 — 핵심 가드)
//   - MatchPendingTags : text 내 문자(서로게이트 쌍 포함) ↔ pending 언어 범위 대조
//                        (경계 오판 = 문자가 등장했는데 로드가 트리거되지 않아 tofu 로 남음)
//   - Manifest/Entry JsonUtility 왕복 : lazyTag/lazyRanges 필드 포함 신규 포맷과, 필드가 없는
//                        구 매니페스트(하위호환) 양쪽 모두 파싱 가능해야 한다.
//   - TriggerLazyLoad/MaybeFinishLazy 인스턴스 리플렉션 테스트(R1) : B0(게이트 대기 중 유실 방지)의
//                        실제 카운터 조작 순서를 회귀 가드한다 — IsLazyFullyDrained 진리표만으로는
//                        TriggerLazyLoad 의 lazyOutstanding++ 누락이나 MaybeFinishLazy 의 판정 카운터
//                        치환(lazyInflight 로 되돌림) 을 잡지 못한다.
// Entry/Manifest 는 AITStreamingFont 내부 private nested struct 라 리플렉션으로 접근한다.
// 실제 번들 다운로드/이벤트 구독/코루틴 동시성은 브라우저(WebGL) 경로 의존이라 EditMode 로는 검증
// 불가 — E2E 가 부팅/재수화를 커버하고, 본 테스트는 그 외 순수 결정 로직을 결정론적으로 고정한다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss;

[TestFixture]
public class AITStreamingFontTests
{
    private static Type EntryType => typeof(AITStreamingFont).GetNestedType("Entry", BindingFlags.NonPublic);

    private static Type ManifestType => typeof(AITStreamingFont).GetNestedType("Manifest", BindingFlags.NonPublic);

    // =====================================================
    // ParseRanges — lazyRanges 문자열 파서
    // =====================================================

    [Test]
    public void ParseRanges_NullOrEmpty_ReturnsEmptyArray()
    {
        Assert.AreEqual(0, AITStreamingFont.ParseRanges(null).Length);
        Assert.AreEqual(0, AITStreamingFont.ParseRanges("").Length);
    }

    [Test]
    public void ParseRanges_SingleRangeToken_ParsesInclusiveBounds()
    {
        var ranges = AITStreamingFont.ParseRanges("U+3040-309F");
        Assert.AreEqual(1, ranges.Length);
        Assert.AreEqual(0x3040, ranges[0].Start);
        Assert.AreEqual(0x309F, ranges[0].End);
    }

    [Test]
    public void ParseRanges_MultipleTokens_ParsesAllInOrder()
    {
        var ranges = AITStreamingFont.ParseRanges("U+3040-309F,U+30A0-30FF,U+4E00-9FFF");
        Assert.AreEqual(3, ranges.Length);
        Assert.AreEqual(0x3040, ranges[0].Start);
        Assert.AreEqual(0x309F, ranges[0].End);
        Assert.AreEqual(0x30A0, ranges[1].Start);
        Assert.AreEqual(0x30FF, ranges[1].End);
        Assert.AreEqual(0x4E00, ranges[2].Start);
        Assert.AreEqual(0x9FFF, ranges[2].End);
    }

    [Test]
    public void ParseRanges_SingleCodepointToken_NoDash_ParsesAsPointRange()
    {
        // 이모지 테이블의 "U+200D", "U+2122" 등 대시 없는 단일 코드포인트 토큰.
        var ranges = AITStreamingFont.ParseRanges("U+200D");
        Assert.AreEqual(1, ranges.Length);
        Assert.AreEqual(0x200D, ranges[0].Start);
        Assert.AreEqual(0x200D, ranges[0].End);
    }

    [Test]
    public void ParseRanges_InvalidTokens_AreIgnored_ValidTokensSurvive()
    {
        // "XYZ"(U+ 접두사 없음), "U+GGGG"(비16진), "U+FFFF-0000"(역전 구간), ""(빈 토큰) 은 무시되고
        // 유효한 마지막 토큰만 반영되어야 한다 — 부분 실패가 전체 파싱을 무너뜨리지 않음.
        var ranges = AITStreamingFont.ParseRanges("XYZ,U+GGGG,U+FFFF-0000,,U+00AB-00CD");
        Assert.AreEqual(1, ranges.Length);
        Assert.AreEqual(0x00AB, ranges[0].Start);
        Assert.AreEqual(0x00CD, ranges[0].End);
    }

    [Test]
    public void ParseRanges_WhitespaceAroundTokens_IsTrimmed()
    {
        var ranges = AITStreamingFont.ParseRanges(" U+0020-007E , U+00A0-00FF ");
        Assert.AreEqual(2, ranges.Length);
        Assert.AreEqual(0x0020, ranges[0].Start);
        Assert.AreEqual(0x007E, ranges[0].End);
    }

    [Test]
    public void ParseRanges_CaseInsensitivePrefix_Accepted()
    {
        var ranges = AITStreamingFont.ParseRanges("u+3040-309f");
        Assert.AreEqual(1, ranges.Length);
        Assert.AreEqual(0x3040, ranges[0].Start);
        Assert.AreEqual(0x309F, ranges[0].End);
    }

    // =====================================================
    // MatchPendingTags — 문자 ↔ pending 언어 범위 대조
    // =====================================================

    [Test]
    public void MatchPendingTags_NullOrEmptyText_ReturnsEmpty()
    {
        var pending = OneTagRanges("ja", 0x3040, 0x309F);
        Assert.AreEqual(0, AITStreamingFont.MatchPendingTags(null, pending).Count);
        Assert.AreEqual(0, AITStreamingFont.MatchPendingTags("", pending).Count);
    }

    [Test]
    public void MatchPendingTags_NullOrEmptyPending_ReturnsEmpty()
    {
        Assert.AreEqual(0, AITStreamingFont.MatchPendingTags("hello", null).Count);
        Assert.AreEqual(
            0,
            AITStreamingFont.MatchPendingTags("hello", new Dictionary<string, AITStreamingFont.CodepointRange[]>()).Count);
    }

    [Test]
    public void MatchPendingTags_NoMatchingChar_ReturnsEmpty()
    {
        // 기본 라틴 텍스트는 ja(히라가나) 범위에 걸리지 않는다.
        var pending = OneTagRanges("ja", 0x3040, 0x309F);
        var matched = AITStreamingFont.MatchPendingTags("Hello World", pending);
        Assert.AreEqual(0, matched.Count);
    }

    [Test]
    public void MatchPendingTags_BmpCharInRange_MatchesTag()
    {
        // U+3042 'あ' 는 히라가나 범위(U+3040-309F) 안.
        var pending = OneTagRanges("ja", 0x3040, 0x309F);
        var matched = AITStreamingFont.MatchPendingTags("あ", pending);
        CollectionAssert.AreEquivalent(new[] { "ja" }, matched);
    }

    [Test]
    public void MatchPendingTags_RangeBoundaries_InclusiveOnBothEnds()
    {
        var pending = OneTagRanges("test", 0x0041, 0x005A); // 'A'-'Z'
        Assert.AreEqual(1, AITStreamingFont.MatchPendingTags("A", pending).Count, "구간 시작 경계 포함");
        Assert.AreEqual(1, AITStreamingFont.MatchPendingTags("Z", pending).Count, "구간 끝 경계 포함");
        Assert.AreEqual(0, AITStreamingFont.MatchPendingTags("@", pending).Count, "시작 바로 앞(U+0040) 은 미매치");
        Assert.AreEqual(0, AITStreamingFont.MatchPendingTags("[", pending).Count, "끝 바로 뒤(U+005B) 은 미매치");
    }

    [Test]
    public void MatchPendingTags_OverlappingRanges_MatchesAllOverlappingTags()
    {
        // 한자 U+4E2D('中') 는 ja/zh-Hans 양쪽 CJK 통합 한자 범위에 겹친다 — 겹치는 태그 전부 매치가 의도된 동작.
        var pending = new Dictionary<string, AITStreamingFont.CodepointRange[]>
        {
            ["ja"] = new[] { new AITStreamingFont.CodepointRange(0x4E00, 0x9FFF) },
            ["zh-Hans"] = new[] { new AITStreamingFont.CodepointRange(0x4E00, 0x9FFF) },
            ["ru"] = new[] { new AITStreamingFont.CodepointRange(0x0400, 0x04FF) },
        };

        var matched = AITStreamingFont.MatchPendingTags("中", pending);
        CollectionAssert.AreEquivalent(new[] { "ja", "zh-Hans" }, matched);
    }

    [Test]
    public void MatchPendingTags_SurrogatePairEmoji_ComposesUtf32AndMatches()
    {
        // U+1F600(😀) 은 BMP 밖 — UTF-16 서로게이트 쌍(😀)으로 인코딩됨.
        var pending = OneTagRanges("emoji", 0x1F300, 0x1F6FF);
        string emoji = char.ConvertFromUtf32(0x1F600);
        var matched = AITStreamingFont.MatchPendingTags(emoji, pending);
        CollectionAssert.AreEquivalent(new[] { "emoji" }, matched);
    }

    [Test]
    public void MatchPendingTags_SurrogatePairOutsideBmpRange_DoesNotFalselyMatchBmpTag()
    {
        // 서로게이트 쌍을 잘못 개별 char 로 취급하면 대리 코드 자체(U+D800-DFFF 대역)가 엉뚱하게
        // BMP 범위와 오매치될 수 있다 — 올바른 UTF-32 합성이면 이모지(BMP 밖)가 BMP 태그와 매치되지 않아야 한다.
        var pending = OneTagRanges("latin-ext", 0x0100, 0x024F);
        string emoji = char.ConvertFromUtf32(0x1F600);
        var matched = AITStreamingFont.MatchPendingTags(emoji, pending);
        Assert.AreEqual(0, matched.Count);
    }

    [Test]
    public void MatchPendingTags_UnpairedHighSurrogate_IgnoredWithoutThrowing()
    {
        var pending = OneTagRanges("emoji", 0x1F300, 0x1F6FF);
        string malformed = "\uD83D"; // 뒤따르는 low surrogate 없음.
        List<string> matched = null;
        Assert.DoesNotThrow(() => matched = AITStreamingFont.MatchPendingTags(malformed, pending));
        Assert.AreEqual(0, matched.Count);
    }

    [Test]
    public void MatchPendingTags_MultipleCharsInText_UnionsMatchesWithoutDuplicates()
    {
        var pending = new Dictionary<string, AITStreamingFont.CodepointRange[]>
        {
            ["ja"] = new[] { new AITStreamingFont.CodepointRange(0x3040, 0x309F) },
            ["ru"] = new[] { new AITStreamingFont.CodepointRange(0x0400, 0x04FF) },
        };

        // "あ"(히라가나, ja) + "Hello"(미매치) + "あ"(중복 ja) → ja 1회만.
        var matched = AITStreamingFont.MatchPendingTags("あHelloあ", pending);
        CollectionAssert.AreEquivalent(new[] { "ja" }, matched);
    }

    private static Dictionary<string, AITStreamingFont.CodepointRange[]> OneTagRanges(string tag, int start, int end)
    {
        return new Dictionary<string, AITStreamingFont.CodepointRange[]>
        {
            [tag] = new[] { new AITStreamingFont.CodepointRange(start, end) },
        };
    }

    // =====================================================
    // Manifest/Entry JsonUtility 왕복 — lazyTag/lazyRanges 포함/부재 하위호환
    // (Entry/Manifest 는 private nested struct 라 리플렉션으로 접근)
    // =====================================================

    [Test]
    public void Manifest_OldFormatJson_WithoutLazyFields_ParsesAndDefaultsToEmpty()
    {
        // 구 매니페스트: lazyTag/lazyRanges 필드 자체가 JSON 에 없음(하위호환 대상).
        const string json = "{\"maxConcurrent\":2,\"entries\":[" +
            "{\"guid\":\"g1\",\"bundle\":\"boot.bundle\",\"fonts\":[\"NotoSansKR\"],\"encoding\":\"\"}" +
            "]}";

        object manifest = UnityEngine.JsonUtility.FromJson(json, ManifestType);
        Assert.IsNotNull(manifest);

        var entries = (Array)GetField(manifest, "entries");
        Assert.AreEqual(1, entries.Length);

        object entry0 = entries.GetValue(0);
        Assert.AreEqual("g1", GetField(entry0, "guid"));
        Assert.AreEqual("boot.bundle", GetField(entry0, "bundle"));
        Assert.IsTrue(string.IsNullOrEmpty((string)GetField(entry0, "lazyTag")), "구 매니페스트는 lazyTag 가 비어 있어야(eager) 한다");
        Assert.IsTrue(string.IsNullOrEmpty((string)GetField(entry0, "lazyRanges")));
    }

    [Test]
    public void Manifest_NewFormatJson_WithLazyFields_ParsesLazyTagAndRanges()
    {
        const string json = "{\"maxConcurrent\":2,\"entries\":[" +
            "{\"guid\":\"g2\",\"bundle\":\"lazy-ja-abc123.bundle\",\"fonts\":[\"NotoSansJP\"],\"encoding\":\"br\"," +
            "\"lazyTag\":\"ja\",\"lazyRanges\":\"U+3040-309F,U+30A0-30FF\"}" +
            "]}";

        object manifest = UnityEngine.JsonUtility.FromJson(json, ManifestType);
        var entries = (Array)GetField(manifest, "entries");
        Assert.AreEqual(1, entries.Length);

        object entry0 = entries.GetValue(0);
        Assert.AreEqual("ja", GetField(entry0, "lazyTag"));
        Assert.AreEqual("U+3040-309F,U+30A0-30FF", GetField(entry0, "lazyRanges"));
        Assert.AreEqual("lazy-ja-abc123.bundle", GetField(entry0, "bundle"));
        Assert.AreEqual("br", GetField(entry0, "encoding"));
    }

    [Test]
    public void Manifest_RoundTrip_SerializeThenDeserialize_PreservesLazyFields()
    {
        object entry = Activator.CreateInstance(EntryType);
        SetField(entry, "guid", "g3");
        SetField(entry, "bundle", "lazy-th-deadbeef.bundle");
        SetField(entry, "fonts", new[] { "NotoSansThai" });
        SetField(entry, "encoding", "br");
        SetField(entry, "lazyTag", "th");
        SetField(entry, "lazyRanges", "U+0E00-0E7F");

        var entries = Array.CreateInstance(EntryType, 1);
        entries.SetValue(entry, 0);

        object manifest = Activator.CreateInstance(ManifestType);
        SetField(manifest, "maxConcurrent", 3);
        SetField(manifest, "entries", entries);

        string json = UnityEngine.JsonUtility.ToJson(manifest);
        object roundTripped = UnityEngine.JsonUtility.FromJson(json, ManifestType);

        Assert.AreEqual(3, GetField(roundTripped, "maxConcurrent"));
        var roundTrippedEntries = (Array)GetField(roundTripped, "entries");
        Assert.AreEqual(1, roundTrippedEntries.Length);

        object roundTrippedEntry = roundTrippedEntries.GetValue(0);
        Assert.AreEqual("g3", GetField(roundTrippedEntry, "guid"));
        Assert.AreEqual("lazy-th-deadbeef.bundle", GetField(roundTrippedEntry, "bundle"));
        Assert.AreEqual("br", GetField(roundTrippedEntry, "encoding"));
        Assert.AreEqual("th", GetField(roundTrippedEntry, "lazyTag"));
        Assert.AreEqual("U+0E00-0E7F", GetField(roundTrippedEntry, "lazyRanges"));
    }

    // =====================================================
    // IsLazyFullyDrained — 게이트 대기 유실 방지(B0) 카운터 상태 전이 순수 로직
    // 진리표 자체는 1건만 유지한다 — 인자 2개짜리 항등 술어라 회귀 감지력이 없다(TriggerLazyLoad 의
    // lazyOutstanding++ 삭제나 MaybeFinishLazy 의 판정 카운터 치환 어느 쪽도 이 술어 자체를 건드리지
    // 않으므로 전부 통과해버린다). 실질 회귀 가드는 아래 "인스턴스 리플렉션 테스트" 섹션이 담당한다.
    // =====================================================

    [Test]
    public void IsLazyFullyDrained_ReturnsTrueOnlyWhenBothPendingAndOutstandingAreZero()
    {
        Assert.IsTrue(AITStreamingFont.IsLazyFullyDrained(0, 0));
        Assert.IsFalse(AITStreamingFont.IsLazyFullyDrained(1, 0));
        Assert.IsFalse(AITStreamingFont.IsLazyFullyDrained(0, 1), "게이트 대기 중(outstanding>0)에는 소진이 아니어야 함(B0 핵심 불변식)");
        Assert.IsFalse(AITStreamingFont.IsLazyFullyDrained(2, 3));
    }

    // =====================================================
    // 인스턴스 리플렉션 테스트(R1) — TriggerLazyLoad/MaybeFinishLazy 의 실제 카운터 조작 순서를 회귀 가드.
    // Entry/private 메서드는 리플렉션으로만 접근 가능. EditMode 에서 StartCoroutine 은 첫 yield 까지만
    // 동기 실행되므로(LoadLazyEntry 의 첫 실제 yield 는 "yield return LoadAndInject(...)" 지점 —
    // LoadAndInject 자신의 MoveNext 는 아직 한 번도 호출되지 않아 본문이 실행되지 않는다), 더미 Entry 로
    // 네트워크/AssetBundle 접근 없이 안전하게 검증할 수 있다.
    // =====================================================

    private readonly List<GameObject> createdGameObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in createdGameObjects)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        createdGameObjects.Clear();
    }

    private AITStreamingFont CreateInstance()
    {
        var go = new GameObject("AITStreamingFontTest");
        createdGameObjects.Add(go);
        return go.AddComponent<AITStreamingFont>();
    }

    private static void AddLazyPendingEntry(AITStreamingFont comp, string tag, string ranges)
    {
        object entry = Activator.CreateInstance(EntryType);
        SetField(entry, "guid", "test-" + tag);
        SetField(entry, "bundle", $"lazy-{tag}-test.bundle");
        SetField(entry, "fonts", new[] { "TestFont" });
        SetField(entry, "encoding", "");
        SetField(entry, "lazyTag", tag);
        SetField(entry, "lazyRanges", ranges);

        object pendingDict = GetField(comp, "lazyPending");
        pendingDict.GetType().GetMethod("Add").Invoke(pendingDict, new object[] { tag, entry });
    }

    private static object InvokePrivateMethod(object target, string name, params object[] args)
    {
        var m = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, $"메서드를 찾지 못함: {name}");
        return m.Invoke(target, args);
    }

    [Test]
    public void TriggerLazyLoad_IncrementsLazyOutstandingBeforeStartingCoroutine()
    {
        var comp = CreateInstance();
        AddLazyPendingEntry(comp, "ja", "U+3040-309F");

        InvokePrivateMethod(comp, "TriggerLazyLoad", new List<string> { "ja" });

        int outstanding = (int)GetField(comp, "lazyOutstanding");
        Assert.AreEqual(1, outstanding,
            "TriggerLazyLoad 는 StartCoroutine 시작 '전' 에 lazyOutstanding 을 증가시켜야 한다(B0) — " +
            "그렇지 않으면 게이트 대기 중인 태그가 완료로 오판되어 GameObject 가 조기 파괴될 수 있다.");
    }

    [Test]
    public void MaybeFinishLazy_OutstandingNonZero_DoesNotEnterDrainPath()
    {
        var comp = CreateInstance();
        SetField(comp, "lazyInflight", 0);
        SetField(comp, "lazyOutstanding", 1);
        // lazyPending 은 새 인스턴스라 기본적으로 비어 있음 → pendingCount==0, outstanding==1.

        var logs = CollectLogs(() => InvokePrivateMethod(comp, "MaybeFinishLazy"));
        bool sawDrainLog = logs.Exists(l => l.message.Contains("lazy 폰트 전 태그 소진"));

        Assert.IsFalse(sawDrainLog,
            "MaybeFinishLazy 는 lazyOutstanding(트리거됐으나 게이트 대기 포함 아직 안 끝난 전체)로 판정해야 한다 — " +
            "lazyInflight 로 오판하면 outstanding>0 인데도(게이트 대기 중인 태그가 있는데도) 소진으로 잘못 종료된다(B0).");
    }

    [Test]
    public void MaybeFinishLazy_OutstandingZero_EntersDrainPath()
    {
        var comp = CreateInstance();
        SetField(comp, "lazyInflight", 0);
        SetField(comp, "lazyOutstanding", 0);

        List<LogEntry> logs;
        LogAssert.ignoreFailingMessages = true; // Destroy() 가 EditMode 에서 에러를 찍을 수 있음 — 흡수.
        try
        {
            logs = CollectLogs(() => InvokePrivateMethod(comp, "MaybeFinishLazy"));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }

        bool sawDrainLog = logs.Exists(l => l.message.Contains("lazy 폰트 전 태그 소진"));
        Assert.IsTrue(sawDrainLog, "pending==0 && outstanding==0 이면 종료(파괴) 경로에 진입해야 한다.");
    }

    // 동작 중 발생한 모든 로그를 타입과 함께 수집(LogAssert.NoUnexpectedReceived 가 정보/경고 로그는
    // 감시하지 않는 한계 보완 — BuildFileSelectionTests.cs 의 동일 관용구 재사용).
    private struct LogEntry
    {
        public LogType type;
        public string message;
    }

    private static List<LogEntry> CollectLogs(Action action)
    {
        var logs = new List<LogEntry>();
        Application.LogCallback handler = null;
        handler = (msg, _, type) =>
        {
            logs.Add(new LogEntry { type = type, message = msg });
        };
        Application.logMessageReceived += handler;
        try { action(); }
        finally { Application.logMessageReceived -= handler; }
        return logs;
    }

    private static object GetField(object target, string name)
    {
        var f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드를 찾지 못함: {name}");
        return f.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드를 찾지 못함: {name}");
        f.SetValue(target, value);
    }
}
