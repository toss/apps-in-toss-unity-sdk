// -----------------------------------------------------------------------
// AITTextureFormatGuardTests.cs - DXT/BC WebGL 텍스처 포맷 가드 검증
// Level 0: 순수 헬퍼(분류·매핑·조치 결정·모드 해석) + 임시 텍스처 변환 통합 테스트
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureFormatGuardTests
{
    private const string TempFolder = "Assets/__AITTextureGuardTest";

    // ---------- 포맷 분류 ----------

    [TestCase(TextureImporterFormat.DXT1)]
    [TestCase(TextureImporterFormat.DXT5)]
    [TestCase(TextureImporterFormat.DXT1Crunched)]
    [TestCase(TextureImporterFormat.DXT5Crunched)]
    [TestCase(TextureImporterFormat.BC4)]
    [TestCase(TextureImporterFormat.BC5)]
    [TestCase(TextureImporterFormat.BC6H)]
    [TestCase(TextureImporterFormat.BC7)]
    public void IsMobileIncompatibleFormat_DxtBc_True(TextureImporterFormat format)
    {
        Assert.IsTrue(AITTextureFormatGuard.IsMobileIncompatibleFormat(format));
    }

    [TestCase(TextureImporterFormat.Automatic)]
    [TestCase(TextureImporterFormat.RGBA32)]
    [TestCase(TextureImporterFormat.ETC2_RGBA8)]
    [TestCase(TextureImporterFormat.ASTC_6x6)]
    public void IsMobileIncompatibleFormat_Others_False(TextureImporterFormat format)
    {
        Assert.IsFalse(AITTextureFormatGuard.IsMobileIncompatibleFormat(format));
    }

    // ---------- 변환 매핑 ----------

    [TestCase(TextureImporterFormat.DXT1)]
    [TestCase(TextureImporterFormat.DXT5)]
    [TestCase(TextureImporterFormat.BC4)]
    [TestCase(TextureImporterFormat.BC5)]
    [TestCase(TextureImporterFormat.BC7)]
    public void GetConversionTarget_Plain_UsesRecommended(TextureImporterFormat format)
    {
        Assert.AreEqual(AITTextureFormatGuard.GetRecommendedTextureFormat(),
            AITTextureFormatGuard.GetConversionTarget(format));
    }

    [Test]
    public void GetConversionTarget_Crunched_MapsPerVersion()
    {
        var dxt1 = AITTextureFormatGuard.GetConversionTarget(TextureImporterFormat.DXT1Crunched);
        var dxt5 = AITTextureFormatGuard.GetConversionTarget(TextureImporterFormat.DXT5Crunched);
#if UNITY_6000_0_OR_NEWER
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, dxt1);
        Assert.AreEqual(TextureImporterFormat.ASTC_6x6, dxt5);
#else
        Assert.AreEqual(TextureImporterFormat.ETC_RGB4Crunched, dxt1);
        Assert.AreEqual(TextureImporterFormat.ETC2_RGBA8Crunched, dxt5);
#endif
    }

    [Test]
    public void GetConversionTarget_Bc6h_HdrOnlyOnUnity6()
    {
        var target = AITTextureFormatGuard.GetConversionTarget(TextureImporterFormat.BC6H);
#if UNITY_6000_0_OR_NEWER
        Assert.AreEqual(TextureImporterFormat.ASTC_HDR_6x6, target);
#else
        Assert.IsNull(target);
#endif
    }

    [Test]
    public void GetConversionTarget_Compatible_ReturnsNull()
    {
        Assert.IsNull(AITTextureFormatGuard.GetConversionTarget(TextureImporterFormat.ASTC_6x6));
        Assert.IsNull(AITTextureFormatGuard.GetConversionTarget(TextureImporterFormat.Automatic));
    }

    // ---------- 조치 결정 ----------

    [Test]
    public void DecideAction_MatrixOfModeAndLabel()
    {
        Assert.AreEqual(AITTextureGuardAction.Convert, AITTextureFormatGuard.DecideAction(AITTextureFormatGuardMode.AutoConvert, false));
        Assert.AreEqual(AITTextureGuardAction.Warn, AITTextureFormatGuard.DecideAction(AITTextureFormatGuardMode.AutoConvert, true));
        Assert.AreEqual(AITTextureGuardAction.Warn, AITTextureFormatGuard.DecideAction(AITTextureFormatGuardMode.WarnOnly, false));
        Assert.AreEqual(AITTextureGuardAction.Warn, AITTextureFormatGuard.DecideAction(AITTextureFormatGuardMode.WarnOnly, true));
        Assert.AreEqual(AITTextureGuardAction.Skip, AITTextureFormatGuard.DecideAction(AITTextureFormatGuardMode.Off, false));
        Assert.AreEqual(AITTextureGuardAction.Skip, AITTextureFormatGuard.DecideAction(AITTextureFormatGuardMode.Off, true));
    }

    // ---------- 모드 해석 ----------

    [TestCase("auto", AITTextureFormatGuardMode.AutoConvert)]
    [TestCase("WARN", AITTextureFormatGuardMode.WarnOnly)]
    [TestCase(" off ", AITTextureFormatGuardMode.Off)]
    public void TryParseMode_Valid(string value, AITTextureFormatGuardMode expected)
    {
        Assert.IsTrue(AITTextureFormatGuard.TryParseMode(value, out var mode));
        Assert.AreEqual(expected, mode);
    }

    [Test]
    public void ResolveMode_EnvWinsOverConfig_AndReportsSource()
    {
        var mode = AITTextureFormatGuard.ResolveMode(0, "off", out string source);
        Assert.AreEqual(AITTextureFormatGuardMode.Off, mode);
        StringAssert.Contains(AITTextureFormatGuard.ModeEnvVar, source);
    }

    [Test]
    public void ResolveMode_NoEnv_UsesConfig_DefaultIsAuto()
    {
        Assert.AreEqual(AITTextureFormatGuardMode.WarnOnly, AITTextureFormatGuard.ResolveMode(1, null, out string source));
        StringAssert.Contains("프로젝트 설정", source);
        Assert.AreEqual(AITTextureFormatGuardMode.AutoConvert, AITTextureFormatGuard.ResolveMode(99, "", out _));
    }

    [Test]
    public void ResolveMode_InvalidEnv_FallsBackToConfig()
    {
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("AIT_TEXTURE_FORMAT_GUARD"));
        var mode = AITTextureFormatGuard.ResolveMode(1, "bogus", out string source);
        Assert.AreEqual(AITTextureFormatGuardMode.WarnOnly, mode);
        StringAssert.Contains("프로젝트 설정", source);
    }

    [Test]
    public void ConfigDefault_IsAutoConvert()
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try { Assert.AreEqual((int)AITTextureFormatGuardMode.AutoConvert, config.textureFormatGuardMode); }
        finally { Object.DestroyImmediate(config); }
    }

    [Test]
    public void SubtargetCompatibility()
    {
        Assert.IsTrue(AITTextureFormatGuard.IsMobileCompatibleSubtarget(WebGLTextureSubtarget.ASTC));
        Assert.IsTrue(AITTextureFormatGuard.IsMobileCompatibleSubtarget(WebGLTextureSubtarget.ETC2));
        Assert.IsFalse(AITTextureFormatGuard.IsMobileCompatibleSubtarget(WebGLTextureSubtarget.DXT));
        Assert.IsFalse(AITTextureFormatGuard.IsMobileCompatibleSubtarget(WebGLTextureSubtarget.Generic));
    }

    [Test]
    public void BuildSubtarget_AutoConvert_IsAstc_EvenFromGeneric()
    {
        var original = EditorUserBuildSettings.webGLBuildSubtarget;
        try
        {
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.Generic;
            AITTextureFormatGuard.ApplyWebGLTextureSubtarget(AITTextureFormatGuardMode.AutoConvert);
            Assert.AreEqual((int)WebGLTextureSubtarget.ASTC, AITTextureFormatGuard.GetBuildSubtarget());
        }
        finally { EditorUserBuildSettings.webGLBuildSubtarget = original; }
    }

    [Test]
    public void BuildSubtarget_WarnOnly_FollowsBuildSettings()
    {
        var original = EditorUserBuildSettings.webGLBuildSubtarget;
        try
        {
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ETC2;
            AITTextureFormatGuard.ApplyWebGLTextureSubtarget(AITTextureFormatGuardMode.WarnOnly);
            Assert.AreEqual((int)WebGLTextureSubtarget.ETC2, AITTextureFormatGuard.GetBuildSubtarget());
        }
        finally { EditorUserBuildSettings.webGLBuildSubtarget = original; }
    }

    [Test]
    public void Rgba32Bytes_IsWidthTimesHeightTimesFour()
    {
        Assert.AreEqual(64L * 1024 * 1024, AITTextureFormatGuard.Rgba32Bytes(4096, 4096));
    }

    // ---------- 임시 텍스처 통합 ----------

    [SetUp]
    public void SetUp() { CleanUp(); }

    [TearDown]
    public void TearDown() { CleanUp(); }

    private static void CleanUp()
    {
        if (!AssetDatabase.IsValidFolder(TempFolder)) return;
        AssetDatabase.DeleteAsset(TempFolder);
        AssetDatabase.Refresh();
    }

    private static string CreateDxt5Texture(string name)
    {
        if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "__AITTextureGuardTest");
        string path = $"{TempFolder}/{name}.png";
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var ps = importer.GetPlatformTextureSettings("WebGL");
        ps.overridden = true;
        ps.format = TextureImporterFormat.DXT5;
        importer.SetPlatformTextureSettings(ps);
        importer.SaveAndReimport();
        return path;
    }

    private static TextureImporterFormat WebGLFormat(string path)
    {
        return ((TextureImporter)AssetImporter.GetAtPath(path)).GetPlatformTextureSettings("WebGL").format;
    }

    [Test]
    public void RunAssetGuard_AutoConvert_ConvertsDxt5Override()
    {
        string path = CreateDxt5Texture("dxt5");
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try
        {
            var result = AITTextureFormatGuard.RunAssetGuard(AITTextureFormatGuardMode.AutoConvert, new[] { TempFolder });
            Assert.AreEqual(1, result.converted);
            Assert.AreEqual(AITTextureFormatGuard.GetRecommendedTextureFormat(), WebGLFormat(path));
        }
        finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
    }

    [Test]
    public void RunAssetGuard_KeepLabel_WarnsAndKeepsFormat()
    {
        string path = CreateDxt5Texture("kept");
        AssetDatabase.SetLabels(AssetDatabase.LoadMainAssetAtPath(path), new[] { AITTextureFormatGuard.KeepFormatLabel });
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try
        {
            var result = AITTextureFormatGuard.RunAssetGuard(AITTextureFormatGuardMode.AutoConvert, new[] { TempFolder });
            Assert.AreEqual(0, result.converted);
            Assert.AreEqual(1, result.keptByLabel);
            Assert.AreEqual(TextureImporterFormat.DXT5, WebGLFormat(path));
        }
        finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
    }

    [Test]
    public void RunAssetGuard_WarnOnlyAndOff_DoNotModify()
    {
        string path = CreateDxt5Texture("warn");
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try
        {
            var warn = AITTextureFormatGuard.RunAssetGuard(AITTextureFormatGuardMode.WarnOnly, new[] { TempFolder });
            Assert.AreEqual(0, warn.converted);
            Assert.AreEqual(1, warn.warnedIncompatible);
            Assert.AreEqual(TextureImporterFormat.DXT5, WebGLFormat(path));

            var off = AITTextureFormatGuard.RunAssetGuard(AITTextureFormatGuardMode.Off, new[] { TempFolder });
            Assert.AreEqual(0, off.converted + off.warnedIncompatible);
            Assert.AreEqual(TextureImporterFormat.DXT5, WebGLFormat(path));
        }
        finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
    }
}
