// IsKnownNonSdkMessageTests.UserEnvironment.cs - 사용자 환경·카테고리 D·A-5 백스톱
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

public partial class IsKnownNonSdkMessageTests
{
    #region 사용자 환경 문제 (WebGL 모듈 미설치, GUID 충돌)

    [Test]
    public void BuildTargetWebGLNotSupported_ReturnsTrue()
    {
        // 사용자 Unity 설치에 WebGL 모듈이 없어서 발생 — SDK-DD
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Build target 'WebGL' not supported"));
    }

    [Test]
    public void BuildTargetWebGLNotSupported_FullMessage_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Build target 'WebGL' not supported. Please install it via Unity Hub."));
    }

    [Test]
    public void GuidConflict_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-BQ에서 실제 관찰된 메시지 형태를 반영한 fixture.
        // 사용자 프로젝트에 동일 GUID의 에셋이 남아있어 AITTemplate 파일과 충돌.
        // AITTemplate 경로가 메시지에 포함되어도 AitKeywords(`[AIT`, `AIT:` 등) 중 어느 것의 부분 문자열도 아니므로
        // SDK 보호 가드가 발동하지 않아 일반 필터 흐름으로 내려가 정상 필터링됨.
        // 필터가 향후 anchored/regex로 tightening되더라도 이 실측 경로를 놓치지 않도록 fixture로 박아둠.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [abc123def456] for asset 'Assets/WebGLTemplates/AITTemplate/TemplateData/diagnostics.css' conflicts with: 'Assets/OldCopy/diagnostics.css'"));
    }

    [Test]
    public void GuidConflict_WithoutAitTemplate_ReturnsTrue()
    {
        // AITTemplate 경로가 없어도 일반 GUID 충돌 메시지라면 사용자 프로젝트 문제
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [deadbeef] for asset 'Assets/Foo/bar.png' conflicts with: 'Assets/Baz/bar.png'"));
    }

    [Test]
    public void GuidWithoutConflictsWith_ReturnsFalse()
    {
        // "GUID ["만 있고 "conflicts with:"가 없으면 composite AND의 한쪽만 충족되어 필터링 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [abc123] for asset 'Assets/Foo/bar.png' updated"));
    }

    [Test]
    public void ConflictsWithoutGuid_ReturnsFalse()
    {
        // "conflicts with:"만 있고 "GUID ["가 없으면 composite AND의 한쪽만 충족되어 필터링 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Package version conflicts with: older version"));
    }

    [Test]
    public void BuildTargetWebGLNotSupported_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Build target 'WebGL' not supported"));
    }

    [Test]
    public void GuidConflict_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 GUID 충돌 메시지는 SDK가 직접 출력한 것으로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] GUID [abc123] for asset 'Assets/Foo/bar.png' conflicts with: 'Assets/Baz/bar.png'"));
    }

    [Test]
    public void AddressableGroupSchemasMissing_ReturnsTrue()
    {
        // Sentry KR — 사용자 프로젝트의 Addressable 설정 문제
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Group 'Default Local Group' does not have any associated AddressableAssetGroupSchemas"));
    }

    [Test]
    public void AddressableGroupSchemasMissing_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Group 'Foo' does not have any associated AddressableAssetGroupSchemas"));
    }

    [Test]
    public void ExecGitBreadcrumb_GitShow_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-NX — 외부 도구의 git 서브프로세스 실행 브레드크럼
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecGitBreadcrumb_GitLog_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-NW
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git log -1 --pretty=format:%h"));
    }

    [Test]
    public void ExecGitBreadcrumb_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: SDK가 [AIT] prefix와 함께 동일 메시지를 출력했다면 필터되면 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void AddressableContentBuildFailure_ReturnsTrue()
    {
        // Sentry SDK-H2 — Unity Addressables 콘텐츠 빌드 실패 (SDK와 무관한 사용자 프로젝트 문제)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Addressable content build failure (duration : 0:00:16.61)"));
    }

    [Test]
    public void AddressableContentBuildFailure_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Addressable content build failure (duration : 0:00:16.61)"));
    }

    [TestCase(
        "[Worker2] Import Error Code:(4)",
        TestName = "WorkerImportError_Worker2_ReturnsTrue")]
    [TestCase(
        "[Worker3] Import Error Code:(4)",
        TestName = "WorkerImportError_Worker3_ReturnsTrue")]
    [TestCase(
        "[Worker4] Import Error Code:(4)",
        TestName = "WorkerImportError_Worker4_ReturnsTrue")]
    // Sentry APPS-IN-TOSS-UNITY-SDK-V7, APPS-IN-TOSS-UNITY-SDK-111 — 워커 prefix 없이 UnityWarning으로 래핑된 변형.
    // Unity SourceAssetDB modification time 불일치 경고로 Unity 자체 에셋 임포트 시스템에서 출력되는 외부 노이즈 — SDK 식별자 없음.
    [TestCase(
        "UnityWarning: Import Error Code:(4)",
        TestName = "WorkerImportError_UnityWarningWrapped_ReturnsTrue")]
    public void WorkerImportError_RealisticVariants_ReturnsTrue(string message)
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-RC/RD/RE/V7/111 — Unity AssetImporter 내부/메인 에셋 임포트 에러.
        // 워커 prefix 유무·워커 번호·코드 숫자가 가변이지만 "Import Error Code:(" 부분 문자열로 모두 매칭.
        // SDK 코드에는 "Import Error Code" 문자열이 존재하지 않음 (grep으로 확인).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
    }

    [Test]
    public void WorkerImportError_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [Worker2] Import Error Code:(4)"));
        // UnityWarning 래핑 변형에 대한 가드 회귀 방지
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Import Error Code:(4)"));
    }

    [Test]
    public void Il2CppStackTracesNotSupportedOnWebGL_ReturnsTrue()
    {
        // Sentry SDK-8B — Unity 엔진 자체 경고. WebGL 빌드는 IL2CPP의 "Method Name, File Name, Line Number"
        // 스택트레이스 옵션을 지원하지 않으므로 PlayerSettings에 해당 옵션이 켜져 있을 때 Unity가 직접 출력.
        // SDK 코드와 무관하므로 노이즈로 분류.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The \"Method Name, File Name, and Line Number\" option for IL2CPP stack traces is not supported on WebGL."));
    }

    [Test]
    public void Il2CppStackTracesNotSupportedOnWebGL_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The \"Method Name, File Name, and Line Number\" option for IL2CPP stack traces is not supported on WebGL."));
    }

    [Test]
    public void MetaFileInvalidGuid_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-R4 — 사용자 Assets/ 경로의 .meta 파일 GUID 손상
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The .meta file Assets/Art/Images/Skin0/specialblock_fire_5.png.meta does not have a valid GUID and its corresponding Asset file will be ignored. If this file is not malformed, please add a GUID, or delete the .meta file and it will be recreated correctly"));
    }

    [Test]
    public void MetaFileInvalidGuid_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The .meta file Assets/Foo/bar.png.meta does not have a valid GUID"));
    }

    [Test]
    public void GuidInsideMetaCannotBeExtracted_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-R3 — YAML Parser GUID 추출 실패 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The GUID inside 'Assets/Art/Images/Skin0/specialblock_fire_5.png.meta' cannot be extracted by the YAML Parser. Attempting to extract it via string matching instead. Please verify the file does not contain unexpected data."));
    }

    [Test]
    public void GuidInsideMetaCannotBeExtracted_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The GUID inside 'Assets/Foo.png.meta' cannot be extracted by the YAML Parser"));
    }

    #endregion

    #region 사용자 환경 / 빌드 외부 노이즈 (SDK-QQ, SDK-7Y, SDK-V1, SDK-TS, SDK-RG, APPS-IN-TOSS-UNITY-SDK-12Q)

    [Test]
    public void PackageManager_ErrorAddingPackages_ReturnsTrue()
    {
        // Sentry SDK-QQ — 사용자 환경 git/네트워크 문제로 Package Manager가 직접 출력.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[Package Manager Window] Error adding/removing packages: " +
            "https://github.com/toss/apps-in-toss-unity-sdk.git #release/v2.4.3."));
    }

    [Test]
    public void PackageManager_ErrorRemovingPackages_AltVersion_ReturnsTrue()
    {
        // Sentry SDK-7Y — 동일 패턴 다른 버전 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[Package Manager Window] Error adding/removing packages: " +
            "https://github.com/toss/apps-in-toss-unity-sdk.git#release/v2.4.5."));
    }

    [Test]
    public void PackageManager_ResolvingPackagesError_ReturnsTrue()
    {
        // Sentry SDK-V1 — 사용자 manifest.json 충돌.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "An error occurred while resolving packages:\n" +
            "  Project has invalid dependencies: com.example.foo"));
    }

    [Test]
    public void Pnpm_UnknownSyntaxError_ApiKey_ReturnsTrue()
    {
        // Sentry SDK-TS — 사용자가 deployment key 미입력 상태로 deploy 실행.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stdout] Unknown Syntax Error: Not enough arguments to option --api-key."));
    }

    [Test]
    public void WindowsShell_NotRecognized_ReturnsTrue()
    {
        // Sentry SDK-RG — Windows cmd 인코딩/PATH 문제.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stderr] '��e' is not recognized as an internal or external command,"));
    }

    [Test]
    public void PackageManager_ErrorAddingPackageSingular_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-12Q — UPM이 단일 패키지 추가 실패 시 출력하는 단수형 변형.
        // "Error adding/removing packages"(복수형, QQ/7Y)와 달리 "Error adding package:"(단수형 + 콜론)으로 출력됨.
        // 패키지 ID에 'apps-in-toss' 토큰이 포함되어 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: [Package Manager Window] Error adding package: " +
            "im.toss.apps-in-toss-unity-sdk@https://github.com/toss/apps-in-toss-unity-sdk.git#2.9.0"));
    }

    [Test]
    public void PackageManager_ErrorAddingPackageSingular_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [Package Manager Window] Error adding package: im.toss.apps-in-toss-unity-sdk@https://example.com#2.9.0"));
    }

    [Test]
    public void PackageManager_GenericPmMessage_NotFiltered()
    {
        // 다른 "[Package Manager Window]" 메시지는 통과 — "Error adding/removing" 토큰 미포함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[Package Manager Window] Successfully installed package"));
    }

    [Test]
    public void Pnpm_OtherSyntaxError_NotFiltered()
    {
        // "Not enough arguments" 부분이 없으면 통과 — 다른 Unknown Syntax 메시지 보호.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stdout] Unknown Syntax Error: Invalid flag --foo."));
    }

    #endregion

    #region 카테고리 D 노이즈 (Addressables / Vite / git / 정책 fetch / Unity 자체)

    [Test]
    public void Addressables_SbpError_ReturnsTrue()
    {
        // Sentry SDK-H4: "SBP ErrorError"는 ScriptableBuildPipeline 출력 자체.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("SBP ErrorError"));
    }

    [Test]
    public void Addressables_FailedToBuildContent_ReturnsTrue()
    {
        // Sentry SDK-S4
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "BuildFailedException: BuildFailedException: Failed to build Addressables content, content not included in Player Build. \"SBP ErrorError\""));
    }

    [Test]
    public void BuildLayout_HasNotOpen_ReturnsTrue()
    {
        // Sentry SDK-EX
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Cannot read BuildLayout header, BuildLayout has not open for a file"));
    }

    [Test]
    public void DefaultAudioDevice_Changed_ReturnsTrue()
    {
        // Sentry SDK-TW
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Default audio device was changed, but the audio system failed to initialize it. Attempting to reset sound system."));
    }

    [Test]
    public void EmscriptenBuild_WebGlBuildFailed_ReturnsTrue()
    {
        // Sentry SDK-RV
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building webgl/Build/204ccce7cc46e2cd9bd7212e664b4738.data.unityweb failed with output:"));
    }

    [Test]
    public void UnityAssetDb_LibraryLoad_ReturnsTrue()
    {
        // Sentry SDK-RT
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Unknown error occurred while loading 'Library/AppsInToss/AITBuildSession.asset'."));
    }

    [Test]
    public void SdkPolicyFetchFailed_ReturnsTrue()
    {
        // Sentry SDK-M9
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] sdk-policy.json fetch 실패: System.Net.WebException: The operation has timed out."));
    }

    [Test]
    public void VitePortTimeout_ReturnsTrue()
    {
        // Sentry SDK-QN
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Vite 포트 5173 대기 타임아웃 (15초), 브라우저를 엽니다"));
    }

    [Test]
    public void DevServerStartFailed_PortBusy_ReturnsTrue()
    {
        // Sentry SDK-KP
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: Dev 서버 시작 실패 - 포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요."));
    }

    [Test]
    public void ProductionServerStartFailed_ReturnsTrue()
    {
        // Sentry SDK-Q3
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: Production 서버 시작 실패 - 프로세스가 비정상 종료되었습니다 (Exit Code: 1)"));
    }

    [Test]
    public void AutoCommit_AuthorIdentity_ReturnsTrue()
    {
        // Sentry SDK-SK
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 자동 커밋 실패: Author identity unknown"));
    }

    [Test]
    public void AutoCommit_GitProcessTimeout_ReturnsTrue()
    {
        // Sentry SDK-TZ
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 자동 커밋 실패: Git 프로세스를 시작할 수 없거나 타임아웃이 발생했습니다."));
    }

    [Test]
    public void GitCommandTimeout_300Seconds_ReturnsTrue()
    {
        // Sentry SDK-TY (300초 변형) — 5초 케이스(SDK-QC)는 #591에서 source 차단됨.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Git 명령 타임아웃 (300초): git commit --quiet -m \"정리: .gitignore 보호 패턴 추가\""));
    }

    [Test]
    public void SdkPolicyFetch_NotMatching_GenericMessage_NotFiltered()
    {
        // negative — 단순 'sdk-policy' 문자열만 포함된 다른 메시지는 영향받지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] sdk-policy.json 적용 완료"));
    }

    #endregion

    #region A-5 백스톱: ImageUtil / AssetDatabase path / play mode / pnpm 실패 / CS0618 (SDK-W1~W3, QK, QJ/QH/QG, RJ, VG/VD/VB, WN/WM)

    [Test]
    public void ImageUtilSpriteNull_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-W1/W2/W3 — 사용자 게임 이미지 유틸리티의 sprite 미할당 경고.
        // 에셋명만 가변이고 "[ImageUtil]" prefix가 불변. SDK는 이 prefix를 출력하지 않으며 AitKeywords에도 없다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[ImageUtil] Icon_1 sprite is null"));
    }

    [Test]
    public void ImageUtilSpriteNull_OtherAssetName_ReturnsTrue()
    {
        // 에셋명 변형(가변 토큰)도 동일 prefix로 흡수.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[ImageUtil] Weapon_3 sprite is null"));
    }

    [Test]
    public void SdkSpriteLog_WithAitPrefix_NeverFiltered()
    {
        // SDK 자체 sprite 관련 로그("[AIT]" prefix)는 "[ImageUtil]" 패턴과 무관 — 보호되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 스프라이트 아틀라스 빌드 완료"));
    }

    [Test]
    public void InvalidAssetDatabasePath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-QK — 사용자 코드가 프로젝트 폴더 밖/절대 경로로 AssetDatabase 호출.
        // Unity 엔진이 직접 출력하는 영문 경고. SDK는 이 문구를 출력하지 않는다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Invalid AssetDatabase path: /Scripts/CameraController.cs. Use path relative to the project folder."));
    }

    [Test]
    public void InvalidAssetDatabasePath_AitBuildToken_NeverFiltered()
    {
        // SDK가 잘못된 경로로 호출하면 경로에 "ait-build" 토큰이 들어가 키워드 가드가 먼저 보호한다(array 도달 전).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Invalid AssetDatabase path: ait-build/dist/index.html. Use path relative to the project folder."));
    }

    [Test]
    public void PlayModeRestriction_AddressablesWrapped_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-QJ/QH/QG — play mode 중 Addressables/빌드 트리거 시 Unity 엔진 제약 에러.
        // 현행 SDK는 DoExport 진입에서 play mode 가드로 차단(sentryCapture:false)하지만 구버전 잔여 이벤트를 backstop으로 흡수.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Failed to build Addressables content, content not included in Player Build. \"This cannot be used during play mode.\""));
    }

    [Test]
    public void SdkPlayModeGuardLog_WithAitPrefix_NeverFiltered()
    {
        // SDK 자체 play mode 가드 메시지("[AIT]" prefix, 한글)는 "This cannot be used during play mode" 문구가 없어
        // 자연히 통과하지만, 보호 의도를 명시적으로 검증한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] play mode 중에는 빌드를 실행할 수 없습니다. 재생을 종료한 뒤 다시 시도하세요."));
    }

    [Test]
    public void PnpmCommandFailed_ExitCode_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-RJ — 구버전(≤2.4.x)이 캡처한 채 보낸 "[pnpm] 명령 실패" 잔여 이벤트.
        // 현행 SDK는 AITNpmRunner.cs:335에서 sentryCapture:false로 source 차단(이 패턴은 backstop).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[pnpm] 명령 실패 (Exit Code: 1): pnpm exec ait build"));
    }

    [Test]
    public void PnpmAsyncCommandFailed_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-VG/VD/VB — "[pnpm] 비동기 명령 실패" 잔여 이벤트.
        // 현행 SDK는 AITNpmRunner.cs:414에서 sentryCapture:false로 source 차단(이 패턴은 backstop).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[pnpm] 비동기 명령 실패 (Exit Code: -1): pnpm exec granite build"));
    }

    [Test]
    public void PnpmCommandFailed_WithAitPrefix_NeverFiltered()
    {
        // SDK가 직접 "[AIT...]" prefix로 출력한 pnpm 실패 로그는 보호되어야 함(키워드 가드).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [pnpm] 명령 실패 (Exit Code: 1): pnpm exec ait build"));
    }

    [Test]
    public void UserCodeCs0618_AssetsBackslashPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-WN — 사용자 프로젝트(Assets/) 코드의 obsolete API 사용 경고.
        // 파일명 'AppsInTossWebGLProjectSetup'이 단어 경계 없이 붙어 키워드 가드를 우회하므로 Assets/ + .cs(L,C) 합성으로 좁혀 매칭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Editor\\AppsInTossWebGLProjectSetup.cs(64,9): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget, ManagedStrippingLevel) instead.'"));
    }

    [Test]
    public void UserCodeCs0618_AssetsForwardSlashPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-WM — POSIX 경로 변형(SetScriptingBackend).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Editor/AppsInTossWebGLProjectSetup.cs(65,9): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget, ScriptingImplementation) instead.'"));
    }

    [Test]
    public void Cs0618_SdkPackagePath_NeverFiltered()
    {
        // SDK 자체 .cs의 CS0618은 Packages/com.toss.apps-in-toss 경로로 출력 → Assets/ 가드 미충족 +
        // "apps-in-toss" 키워드 가드 보호. 현행 SDK는 #if UNITY_6000_0_OR_NEWER로 obsolete API를 피하지만
        // 만약 출력되더라도 SDK 코드 경고는 절대 드롭하지 않는다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): warning CS0618: 'X' is obsolete: 'use Y'"));
    }

    [Test]
    public void Cs0618_NoAssetsPath_NotFiltered()
    {
        // Assets/ 경로도 .cs(L,C)도 없는 일반 CS0618(예: 사전 컴파일된 dll)은 합성 가드가 매칭되지 않아 통과.
        // CS0618을 무차별 드롭하지 않음을 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SomeLib.dll: warning CS0618: 'Z' is obsolete"));
    }

    [Test]
    public void UserCodeCs0067_AssetsBackslashPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-1A3 — 사용자 프로젝트(Assets/) 코드의 미사용 이벤트 선언 경고.
        // 사용자 클래스명 'AppsInTossStorageManager'가 단어 경계 없이 붙어 키워드 가드를 우회하므로
        // Assets/ + .cs(L,C) 합성으로 좁혀 매칭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\MyGame\\Scripts\\AppsInToss\\AppsInTossStorageManager.Ranking.cs(89,31): warning CS0067: The event 'AppsInTossStorageManager.RankingResultStateChanged' is never used"));
    }

    [Test]
    public void Cs0067_SdkPackagePath_NeverFiltered()
    {
        // SDK 자체 .cs의 CS0067은 Packages/com.toss.apps-in-toss 경로로 출력 → Assets/ 가드 미충족 +
        // "apps-in-toss" 키워드 가드 보호. SDK 코드 경고는 절대 드롭하지 않는다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Runtime/Foo.cs(10,5): warning CS0067: The event 'Foo.OnChanged' is never used"));
    }

    [Test]
    public void Cs0067_NoAssetsPath_NotFiltered()
    {
        // Assets/ 경로도 .cs(L,C)도 없는 일반 CS0067은 합성 가드가 매칭되지 않아 통과.
        // CS0067을 무차별 드롭하지 않음을 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SomeLib.dll: warning CS0067: 'Z' is never used"));
    }

    #endregion
}
