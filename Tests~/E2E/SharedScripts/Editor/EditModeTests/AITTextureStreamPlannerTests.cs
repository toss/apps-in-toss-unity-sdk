// -----------------------------------------------------------------------
// AITTextureStreamPlannerTests.cs - 텍스처 스트리밍 메모리 예산·GPU 포맷 보존·매니페스트 계약 검증
// Level 0: 빌드타임 순수 로직(AITTextureStreamPlanner) + 런타임 순수 판정(AITStreamingTexture).
//
// 핵심 불변식:
//   1) 스텁 RGBA32 − 원본 GPU 바이트가 예산 이내인 텍스처만 auto 로 외부화한다(작은 증가부터, 중립 이하는 항상).
//   2) 원본이 non-readable 이면 매니페스트에 nonReadable=1 이 실리고, 없으면(readable/구 매니페스트) 필드가 없다.
//   3) 매니페스트 엔트리 JSON 의 키는 런타임 Entry 필드와 이름이 같아야 한다(JsonUtility 가 이름으로 채운다).
//   4) raw(ASTC 블록) 사용은 포맷·mip 이 정확히 같고 기기가 지원할 때만, 아니면 PNG/JPG 폴백.
//   5) 동시 상한: 기본 1, 저사양 티어는 매니페스트 값과 무관하게 1.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITTextureStreamPlannerTests
{
    private const long MB = 1048576L;

    // ─────────────────────── 크기 계산 ───────────────────────

    [Test]
    public void ExpectedRawBytes_Astc6x6_2048_SingleMip_Is1_78MiB()
    {
        // ceil(2048/6)=342 블록 → 342*342*16 = 1,871,424B (약 1.78MiB — 계획서의 "약 1.9MB").
        Assert.AreEqual(342L * 342 * 16, AITTextureStreamPlanner.ExpectedRawBytes("ASTC_6x6", 2048, 2048, 1));
    }

    [Test]
    public void ExpectedRawBytes_NonAstc_IsZero()
    {
        Assert.AreEqual(0, AITTextureStreamPlanner.ExpectedRawBytes("DXT5", 256, 256, 1));
        Assert.AreEqual(0, AITTextureStreamPlanner.ExpectedRawBytes("RGBA32", 256, 256, 1));
    }

    [Test]
    public void ComputeMipChainBytes_SumsEveryLevel_ClampsToOne()
    {
        // RGBA32 4x4, 3 레벨: 4x4 + 2x2 + 1x1 = 21 픽셀 * 4B.
        Assert.AreEqual(21 * 4, AITTextureStreamPlanner.ComputeMipChainBytes(4, 4, 3, 1, 1, 4));
        // mips 가 0 이하면 1 레벨로 본다.
        Assert.AreEqual(16 * 4, AITTextureStreamPlanner.ComputeMipChainBytes(4, 4, 0, 1, 1, 4));
        // 레벨 차원은 1 아래로 내려가지 않는다(8x2 를 4 레벨: 8x2, 4x1, 2x1, 1x1).
        Assert.AreEqual((16 + 4 + 2 + 1) * 4, AITTextureStreamPlanner.ComputeMipChainBytes(8, 2, 4, 1, 1, 4));
    }

    [Test]
    public void EstimateStubBytes_Is_Rgba32_PerPixel()
    {
        Assert.AreEqual(2048L * 2048 * 4, AITTextureStreamPlanner.EstimateStubBytes(2048, 2048, 1));
    }

    [Test]
    public void EstimateGpuBytes_CompressedIsFarSmallerThanStub_UnknownAssumesRgba32()
    {
        long stub = AITTextureStreamPlanner.EstimateStubBytes(2048, 2048, 1);
        long astc = AITTextureStreamPlanner.EstimateGpuBytes("ASTC_6x6", 2048, 2048, 1);
        Assert.Less(astc, stub / 8, "ASTC 6x6 는 RGBA32 의 1/8 미만이어야 한다(약 −88%).");
        Assert.AreEqual(2048L * 2048, AITTextureStreamPlanner.EstimateGpuBytes("DXT5Crunched", 2048, 2048, 1), "크런치는 풀린 DXT5 바이트로 센다.");
        Assert.AreEqual(stub, AITTextureStreamPlanner.EstimateGpuBytes("NoSuchFormat", 2048, 2048, 1), "모르는 포맷은 스텁과 같게 가정(메모리 중립).");
        Assert.AreEqual(0, AITTextureStreamPlanner.ComputeMemoryDelta(stub, stub));
    }

    [Test]
    public void TryParseAstcBlock_ParsesLdr_RejectsHdrAndOthers()
    {
        Assert.IsTrue(AITTextureStreamPlanner.TryParseAstcBlock("ASTC_10x10", out int bw, out int bh));
        Assert.AreEqual(10, bw);
        Assert.AreEqual(10, bh);
        Assert.IsFalse(AITTextureStreamPlanner.TryParseAstcBlock("ASTC_HDR_6x6", out _, out _));
        Assert.IsFalse(AITTextureStreamPlanner.TryParseAstcBlock("DXT5", out _, out _));
        Assert.IsFalse(AITTextureStreamPlanner.TryParseAstcBlock(null, out _, out _));
    }

    // ─────────────────────── 예산 선택 ───────────────────────

    [Test]
    public void SelectWithinBudget_NeutralAlwaysIn_PositiveSmallestFirstUntilBudget()
    {
        // 중립(0), 이득(-5)은 항상. 양수는 작은 것부터 누적: 10 + 10 = 20 <= 25, 다음 20 은 40 > 25 → 제외.
        var deltas = new long[] { 20, -5, 0, 10, 10 };
        bool[] pick = AITTextureStreamPlanner.SelectWithinBudget(deltas, 25);
        CollectionAssert.AreEqual(new[] { false, true, true, true, true }, pick);
    }

    [Test]
    public void SelectWithinBudget_StopsAtFirstThatDoesNotFit_NoSkipAhead()
    {
        // 정렬 후 15, 16: 15 는 들어가고 16 은 31 > 20 → 제외. 더 큰 것도 제외.
        bool[] pick = AITTextureStreamPlanner.SelectWithinBudget(new long[] { 30, 15, 16 }, 20);
        CollectionAssert.AreEqual(new[] { false, true, false }, pick);
    }

    [Test]
    public void SelectWithinBudget_EqualDeltasFollowInputOrder()
    {
        bool[] pick = AITTextureStreamPlanner.SelectWithinBudget(new long[] { 10, 10, 10 }, 20);
        CollectionAssert.AreEqual(new[] { true, true, false }, pick, "같은 증가량은 입력 순서대로 채운다(결정적).");
    }

    [Test]
    public void SelectWithinBudget_ZeroOrNegativeBudget_IsUnlimited()
    {
        CollectionAssert.AreEqual(new[] { true, true }, AITTextureStreamPlanner.SelectWithinBudget(new long[] { 100 * MB, 5 }, 0));
        CollectionAssert.AreEqual(new[] { true }, AITTextureStreamPlanner.SelectWithinBudget(new long[] { 100 * MB }, -1));
    }

    [Test]
    public void SelectWithinBudget_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(0, AITTextureStreamPlanner.SelectWithinBudget(new long[0], 16 * MB).Length);
        Assert.AreEqual(0, AITTextureStreamPlanner.SelectWithinBudget(null, 16 * MB).Length);
    }

    [Test]
    public void DefaultBudget_2048SquareAstcSprite_IsNotNeutral_SoAutoSkipsIt()
    {
        // 계획서 시나리오: 2048² ASTC 6x6 스프라이트 하나는 스텁 RGBA32(16MB) − GPU(약 1.8MB) ≈ +14MB 라
        // 기본 예산 16MB 에서는 하나만 들어가고(14 <= 16) 둘째부터 제외된다.
        long delta = AITTextureStreamPlanner.ComputeMemoryDelta(
            AITTextureStreamPlanner.EstimateStubBytes(2048, 2048, 1),
            AITTextureStreamPlanner.EstimateGpuBytes("ASTC_6x6", 2048, 2048, 1));
        Assert.Greater(delta, 0);
        bool[] pick = AITTextureStreamPlanner.SelectWithinBudget(new[] { delta, delta, delta, delta }, 16 * MB);
        CollectionAssert.AreEqual(new[] { true, false, false, false }, pick);
    }

    // ─────────────────────── tri-state / 동시성 ───────────────────────

    [Test]
    public void ResolveTriState_ExplicitBeatsAuto()
    {
        Assert.IsTrue(AITTextureStreamPlanner.ResolveTriState(1, false));
        Assert.IsFalse(AITTextureStreamPlanner.ResolveTriState(0, true));
        Assert.IsTrue(AITTextureStreamPlanner.ResolveTriState(-1, true));
        Assert.IsFalse(AITTextureStreamPlanner.ResolveTriState(-1, false));
    }

    [Test]
    public void ParseTriStateEnv_OneZeroTrueFalse_ElseFallback()
    {
        Assert.AreEqual(1, AITTextureStreamPlanner.ParseTriStateEnv("1", -1));
        Assert.AreEqual(1, AITTextureStreamPlanner.ParseTriStateEnv(" true ", -1));
        Assert.AreEqual(0, AITTextureStreamPlanner.ParseTriStateEnv("0", 1));
        Assert.AreEqual(0, AITTextureStreamPlanner.ParseTriStateEnv("FALSE", 1));
        Assert.AreEqual(-1, AITTextureStreamPlanner.ParseTriStateEnv("auto", -1));
        Assert.AreEqual(1, AITTextureStreamPlanner.ParseTriStateEnv(null, 1));
    }

    [Test]
    public void ResolveMaxConcurrent_Build_DefaultsToOne()
    {
        Assert.AreEqual(1, AITTextureStreamPlanner.ResolveMaxConcurrent(0));
        Assert.AreEqual(1, AITTextureStreamPlanner.ResolveMaxConcurrent(-3));
        Assert.AreEqual(4, AITTextureStreamPlanner.ResolveMaxConcurrent(4), "사용자가 올린 값은 존중한다.");
    }

    [Test]
    public void ResolveKeepGpuFormat_DefaultOff_EnvOverrides()
    {
        string prev = Environment.GetEnvironmentVariable(AITTextureStreamPlanner.KeepGpuFormatEnvVar);
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.KeepGpuFormatEnvVar, null);
            Assert.IsFalse(AITLargeTextureExternalizer.ResolveKeepGpuFormat(config), "자동은 꺼짐(실기기 검증 전).");
            Assert.IsFalse(AITLargeTextureExternalizer.ResolveKeepGpuFormat(null));

            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.KeepGpuFormatEnvVar, "1");
            Assert.IsTrue(AITLargeTextureExternalizer.ResolveKeepGpuFormat(config));

            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.KeepGpuFormatEnvVar, "0");
            Assert.IsFalse(AITLargeTextureExternalizer.ResolveKeepGpuFormat(config));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.KeepGpuFormatEnvVar, prev);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    // ─────────────────────── raw 적격 ───────────────────────

    [Test]
    public void IsRawEligible_OnlyPlainLdrAstc2D()
    {
        Assert.IsTrue(AITTextureStreamPlanner.IsRawEligible("ASTC_6x6", false, false, true, 2048, 2048, 1));
        Assert.IsTrue(AITTextureStreamPlanner.IsRawEligible("ASTC_6x6", false, false, true, 2048, 2048, 12), "2048 의 전체 mip 체인은 12 레벨.");
        Assert.IsFalse(AITTextureStreamPlanner.IsRawEligible("ASTC_6x6", false, false, true, 2048, 2048, 13), "차원에서 나올 수 없는 mip 수는 거부.");
        Assert.IsFalse(AITTextureStreamPlanner.IsRawEligible("ASTC_6x6", true, false, true, 2048, 2048, 1), "크런치는 거부.");
        Assert.IsFalse(AITTextureStreamPlanner.IsRawEligible("ASTC_6x6", false, true, true, 2048, 2048, 1), "mip 스트리밍은 거부.");
        Assert.IsFalse(AITTextureStreamPlanner.IsRawEligible("ASTC_6x6", false, false, false, 2048, 2048, 1), "2D 가 아니면 거부.");
        Assert.IsFalse(AITTextureStreamPlanner.IsRawEligible("DXT5", false, false, true, 2048, 2048, 1), "ASTC 가 아니면 거부.");
        Assert.IsFalse(AITTextureStreamPlanner.IsRawEligible("ASTC_HDR_6x6", false, false, true, 2048, 2048, 1), "HDR 은 거부.");
    }

    [Test]
    public void MaxMipCount_CountsDownToOneByOne()
    {
        Assert.AreEqual(1, AITTextureStreamPlanner.MaxMipCount(1, 1));
        Assert.AreEqual(12, AITTextureStreamPlanner.MaxMipCount(2048, 2048));
        Assert.AreEqual(12, AITTextureStreamPlanner.MaxMipCount(2048, 4));
        Assert.AreEqual(3, AITTextureStreamPlanner.MaxMipCount(3, 4));
    }

    // ─────────────────────── 매니페스트 JSON ───────────────────────

    [Test]
    public void BuildEntryJson_ReadableOriginal_OmitsNonReadable_AndRaw()
    {
        string json = AITTextureStreamPlanner.BuildEntryJson(new AITTextureStreamPlanner.EntrySpec
        {
            guid = "0123456789abcdef0123456789abcdef", name = "bg", file = "g.png", width = 2048, height = 1024,
        });
        StringAssert.DoesNotContain("nonReadable", json, "readable 원본은 필드를 쓰지 않는다(구 매니페스트와 같은 의미: readable 복원).");
        StringAssert.DoesNotContain("raw", json);
        StringAssert.DoesNotContain("encoding", json);
        StringAssert.DoesNotContain("\"sw\"", json);
        StringAssert.StartsWith("{\"guid\":\"0123456789abcdef0123456789abcdef\"", json, "guid 는 AITDataBreakdownReport 정규식 계약상 첫 필드.");
    }

    [Test]
    public void BuildEntryJson_NonReadableOriginal_EmitsFlag()
    {
        string json = AITTextureStreamPlanner.BuildEntryJson(new AITTextureStreamPlanner.EntrySpec
        {
            guid = "0123456789abcdef0123456789abcdef", name = "bg", file = "g.png", width = 4, height = 4, nonReadable = true,
        });
        StringAssert.Contains("\"nonReadable\":1", json);
    }

    [Test]
    public void BuildEntryJson_FullEntry_EscapesNameAndCarriesRawFields()
    {
        string json = AITTextureStreamPlanner.BuildEntryJson(new AITTextureStreamPlanner.EntrySpec
        {
            guid = "0123456789abcdef0123456789abcdef", name = "a\"b\\c", file = "g.png.br", fileBrotli = true,
            width = 2048, height = 2048, sw = 1024, sh = 1024, nonReadable = true,
            rawFile = "g.astc.br", rawBrotli = true, rawFormat = 49, rawMips = 1, rawSize = 1871424,
        });
        StringAssert.Contains("\"name\":\"a\\\"b\\\\c\"", json);
        StringAssert.Contains("\"encoding\":\"br\"", json);
        StringAssert.Contains("\"sw\":1024,\"sh\":1024", json);
        StringAssert.Contains("\"rawFile\":\"g.astc.br\"", json);
        StringAssert.Contains("\"rawEncoding\":\"br\"", json);
        StringAssert.Contains("\"rawFormat\":49,\"rawMips\":1,\"rawSize\":1871424", json);
    }

    [Test]
    public void BuildEntryJson_Keys_AreAllFieldsOfRuntimeEntry()
    {
        // 런타임 Entry(JsonUtility 가 필드 이름으로 채운다)와 키 이름이 어긋나면 값이 조용히 버려진다.
        string json = AITTextureStreamPlanner.BuildEntryJson(new AITTextureStreamPlanner.EntrySpec
        {
            guid = "0123456789abcdef0123456789abcdef", name = "n", file = "f", fileBrotli = true,
            width = 1, height = 1, sw = 1, sh = 1, nonReadable = true,
            rawFile = "r", rawBrotli = true, rawFormat = 1, rawMips = 1, rawSize = 1,
        });

        Type entry = typeof(AITStreamingTexture).GetNestedType("Entry", BindingFlags.NonPublic);
        Assert.IsNotNull(entry, "AITStreamingTexture.Entry 를 찾지 못했다(이름이 바뀌면 이 테스트와 매니페스트 계약을 함께 갱신).");
        var fieldNames = new HashSet<string>();
        foreach (var f in entry.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            fieldNames.Add(f.Name);
        }

        foreach (Match m in Regex.Matches(json, "\"([A-Za-z]+)\":"))
        {
            Assert.IsTrue(fieldNames.Contains(m.Groups[1].Value), $"매니페스트 키 '{m.Groups[1].Value}' 에 대응하는 런타임 Entry 필드가 없다.");
        }
    }

    [Test]
    public void BuildManifestJson_WrapsEntries()
    {
        Assert.AreEqual("{\"maxConcurrent\":1,\"entries\":[{\"a\":1},{\"b\":2}]}",
            AITTextureStreamPlanner.BuildManifestJson(1, new List<string> { "{\"a\":1}", "{\"b\":2}" }));
        Assert.AreEqual("{\"maxConcurrent\":3,\"entries\":[]}",
            AITTextureStreamPlanner.BuildManifestJson(3, new List<string>()));
    }

    // ─────────────────────── 런타임 순수 판정 ───────────────────────

    [Test]
    public void Runtime_ResolveMaxConcurrent_LowMemTierForcesOne()
    {
        Assert.AreEqual(1, AITStreamingTexture.ResolveMaxConcurrent(0, 0), "매니페스트에 값이 없으면 기본 1.");
        Assert.AreEqual(3, AITStreamingTexture.ResolveMaxConcurrent(3, 0), "정상 기기는 매니페스트 값을 따른다.");
        Assert.AreEqual(1, AITStreamingTexture.ResolveMaxConcurrent(3, 1), "저사양 티어 1 은 강제 1.");
        Assert.AreEqual(1, AITStreamingTexture.ResolveMaxConcurrent(8, 2), "저사양 티어 2 도 강제 1.");
    }

    [Test]
    public void Runtime_ShouldMarkNonReadable_FlagZeroKeepsLegacyReadable()
    {
        Assert.IsFalse(AITStreamingTexture.ShouldMarkNonReadable(0), "필드 없음/0 = 기존처럼 readable 복원.");
        Assert.IsTrue(AITStreamingTexture.ShouldMarkNonReadable(1));
    }

    [Test]
    public void Runtime_IsRawUsable_RequiresExactFormatMipsSupportAndNoPriorFailure()
    {
        const int astc6 = 49;
        Assert.IsTrue(AITStreamingTexture.IsRawUsable("a.astc", false, astc6, 1, 100, true, astc6, 1));
        Assert.IsFalse(AITStreamingTexture.IsRawUsable(null, false, astc6, 1, 100, true, astc6, 1), "raw 없음.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("", false, astc6, 1, 100, true, astc6, 1), "raw 없음.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("a.astc", true, astc6, 1, 100, true, astc6, 1), "이전에 실패 → 폴백.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("a.astc", false, astc6, 1, 100, false, astc6, 1), "기기 미지원 → PNG 폴백.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("a.astc", false, astc6, 1, 100, true, 4, 1), "스텁 포맷 불일치 → 폴백.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("a.astc", false, astc6, 12, 100, true, astc6, 1), "mip 수 불일치 → 폴백.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("a.astc", false, 0, 1, 100, true, 0, 1), "포맷 값 없음.");
        Assert.IsFalse(AITStreamingTexture.IsRawUsable("a.astc", false, astc6, 1, 0, true, astc6, 1), "크기 값 없음.");
    }
}
