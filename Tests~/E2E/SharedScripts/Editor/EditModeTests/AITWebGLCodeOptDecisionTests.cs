// -----------------------------------------------------------------------
// AITWebGLCodeOptDecisionTests.cs - WebGL code optimization 적용 정책 결정 검증
// Level 0: AITWebGLCodeOptimization.ResolveDecision 순수 로직(버전 게이트 × config × env 킬스위치)
// 데이터 주도 검증. PlayerSettings/Application 등 실제 API를 건드리지 않는 순수 함수이므로
// 실행 중인 Unity 버전과 무관하게 항상 전건 실행된다(Assert.Ignore 없음).
//
// Unity 6000.0 OOM 회피가 "완전 스킵"(webGLCodeOptOomSkipped)에서 "LTO 제외 DiskSize 적용"
// (ResolveDecision 기반)으로 바뀐 회귀 방지용. 이전에는 AITBuildInitializer.cs의 6000.0 분기를
// 커버하는 EditMode 테스트가 0건이었다.
//
// 특히 강제 오버라이드(env로 특정 멤버 강제) 분기의 AllowLto가 버전 게이트 값을 무시하고
// 무조건 true로 고정되면, 강제 멤버명이 오타/버전 불일치로 enum에 없을 때(TrySetByName 실패)
// 호출자의 폴백 사다리가 AllowLto=true로 타 6000.0에서 DiskSizeLTO(OOM 레버)가 조용히
// 재활성화되는 치명 결함이 된다 — ResolveDecision_EnvForcedMember_KeepsVersionGatedAllowLto*
// 테스트가 이 회귀를 전담 방지한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITWebGLCodeOptDecisionTests
{
    // =================================================================
    // 버전 게이트: 6000.0.x만 AllowLto=false, 그 외 버전은 true
    // =================================================================

    [TestCase("6000.0.58f1", false)]   // OOM 위험 버전 → LTO 제외
    [TestCase("6000.0.1f1", false)]
    [TestCase("6000.1.0f1", true)]     // 신형 툴체인 — LTO 정상 링크 확인됨
    [TestCase("6000.3.4f1", true)]
    [TestCase("2021.3.45f1", true)]
    [TestCase("2022.3.62f2", true)]
    public void ResolveDecision_VersionGate_ControlsAllowLto(string version, bool expectedAllowLto)
    {
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: true, version, envValue: null);

        Assert.IsTrue(decision.Apply, "configApply=true, env 미지정이면 항상 적용되어야 합니다.");
        Assert.AreEqual(expectedAllowLto, decision.AllowLto,
            $"버전 '{version}'의 AllowLto 기대값과 다릅니다.");
        Assert.IsNull(decision.ForcedMember, "env 미지정 시 ForcedMember는 null이어야 합니다.");
    }

    // =================================================================
    // editorConfig 시맨틱(0=미적용) 유지: env가 auto/빈값이면 GUI 값을 그대로 따른다
    // =================================================================

    [Test]
    public void ResolveDecision_ConfigOff_EnvAuto_DoesNotApply()
    {
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: false, "6000.0.58f1", envValue: null);

        Assert.IsFalse(decision.Apply, "GUI '미적용'(0)이고 env 오버라이드가 없으면 적용하지 않아야 합니다.");
        Assert.IsNull(decision.ForcedMember);
    }

    [Test]
    public void ResolveDecision_ConfigOn_EnvAuto_Applies()
    {
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: true, "2022.3.62f2", envValue: "auto");

        Assert.IsTrue(decision.Apply);
        Assert.IsTrue(decision.AllowLto, "6000.0이 아닌 버전은 LTO를 허용해야 합니다.");
        Assert.IsNull(decision.ForcedMember);
    }

    // =================================================================
    // 킬스위치: off/none/false(대소문자 무관)는 GUI 설정·Unity 버전과 무관하게 항상 미적용
    // (6000.0뿐 아니라 모든 버전에서 적용을 끈다는 점이 핵심 — plan 상 정밀도 교정 사항)
    // =================================================================

    [TestCase("off")]
    [TestCase("OFF")]
    [TestCase("Off")]
    [TestCase("none")]
    [TestCase("NONE")]
    [TestCase("false")]
    [TestCase("FALSE")]
    public void ResolveDecision_EnvKillSwitch_DoesNotApply_OnRiskyVersion(string env)
    {
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: true, "6000.0.58f1", env);

        Assert.IsFalse(decision.Apply, $"env='{env}'는 GUI 설정과 무관하게 미적용이어야 합니다.");
        Assert.IsNull(decision.ForcedMember);
    }

    [TestCase("off")]
    [TestCase("none")]
    [TestCase("false")]
    public void ResolveDecision_EnvKillSwitch_DoesNotApply_OnNonRiskyVersion(string env)
    {
        // 킬스위치는 6000.0 한정이 아니라 모든 Unity 버전에서 적용을 끈다.
        // 2022.3/6000.3 등 기존에는 DiskSizeLTO가 정상 적용되던 버전에서도 env=off면 꺼져야 한다.
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: true, "6000.3.4f1", env);

        Assert.IsFalse(decision.Apply,
            "킬스위치는 6000.0뿐 아니라 모든 Unity 버전에서 code optimization 적용을 꺼야 합니다.");
        Assert.IsNull(decision.ForcedMember);
    }

    [Test]
    public void ResolveDecision_EnvKillSwitch_OverridesConfigOn()
    {
        // configApply=true(GUI '적용')여도 킬스위치가 이긴다.
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: true, "2022.3.62f2", "off");

        Assert.IsFalse(decision.Apply);
    }

    // =================================================================
    // 강제 오버라이드: env가 off/none/false/auto/빈값이 아니면 해당 멤버를 강제한다.
    // GUI '미적용'(configApply=false)도 무시하고 Apply=true가 된다 — 의도된 운영자 오버라이드.
    // =================================================================

    [Test]
    public void ResolveDecision_EnvForcedMember_OverridesConfigOff()
    {
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: false, "6000.0.58f1", "DiskSizeLTO");

        Assert.IsTrue(decision.Apply,
            "env로 특정 멤버를 강제하면 GUI '미적용'(0)도 무시하고 적용되어야 합니다(의도된 오버라이드).");
        Assert.AreEqual("DiskSizeLTO", decision.ForcedMember);
    }

    // =================================================================
    // [blocker 수정 검증] 강제 오버라이드 분기에서도 AllowLto는 버전 게이트 값을 유지해야 한다.
    // 무조건 true로 고정하면, 강제 멤버명이 오타/버전 불일치로 enum에 없어 TrySetByName이
    // 실패했을 때 호출자의 폴백 사다리(TrySetBestAvailable(decision.AllowLto))가 6000.0에서도
    // DiskSizeLTO(OOM 레버)부터 다시 타는 치명적 회귀가 발생한다.
    // =================================================================

    [TestCase("6000.0.58f1", false)]
    [TestCase("6000.1.0f1", true)]
    [TestCase("6000.3.4f1", true)]
    [TestCase("2021.3.45f1", true)]
    public void ResolveDecision_EnvForcedMember_KeepsVersionGatedAllowLto(string version, bool expectedAllowLto)
    {
        // 강제 대상 멤버명은 임의 문자열(오타 포함 가능) — ResolveDecision은 이 이름의 유효성을
        // 검증하지 않는다(유효성 검증/폴백은 호출자 AITBuildInitializer + TrySetByName의 몫).
        var decision = AITWebGLCodeOptimization.ResolveDecision(configApply: true, version, "SomeForcedMember");

        Assert.IsTrue(decision.Apply);
        Assert.AreEqual("SomeForcedMember", decision.ForcedMember);
        Assert.AreEqual(expectedAllowLto, decision.AllowLto,
            $"강제 오버라이드 분기에서도 AllowLto는 버전 '{version}'의 게이트 값을 유지해야 합니다 " +
            "(무조건 true 고정 시 6000.0에서 강제 실패 후 폴백이 DiskSizeLTO를 재활성화하는 회귀).");
    }

    [Test]
    public void ResolveDecision_EnvForcedMember_OnRiskyVersion_NeverForcesAllowLtoTrue()
    {
        // 위 데이터 주도 테스트의 6000.0 케이스를 별도로 못박아 둔다: 강제 멤버명이 6000.0의
        // WasmCodeOptimization enum에 없어(오타 등) TrySetByName이 실패하더라도, 이 Decision을
        // 그대로 TrySetBestAvailable(decision.AllowLto)에 넘기면 사다리는 DiskSize(2순위)부터
        // 타야 하며 DiskSizeLTO(1순위, OOM 레버)로 폴백해서는 안 된다.
        var decision = AITWebGLCodeOptimization.ResolveDecision(
            configApply: true, "6000.0.58f1", "TypoedMemberNameThatDoesNotExist");

        Assert.IsFalse(decision.AllowLto,
            "6000.0에서는 강제 오버라이드가 실패하더라도 AllowLto=false를 유지해 OOM 레버를 막아야 합니다.");
    }
}
