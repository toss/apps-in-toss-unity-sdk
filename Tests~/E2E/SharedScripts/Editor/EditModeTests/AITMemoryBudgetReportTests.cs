// -----------------------------------------------------------------------
// AITMemoryBudgetReportTests.cs - 빌드 시점 iOS 메모리 예산 추정(AITMemoryBudgetReport) 검증
// Level 0: 합성 입력만 쓴다(실제 빌드 산출물 없음). 계수 계산·등급 경계, wasm 함수 본문 크기 파싱,
// symbols 파싱(JSON 객체/배열/줄 형식), 심볼 그룹 집계를 고정한다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITMemoryBudgetReportTests
{
    // =================================================================
    // 계수 계산·등급
    // =================================================================

    [Test]
    public void Compute_FollowsPlanFormula()
    {
        var e = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs
        {
            codeMB = 10.0,
            dataRawMB = 20.0,
            streamingHeldMB = 5.0,
        });

        double heap = AITMemoryBudgetReport.HeapBaseMB + 20.0;
        double steady = AITMemoryBudgetReport.BaseMB + 7.4 * 10.0 + 20.0 + heap + 5.0 + AITMemoryBudgetReport.OtherMB;
        Assert.AreEqual(heap, e.heapMB, 1e-9);
        Assert.AreEqual(steady, e.steadyMB, 1e-9);
        Assert.AreEqual(steady + 260.0 * (10.0 / 32.6), e.peakMB, 1e-9);
    }

    [Test]
    public void Compute_HeavyFixture_LandsNearMeasuredFootprintAndIsOver()
    {
        // 6000.0 heavy 실측: wasm code 32.6MB, data 30.7MB, 외부화 오디오·폰트 약 28MB → iOS 지속 557MB, 피크 817MB.
        var e = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs
        {
            codeMB = 32.6,
            dataRawMB = 30.7,
            streamingHeldMB = 27.7,
        });

        Assert.That(e.steadyMB, Is.InRange(500.0, 580.0), "계수가 실측 557MB 에서 크게 벗어났습니다.");
        Assert.That(e.peakMB, Is.InRange(760.0, 840.0), "피크가 실측 817MB 에서 크게 벗어났습니다.");
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Over, e.grade);
    }

    [Test]
    public void Compute_SmallGame_IsComfortable()
    {
        var e = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs { codeMB = 8.0, dataRawMB = 10.0, streamingHeldMB = 0.0 });

        Assert.Less(e.steadyMB, AITMemoryBudgetReport.SteadyBudgetMB * AITMemoryBudgetReport.CautionRatio);
        Assert.Less(e.peakMB, AITMemoryBudgetReport.PeakBudgetMB * AITMemoryBudgetReport.CautionRatio);
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Comfortable, e.grade);
    }

    [Test]
    public void Compute_MidGame_IsCaution()
    {
        var e = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs { codeMB = 12.0, dataRawMB = 15.0, streamingHeldMB = 0.0 });

        Assert.AreEqual(AITMemoryBudgetReport.Grade.Caution, e.grade,
            $"steady={e.steadyMB:F1} peak={e.peakMB:F1}");
    }

    [Test]
    public void GradeOf_SteadyOverBudget_IsOverEvenWhenPeakIsLow()
    {
        // 지속이 예산(350)을 넘으면 피크가 낮아도(비현실적이지만 입력 독립성 확인) 초과.
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Over, AITMemoryBudgetReport.GradeOf(351.0, 100.0));
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Over, AITMemoryBudgetReport.GradeOf(100.0, 451.0));
    }

    [Test]
    public void GradeOf_Boundaries()
    {
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Comfortable, AITMemoryBudgetReport.GradeOf(297.0, 382.0));
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Caution, AITMemoryBudgetReport.GradeOf(298.0, 100.0));
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Caution, AITMemoryBudgetReport.GradeOf(100.0, 383.0));
        Assert.AreEqual(AITMemoryBudgetReport.Grade.Caution, AITMemoryBudgetReport.GradeOf(350.0, 450.0));
    }

    [Test]
    public void Compute_NegativeInputs_ClampToZero()
    {
        var e = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs { codeMB = -5.0, dataRawMB = -1.0, streamingHeldMB = -2.0 });

        Assert.AreEqual(0.0, e.codeMB);
        Assert.AreEqual(0.0, e.dataRawMB);
        Assert.AreEqual(0.0, e.streamingHeldMB);
        Assert.AreEqual(e.steadyMB, e.peakMB, 1e-9, "코드가 0 이면 피크 상승분도 0 입니다.");
    }

    [Test]
    public void FormatSummary_ContainsGradeLabelAndBudgets()
    {
        var e = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs { codeMB = 32.6, dataRawMB = 30.7, streamingHeldMB = 27.7 });
        string line = AITMemoryBudgetReport.FormatSummary(e);

        StringAssert.Contains("[AIT-MemBudget]", line);
        StringAssert.Contains("초과", line);
        StringAssert.Contains("예산 350", line);
        StringAssert.Contains("예산 450", line);
    }

    [Test]
    public void DominantHint_NullWhenComfortable_CodeHintWhenCodeDominates()
    {
        var small = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs { codeMB = 4.0, dataRawMB = 5.0 });
        Assert.IsNull(AITMemoryBudgetReport.DominantHint(small));

        var big = AITMemoryBudgetReport.Compute(new AITMemoryBudgetReport.Inputs { codeMB = 40.0, dataRawMB = 5.0 });
        StringAssert.Contains("wasm 코드", AITMemoryBudgetReport.DominantHint(big));
    }

    [TestCase("2021.3.45f1", true)]
    [TestCase("2021.3.1f1", true)]
    [TestCase("2022.3.62f2", false)]
    [TestCase("6000.0.58f1", false)]
    [TestCase("6000.3.4f1", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void LegacyEngineHint_OnlyFor2021_3(string version, bool expectHint)
    {
        string hint = AITMemoryBudgetReport.LegacyEngineHint(version);
        Assert.AreEqual(expectHint, hint != null);
        if (expectHint)
        {
            StringAssert.Contains("6000.x", hint);
            StringAssert.Contains("90~125MB", hint);
        }
    }

    // =================================================================
    // 합성 wasm: import(함수 2 + memory + global) + function 섹션 + code 섹션(본문 3개)
    // =================================================================

    private static void Leb(List<byte> dst, uint v)
    {
        do
        {
            byte b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            dst.Add(b);
        } while (v != 0);
    }

    private static void Name(List<byte> dst, string s)
    {
        Leb(dst, (uint)s.Length);
        foreach (char c in s) dst.Add((byte)c);
    }

    private static void Section(List<byte> wasm, byte id, List<byte> payload)
    {
        wasm.Add(id);
        Leb(wasm, (uint)payload.Count);
        wasm.AddRange(payload);
    }

    private static List<byte> BuildWasm(int[] bodySizes, int importedFunctions)
    {
        var wasm = new List<byte> { 0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00 };

        var imports = new List<byte>();
        Leb(imports, (uint)(importedFunctions + 2));
        for (int i = 0; i < importedFunctions; i++)
        {
            Name(imports, "env");
            Name(imports, "f" + i);
            imports.Add(0x00); // func
            Leb(imports, 0);   // typeidx
        }
        Name(imports, "env"); Name(imports, "memory");
        imports.Add(0x02);                 // memory
        imports.Add(0x01); Leb(imports, 1); Leb(imports, 300); // flags(max), min, max
        Name(imports, "env"); Name(imports, "g");
        imports.Add(0x03); imports.Add(0x7F); imports.Add(0x00); // global i32 const
        Section(wasm, 2, imports);

        var funcs = new List<byte>();
        Leb(funcs, (uint)bodySizes.Length);
        foreach (int _ in bodySizes) Leb(funcs, 0);
        Section(wasm, 3, funcs);

        var code = new List<byte>();
        Leb(code, (uint)bodySizes.Length);
        foreach (int size in bodySizes)
        {
            Leb(code, (uint)size);
            for (int i = 0; i < size; i++) code.Add(0x0B);
        }
        Section(wasm, 10, code);

        return wasm;
    }

    [Test]
    public void TryReadFunctionBodySizes_CountsImportsAndBodies()
    {
        byte[] wasm = BuildWasm(new[] { 5, 300, 1 }, importedFunctions: 2).ToArray();

        bool ok = AITMemoryBudgetReport.TryReadFunctionBodySizes(wasm, out List<int> bodies, out int importFuncCount);

        Assert.IsTrue(ok);
        Assert.AreEqual(2, importFuncCount, "memory/global import 는 함수 import 로 세지 않아야 합니다.");
        CollectionAssert.AreEqual(new[] { 5, 300, 1 }, bodies);
    }

    [Test]
    public void TryReadFunctionBodySizes_NoImports()
    {
        byte[] wasm = BuildWasm(new[] { 7 }, importedFunctions: 0).ToArray();

        Assert.IsTrue(AITMemoryBudgetReport.TryReadFunctionBodySizes(wasm, out List<int> bodies, out int importFuncCount));
        Assert.AreEqual(0, importFuncCount);
        CollectionAssert.AreEqual(new[] { 7 }, bodies);
    }

    [Test]
    public void TryReadFunctionBodySizes_RejectsBadMagicAndTruncation()
    {
        Assert.IsFalse(AITMemoryBudgetReport.TryReadFunctionBodySizes(null, out _, out _));
        Assert.IsFalse(AITMemoryBudgetReport.TryReadFunctionBodySizes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, out _, out _));

        byte[] wasm = BuildWasm(new[] { 50 }, importedFunctions: 1).ToArray();
        var truncated = new byte[wasm.Length - 10];
        System.Array.Copy(wasm, truncated, truncated.Length);
        Assert.IsFalse(AITMemoryBudgetReport.TryReadFunctionBodySizes(truncated, out _, out _), "잘린 파일은 거부해야 합니다.");
    }

    // =================================================================
    // symbols 파싱
    // =================================================================

    [Test]
    public void TryParseSymbols_JsonObject_IndexToName()
    {
        const string json = "{\"2\":\"AppsInToss_Boot_Awake_m1A2B3C4D5E6F\",\"3\":\"_ZN6il2cpp2vm3FooEv\",\n \"10\":\"with\\\"quote\"}";

        Assert.IsTrue(AITMemoryBudgetReport.TryParseSymbols(json, out var names));
        Assert.AreEqual(3, names.Count);
        Assert.AreEqual("AppsInToss_Boot_Awake_m1A2B3C4D5E6F", names[2]);
        Assert.AreEqual("_ZN6il2cpp2vm3FooEv", names[3]);
        Assert.AreEqual("with\"quote", names[10]);
    }

    [Test]
    public void TryParseSymbols_JsonObject_NameToIndex()
    {
        Assert.IsTrue(AITMemoryBudgetReport.TryParseSymbols("{\"foo\":4,\"bar\":9}", out var names));
        Assert.AreEqual("foo", names[4]);
        Assert.AreEqual("bar", names[9]);
    }

    [Test]
    public void TryParseSymbols_JsonArray_PositionIsIndex()
    {
        Assert.IsTrue(AITMemoryBudgetReport.TryParseSymbols("[\"a\", \"b\" ,\"c\"]", out var names));
        Assert.AreEqual("a", names[0]);
        Assert.AreEqual("c", names[2]);
    }

    [Test]
    public void TryParseSymbols_EmscriptenLines()
    {
        Assert.IsTrue(AITMemoryBudgetReport.TryParseSymbols("0:__wasm_call_ctors\r\n1:main\r\nbad line\r\n", out var names));
        Assert.AreEqual(2, names.Count);
        Assert.AreEqual("main", names[1]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("not symbols")]
    public void TryParseSymbols_EmptyOrUnparseable_ReturnsFalse(string text)
    {
        Assert.IsFalse(AITMemoryBudgetReport.TryParseSymbols(text, out var names));
        Assert.AreEqual(0, names.Count);
    }

    // =================================================================
    // 심볼 분류·그룹 집계
    // =================================================================

    [TestCase("AppsInToss_Boot_Awake_m1A2B3C4D5E6F7A8B", "AppsInToss")]
    [TestCase("UnityEngine_Object_Foo_mABCDEF0123456789", "UnityEngine")]
    [TestCase("PlayerController_Update_m0123456789ABCDEF", "PlayerController")]
    [TestCase("List_1_Add_mABCDEF0123456789", AITMemoryBudgetReport.GenericGroup)]
    [TestCase("Dictionary_2_TryGetValue_mABCDEF0123456789_gshared", AITMemoryBudgetReport.GenericGroup)]
    [TestCase("U3CStartU3Ed__5_MoveNext_mABCDEF0123456789", AITMemoryBudgetReport.CompilerGeneratedGroup)]
    [TestCase("_ZN6il2cpp2vm3FooEv", AITMemoryBudgetReport.NativeGroup)]
    [TestCase("emscripten_resize_heap", AITMemoryBudgetReport.NativeGroup)]
    [TestCase("_mABCDEF0123456789", AITMemoryBudgetReport.NativeGroup)]
    [TestCase("", AITMemoryBudgetReport.UnnamedGroup)]
    [TestCase(null, AITMemoryBudgetReport.UnnamedGroup)]
    public void ClassifySymbol_Table(string name, string expectedGroup)
    {
        Assert.AreEqual(expectedGroup, AITMemoryBudgetReport.ClassifySymbol(name));
    }

    [Test]
    public void GroupCodeBytes_OffsetsByImportCount_AndSortsDescending()
    {
        // 함수 인덱스: 0,1 = import. 본문 0 → 인덱스 2, 본문 1 → 3 ...
        var names = new Dictionary<int, string>
        {
            { 2, "AppsInToss_Boot_Awake_m1A2B3C4D5E6F7A8B" },
            { 3, "UnityEngine_Object_Foo_mABCDEF0123456789" },
            { 4, "_ZN6il2cpp2vm3FooEv" },
            { 5, "AppsInToss_Other_Run_m1111111111111111" },
            // 인덱스 6 은 심볼 없음
        };
        var sizes = new List<int> { 100, 50, 25, 10, 7 };

        var groups = AITMemoryBudgetReport.GroupCodeBytes(sizes, importFuncCount: 2, names);

        Assert.AreEqual(4, groups.Count);
        Assert.AreEqual("AppsInToss", groups[0].name);
        Assert.AreEqual(110, groups[0].bytes);
        Assert.AreEqual(2, groups[0].functions);
        Assert.AreEqual("UnityEngine", groups[1].name);
        Assert.AreEqual(50, groups[1].bytes);
        Assert.AreEqual(AITMemoryBudgetReport.NativeGroup, groups[2].name);
        Assert.AreEqual(AITMemoryBudgetReport.UnnamedGroup, groups[3].name);
        Assert.AreEqual(7, groups[3].bytes);
    }

    [Test]
    public void GroupCodeBytes_WithoutNames_AllUnnamed()
    {
        var groups = AITMemoryBudgetReport.GroupCodeBytes(new List<int> { 3, 4 }, 0, null);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(AITMemoryBudgetReport.UnnamedGroup, groups[0].name);
        Assert.AreEqual(7, groups[0].bytes);
    }

    [Test]
    public void FormatTopGroups_CapsAtTopAndSummarizesRest()
    {
        var groups = new List<AITMemoryBudgetReport.GroupEntry>();
        for (int i = 0; i < 25; i++)
            groups.Add(new AITMemoryBudgetReport.GroupEntry { name = "G" + i, bytes = (25 - i) * 1024 * 1024L, functions = i + 1 });

        string table = AITMemoryBudgetReport.FormatTopGroups(groups, AITMemoryBudgetReport.TopGroupCount);

        StringAssert.Contains("Top 20", table);
        StringAssert.Contains("G0 ", table);
        StringAssert.Contains("G19 ", table);
        StringAssert.DoesNotContain("G20 ", table);
        StringAssert.Contains("나머지 5개 그룹", table);
    }

    [Test]
    public void FormatTopGroups_EmptyReturnsNull()
    {
        Assert.IsNull(AITMemoryBudgetReport.FormatTopGroups(new List<AITMemoryBudgetReport.GroupEntry>(), 20));
    }

    [Test]
    public void EndToEnd_SyntheticWasmAndSymbols()
    {
        byte[] wasm = BuildWasm(new[] { 120, 30 }, importedFunctions: 3).ToArray();
        Assert.IsTrue(AITMemoryBudgetReport.TryReadFunctionBodySizes(wasm, out var bodies, out int importCount));
        Assert.IsTrue(AITMemoryBudgetReport.TryParseSymbols(
            "{\"3\":\"MyGame_Player_Update_m00112233445566778899\",\"4\":\"_ZN6il2cpp2vm3FooEv\"}", out var names));

        var groups = AITMemoryBudgetReport.GroupCodeBytes(bodies, importCount, names);

        Assert.AreEqual("MyGame", groups[0].name);
        Assert.AreEqual(120, groups[0].bytes);
        Assert.AreEqual(AITMemoryBudgetReport.NativeGroup, groups[1].name);
    }
}
