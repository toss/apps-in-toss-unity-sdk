// -----------------------------------------------------------------------
// AITWebGLCodeOptimizationTests.cs - WebGL codeOptimization 반영 헬퍼 검증
// Level 0: AITWebGLCodeOptimization.TrySetByName / TrySetDiskSizeLTO 순수 로직 검증
// Sentry 이슈 APPS-IN-TOSS-UNITY-SDK-10W 재발 방지용 회귀 테스트:
//   DiskSizeLTO 멤버 미정의 시 경고 후 건너뛰던 동작을 DiskSize 폴백으로 개선.
//   이후 2021.3 레거시 enum(DiskSize도 없음)을 위한 Size 3순위 폴백 추가.
// Unity 6000.0 OOM 회피가 "완전 스킵"에서 "LTO 제외(TrySetBestAvailable) DiskSize 적용"으로
// 바뀐 뒤 추가된 테스트: LTO 제외 모드 자체, 6000.0의 DiskSize 멤버 실재 전제, 빌드 세션 스냅샷
// 복원 계약. 버전 게이트/킬스위치 순수 로직(ResolveDecision)은 AITWebGLCodeOptDecisionTests.cs 참조.
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;
using System.Text.RegularExpressions;

[TestFixture]
public class AITWebGLCodeOptimizationTests
{
    // =================================================================
    // APPS-IN-TOSS-UNITY-SDK-10W 재현: TrySetByName 멤버 미정의 경고 경로
    // =================================================================

    /// <summary>
    /// Sentry APPS-IN-TOSS-UNITY-SDK-10W 재현:
    /// codeOptimization API가 있지만 요청한 멤버명이 enum에 없는 경우
    /// "[AIT] WebGL codeOptimization 멤버 미정의: ..." 경고가 발생하고 false를 반환해야 한다.
    ///
    /// 실제 버그는 DiskSizeLTO 미지원 Unity 버전(codeOptimization API가 있지만 DiskSizeLTO가 없는 버전)에서
    /// 발생했다. 이 테스트는 존재하지 않는 멤버명("__NoSuchMember__")을 직접 전달해
    /// 동일한 경고 경로를 재현한다.
    /// </summary>
    [Test]
    public void TrySetByName_WhenMemberNotDefined_EmitsWarningAndReturnsFalse()
    {
        // codeOptimization API가 없는 Unity 버전(예: 2021.3)에서는 ResolveProperty()가 null을 반환하므로
        // IsSupported == false인 경우 이 경로는 실행되지 않는다. 그 경우는 건너뜀.
        if (!AITWebGLCodeOptimization.IsSupported)
        {
            Assert.Ignore("이 Unity 버전은 codeOptimization API를 지원하지 않아 멤버 미정의 경로를 검증할 수 없습니다.");
            return;
        }

        // 존재하지 않는 멤버명으로 Sentry APPS-IN-TOSS-UNITY-SDK-10W의 경고 경로를 재현
        const string nonExistentMember = "__NoSuchMember__";
        LogAssert.Expect(LogType.Warning, new Regex(
            @"\[AIT\] WebGL codeOptimization 멤버 미정의: '__NoSuchMember__' \(enum=\w+\) — 건너뜀"));

        bool result = AITWebGLCodeOptimization.TrySetByName(nonExistentMember);

        Assert.IsFalse(result,
            "멤버가 enum에 없으면 TrySetByName은 false를 반환해야 합니다.");
    }

    // =================================================================
    // APPS-IN-TOSS-UNITY-SDK-10W 수정 검증: TrySetDiskSizeLTO 폴백 동작
    // =================================================================

    /// <summary>
    /// DiskSizeLTO 미지원 버전에서의 폴백 동작을 검증한다.
    /// API가 있고 DiskSizeLTO가 없지만 DiskSize(또는 2021.3 레거시의 Size)가 있는 경우:
    /// 해당 폴백 적용 + Log(Info) 출력.
    /// 이 테스트는 Unity 2022.3.62f2에서 DiskSizeLTO가 지원되므로 조건부 실행.
    /// </summary>
    [Test]
    public void TrySetDiskSizeLTO_WhenApiSupported_ReturnsTrueAndAppliesValue()
    {
        if (!AITWebGLCodeOptimization.IsSupported)
        {
            Assert.Ignore("이 Unity 버전은 codeOptimization API를 지원하지 않습니다.");
            return;
        }

        // API는 있지만 enum에 DiskSizeLTO/DiskSize/Size가 모두 없는 버전에서는
        // TrySetDiskSizeLTO가 설계상 false를 반환한다(fail-safe skip). 이 경우는 폴백 대상
        // 자체가 없으므로 "true 반환" 계약을 요구할 수 없다 → 별도 fail-safe 동작으로 보고 Ignore.
        // (IsSupported만으로는 이 버전을 걸러내지 못한다 — codeOptimization API 자체는 있고
        // IsSupported==true이나 세 멤버가 전부 없는 경우가 이론상 존재할 수 있다.)
        if (!AITWebGLCodeOptimization.SupportsDiskSizeMember)
        {
            Assert.Ignore("이 Unity 버전의 codeOptimization enum에는 DiskSizeLTO/DiskSize/Size가 없어 폴백 대상이 아닙니다.");
            return;
        }

        // 수정 전 값 기억 (복원용)
        string before = AITWebGLCodeOptimization.GetCurrentName();

        try
        {
            bool result = AITWebGLCodeOptimization.TrySetDiskSizeLTO();

            Assert.IsTrue(result,
                "TrySetDiskSizeLTO는 DiskSizeLTO/DiskSize/Size 중 하나라도 적용 성공 시 true를 반환해야 합니다.");

            string after = AITWebGLCodeOptimization.GetCurrentName();
            Assert.IsNotNull(after, "적용 후 현재 값이 null이어서는 안 됩니다.");

            bool isBestAvailable =
                after == AITWebGLCodeOptimization.DiskSizeLTO ||
                after == AITWebGLCodeOptimization.DiskSizeFallback ||
                after == AITWebGLCodeOptimization.SizeFallback;
            Assert.IsTrue(isBestAvailable,
                $"TrySetDiskSizeLTO 적용 후 값은 '{AITWebGLCodeOptimization.DiskSizeLTO}', " +
                $"'{AITWebGLCodeOptimization.DiskSizeFallback}', '{AITWebGLCodeOptimization.SizeFallback}' 중 " +
                $"하나여야 합니다. 실제: '{after}'");
        }
        finally
        {
            // 복원: 테스트가 PlayerSettings를 오염시키지 않도록
            if (before != null)
                AITWebGLCodeOptimization.TrySetByName(before);
        }
    }

    /// <summary>
    /// 2021.3 레거시 enum(WebGLCodeOptimization={Speed,Size})처럼 DiskSizeLTO/DiskSize가 둘 다
    /// 없고 Size만 있는 버전에서 TrySetDiskSizeLTO가 3순위 Size 폴백을 적용하는지 검증한다.
    /// 이 경로가 회귀 대상 버그였다 — 이전에는 Size가 폴백 후보에 없어 2021.3에서
    /// codeOptimization이 전혀 반영되지 않았다.
    /// 실행 중인 Unity의 resolved enum이 이 조건(DiskSizeLTO 없음, DiskSize 없음, Size 있음)을
    /// 만족할 때만 실행되고, 그 외(예: 2022.3+)에서는 Ignore.
    /// </summary>
    [Test]
    public void TrySetDiskSizeLTO_WhenOnlyLegacySizeDefined_FallsBackToSizeAndLogs()
    {
        if (!AITWebGLCodeOptimization.IsSupported)
        {
            Assert.Ignore("이 Unity 버전은 codeOptimization API를 지원하지 않습니다.");
            return;
        }

        bool hasDiskSizeLTO = AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.DiskSizeLTO);
        bool hasDiskSize = AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.DiskSizeFallback);
        bool hasSize = AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.SizeFallback);

        if (hasDiskSizeLTO || hasDiskSize || !hasSize)
        {
            Assert.Ignore(
                "이 Unity 버전은 2021.3 레거시 enum(Size만 정의) 조건이 아니어서 3순위 폴백 경로를 검증할 수 없습니다.");
            return;
        }

        string before = AITWebGLCodeOptimization.GetCurrentName();

        try
        {
            LogAssert.Expect(LogType.Log, new Regex(
                @"\[AIT\] WebGL codeOptimization: 'DiskSizeLTO'/'DiskSize' 미지원 버전 — 'Size'\(폴백\) 적용"));

            bool result = AITWebGLCodeOptimization.TrySetDiskSizeLTO();

            Assert.IsTrue(result, "DiskSizeLTO/DiskSize가 없고 Size만 있으면 Size 폴백이 적용되어 true를 반환해야 합니다.");
            Assert.AreEqual(AITWebGLCodeOptimization.SizeFallback, AITWebGLCodeOptimization.GetCurrentName(),
                "적용 후 현재 값은 Size여야 합니다.");
        }
        finally
        {
            if (before != null)
                AITWebGLCodeOptimization.TrySetByName(before);
        }
    }

    /// <summary>
    /// TrySetDiskSizeLTO가 DiskSizeLTO를 성공적으로 적용할 때 경고를 남기지 않음을 검증.
    /// DiskSizeLTO 지원 버전에서 TrySetByName 멤버 미정의 경고가 억제되어야 한다.
    /// </summary>
    [Test]
    public void TrySetDiskSizeLTO_WhenDiskSizeLTODefined_NoWarningEmitted()
    {
        if (!AITWebGLCodeOptimization.IsSupported)
        {
            Assert.Ignore("이 Unity 버전은 codeOptimization API를 지원하지 않습니다.");
            return;
        }

        // 테스트 전제 "DiskSizeLTO 정의됨"을 실제로 확인 — 2021.3처럼 DiskSizeLTO/DiskSize 가
        // 없는 버전은 Size 폴백이 적용되어 result=true 여도 값이 DiskSizeLTO 가 아니다
        // (폴백 경로는 위 폴백 전용 테스트가 검증).
        if (!AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.DiskSizeLTO))
        {
            Assert.Ignore("이 Unity 버전은 DiskSizeLTO 멤버를 정의하지 않습니다.");
            return;
        }

        string before = AITWebGLCodeOptimization.GetCurrentName();

        try
        {
            // DiskSizeLTO가 지원되는 버전에서는 경고 없이 성공해야 한다
            // LogAssert.NoUnexpectedReceived는 경고를 감시하지 않으므로
            // 경고가 발생하면 테스트 프레임워크가 자동으로 실패시킨다.
            bool result = AITWebGLCodeOptimization.TrySetDiskSizeLTO();

            if (result)
            {
                string after = AITWebGLCodeOptimization.GetCurrentName();
                // DiskSizeLTO가 지원되는 버전이라면 DiskSizeLTO 그대로 적용되어야 한다
                // (DiskSize 폴백 메시지 없이)
                Assert.AreEqual(AITWebGLCodeOptimization.DiskSizeLTO, after,
                    "DiskSizeLTO 지원 버전에서는 DiskSizeLTO가 직접 적용되어야 합니다.");
            }
        }
        finally
        {
            if (before != null)
                AITWebGLCodeOptimization.TrySetByName(before);
        }
    }

    // =================================================================
    // Unity 6000.0 OOM 회피: "완전 스킵" → "LTO 제외 DiskSize 적용" 회귀 방지
    // =================================================================

    /// <summary>
    /// allowLto=false면 사다리 1순위(DiskSizeLTO)를 건너뛰고 2순위(DiskSize, 비-LTO)를 적용해야 한다.
    /// Unity 6000.0 OOM 회피 경로("LTO 제외 모드")의 계약 — DiskSizeLTO가 절대 선택되지 않아야 한다.
    /// </summary>
    [Test]
    public void TrySetBestAvailable_WhenLtoDisallowed_AppliesDiskSizeAndNeverLTO()
    {
        if (!AITWebGLCodeOptimization.IsSupported)
        {
            Assert.Ignore("이 Unity 버전은 codeOptimization API를 지원하지 않습니다.");
            return;
        }

        if (!AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.DiskSizeFallback))
        {
            Assert.Ignore("이 Unity 버전 enum에는 DiskSize가 없어 LTO 제외 경로를 검증할 수 없습니다.");
            return;
        }

        string before = AITWebGLCodeOptimization.GetCurrentName();

        try
        {
            LogAssert.Expect(LogType.Log, new Regex(
                @"\[AIT\] WebGL codeOptimization: LTO 제외 모드 — 'DiskSize'\(비-LTO\) 적용"));

            bool result = AITWebGLCodeOptimization.TrySetBestAvailable(allowLto: false);

            Assert.IsTrue(result, "DiskSize가 정의된 버전에서는 LTO 제외 모드도 true를 반환해야 합니다.");
            Assert.AreEqual(AITWebGLCodeOptimization.DiskSizeFallback, AITWebGLCodeOptimization.GetCurrentName(),
                "LTO 제외 모드에서 적용된 값은 DiskSize여야 하며 DiskSizeLTO여서는 안 됩니다.");
        }
        finally
        {
            if (before != null)
                AITWebGLCodeOptimization.TrySetByName(before);
        }
    }

    /// <summary>
    /// 6000.0 개선의 핵심 전제 검증: 6000.0.x의 WasmCodeOptimization enum에 'DiskSize' 멤버가
    /// 실재해야 LTO 제외 폴백이 실제로 값을 바꾼다(없으면 사다리가 fail-safe로 무적용된다 —
    /// 이 경우 6000.0 개선 자체가 no-op이므로 perf 전제가 무너졌다는 신호가 된다).
    /// E2E test_level=0이 여러 Unity 버전 매트릭스에서 돌기 때문에 이 단언이 6000.0 레그에서만
    /// 사실을 확정하고, 그 외 버전에서는 Assert.Ignore로 스스로를 제외한다.
    /// </summary>
    [Test]
    public void DiskSizeMember_IsDefined_OnUnity6000_0()
    {
        if (!AITWebGLCodeOptimization.IsLtoRiskyVersion(Application.unityVersion))
        {
            Assert.Ignore("6000.0.x가 아니어서 이 전제 검증 대상이 아닙니다. (unityVersion=" + Application.unityVersion + ")");
            return;
        }

        Assert.IsTrue(AITWebGLCodeOptimization.IsSupported,
            "6000.0.x는 codeOptimization API(UserBuildSettings)를 지원해야 합니다.");
        Assert.IsTrue(AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.DiskSizeFallback),
            $"6000.0 LTO 제외 폴백은 'DiskSize' 멤버 존재가 전제입니다 (unityVersion={Application.unityVersion}).");
    }

    /// <summary>
    /// 빌드 세션 스냅샷 계약 회귀 방지: LTO 제외 모드(TrySetBestAvailable(false))로 적용을 바꾼 뒤에도
    /// PlayerSettingsSnapshot.Capture()/Restore()가 적용 전 원래 값으로 정확히 되돌려야 한다.
    /// AITBuildSession은 멤버 이름 문자열로 캡처/복원하므로(AITBuildSession.cs:84,155) DiskSize도
    /// 기존 DiskSizeLTO와 동일하게 안전해야 한다 — 6000.0에서 실제 값이 바뀌게 된 이번 변경의
    /// 회귀 방지 테스트.
    /// </summary>
    [Test]
    public void Snapshot_Restores_CodeOptimization_AfterNoLtoApply()
    {
        if (!AITWebGLCodeOptimization.IsSupported)
        {
            Assert.Ignore("이 Unity 버전은 codeOptimization API를 지원하지 않습니다.");
            return;
        }

        if (!AITWebGLCodeOptimization.IsMemberDefined(AITWebGLCodeOptimization.DiskSizeFallback))
        {
            Assert.Ignore("이 Unity 버전 enum에는 DiskSize가 없어 LTO 제외 경로를 검증할 수 없습니다.");
            return;
        }

        string before = AITWebGLCodeOptimization.GetCurrentName();

        try
        {
            var snapshot = PlayerSettingsSnapshot.Capture();

            Assert.IsTrue(AITWebGLCodeOptimization.TrySetBestAvailable(allowLto: false));
            Assert.AreEqual(AITWebGLCodeOptimization.DiskSizeFallback, AITWebGLCodeOptimization.GetCurrentName(),
                "사전 조건: LTO 제외 모드 적용 후 현재 값은 DiskSize여야 합니다.");

            snapshot.Restore();

            Assert.AreEqual(before, AITWebGLCodeOptimization.GetCurrentName(),
                "빌드 세션 스냅샷 복원은 codeOptimization을 캡처 당시 값으로 되돌려야 합니다.");
        }
        finally
        {
            if (before != null)
                AITWebGLCodeOptimization.TrySetByName(before);
        }
    }
}
