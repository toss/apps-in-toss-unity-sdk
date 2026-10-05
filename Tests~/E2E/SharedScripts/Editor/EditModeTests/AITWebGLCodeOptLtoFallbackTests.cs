// -----------------------------------------------------------------------
// AITWebGLCodeOptLtoFallbackTests.cs - 6000.0 LTO "시도 후 OOM 폴백"(P0-5) 결정 로직 검증
// Level 0: ResolveDecision(RAM 입력 오버로드)과 IsLinkOomSignature 순수 함수만 다룬다.
// 기존 3인자 ResolveDecision 계약(6000.0 = LTO 제외)은 AITWebGLCodeOptDecisionTests 가 그대로 고정한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITWebGLCodeOptLtoFallbackTests
{
    private const long Gb = 1024;

    // =================================================================
    // 6000.0 RAM 게이트
    // =================================================================

    [TestCase(64 * Gb)]
    [TestCase(32 * Gb)]
    [TestCase(31 * Gb + 800)] // 32GB 장착 Windows 머신이 보고하는 약 31.8GB
    public void Risky_EnoughRam_TriesLtoWithFallback(long ramMB)
    {
        var d = AITWebGLCodeOptimization.ResolveDecision(true, "6000.0.58f1", null, ramMB);

        Assert.IsTrue(d.Apply);
        Assert.IsTrue(d.AllowLto, "RAM 이 기준 이상이면 6000.0 도 LTO 를 시도해야 합니다.");
        Assert.IsTrue(d.LtoFallbackOnOom, "시도한 LTO 는 OOM 시 폴백 대상이어야 합니다.");
        Assert.IsNull(d.LtoSkipReason);
        Assert.IsNull(d.ForcedMember);
    }

    [TestCase(16 * Gb)]
    [TestCase(8 * Gb)]
    [TestCase(30 * Gb)]
    [TestCase(0)]
    [TestCase(-1)]
    public void Risky_LowOrUnknownRam_SkipsLtoWithoutFallback(long ramMB)
    {
        var d = AITWebGLCodeOptimization.ResolveDecision(true, "6000.0.58f1", null, ramMB);

        Assert.IsTrue(d.Apply, "LTO 만 제외하고 DiskSize 는 계속 적용해야 합니다.");
        Assert.IsFalse(d.AllowLto);
        Assert.IsFalse(d.LtoFallbackOnOom, "시도하지 않았으면 폴백할 것도 없습니다.");
        Assert.IsNotNull(d.LtoSkipReason);
        StringAssert.Contains("RAM", d.LtoSkipReason);
    }

    [Test]
    public void ThreeArgOverload_TreatsRamAsUnknown_KeepsLegacy6000Behaviour()
    {
        var d = AITWebGLCodeOptimization.ResolveDecision(true, "6000.0.58f1", null);

        Assert.IsFalse(d.AllowLto);
        Assert.IsFalse(d.LtoFallbackOnOom);
    }

    [TestCase("6000.3.4f1")]
    [TestCase("6000.1.0f1")]
    [TestCase("2022.3.62f2")]
    [TestCase("2021.3.45f1")]
    public void NonRisky_AlwaysAllowsLto_NoFallback_RegardlessOfRam(string version)
    {
        foreach (long ram in new long[] { 0, 8 * Gb, 64 * Gb })
        {
            var d = AITWebGLCodeOptimization.ResolveDecision(true, version, null, ram);

            Assert.IsTrue(d.AllowLto, $"{version}/{ram}MB");
            Assert.IsFalse(d.LtoFallbackOnOom, $"{version}: 6000.0 이 아니면 폴백 대상이 아닙니다.");
            Assert.IsNull(d.LtoSkipReason);
        }
    }

    [Test]
    public void Risky_ConfigOff_DoesNotApplyOrFallback()
    {
        var d = AITWebGLCodeOptimization.ResolveDecision(false, "6000.0.58f1", null, 64 * Gb);

        Assert.IsFalse(d.Apply);
        Assert.IsFalse(d.LtoFallbackOnOom);
    }

    [TestCase("off")]
    [TestCase("none")]
    [TestCase("false")]
    public void KillSwitch_DisablesEverything(string env)
    {
        var d = AITWebGLCodeOptimization.ResolveDecision(true, "6000.0.58f1", env, 64 * Gb);

        Assert.IsFalse(d.Apply);
        Assert.IsFalse(d.AllowLto);
        Assert.IsFalse(d.LtoFallbackOnOom);
    }

    // =================================================================
    // env 강제(lto600 측정 변형 등): 폴백하지 않는다 — 실패 자체가 측정 대상
    // =================================================================

    [Test]
    public void EnvForced_NeverArmsFallback_EvenWithEnoughRam()
    {
        var d = AITWebGLCodeOptimization.ResolveDecision(true, "6000.0.58f1", "DiskSizeLTO", 64 * Gb);

        Assert.IsTrue(d.Apply);
        Assert.AreEqual("DiskSizeLTO", d.ForcedMember);
        Assert.IsFalse(d.LtoFallbackOnOom, "강제 실행이 OOM 으로 실패하면 그대로 실패해야 측정/재현이 가능합니다.");
    }

    [Test]
    public void EnvForced_TypoOnLowRam6000_KeepsLtoExcluded()
    {
        // 강제 멤버가 enum 에 없어 TrySetByName 이 실패하면 호출자는 TrySetBestAvailable(decision.AllowLto)로 폴백한다.
        // RAM 이 모자란 6000.0 에서는 이때도 DiskSizeLTO 가 되살아나면 안 된다.
        var d = AITWebGLCodeOptimization.ResolveDecision(true, "6000.0.58f1", "Typo", 8 * Gb);

        Assert.IsFalse(d.AllowLto);
        Assert.IsFalse(d.LtoFallbackOnOom);
    }

    [Test]
    public void LtoMinSystemMemory_IsAbout32GbWithOneGbTolerance()
    {
        Assert.AreEqual(31L * 1024, AITWebGLCodeOptimization.LtoMinSystemMemoryMB);
    }

    // =================================================================
    // 링크 OOM 판정
    // =================================================================

    [TestCase("wasm-ld: error: ...\nKilled: 9")]
    [TestCase("Failed running emcc ... returned 137")]
    [TestCase("em++ terminated by SIGKILL")]
    [TestCase("LLVM ERROR: out of memory while running wasm-ld")]
    [TestCase("emscripten link step: std::bad_alloc")]
    [TestCase("Link_WebGL_wasm failed: Cannot allocate memory")]
    [TestCase("EMCC exited with code 137")]
    public void IsLinkOomSignature_True(string text)
    {
        Assert.IsTrue(AITWebGLCodeOptimization.IsLinkOomSignature(text));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Build completed with a result of 'Failed'")]
    [TestCase("error CS0103: The name 'x' does not exist")]              // 컴파일 에러
    [TestCase("Out of memory while importing texture")]                  // 링크 문맥 없는 OOM
    [TestCase("wasm-ld: error: undefined symbol: foo")]                  // 링크 문맥은 있지만 OOM 아님
    [TestCase("Killed: 9")]                                              // 문맥 없는 Killed
    public void IsLinkOomSignature_False(string text)
    {
        Assert.IsFalse(AITWebGLCodeOptimization.IsLinkOomSignature(text));
    }

    [Test]
    public void LtoFallbackArmed_DefaultsFalse_AndIsSettable()
    {
        bool before = AITWebGLCodeOptimization.LtoFallbackArmed;
        try
        {
            AITWebGLCodeOptimization.LtoFallbackArmed = true;
            Assert.IsTrue(AITWebGLCodeOptimization.LtoFallbackArmed);
            AITWebGLCodeOptimization.LtoFallbackArmed = false;
            Assert.IsFalse(AITWebGLCodeOptimization.LtoFallbackArmed);
        }
        finally
        {
            AITWebGLCodeOptimization.LtoFallbackArmed = before;
        }
    }
}
