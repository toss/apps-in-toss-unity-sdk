// IsKnownNonSdkMessageTests.SdkSelfOutput.cs - SDK 자체 출력·[AIT] prefix (가드보다 먼저 매칭)
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

public partial class IsKnownNonSdkMessageTests
{
    #region Sentry transport 자기참조 노이즈 (4xx/5xx + 네트워크 + 동기)

    [Test]
    public void SentryTransport_Http503_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T4 — Sentry 서버의 일시적 503 응답.
        // Transport 자체의 실패를 다시 Sentry로 보내면 self-loop이 발생하므로 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 503)"));
    }

    [Test]
    public void SentryTransport_Http500_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 500)"));
    }

    [Test]
    public void SentryTransport_Http502_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 502)"));
    }

    [Test]
    public void SentryTransport_Http504_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 504)"));
    }

    [Test]
    public void SentryTransport_Http400_ReturnsTrue()
    {
        // 4xx도 self-loop 방지 정책에 따라 동일하게 차단. SDK 분기 정보가 없고
        // SubmitResult.Fail로 호출자에게 결과가 전달되므로 가시성 손실 없음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 400)"));
    }

    [Test]
    public void SentryTransport_Http401_ReturnsTrue()
    {
        // DSN 오설정도 사용자 가시성은 콘솔에 유지되므로 self-loop 방지를 우선.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 401)"));
    }

    [Test]
    public void SentryTransport_SyncSendFailure_ReturnsTrue()
    {
        // 에디터 종료 FlushSync 경로의 예외도 self-loop 방지를 위해 차단.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 동기 전송 실패: System.Net.WebException: ..."));
    }

    [Test]
    public void SentryTransport_NetworkError_ReturnsTrue()
    {
        // Sentry SDK-CZ, KA — ConnectionError 등 transport 네트워크 일시 장애는 SDK 가드 우회로 드롭.
        // Transport가 자기 출력을 다시 Sentry로 보내면 캐스케이드 위험.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Connection refused"));
    }

    [Test]
    public void SentryTransport_NetworkErrorUnknown_ReturnsTrue()
    {
        // Sentry SDK-CZ
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Unknown Error"));
    }

    [Test]
    public void SentryTransport_NetworkErrorTimeout_ReturnsTrue()
    {
        // Sentry SDK-KA
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Request timeout"));
    }

    [Test]
    public void SentryTransport_NetworkErrorUnableToReadData_ReturnsTrue()
    {
        // Sentry SDK-RR — UnityWebRequest.error 변형 "Unable to read data" (transient I/O 실패).
        // 기존 일반 패턴이 suffix 변형을 모두 흡수함을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Unable to read data"));
    }

    [Test]
    public void SentryTransport_OtherDiagnostic_NotFiltered()
    {
        // Transport의 다른 진단 메시지(예: 큐 가득 참, 동기 전송 등)는 영향받지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 큐가 가득 차서 가장 오래된 이벤트를 버립니다"));
    }

    #endregion

    #region 신규 노이즈: AIT_Auth prefix / FPS 모니터 / libuv assertion (Sentry SDK-NB/WK/BE)

    [Test]
    public void ExternalAitAuthPrefix_CustomTokenFailure_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-NB — 사용자 게임 인증 래퍼가 "[AIT_Auth]" prefix로 출력하는 경고.
        // SDK 코드에는 "[AIT_Auth]" prefix가 없음(grep 확인). "[AIT"로 시작해 AitKeywords 가드에 걸리므로
        // ExternalAitPrefixes로 가드보다 먼저 드롭되어야 한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT_Auth] Custom Token 발급 실패 — 로컬 모드로 동작"));
    }

    [Test]
    public void RegularAitPrefix_NotAitAuth_StillProtected()
    {
        // "[AIT_Auth]"가 아닌 일반 "[AIT]" SDK 로그는 보호되어야 함 — 새 prefix가 과도하게 넓지 않음을 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Custom Token 발급 성공"));
    }

    [Test]
    public void FpsMonitor_AverageFpsBelowTarget_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-WK — 사용자 게임 FPS 모니터가 SDK의 "[AIT]" prefix를 흉내내 출력.
        // SDK 코드에는 "평균 FPS"/"미달" 성능 경고 문자열이 없음(grep 확인). "[AIT]"가 AitKeywords 가드에
        // 걸리므로 "평균 FPS" + "미달" 합성 복합 검사가 가드보다 먼저 드롭한다(FPS 수치는 가변).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 평균 FPS 20.4 — 목표 30+ 미달"));
    }

    [Test]
    public void FpsMonitor_UnityWarningWrapped_ReturnsTrue()
    {
        // Unity 로그 핸들러가 "UnityWarning:" prefix를 덧붙인 변형도 동일 복합 검사로 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AIT] 평균 FPS 18.0 — 목표 30+ 미달"));
    }

    [Test]
    public void NormalAitFpsLog_WithoutBelowTarget_StillProtected()
    {
        // "평균 FPS"만 있고 "미달"이 없으면 복합 AND의 한쪽만 충족 → SDK 키워드 보호 가드가 정상 동작해야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 평균 FPS 60.0 (정상)"));
    }

    [Test]
    public void LibuvAssertionCrash_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-BE — 번들 Node.js(libuv)가 종료 시점에 출력하는 내부 assertion crash.
        // "AIT: [stderr]" prefix가 SDK 키워드("AIT:") 가드에 걸리므로 "AIT: [std" + "Assertion failed:" 합성으로
        // 가드보다 먼저 드롭한다. SDK 코드로 분기/조치할 정보가 아님.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stderr] Assertion failed: !(handle->flags & UV_HANDLE_CLOSING), file src\\win\\async.c, line 76"));
    }

    [Test]
    public void NormalAitStderr_WithoutAssertion_StillProtected()
    {
        // "AIT: [stderr]"이지만 어떤 복합 검사 문구(Assertion failed/is not recognized/Unknown Syntax Error/[?25)도
        // 없는 일반 SDK stderr 전달 로그는 보호되어야 함 — SDK 키워드("AIT:") 가드로 통과.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stderr] granite build progress: 50%"));
    }

    #endregion

    #region AITSentryContextEnricher CollectSafe 수집 실패 노이즈 (APPS-IN-TOSS-UNITY-SDK-11F / 11G)

    [Test]
    public void AITSentryCollectSafe_GetTossAppVersionFailure_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-11G — WebGL 빌드에서 window.AppsInToss가 초기화되기 전에
        // AITSentryContextEnricher.CollectSafe가 GetTossAppVersion을 호출하면 jslib에서
        // "Cannot read properties of undefined (reading 'getTossAppVersion')" 예외가 발생한다.
        // CollectSafe는 이 예외를 잡아 "unavailable"를 반환하므로 SDK 동작은 중단되지 않는다.
        // 에디터 Sentry로 전송하면 조치 불가한 노이즈가 되므로 차단한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] GetTossAppVersion 호출 실패: Cannot read properties of undefined (reading 'getTossAppVersion')"));
    }

    [Test]
    public void AITSentryCollectSafe_GetTossAppVersionFailure_UnityWarningPrefix_ReturnsTrue()
    {
        // Unity가 Debug.LogWarning을 에디터 콘솔로 보낼 때 "UnityWarning: " prefix가 붙는 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AITSentry] GetTossAppVersion 호출 실패: Cannot read properties of undefined (reading 'getTossAppVersion')"));
    }

    [Test]
    public void AITSentryCollectSafe_GetDeviceIdFailure_ReturnsTrue()
    {
        // 동일 CollectSafe 경로 — GetDeviceId API도 동일 패턴으로 실패할 수 있음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] GetDeviceId 호출 실패: Cannot read properties of undefined (reading 'getDeviceId')"));
    }

    [Test]
    public void AITSentryCollectSafe_GenericApiFailure_ReturnsTrue()
    {
        // CollectSafe가 감싸는 모든 AIT API의 실패 변형을 포괄적으로 드롭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] GetOperationalEnvironment 호출 실패: some platform error"));
    }

    [Test]
    public void AITSentryCollectSafe_GetOperationalEnvironmentUndefinedAccess_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-11E — WebGL 빌드에서 window.AppsInToss가 초기화되기 전에
        // AITSentryContextEnricher.CollectSafe가 GetOperationalEnvironment를 호출하면 jslib에서
        // "Cannot read properties of undefined (reading 'getOperationalEnvironment')" JS TypeError가 발생한다.
        // CollectSafe는 이 예외를 잡아 "unavailable"를 반환하므로 SDK 동작은 중단되지 않는다.
        // 에디터 Sentry로 전송하면 조치 불가한 노이즈가 되므로 차단한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] GetOperationalEnvironment 호출 실패: Cannot read properties of undefined (reading 'getOperationalEnvironment')"));
    }

    [Test]
    public void AITSentryCollectSafe_GetOperationalEnvironmentUndefinedAccess_UnityWarningPrefix_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-11E 실측 메시지 — Unity가 Debug.LogWarning을 에디터 콘솔로
        // 보낼 때 "UnityWarning: " prefix가 붙는 변형. 에디터 backstop 필터(IsKnownNonSdkMessage)가
        // [AITSentry] + 호출 실패: 합성으로 이 변형도 드롭함을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AITSentry] GetOperationalEnvironment 호출 실패: Cannot read properties of undefined (reading 'getOperationalEnvironment')"));
    }

    [Test]
    public void AITSentryCollectSafe_EnvGetDeploymentIdUndefinedAccess_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-11F — WebGL 빌드에서 window.AppsInToss가 초기화되기 전에
        // AITSentryContextEnricher.CollectSafe가 EnvGetDeploymentId를 호출하면 jslib에서
        // window.AppsInToss.env.getDeploymentId() 접근 시 "Cannot read properties of undefined
        // (reading 'env')" JS TypeError가 발생한다.
        // 11E(GetOperationalEnvironment)와 동일한 window.AppsInToss 미초기화 타이밍 조건이지만,
        // EnvGetDeploymentId는 window.AppsInToss.env.getDeploymentId() — 중첩 속성 접근 구조라
        // 에러 메시지의 reading 키가 'env'로 다르다.
        // CollectSafe의 isUndefinedAccess 가드가 이 패턴도 "unavailable"로 폴백 처리하므로
        // SDK 동작은 중단되지 않으며, 에디터 backstop 필터가 Sentry 전송을 차단한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] EnvGetDeploymentId 호출 실패: Cannot read properties of undefined (reading 'env')"));
    }

    [Test]
    public void AITSentryCollectSafe_EnvGetDeploymentIdUndefinedAccess_UnityWarningPrefix_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-11F 실측 메시지 — Unity가 Debug.LogWarning을 에디터 콘솔로
        // 보낼 때 "UnityWarning: " prefix가 붙는 변형. 중간에 "Autoconnected Player" 원격 디버거
        // prefix가 포함된 경우에도 "[AITSentry]" + "호출 실패:" 부분 문자열 합성 조건이 매칭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AITSentry] EnvGetDeploymentId 호출 실패: Cannot read properties of undefined (reading 'env')"));
    }

    [Test]
    public void AITSentryCollectSafe_OtherDiagnostic_NotFiltered()
    {
        // "[AITSentry]" prefix라도 "호출 실패:" 패턴이 없는 다른 진단 메시지는 통과해야 한다.
        // (CollectSafe 경로 전용 필터이므로 다른 [AITSentry] 경고는 영향받지 않음.)
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] AIT 컨텍스트 수집 완료 (device=abc, platform=WebGL, locale=ko)"));
    }

    [Test]
    public void AITSentryCollectSafe_NoAITSentryPrefix_NotFiltered()
    {
        // "호출 실패:" 패턴만 있고 "[AITSentry]" prefix가 없으면 드롭하지 않는다.
        // (다른 SDK 모듈의 "호출 실패" 메시지와 충돌하지 않도록 AND 조건 사용.)
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] GetTossAppVersion 호출 실패: some error"));
    }

    #endregion

    #region AITSentry WebGL JS 브리지 핸들러 미등록 노이즈 (APPS-IN-TOSS-UNITY-SDK-11J)

    [Test]
    public void AitSentryGetLocaleConstantHandlerFailure_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-11J — AITSentryContextEnricher.CollectSafe가
        // window.AppsInToss.getLocale 핸들러가 없는 환경(sandbox, 구버전 웹뷰 등)에서 발생하는
        // "getLocale is not a constant handler" AITException을 잡아 출력한 경고.
        // CollectSafe가 "unavailable"로 폴백 처리하므로 SDK 동작은 정상이며 Sentry 캡처 대상이 아님.
        // "[AITSentry]" prefix가 AitKeywords("[AIT")에 걸려 SDK 보호 가드를 통과하므로
        // 가드보다 먼저 매칭하는 composite AND 필터로 차단.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AITSentry] GetLocale 호출 실패: getLocale is not a constant handler"));
    }

    [Test]
    public void AitSentryGetDeviceIdConstantHandlerFailure_ReturnsTrue()
    {
        // 동일 패턴의 다른 API 변형 (GetDeviceId, GetPlatformOS 등) 도 동일하게 드롭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [AITSentry] GetDeviceId 호출 실패: getDeviceId is not a constant handler"));
    }

    [Test]
    public void AitSentryConstantHandlerFailure_BareMessage_ReturnsTrue()
    {
        // "UnityWarning:" prefix 없이 메시지 본문만 도달하는 변형도 드롭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentry] GetLocale 호출 실패: getLocale is not a constant handler"));
    }

    [Test]
    public void AitSentryConstantHandlerFailure_WithoutAitSentryPrefix_StillFiltered()
    {
        // "is not a constant handler"가 있으면 "[AITSentry]" prefix와 함께 드롭됨을 검증.
        // 다른 prefix이지만 "[AITSentry]" + "is not a constant handler" 합성 조건을 충족하지 않으면 통과.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SomeOtherSystem is not a constant handler"));
    }

    #endregion
}
