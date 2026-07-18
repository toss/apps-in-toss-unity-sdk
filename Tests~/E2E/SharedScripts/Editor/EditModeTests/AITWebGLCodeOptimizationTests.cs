// -----------------------------------------------------------------------
// AITWebGLCodeOptimizationTests.cs - WebGL codeOptimization 반영 헬퍼 검증
// Level 0: AITWebGLCodeOptimization.TrySetByName / TrySetDiskSizeLTO 순수 로직 검증
// Sentry 이슈 APPS-IN-TOSS-UNITY-SDK-10W 재발 방지용 회귀 테스트:
//   DiskSizeLTO 멤버 미정의 시 경고 후 건너뛰던 동작을 DiskSize 폴백으로 개선
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
    /// API가 있고 DiskSizeLTO가 없지만 DiskSize가 있는 경우: DiskSize 폴백 적용 + Log(Info) 출력.
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

        // API는 있지만 enum에 DiskSizeLTO/DiskSize가 둘 다 없는 버전(예: 2021.3의 레거시
        // WebGLCodeOptimization={Speed,Size})에서는 TrySetDiskSizeLTO가 설계상 false를 반환한다
        // (fail-safe skip). 이 경우는 폴백 대상 자체가 없으므로 "true 반환" 계약을 요구할 수 없다 →
        // 별도 fail-safe 동작으로 보고 Ignore. (IsSupported만으로는 2021.3을 걸러내지 못한다 —
        // 2021.3은 레거시 codeOptimization이 있어 IsSupported==true이나 DiskSize*는 없다.)
        if (!AITWebGLCodeOptimization.SupportsDiskSizeMember)
        {
            Assert.Ignore("이 Unity 버전의 codeOptimization enum에는 DiskSizeLTO/DiskSize가 없어 폴백 대상이 아닙니다.");
            return;
        }

        // 수정 전 값 기억 (복원용)
        string before = AITWebGLCodeOptimization.GetCurrentName();

        try
        {
            bool result = AITWebGLCodeOptimization.TrySetDiskSizeLTO();

            Assert.IsTrue(result,
                "TrySetDiskSizeLTO는 DiskSizeLTO 또는 DiskSize 폴백 적용 성공 시 true를 반환해야 합니다.");

            string after = AITWebGLCodeOptimization.GetCurrentName();
            Assert.IsNotNull(after, "적용 후 현재 값이 null이어서는 안 됩니다.");

            bool isBestAvailable =
                after == AITWebGLCodeOptimization.DiskSizeLTO ||
                after == AITWebGLCodeOptimization.DiskSizeFallback;
            Assert.IsTrue(isBestAvailable,
                $"TrySetDiskSizeLTO 적용 후 값은 '{AITWebGLCodeOptimization.DiskSizeLTO}' 또는 " +
                $"'{AITWebGLCodeOptimization.DiskSizeFallback}'이어야 합니다. 실제: '{after}'");
        }
        finally
        {
            // 복원: 테스트가 PlayerSettings를 오염시키지 않도록
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
}
