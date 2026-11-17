// ---------------------------------------------------------------------------
// IsKnownNonSdkMessageTests.cs - IsKnownNonSdkMessage 단위 테스트
// IsAitRelated를 통과한 메시지 중 Unity 내부/사용자 프로젝트 패턴 필터링 검증.
// ---------------------------------------------------------------------------

// 카테고리 → 파일 색인
//   IsKnownNonSdkMessageTests.cs (본 파일): 가드 진입점 — Null/Empty, 외부 AIT prefix 드롭, SDK 보호, negative cases
//   .UnityInternal.cs: Unity 엔진·패키지 자체 경고
//   .UserProject.cs: 사용자 에셋·직렬화·외부 패키지·외부 WebGL 템플릿
//   .UserCodeCompile.cs: 사용자 코드 CS#### 컴파일 에러·경고
//   .BuildToolchain.cs: pnpm/ait deploy 패스스루, powershell 실행 실패 등 빌드 툴체인 노이즈
//   .SdkSelfOutput.cs: SDK 자체 출력·[AIT] prefix를 가드보다 먼저 매칭
//   .UserEnvironment.cs: 사용자 환경·카테고리 D·A-5 백스톱
//
// 규칙:
//   1. 새 테스트는 고른 partial 파일의 마지막 #endregion 바로 뒤에 새 region으로 추가한다.
//   2. partial 파일에는 클래스 어트리뷰트([TestFixture], [Category] 등)를 달지 않는다.
//   3. 새 partial 파일은 .meta를 동반한다.

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public partial class IsKnownNonSdkMessageTests
{
    #region Null/Empty Tests

    [Test]
    public void NullMessage_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(null));
    }

    [Test]
    public void EmptyMessage_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(""));
    }

    #endregion

    #region 외부 AIT prefix (SDK가 출력하지 않는 prefix) — 가드 우회 드롭

    [Test]
    public void ExternalAitLoginPrefix_AitMockOrTimeout_ReturnsTrue()
    {
        // Sentry SDK-D2 — 외부 코드가 [AIT Login] prefix로 출력한 fallback 경고.
        // SDK 코드에는 "[AIT Login]" 문자열이 존재하지 않으므로 노이즈로 드롭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Login][src=AIT_MOCK_OR_TIMEOUT] status=RanToCompletion, len=0 → device fallback"));
    }

    [Test]
    public void ExternalAitLoginPrefix_ForbiddenOrigin_ReturnsTrue()
    {
        // Sentry SDK-D3
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Login] InitSession failed: FORBIDDEN_ORIGIN"));
    }

    [Test]
    public void RegularAitPrefix_NotMatchingExternal_StillProtected()
    {
        // "[AIT Login]"이 아닌 일반 [AIT] prefix는 SDK 보호 가드로 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] InitSession started"));
    }

    [Test]
    public void ExternalTossFirebasePrefix_GameLoginFailure_ReturnsTrue()
    {
        // Sentry SDK-CF — 사용자 게임 백엔드의 [Toss Firebase] prefix.
        // SDK 코드에는 "[Toss Firebase]"/"게임로그인" 문자열이 존재하지 않음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[Toss Firebase] 게임로그인 실패: Object reference not set to an instance of an object"));
    }

    [Test]
    public void ExternalFtrAppsInTossPath_BackslashPrefix_ReturnsTrue()
    {
        // Sentry SDK-PK/PJ/PF/PC/PB — Windows 경로 변형. 사용자 프로젝트 경로 prefix는
        // ExternalAitPrefixes를 통해 SDK 보호 가드보다 먼저 매칭되어 노이즈로 드롭된다.
        // CS0414/NonSdkMessagePatterns에 의존하지 않는 입력으로 ExternalAitPrefixes 코드 경로를 직접 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\FTR_AppsInToss\\Optimization\\Platform\\PlatformMemoryManager.cs(19,36): some unrelated diagnostic"));
    }

    [Test]
    public void ExternalFtrAppsInTossPath_ForwardSlashPrefix_ReturnsTrue()
    {
        // Sentry SDK-QA/Q9/Q8/Q7/Q6/Q5/Q4 — POSIX 경로 변형. NonSdkMessagePatterns 키워드 없이도
        // ExternalAitPrefixes만으로 노이즈 분류되어야 함.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/FTR_AppsInToss/Optimization/Platform/PlatformOptimizer.cs(17,35): some unrelated diagnostic"));
    }

    [Test]
    public void ExternalFtrAppsInTossPath_WithAitKeyword_StillFiltered()
    {
        // ExternalAitPrefixes는 AitKeywords 가드보다 먼저 매칭되어야 한다.
        // 사용자 코드에 AppsInToss 식별자가 섞여도 FTR_AppsInToss 경로 prefix가 우선해 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/FTR_AppsInToss/Foo.cs(1,1): warning CS0123: AppsInToss reference unresolved"));
    }

    [Test]
    public void SdkAssetsPath_NotFtrAppsInToss_StillProtected()
    {
        // SDK 자체 경로(Runtime/, Editor/) 또는 일반 Assets/ 경로는
        // FTR_AppsInToss prefix와 무관하므로 SDK 보호 가드가 정상 동작해야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Assets/Other/Foo.cs error: AppsInToss runtime issue"));
    }

    [Test]
    public void ExternalAitCloudSavePrefix_IdentifyKeyMissing_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-19D — 사용자 게임이 AIT.Storage/GetAnonymousKey 위에 직접
        // 구현한 Cloud Save 래퍼가 SDK와 동일한 "[AIT ...]" 컨벤션으로 출력하는 정상 폴백 로그.
        // SDK 코드에는 Cloud Save 기능 자체가 없으므로(grep 확인) "[AIT Cloud Save]" 문자열이
        // 존재하지 않는다. ExternalAitPrefixes로 AitKeywords 가드보다 먼저 매칭되어 노이즈로 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Cloud Save] 게임 사용자 식별키를 받지 못했습니다. 로컬 저장으로 계속합니다."));
    }

    [Test]
    public void ExternalAitCloudSavePrefix_WithUnityWarningPrefix_ReturnsTrue()
    {
        // 실제 Sentry 이슈 제목은 "UnityWarning: [AIT Cloud Save] ..." 형태로 캡처된다.
        // Unity 로그 핸들러가 덧붙이는 "UnityWarning:" prefix가 있어도 IndexOf 부분 매칭이므로 동일하게 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AIT Cloud Save] 게임 사용자 식별키를 받지 못했습니다. 로컬 저장으로 계속합니다."));
    }

    [Test]
    public void RegularAitPrefix_NotMatchingCloudSave_StillProtected()
    {
        // "[AIT Cloud Save]"가 아닌 일반 [AIT] prefix는 SDK 보호 가드로 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Cloud Save initialized"));
    }

    [Test]
    public void ExternalAitPromotionPrefix_CaptureEntryPointSkipped_ReturnsTrue()
    {
        // Sentry SDK-S1 — 사용자 게임의 프로모션 로직이 <color=Yellow>AITPromotion</color> tag로 출력하는 로그.
        // SDK 코드 어디에도 "AITPromotion" 문자열이 존재하지 않음 (grep 확인).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[05:58:01:496] <color=Yellow>AITPromotion</color>: CaptureEntryPoint skipped on non-WebGL"));
    }

    [Test]
    public void ExternalAitPromotionPrefix_SkipNotPromotionEntry_ReturnsTrue()
    {
        // Sentry SDK-SX — 동일 prefix를 사용하는 또 다른 분기 출력 (rebirthCount 조건).
        // 단일 ExternalAitPrefixes 패턴 "AITPromotion</color>"이 두 변형 모두 매칭함을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[02:00:21:536] <color=Yellow>AITPromotion</color>: Skip: not a promotion entry (rebirthCount=1)"));
    }

    [Test]
    public void AitPromotionPrefix_WithAitKeyword_StillFiltered()
    {
        // ExternalAitPrefixes는 AitKeywords 가드보다 먼저 매칭되므로,
        // 동일 메시지에 SDK 키워드가 섞여도 외부 prefix가 우선해 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "<color=Yellow>AITPromotion</color>: AppsInToss helper called"));
    }

    [Test]
    public void ExternalAitPromotionPrefix_WithUnityWarningPrefix_ReturnsTrue()
    {
        // Sentry SDK-S1 회귀 — Unity 로그 핸들러가 "UnityWarning:" prefix를 덧붙여
        // 캡처한 변형도 동일한 ExternalAitPrefixes 패턴("AITPromotion</color>")으로 드롭됨을 검증.
        // IndexOf 부분 문자열 매칭이므로 prefix 유무와 무관하지만, 실제 보고된 메시지 포맷이
        // 미래 변경(예: prefix-anchored 매칭으로 회귀)에서도 계속 매칭되도록 회귀 케이스로 고정.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [17:07:22:329] <color=Yellow>AITPromotion</color>: CaptureEntryPoint skipped on non-WebGL"));
    }

    #endregion

    #region SDK 보호: AIT 키워드 포함 시 절대 필터링 안 함

    [Test]
    public void AitPrefix_NeverFiltered()
    {
        // [AIT] 접두사가 있는 메시지는 다른 패턴이 매칭되어도 필터링되면 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("[AIT] GfxDevice renderer is null"));
    }

    [Test]
    public void AitWarnPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("[AITWarn] some warning"));
    }

    [Test]
    public void AppsInTossKeyword_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("AppsInToss build error matches more than one built-in atlases"));
    }

    [Test]
    public void AppsInTossPackageId_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("apps-in-toss config exceeds previous array size"));
    }

    [Test]
    public void AitNpmRunner_NeverFiltered()
    {
        // AitKeywords의 다른 키워드도 Unity 내부 패턴과 섞여도 보호되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITNpmRunner] pnpm install failed: matches more than one built-in atlases"));
    }

    [Test]
    public void AitConvertCore_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AITConvertCore.DoExport: GfxDevice renderer is null"));
    }

    [Test]
    public void AitPackageBuilder_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AITPackageBuilder: Localization-String-Tables-Shared missing"));
    }

    [Test]
    public void AitNodeJS_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AITNodeJS download: exceeds previous array size"));
    }

    [Test]
    public void AitColonPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: Ignoring locale en-US"));
    }

    [Test]
    public void AitColonPrefix_MidSentence_LeadingNonWordBoundary_NeverFiltered()
    {
        // "AIT:" prefix는 줄 시작이 아니어도 토큰 경계(공백 등) 직후이면 SDK 자체 로그로 보호되어야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[WARN] AIT: Ignoring locale en-US"));
    }

    [Test]
    public void PortraitColonSubstring_NotMatchingAitColon_FilteredAsNoise()
    {
        // 회귀 가드: "Portrait:" 내부의 "ait:" substring은 "AIT:"와 case-insensitive로
        // 충돌하지만 왼쪽이 letter라 단어 경계 정책에 의해 SDK 키워드로 인정하지 않는다.
        // 따라서 SDK 보호 가드가 발동하지 않고 NonSdkMessagePatterns로 정상 필터된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate (ViewPortrait: OnFoo)"));
    }

    [Test]
    public void AitBuildKeyword_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "ait-build step failed: Cannot read BuildLayout header"));
    }

    #endregion

    #region SDK 관련 메시지는 통과 (negative cases)

    [Test]
    public void GenericSdkErrorWithoutPattern_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("Random SDK error"));
    }

    [Test]
    public void EmptyAitNothingMatches_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("Some unrelated message"));
    }

    #endregion
}
