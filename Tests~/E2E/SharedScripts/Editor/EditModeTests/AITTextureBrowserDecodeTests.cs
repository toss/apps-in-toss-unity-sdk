// -----------------------------------------------------------------------
// AITTextureBrowserDecodeTests.cs - 텍스처 스트리밍 브라우저 디코드 매니페스트 필드와 적격 판정 검증
// Level 0: 빌드타임 순수 로직(AITTextureStreamPlanner/AITLargeTextureExternalizer) + 런타임 순수 판정(AITStreamingTexture).
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITTextureBrowserDecodeTests
{
    private static readonly Regex BrowserDecodeKey = new Regex("\"browserDecode\"\\s*:\\s*1", RegexOptions.Compiled);

    [Test]
    public void ManifestJson_WritesBrowserDecode_OnlyWhenEnabled()
    {
        var entries = new List<string>();
        Assert.IsTrue(BrowserDecodeKey.IsMatch(AITTextureStreamPlanner.BuildManifestJson(1, entries, true)));
        Assert.IsFalse(BrowserDecodeKey.IsMatch(AITTextureStreamPlanner.BuildManifestJson(1, entries, false)));
        Assert.IsFalse(BrowserDecodeKey.IsMatch(AITTextureStreamPlanner.BuildManifestJson(1, entries)),
            "구 시그니처는 필드를 쓰지 않는다(구 런타임 호환).");
    }

    [Test]
    public void ManifestJson_WithBrowserDecode_IsParsableJson()
    {
        string json = AITTextureStreamPlanner.BuildManifestJson(2, new List<string>(), true);
        var parsed = JsonUtility.FromJson<ManifestProbe>(json);
        Assert.AreEqual(2, parsed.maxConcurrent);
        Assert.AreEqual(1, parsed.browserDecode, "런타임 Manifest.browserDecode 필드와 같은 이름이어야 한다.");
    }

    [Serializable]
    private class ManifestProbe
    {
        public int maxConcurrent;
        public int browserDecode;
    }

    [Test]
    public void ResolveBrowserDecode_AutoOn_ExplicitAndEnvOverride()
    {
        string prev = Environment.GetEnvironmentVariable(AITTextureStreamPlanner.BrowserDecodeEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.BrowserDecodeEnvVar, null);
            var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                Assert.AreEqual(-1, config.textureStreamBrowserDecode);
                Assert.IsTrue(AITLargeTextureExternalizer.ResolveBrowserDecode(config), "자동은 켬.");
                Assert.IsTrue(AITLargeTextureExternalizer.ResolveBrowserDecode(null));

                config.textureStreamBrowserDecode = 0;
                Assert.IsFalse(AITLargeTextureExternalizer.ResolveBrowserDecode(config));

                Environment.SetEnvironmentVariable(AITTextureStreamPlanner.BrowserDecodeEnvVar, "1");
                Assert.IsTrue(AITLargeTextureExternalizer.ResolveBrowserDecode(config), "환경 변수가 설정값보다 우선.");

                config.textureStreamBrowserDecode = 1;
                Environment.SetEnvironmentVariable(AITTextureStreamPlanner.BrowserDecodeEnvVar, "0");
                Assert.IsFalse(AITLargeTextureExternalizer.ResolveBrowserDecode(config));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.BrowserDecodeEnvVar, prev);
        }
    }

    [Test]
    public void ResolveBrowserDecodeEnabled_NeedsManifestFlagAndWebGl2()
    {
        Assert.IsTrue(AITStreamingTexture.ResolveBrowserDecodeEnabled(1, 2));
        Assert.IsFalse(AITStreamingTexture.ResolveBrowserDecodeEnabled(0, 2), "구 매니페스트(필드 없음)는 LoadImage.");
        Assert.IsFalse(AITStreamingTexture.ResolveBrowserDecodeEnabled(1, 0), "능력 없음(WebGL 1/createImageBitmap 없음).");
    }

    [Test]
    public void IsBrowserDecodeUsable_Gates()
    {
        Assert.IsTrue(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "", false, false));
        Assert.IsTrue(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "br", false, false));
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(false, false, 1, false, "", false, false), "비활성");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, true, 1, false, "", false, false), "raw 는 대상 아님");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 0, false, "", false, false), "readable 원본은 LoadImage");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, true, "", false, false), "이전 실패");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "br", true, false), "br 미해제 확인 후 br 제외");
        Assert.IsTrue(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "", true, false), "br 차단은 무압축 엔트리에 영향 없음");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "", false, true), "readable 스텁은 LoadImage 경로");
    }

    // ─────────────────────── 작은 스텁 포맷 선택 ───────────────────────

    [TestCase("ASTC", "ASTC_12x12")]
    [TestCase("ETC2", "ETC2_RGB4")]
    [TestCase("DXT", "DXT1")]
    [TestCase("Generic", "DXT1")]
    [TestCase("", "DXT1")]
    public void ChooseCompactStubFormat_BySubtarget(string subtarget, string expected)
    {
        Assert.AreEqual(expected, AITTextureStreamPlanner.ChooseCompactStubFormat(subtarget));
    }

    [Test]
    public void ChooseCompactStubFormat_NamesParseAsImporterFormats()
    {
        foreach (string sub in new[] { "ASTC", "ETC2", "DXT" })
        {
            string name = AITTextureStreamPlanner.ChooseCompactStubFormat(sub);
            Assert.IsTrue(Enum.TryParse(name, out UnityEditor.TextureImporterFormat _), name + " 는 TextureImporterFormat 이어야 한다.");
        }
    }

    [Test]
    public void EstimateCompactStubBytes_IsMuchSmallerThanRgba32()
    {
        long rgba = AITTextureStreamPlanner.EstimateStubBytes(2048, 2048, 1);
        long astc = AITTextureStreamPlanner.EstimateCompactStubBytes("ASTC", 2048, 2048, 1);
        long dxt = AITTextureStreamPlanner.EstimateCompactStubBytes("DXT", 2048, 2048, 1);
        Assert.AreEqual(16L * 1048576, rgba);
        Assert.AreEqual(171L * 171 * 16, astc, "ASTC 12x12: 블록 171x171, 블록당 16B");
        Assert.AreEqual(2L * 1048576, dxt, "DXT1: 2048² = 2MB");
        Assert.Less(dxt, rgba / 4);
    }

    [Test]
    public void EstimateCompactStubBytes_CountsMipChainOnlyWhenOriginalHadMips()
    {
        long noMips = AITTextureStreamPlanner.EstimateCompactStubBytes("DXT", 1024, 1024, 1);
        long withMips = AITTextureStreamPlanner.EstimateCompactStubBytes("DXT", 1024, 1024, 11);
        Assert.Greater(withMips, noMips);
        Assert.Less(withMips, noMips * 2);
    }

    [Test]
    public void UseCompactStub_OnlyForNonReadableBrowserDecodeNonRaw()
    {
        Assert.IsTrue(AITTextureStreamPlanner.UseCompactStub(true, false, false));
        Assert.IsFalse(AITTextureStreamPlanner.UseCompactStub(false, false, false), "브라우저 디코드 꺼짐 → 기존 readable 스텁");
        Assert.IsFalse(AITTextureStreamPlanner.UseCompactStub(true, true, false), "readable 원본은 LoadImage 가 필요");
        Assert.IsTrue(AITTextureStreamPlanner.UseCompactStub(true, false, true), "raw 도 브라우저 디코드가 켜져 있으면 raw-swap 이라 작은 스텁");
        Assert.IsFalse(AITTextureStreamPlanner.UseCompactStub(false, false, true), "브라우저 디코드 꺼짐 → raw 는 원본 포맷 readable 스텁(LoadRawTextureData)");
    }

    [Test]
    public void BrowserFailureText_NotImagePrefixIsStable()
    {
        Assert.IsTrue(AITStreamingTexture.BrowserFailureText(-2).StartsWith("이미지 아님"),
            "런타임이 이 접두로 brotli 미해제를 판정한다.");
        Assert.IsNotEmpty(AITStreamingTexture.BrowserFailureText(-999));
    }

    [Test]
    public void RawSkipReason_ExplainsFormatMismatch()
    {
        int dxt5 = (int)TextureFormat.DXT5;
        string r = AITStreamingTexture.RawSkipReason("a.astc", false, 48, 12, 100, true, dxt5, 12);
        StringAssert.Contains("스텁 포맷", r);
        StringAssert.Contains("미지원", AITStreamingTexture.RawSkipReason("a.astc", false, 48, 12, 100, false, 48, 12));
    }

    // ─────────────────────── raw-swap ───────────────────────

    [Test]
    public void IsRawSwapUsable_Gates()
    {
        int astc6 = (int)TextureFormat.ASTC_6x6;
        Assert.IsTrue(AITStreamingTexture.IsRawSwapUsable("a.astc", false, astc6, 12, 1000, true, false, true));
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("", false, astc6, 12, 1000, true, false, true), "raw 사본 없음");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", true, astc6, 12, 1000, true, false, true), "이전 실패");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", false, astc6, 12, 1000, true, true, true), "readable 스텁은 LoadRawTextureData 경로");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", false, astc6, 12, 1000, true, false, false), "브라우저 경로 불가");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", false, astc6, 12, 1000, false, false, true), "JS 확장 미지원");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", false, 0, 12, 1000, true, false, true), "필드 누락");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", false, astc6, 0, 1000, true, false, true), "필드 누락");
        Assert.IsFalse(AITStreamingTexture.IsRawSwapUsable("a.astc", false, astc6, 12, 0, true, false, true), "필드 누락");
    }

    [Test]
    public void IsRawSwapUsable_DoesNotNeedStubFormatEquality()
    {
        // 스텁이 작은 DXT1/ASTC_12x12 여도 raw-swap 은 판정에 스텁 포맷을 받지 않는다(시그니처에 없다).
        Assert.IsTrue(AITStreamingTexture.IsRawSwapUsable("a.astc", false, (int)TextureFormat.ASTC_4x4, 11, 123, true, false, true));
    }

    [Test]
    public void RawSkipReason_NonReadableStub_DoesNotReportFormatMismatch()
    {
        int dxt1 = (int)TextureFormat.DXT1;
        string unsupported = AITStreamingTexture.RawSkipReason("a.astc", false, 50, 12, 100, false, dxt1, 1, false, true);
        StringAssert.Contains("미지원", unsupported);
        StringAssert.DoesNotContain("스텁 포맷", unsupported);

        string noBrowser = AITStreamingTexture.RawSkipReason("a.astc", false, 50, 12, 100, true, dxt1, 1, false, false);
        StringAssert.Contains("raw-swap 불가", noBrowser);
        StringAssert.DoesNotContain("스텁 포맷", noBrowser);

        // readable 스텁(기존 raw 경로)은 그대로 포맷 불일치를 보고한다.
        StringAssert.Contains("스텁 포맷", AITStreamingTexture.RawSkipReason("a.astc", false, 50, 12, 100, true, dxt1, 12, true, true));
    }

    [Test]
    public void BrowserFailureText_RawCodes()
    {
        StringAssert.Contains("raw", AITStreamingTexture.BrowserFailureText(-7));
        StringAssert.Contains("raw", AITStreamingTexture.BrowserFailureText(-8));
    }

    // ─────────────────────── 저사양 티어 다운스케일 ───────────────────────

    [TestCase(1, 0, 0)]
    [TestCase(0, 1, 0)]
    [TestCase(1, 1, 1)]
    [TestCase(1, 2, 2)]
    [TestCase(1, 5, 2)]
    public void ResolveDownscaleTier_NeedsManifestFlagAndTier(int manifestFlag, int tier, int expected)
    {
        Assert.AreEqual(expected, AITStreamingTexture.ResolveDownscaleTier(manifestFlag, tier));
    }

    [TestCase(2048, 2048, 0, 2048, 2048)]
    [TestCase(2048, 2048, 1, 1024, 1024)]
    [TestCase(512, 512, 1, 512, 512)]
    [TestCase(513, 100, 1, 257, 50)]
    [TestCase(2048, 1024, 2, 512, 256)]
    [TestCase(1024, 1024, 2, 512, 512)]
    [TestCase(1025, 1025, 2, 257, 257)]
    [TestCase(300, 300, 2, 150, 150)]
    [TestCase(256, 256, 2, 256, 256)]
    [TestCase(4096, 8, 2, 1024, 2)]
    [TestCase(4096, 1, 2, 1024, 1)]
    public void ComputeDownscaleSize_Math(int w, int h, int tier, int ew, int eh)
    {
        AITStreamingTexture.ComputeDownscaleSize(w, h, tier, out int rw, out int rh);
        Assert.AreEqual(ew, rw);
        Assert.AreEqual(eh, rh);
    }

    [Test]
    public void ComputeDownscaleSize_Tier2_LongestSideNeverBelow256WhenShrunk()
    {
        for (int w = 257; w <= 4096; w += 37)
        {
            AITStreamingTexture.ComputeDownscaleSize(w, w / 2 + 1, 2, out int rw, out int rh);
            Assert.GreaterOrEqual(Mathf.Max(rw, rh), 128, $"w={w}");
            Assert.GreaterOrEqual(rw, 1);
            Assert.GreaterOrEqual(rh, 1);
        }

        // 긴 변이 1024 를 넘을 때만 1/4 — 결과 긴 변은 항상 256 이상이다.
        for (int w = 1025; w <= 4096; w += 31)
        {
            AITStreamingTexture.ComputeDownscaleSize(w, 64, 2, out int rw, out _);
            Assert.GreaterOrEqual(rw, 256, $"w={w}");
        }
    }

    [TestCase(0, 12, 0)]
    [TestCase(1, 1, 0)]
    [TestCase(1, 2, 1)]
    [TestCase(1, 12, 1)]
    [TestCase(2, 12, 2)]
    [TestCase(2, 2, 1)]
    public void RawSkipLevels_Math(int tier, int mips, int expected)
    {
        Assert.AreEqual(expected, AITStreamingTexture.RawSkipLevels(tier, mips));
    }

    [Test]
    public void ManifestJson_WritesLowTierDownscale_OnlyWhenEnabled()
    {
        var entries = new List<string>();
        string on = AITTextureStreamPlanner.BuildManifestJson(1, entries, true, true);
        StringAssert.Contains("\"lowTierDownscale\":1", on);
        Assert.AreEqual(1, JsonUtility.FromJson<DownscaleManifestProbe>(on).lowTierDownscale, "런타임 Manifest.lowTierDownscale 필드와 같은 이름이어야 한다.");
        StringAssert.DoesNotContain("lowTierDownscale", AITTextureStreamPlanner.BuildManifestJson(1, entries, true, false));
        StringAssert.DoesNotContain("lowTierDownscale", AITTextureStreamPlanner.BuildManifestJson(1, entries, true), "구 시그니처는 쓰지 않는다.");
    }

    [Serializable]
    private class DownscaleManifestProbe
    {
        public int lowTierDownscale;
    }

    [Test]
    public void ResolveLowTierDownscale_AutoOn_ExplicitAndEnvOverride()
    {
        string prev = Environment.GetEnvironmentVariable(AITTextureStreamPlanner.LowTierDownscaleEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.LowTierDownscaleEnvVar, null);
            var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                Assert.AreEqual(-1, config.textureStreamLowTierDownscale);
                Assert.IsTrue(AITLargeTextureExternalizer.ResolveLowTierDownscale(config), "자동은 켬.");
                Assert.IsTrue(AITLargeTextureExternalizer.ResolveLowTierDownscale(null));

                config.textureStreamLowTierDownscale = 0;
                Assert.IsFalse(AITLargeTextureExternalizer.ResolveLowTierDownscale(config), "0 = 옵트아웃");

                config.textureStreamLowTierDownscale = 1;
                Assert.IsTrue(AITLargeTextureExternalizer.ResolveLowTierDownscale(config));

                Environment.SetEnvironmentVariable(AITTextureStreamPlanner.LowTierDownscaleEnvVar, "0");
                Assert.IsFalse(AITLargeTextureExternalizer.ResolveLowTierDownscale(config), "환경 변수가 설정값보다 우선.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(AITTextureStreamPlanner.LowTierDownscaleEnvVar, prev);
        }
    }

    // ─────────────────────── jslib 문자열 대조 ───────────────────────

    private static string ThisFile([CallerFilePath] string path = null) => path;

    private static string ReadRepoFile(string relative)
    {
        string dir = Path.GetDirectoryName(ThisFile());
        if (string.IsNullOrEmpty(dir)) return null;
        string full = Path.GetFullPath(Path.Combine(dir, "../../../../../", relative));
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    [Test]
    public void Jslib_RawSwapAndDownscale_ContractStrings()
    {
        string js = ReadRepoFile("Runtime/Helpers/Plugins/AppsInToss-TextureDecode.jslib");
        string cs = ReadRepoFile("Runtime/Helpers/AIT.StreamingTexture.cs");
        if (js == null || cs == null)
        {
            Assert.Ignore("소스 경로를 찾지 못했다(패키지가 복사된 환경). 대조를 건너뛴다.");
        }

        StringAssert.Contains("__AITTexDecode_RawSupported: function(format)", js);
        StringAssert.Contains("__AITTexDecode_RawStart: function(urlPtr, reqId, format, srgb, mips, rawSize, skipLevels, width, height)", js);
        StringAssert.Contains("compressedTexImage2D", js);
        StringAssert.Contains("WEBGL_compressed_texture_astc", js);
        StringAssert.Contains("resizeWidth: resizeW", js);
        StringAssert.Contains("resizeQuality: 'medium'", js);
        StringAssert.Contains("imageOrientation: 'flipY'", js);

        // raw 경로에는 이미지 디코드 옵션(imageOrientation)이 없다 — Unity 네이티브 업로드와 같은 방향으로 뒤집지 않고 올린다.
        int rawStart = js.IndexOf("__AITTexDecode_RawStart", StringComparison.Ordinal);
        int pollStart = js.IndexOf("__AITTexDecode_Poll", StringComparison.Ordinal);
        Assert.Greater(pollStart, rawStart);
        StringAssert.DoesNotContain("imageOrientation", js.Substring(rawStart, pollStart - rawStart));

        // C# extern 인자 개수와 jslib 함수 인자 개수가 같다.
        foreach (string name in new[] { "Start", "RawSupported", "RawStart", "Swap" })
        {
            var csM = System.Text.RegularExpressions.Regex.Match(cs, @"static extern \w+ __AITTexDecode_" + name + @"\(([^)]*)\)");
            var jsM = System.Text.RegularExpressions.Regex.Match(js, @"__AITTexDecode_" + name + @"\s*:\s*function\s*\(([^)]*)\)");
            Assert.IsTrue(csM.Success, "C# extern 없음: " + name);
            Assert.IsTrue(jsM.Success, "jslib 함수 없음: " + name);
            Assert.AreEqual(csM.Groups[1].Value.Split(',').Length, jsM.Groups[1].Value.Split(',').Length, name + " 인자 개수");
        }
    }
}
