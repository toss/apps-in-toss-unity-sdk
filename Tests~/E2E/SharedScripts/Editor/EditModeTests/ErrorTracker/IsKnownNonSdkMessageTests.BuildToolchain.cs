// IsKnownNonSdkMessageTests.BuildToolchain.cs - pnpm/ait deploy/powershell/IL2CPP·Bee/git/빌드취소/키스토어/Gradle/MCP/adb 패스스루
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

public partial class IsKnownNonSdkMessageTests
{
    #region pnpm stdout/stderr 패스스루 (SDK-HA, SDK-R6, SDK-VF, SDK-VA)

    [Test]
    public void PnpmStdoutPassthrough_TrailingAnchor_ReturnsTrue()
    {
        // Sentry SDK-HA — 본문 없는 "[pnpm] 출력:" stdout passthrough
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("[pnpm] 출력:"));
    }

    [Test]
    public void PnpmStdoutPassthrough_LeadingAnchor_ReturnsTrue()
    {
        // Sentry SDK-R6 — "[pnpm] 출력:"으로 시작하는 패스스루 라인 (후행 본문이 있어도 노이즈)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[pnpm] 출력: WARN deprecated some message"));
    }

    [Test]
    public void PnpmStderrPassthrough_TrailingAnchor_ReturnsTrue()
    {
        // Sentry SDK-VF — Unity가 외부 pnpm 프로세스 stderr를 래핑한 "[pnpm] 오류:" 라인
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("[pnpm] 오류:"));
    }

    [Test]
    public void PnpmStderrPassthrough_UnityWrapped_ReturnsTrue()
    {
        // Sentry SDK-VA — UnityWarning prefix로 래핑된 "[pnpm] 오류:" 본문 (후행 내용 포함)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[pnpm] 오류: ERR_PNPM_FETCH_404 GET https://registry.npmjs.org/foo"));
    }

    [Test]
    public void PnpmStdoutPassthrough_WithAitPrefix_NeverFiltered()
    {
        // SDK가 직접 출력한 "[AIT...]" prefix가 붙으면 SDK 보호 가드로 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [pnpm] 출력: build failed"));
    }

    [Test]
    public void PnpmStderrPassthrough_WithAitPrefix_NeverFiltered()
    {
        // SDK가 직접 출력한 "[AIT...]" prefix가 붙으면 SDK 보호 가드로 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [pnpm] 오류: 빌드 의존성 설치 실패"));
    }

    #endregion

    #region 'ait deploy' stdout/stderr ANSI escape 노이즈 (SDK-VK, SDK-BD, SDK-T5)

    [Test]
    public void DeployStdout_AnsiCursorHide_ReturnsTrue()
    {
        // Sentry SDK-VK/BD/T5 — pnpm progress bar의 커서 hide(\x1b[?25l) escape가 stdout에 새는 경우.
        // 구버전 SDK 사용자가 deploy 실행 시 다수의 별도 fingerprint로 캡처되던 메시지.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stdout] \x1b[?25l│"));
    }

    [Test]
    public void DeployStderr_AnsiCursorShow_ReturnsTrue()
    {
        // 동일 패턴의 stderr 변형 — 커서 show(\x1b[?25h) escape 포함.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stderr] \x1b[?25h│"));
    }

    [Test]
    public void DeployStdout_WithoutAnsiEscape_NotFiltered()
    {
        // 실제 진단 가치가 있는 stdout 메시지는 보호되어야 한다 — ANSI escape "[?25" 미포함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stdout] Building project... done in 12.3s"));
    }

    [Test]
    public void DeployStdout_AnsiEscape_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: [AIT...] prefix가 붙은 동일 패턴은 SDK 자체 진단 로그로 간주.
        // 합성 가드의 "AIT: [std" 토큰을 우회하기 위해 [AIT] prefix가 먼저 와야 보호 가능.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] deploy diag: stdout snapshot contains \x1b[?25l progress bar fragment"));
    }

    #endregion

    #region AITAsyncCommandRunner Windows powershell 실행 실패 (SDK-VE, SDK-VC)

    [Test]
    public void AsyncCommand_Win32Exception_PowershellMissing_ReturnsTrue()
    {
        // Sentry SDK-VE/VC — 사용자 Windows 환경에서 powershell.exe 실행 실패. PATH/실행 정책 문제.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Async] 명령 실행 예외: System.ComponentModel.Win32Exception (0x80004005): " +
            "ApplicationName='powershell.exe', CommandLine='-ExecutionPolicy Bypass -NoProfile -NoLogo -Command \"...\"'"));
    }

    [Test]
    public void AsyncCommand_OtherException_NotFiltered()
    {
        // Win32Exception이 아닌 일반 [AIT Async] 예외는 SDK 디버깅에 가치가 있으므로 보호.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Async] 명령 실행 예외: System.InvalidOperationException: pipe closed"));
    }

    [Test]
    public void AsyncCommand_Win32Exception_NonPowershell_NotFiltered()
    {
        // powershell.exe 외 다른 ApplicationName의 Win32Exception은 패턴 좁히기 위해 통과.
        // (현재는 pnpm/git/node 등은 별도 진단 경로가 있어 SDK 가치 보존)
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Async] 명령 실행 예외: System.ComponentModel.Win32Exception (0x80004005): " +
            "ApplicationName='node.exe', CommandLine='...'"));
    }

    #endregion

    #region IL2CPP/Bee 빌드 단위별 실패 (SDK-SA~SV, T7, TV)

    [Test]
    public void BuildLibraryBee_ObjFailed_ReturnsTrue()
    {
        // Sentry SDK-SV: 매번 다른 해시 파일명 — "Building Library/Bee/artifacts/WebGL/" 부분 문자열로 일괄 매칭
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/GameAssembly/master_WebGL_wasm/uqx36jn5evd9.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseObjFailed_ReturnsTrue()
    {
        // Sentry SDK-SQ
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/287iqgly6k3x.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ManagedStripped_ReturnsTrue()
    {
        // Sentry SDK-T7
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/ManagedStripped failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_BuildJs_ReturnsTrue()
    {
        // Sentry SDK-TV
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.js failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseWasmObjFailed_W0_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-W0: release_WebGL_wasm 해시 .o 컴파일 실패.
        // 기존 "Building Library/Bee/artifacts/WebGL/" 패턴이 커버하는지 evidence로 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/xv9ku506h1iu.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseWasmObjFailed_VZ_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-VZ: 동일 형식의 다른 해시 .o 파일.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/69d9yupbfnl2.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseWasmObjFailed_19T_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-19T: 동일 형식의 또 다른 해시 .o 파일(GameAssembly release_WebGL_wasm).
        // 기존 "Building Library/Bee/artifacts/WebGL/" 패턴이 새 해시 변형도 커버하는지 evidence로 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/k3yyjft82n8a.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_MasterWasmObjFailed_19V_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-19V: master_WebGL_wasm 변형의 또 다른 해시 .o 파일.
        // 기존 "Building Library/Bee/artifacts/WebGL/" 패턴이 release/master 변형 모두 커버하는지 evidence로 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Building Library/Bee/artifacts/WebGL/GameAssembly/master_WebGL_wasm/j9ysjchgfghh.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseWasmObjFailed_10S_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-10S: 동일 형식의 또 다른 해시 .o 파일(GameAssembly release_WebGL_wasm).
        // 기존 "Building Library/Bee/artifacts/WebGL/" 패턴이 새 해시 변형도 커버하는지 evidence로 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/33pcnmr1fbpj.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseWasmObjFailed_19S_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-19S: 동일 형식의 또 다른 해시 .o 파일(GameAssembly release_WebGL_wasm).
        // 기존 "Building Library/Bee/artifacts/WebGL/" 패턴이 새 해시 변형도 커버하는지 evidence로 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/t07s1i7wa2fq.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: SDK가 동일 prefix로 출력하는 가상의 케이스도 필터링 안 되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/xxx.o failed"));
    }

    #endregion

    #region git wrapper trace (SDK-TE, SDK-TF)

    [Test]
    public void ExecCmdGit_ShowVariant_ReturnsTrue()
    {
        // Sentry SDK-TF: Unity Collab/CCD 등이 cmd 래핑으로 git 호출
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> cmd /c \"git\" show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecCmdGit_LogVariant_ReturnsTrue()
    {
        // Sentry SDK-TE
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> cmd /c \"git\" log -1 --pretty=format:%h"));
    }

    [Test]
    public void ExecCmdGit_WithAitPrefix_StillProtected()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Exec> cmd /c \"git\" show -s --pretty=%D HEAD"));
    }

    #endregion

    #region 사용자 WebGL 빌드 취소 (SDK-TX)

    [Test]
    public void UserCancelledWebGLBuild_ReturnsTrue()
    {
        // Sentry SDK-TX — 사용자가 WebGL 빌드를 직접 취소한 정상 액션. AITConvertCore.cs의
        // AITLog.Warning("[AIT] 사용자에 의해 WebGL 빌드가 취소되었습니다.")가 출력한 본문.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 사용자에 의해 WebGL 빌드가 취소되었습니다."));
    }

    [Test]
    public void UserCancelledBuild_ShortVariant_ReturnsTrue()
    {
        // AITConvertCore.cs의 Debug.LogWarning("[AIT] 빌드가 취소되었습니다.") 변형도 동일 노이즈.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 빌드가 취소되었습니다."));
    }

    [Test]
    public void BuildFailure_NotCancellation_NotFiltered()
    {
        // 실제 빌드 실패는 SDK 진단 가치가 있으므로 보호 — "취소" 문구가 없으면 매칭되지 않음.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] WebGL 빌드가 실패했습니다."));
    }

    [Test]
    public void Cancellation_WithoutAitPrefix_NotFiltered()
    {
        // composite AND 가드의 반대 축 검증 — "[AIT" prefix 없이 "빌드가 취소되었습니다"만
        // 포함하는 메시지는 가드에 걸리지 않아야 한다(외부 코드의 다른 빌드 시스템 로그 보호).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Addressables 빌드가 취소되었습니다."));
    }

    #endregion

    #region Android 키스토어 노이즈 (APPS-IN-TOSS-UNITY-SDK-118)

    [Test]
    public void AndroidKeystore_UnableToListKeys_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-118 — Unity Android 빌드 시 사용자가 잘못된 키스토어
        // 경로/비밀번호를 설정했을 때 OpenJDK + sdktools.jar가 직접 출력하는 오류.
        // AIT SDK 식별자 없음, SDK 코드는 키스토어를 직접 다루지 않으므로 SDK 버그 아님.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Unable to list keys in the keystore. Please make sure the location and password of the keystore is correct."));
    }

    [Test]
    public void AndroidKeystore_UnableToListKeys_BareMessage_ReturnsTrue()
    {
        // prefix 없이 핵심 문구만 도달하는 변형도 동일하게 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Unable to list keys in the keystore. Please make sure the location and password of the keystore is correct."));
    }

    [Test]
    public void AndroidKeystore_UnableToListKeys_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Unable to list keys in the keystore."));
    }

    #endregion

    #region Android Gradle 빌드 실패 노이즈 (APPS-IN-TOSS-UNITY-SDK-134)

    [Test]
    public void GradleBuildFailed_CommandInvokationFailure_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-134 — Unity Editor가 Android Gradle/Java 외부 빌드 실패를
        // 출력하는 순수 외부 노이즈. 사용자 프로젝트 Android 빌드 설정 문제(JDK/SDK 경로,
        // Gradle 버전 호환성 등)이며 SDK 코드와 무관.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "CommandInvokationFailure: Gradle build failed."));
    }

    [Test]
    public void GradleBuildFailed_WithTrailingDetail_ReturnsTrue()
    {
        // 빌드 에러 상세 내용이 후행하는 변형도 부분 문자열 매칭으로 드롭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "CommandInvokationFailure: Gradle build failed.\r\nstderr[\r\nFAILURE: Build failed with an exception."));
    }

    [Test]
    public void GradleBuildFailed_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] CommandInvokationFailure: Gradle build failed."));
    }

    #endregion

    #region 서드파티 Unity MCP 빌드 도구 노이즈 (APPS-IN-TOSS-UNITY-SDK-13E)

    [Test]
    public void McpBuildPrefix_WebGLManagedStrippedFailure_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-13E — 서드파티 Unity MCP 빌드 도구가 stdout/stderr에
        // 남기는 로그 prefix. 본문은 개인/사용자 프로젝트의 IL2CPP UnityLinker(ManagedStripped)
        // 빌드 실패이며, SDK 코드베이스 어디에도 "[MCP Build]" 문자열이 없음(grep 확인).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: [MCP Build] ✗ Build failed: WebGL — Building Library/Bee/artifacts/WebGL/ManagedStripped failed with output:"));
    }

    [Test]
    public void McpBuildPrefix_BareVariant_ReturnsTrue()
    {
        // prefix만 있고 후행 문구가 다른 변형도 부분 문자열 매칭으로 동일하게 드롭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[MCP Build] Build failed: iOS — xcodebuild exited with code 65"));
    }

    [Test]
    public void McpBuildPrefix_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [MCP Build] Build failed: WebGL"));
    }

    [Test]
    public void McpBuildPrefix_WithAppsInTossKeyword_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: 메시지 본문에 AppsInToss 식별자가 섞이면
        // MessageContainsSdkKeyword 가드가 먼저 매칭되어 "[MCP Build]" 패턴보다 우선해 보호되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[MCP Build] Build failed while referencing AppsInToss.dll during IL2CPP link step"));
    }

    #endregion

    #region Android 기기 연결(adb reverse) 노이즈 (APPS-IN-TOSS-UNITY-SDK-13K)

    [Test]
    public void AndroidAdbReverseFailure_UnityWarningPrefix_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-13K — Unity 에디터가 로컬 Android 기기 연결(adb reverse,
        // USB 디버깅) 시 직접 출력하는 표준 경고. AIT SDK 코드는 adb/reverse를 전혀 호출하지 않으며
        // (grep 확인), 사용자 PC의 Android SDK/adb 설정 문제로 발생.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Connection to Android device failed: Unable to reverse network traffic to device. Please make sure the Android SDK is installed and is properly configured in the Editor. See the Console for more details."));
    }

    [Test]
    public void AndroidAdbReverseFailure_BareVariant_ReturnsTrue()
    {
        // "UnityWarning: " prefix가 없는 변형도 부분 문자열 매칭으로 동일하게 드롭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Connection to Android device failed: Unable to reverse network traffic to device."));
    }

    [Test]
    public void AndroidAdbReverseFailure_WithAitKeyword_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: 메시지 본문에 AppsInToss 식별자가 섞이면
        // MessageContainsSdkKeyword 가드가 먼저 매칭되어 이 패턴보다 우선해 보호되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Unable to reverse network traffic to device during AppsInToss device sync"));
    }

    #endregion

    #region pnpm 중첩 가상 스토어 경로 삭제 실패 경고 (APPS-IN-TOSS-UNITY-SDK-1A9)

    [Test]
    public void PnpmNestedStoreDelete_ReportedMessage_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-1A9 — ait-build/node_modules 하위 pnpm 가상 스토어의
        // 중첩 경로(.pnpm/<pkg>@<ver>_<hash>/node_modules/...)를 Unity가 자체적으로 삭제 시도하다
        // 경로 검증에 실패해 출력하는 경고. ait-build는 Assets/ 밖에 위치해 SDK 코드는 이 경로를
        // AssetDatabase API로 다루지 않으므로(grep 확인) Unity 자체 노이즈로 분류한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Cannot delete asset. ait-build/node_modules/.pnpm/@granite-js+cli@1.0.4_@granite-js+types@1.0.4_typescript@5.9.3_zod@3.25.76__7197c115a07e4eff2a78d23f36868faf/node_modules/clipanion/lib/advanced/index.mjs is not a valid path."));
    }

    [Test]
    public void PnpmNestedStoreDelete_UnityWarningPrefix_ReturnsTrue()
    {
        // "UnityWarning: " prefix가 덧붙은 변형(에디터 로그 핸들러 래핑)도 동일하게 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Cannot delete asset. ait-build/node_modules/.pnpm/@some+other-pkg@2.0.0/node_modules/foo/bar.mjs is not a valid path."));
    }

    [Test]
    public void PnpmNestedStoreDelete_WithoutPnpmSegment_NotFiltered()
    {
        // ".pnpm" 세그먼트가 없는 일반 "삭제 실패" 경고는 이 규칙과 무관해야 함 — 과매칭 방지.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Cannot delete asset. Assets/SomeFolder/File.txt is not a valid path."));
    }

    [Test]
    public void PnpmNestedStoreDelete_WithoutCannotDeletePhrase_NotFiltered()
    {
        // "Cannot delete asset" 문구가 없으면 매칭되지 않아야 함 — composite AND 가드 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "node_modules/.pnpm/foo@1.0.0/bar.mjs is not a valid path."));
    }

    #endregion
}
