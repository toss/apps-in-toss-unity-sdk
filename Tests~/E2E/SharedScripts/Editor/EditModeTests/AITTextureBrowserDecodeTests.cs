// -----------------------------------------------------------------------
// AITTextureBrowserDecodeTests.cs - 텍스처 스트리밍 브라우저 디코드 매니페스트 필드와 적격 판정 검증
// Level 0: 빌드타임 순수 로직(AITTextureStreamPlanner/AITLargeTextureExternalizer) + 런타임 순수 판정(AITStreamingTexture).
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
        Assert.IsTrue(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "", false));
        Assert.IsTrue(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "br", false));
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(false, false, 1, false, "", false), "비활성");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, true, 1, false, "", false), "raw 는 대상 아님");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 0, false, "", false), "readable 원본은 LoadImage");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, true, "", false), "이전 실패");
        Assert.IsFalse(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "br", true), "br 미해제 확인 후 br 제외");
        Assert.IsTrue(AITStreamingTexture.IsBrowserDecodeUsable(true, false, 1, false, "", true), "br 차단은 무압축 엔트리에 영향 없음");
    }

    [Test]
    public void NeedsReinitialize_OnlyWhenSizeOrFormatDiffer()
    {
        int rgba = (int)TextureFormat.RGBA32;
        Assert.IsFalse(AITStreamingTexture.NeedsReinitialize(2048, 2048, rgba, 2048, 2048));
        Assert.IsTrue(AITStreamingTexture.NeedsReinitialize(2048, 2048, rgba, 1024, 1024));
        Assert.IsTrue(AITStreamingTexture.NeedsReinitialize(2048, 2048, (int)TextureFormat.DXT5, 2048, 2048));
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
}
