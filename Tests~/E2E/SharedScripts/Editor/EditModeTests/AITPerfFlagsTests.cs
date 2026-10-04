// -----------------------------------------------------------------------
// AITPerfFlagsTests.cs - 모바일 런타임 최적화 플래그의 tri-state 해석과 JSON 직렬화 검증
//   · 신규 필드 선언 기본값(-1 자동, audioForceCompressedMinSeconds 10)
//   · 자동(-1) 실효값: adaptiveFrameRate / releaseConsumedData / audioForceCompressed / antialias drop 은 꺼짐, 나머지는 켜짐
//   · 명시 0/1 이 자동을 이김
//   · __AIT_PERF JSON 키 계약과 AITJsStringEscaper 왕복
//   · WebGLBuildCopier 패치 헬퍼(ResolveRenamed, IsUnitywebBuild, MeasureDataRawSizeIfEnabled)
// -----------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
[Category("Unit")]
public class AITPerfFlagsTests
{
    private AITEditorScriptObject _config;
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ait-perfflags-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
        if (_tempDir != null && System.IO.Directory.Exists(_tempDir))
        {
            System.IO.Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void NewFields_DeclareAutoDefaults()
    {
        Assert.AreEqual(-1, _config.webglAntialiasOpt);
        Assert.AreEqual(-1, _config.webglContextRecovery);
        Assert.AreEqual(-1, _config.frameRateCap);
        Assert.AreEqual(-1, _config.adaptiveFrameRate);
        Assert.AreEqual(-1, _config.mobileLifecycle);
        Assert.AreEqual(-1, _config.memoryTelemetry);
        Assert.AreEqual(-1, _config.exactDataBody);
        Assert.AreEqual(-1, _config.releaseConsumedData);
        Assert.AreEqual(-1, _config.audioForceCompressedPlayback);
        Assert.AreEqual(10f, _config.audioForceCompressedMinSeconds);
    }

    [Test]
    public void Auto_ResolvesToDocumentedEffectiveValues()
    {
        Assert.IsTrue(AITPerfFlags.EffectiveGlHook(_config), "자동: 훅(기록 + probe 해제)은 켜짐");
        Assert.IsFalse(AITPerfFlags.EffectiveGlDropAntialias(_config), "자동: antialias 끄기는 검증 전까지 꺼짐");
        Assert.IsTrue(AITPerfFlags.EffectiveGlContextRecovery(_config));
        Assert.AreEqual(60, AITPerfFlags.EffectiveFrameRateCap(_config));
        Assert.IsFalse(AITPerfFlags.EffectiveAdaptiveFrameRate(_config), "자동: adaptive 는 꺼짐");
        Assert.IsTrue(AITPerfFlags.EffectiveMobileLifecycle(_config));
        Assert.IsTrue(AITPerfFlags.EffectiveMemoryTelemetry(_config));
        Assert.IsTrue(AITPerfFlags.EffectiveExactDataBody(_config));
        Assert.IsFalse(AITPerfFlags.EffectiveReleaseConsumedData(_config), "자동: releaseConsumedData 는 꺼짐");
        Assert.IsFalse(AITPerfFlags.EffectiveAudioForceCompressed(_config), "자동: 강제 압축 재생은 꺼짐");
        Assert.AreEqual(10f, AITPerfFlags.EffectiveAudioForceCompressedMinSeconds(_config));
    }

    [Test]
    public void ExplicitValues_OverrideAuto()
    {
        _config.webglAntialiasOpt = 0;
        Assert.IsFalse(AITPerfFlags.EffectiveGlHook(_config), "0 은 훅 자체를 끈다");
        Assert.IsFalse(AITPerfFlags.EffectiveGlDropAntialias(_config));

        _config.webglAntialiasOpt = 1;
        Assert.IsTrue(AITPerfFlags.EffectiveGlHook(_config));
        Assert.IsTrue(AITPerfFlags.EffectiveGlDropAntialias(_config));

        _config.frameRateCap = 0;
        Assert.AreEqual(0, AITPerfFlags.EffectiveFrameRateCap(_config));
        _config.frameRateCap = 1;
        Assert.AreEqual(60, AITPerfFlags.EffectiveFrameRateCap(_config));

        _config.adaptiveFrameRate = 1;
        _config.releaseConsumedData = 1;
        _config.audioForceCompressedPlayback = 1;
        Assert.IsTrue(AITPerfFlags.EffectiveAdaptiveFrameRate(_config));
        Assert.IsTrue(AITPerfFlags.EffectiveReleaseConsumedData(_config));
        Assert.IsTrue(AITPerfFlags.EffectiveAudioForceCompressed(_config));

        _config.mobileLifecycle = 0;
        _config.memoryTelemetry = 0;
        _config.exactDataBody = 0;
        _config.webglContextRecovery = 0;
        Assert.IsFalse(AITPerfFlags.EffectiveMobileLifecycle(_config));
        Assert.IsFalse(AITPerfFlags.EffectiveMemoryTelemetry(_config));
        Assert.IsFalse(AITPerfFlags.EffectiveExactDataBody(_config));
        Assert.IsFalse(AITPerfFlags.EffectiveGlContextRecovery(_config));
    }

    [TestCase(0f)]
    [TestCase(-5f)]
    [TestCase(float.NaN)]
    public void AudioForceMinSeconds_NonPositiveFallsBackToTen(float stored)
    {
        _config.audioForceCompressedMinSeconds = stored;
        Assert.AreEqual(10f, AITPerfFlags.EffectiveAudioForceCompressedMinSeconds(_config));
    }

    [Test]
    public void AudioForceMinSeconds_PositiveValueIsKept()
    {
        _config.audioForceCompressedMinSeconds = 30f;
        Assert.AreEqual(30f, AITPerfFlags.EffectiveAudioForceCompressedMinSeconds(_config));
    }

    [Test]
    public void NullConfig_FailsOpenToAutoDefaults()
    {
        Assert.IsTrue(AITPerfFlags.EffectiveGlHook(null));
        Assert.IsFalse(AITPerfFlags.EffectiveGlDropAntialias(null));
        Assert.IsFalse(AITPerfFlags.EffectiveAdaptiveFrameRate(null));
        Assert.IsFalse(AITPerfFlags.EffectiveReleaseConsumedData(null));
        Assert.IsFalse(AITPerfFlags.EffectiveAudioForceCompressed(null));
        Assert.DoesNotThrow(() => AITPerfFlags.ToJson(null, -1, false));
    }

    [Test]
    public void ToJson_Default_ContainsContractKeysAndAutoValues()
    {
        string json = AITPerfFlags.ToJson(_config, -1, false);

        StringAssert.StartsWith("{", json);
        StringAssert.EndsWith("}", json);
        foreach (string expected in new[]
                 {
                     "\"v\":1",
                     "\"patchSet\":" + AITPatchedFileNaming.PatchSetVersion,
                     "\"glHook\":true",
                     "\"glDropAntialias\":false",
                     "\"glContextRecovery\":true",
                     "\"frameRateCap\":60",
                     "\"adaptiveFrameRate\":false",
                     "\"mobileLifecycle\":true",
                     "\"memoryTelemetry\":true",
                     "\"exactDataBody\":true",
                     "\"dataRawSize\":-1",
                     "\"releaseConsumedData\":false",
                     "\"audioForceCompressed\":false",
                     "\"audioForceCompressedMinSeconds\":10",
                     "\"unityweb\":false",
                     "\"audioPatched\":false",
                     "\"raw\":{",
                 })
        {
            StringAssert.Contains(expected, json);
        }
    }

    [Test]
    public void ToJson_AudioPatched_ReflectsArgument()
    {
        StringAssert.Contains("\"audioPatched\":true", AITPerfFlags.ToJson(_config, -1, false, true));
        StringAssert.Contains("\"audioPatched\":false", AITPerfFlags.ToJson(_config, -1, false, false));
    }

    [Test]
    public void ToJson_ReflectsAllZeroAndDataSize()
    {
        HeavyBuildVariants.ApplyAll0(_config);
        string json = AITPerfFlags.ToJson(_config, 123456789L, true);

        StringAssert.Contains("\"glHook\":false", json);
        StringAssert.Contains("\"glDropAntialias\":false", json);
        StringAssert.Contains("\"glContextRecovery\":false", json);
        StringAssert.Contains("\"frameRateCap\":0", json);
        StringAssert.Contains("\"adaptiveFrameRate\":false", json);
        StringAssert.Contains("\"mobileLifecycle\":false", json);
        StringAssert.Contains("\"memoryTelemetry\":false", json);
        StringAssert.Contains("\"exactDataBody\":false", json);
        StringAssert.Contains("\"releaseConsumedData\":false", json);
        StringAssert.Contains("\"audioForceCompressed\":false", json);
        StringAssert.Contains("\"dataRawSize\":123456789", json);
        StringAssert.Contains("\"unityweb\":true", json);
        StringAssert.Contains("\"webglAntialiasOpt\":0", json, "raw 블록은 설정의 원본 tri-state 를 그대로 싣는다");
    }

    [Test]
    public void ToJson_IsStable_AndSafeInsideSingleQuotedJsLiteral()
    {
        string json = AITPerfFlags.ToJson(_config, 42L, false);
        Assert.AreEqual(json, AITPerfFlags.ToJson(_config, 42L, false), "같은 입력이면 같은 출력");

        // 템플릿은 JSON.parse('...') 안에 넣는다. 이스케이프가 JSON 을 바꾸면(따옴표·백슬래시 등) 안 된다.
        // 값이 bool/숫자뿐이므로 이스케이프는 항등이어야 한다.
        Assert.AreEqual(json, AITJsStringEscaper.EscapeSingleQuoted(json));
        StringAssert.DoesNotContain("'", json);
        StringAssert.DoesNotContain("\n", json);
    }

    [Test]
    public void ToJson_UsesInvariantCulture_ForFloat()
    {
        _config.audioForceCompressedMinSeconds = 12.5f;
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            StringAssert.Contains("\"audioForceCompressedMinSeconds\":12.5", AITPerfFlags.ToJson(_config, -1, false));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    // ---- WebGLBuildCopier 패치 호출 지점 헬퍼 ----

    [Test]
    public void IsUnitywebBuild_DetectsMarkerOrExtension()
    {
        Assert.IsTrue(WebGLBuildCopier.IsUnitywebBuild(true, "a.loader.js", "a.data", "a.framework.js", "a.wasm"));
        Assert.IsTrue(WebGLBuildCopier.IsUnitywebBuild(false, "a.loader.js", "a.data.unityweb", "a.framework.js.unityweb", "a.wasm.unityweb"));
        Assert.IsFalse(WebGLBuildCopier.IsUnitywebBuild(false, "a.loader.js", "a.data.br", "a.framework.js.br", "a.wasm.br"));
        Assert.IsFalse(WebGLBuildCopier.IsUnitywebBuild(false, null, "", null));
    }

    [Test]
    public void ResolveRenamed_FollowsChain_AndPassesThroughUnknown()
    {
        var renames = new Dictionary<string, string>
        {
            { "a", "b" },
            { "b", "c" },
            { "loop1", "loop2" },
            { "loop2", "loop1" },
        };
        Assert.AreEqual("c", WebGLBuildCopier.ResolveRenamed("a", renames));
        Assert.AreEqual("untouched", WebGLBuildCopier.ResolveRenamed("untouched", renames));
        Assert.IsNull(WebGLBuildCopier.ResolveRenamed(null, renames));
        Assert.AreEqual("a", WebGLBuildCopier.ResolveRenamed("a", null));
        Assert.DoesNotThrow(() => WebGLBuildCopier.ResolveRenamed("loop1", renames), "순환 rename 맵에서도 끝나야 한다");
    }

    [Test]
    public void ApplyBuildPatches_StubPatchers_ChangeNothing()
    {
        string loader = "h.loader.js", data = "h.data.br", framework = "h.framework.js.br", wasm = "h.wasm.br", symbols = null;
        int patched = WebGLBuildCopier.ApplyBuildPatches(_config, _tempDir, false,
            ref loader, ref data, ref framework, ref wasm, ref symbols);

        // 후속 배치가 패처를 채우면 이 단언은 해당 배치의 테스트로 대체된다 — 여기서는 호출 지점이 이름을 깨뜨리지 않는지만 본다.
        Assert.GreaterOrEqual(patched, 0);
        if (patched == 0)
        {
            Assert.AreEqual("h.loader.js", loader);
            Assert.AreEqual("h.data.br", data);
            Assert.AreEqual("h.framework.js.br", framework);
            Assert.AreEqual("h.wasm.br", wasm);
            Assert.IsNull(symbols);
        }
    }

    [Test]
    public void ApplyBuildPatches_Unityweb_SkipsEverything()
    {
        string loader = "h.loader.js", data = "h.data.unityweb", framework = "h.framework.js.unityweb", wasm = "h.wasm.unityweb", symbols = null;
        int patched = WebGLBuildCopier.ApplyBuildPatches(_config, _tempDir, true,
            ref loader, ref data, ref framework, ref wasm, ref symbols);

        Assert.AreEqual(0, patched);
        Assert.AreEqual("h.framework.js.unityweb", framework);
    }

    [Test]
    public void MeasureDataRawSizeIfEnabled_ReturnsMinusOne_WhenDisabledOrUnityweb()
    {
        _config.exactDataBody = 0;
        Assert.AreEqual(-1L, WebGLBuildCopier.MeasureDataRawSizeIfEnabled(_config, _tempDir, "h.data.br", false));

        _config.exactDataBody = 1;
        Assert.AreEqual(-1L, WebGLBuildCopier.MeasureDataRawSizeIfEnabled(_config, _tempDir, "h.data.unityweb", true));
        Assert.AreEqual(-1L, WebGLBuildCopier.MeasureDataRawSizeIfEnabled(_config, _tempDir, "", false));
    }
}
