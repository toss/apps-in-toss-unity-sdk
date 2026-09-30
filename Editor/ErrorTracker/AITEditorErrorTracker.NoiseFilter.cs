// 노이즈 패턴을 추가할 때 고치는 곳은 이 파일 하나다(패턴 배열과 IsKnownNonSdkMessage).
// 가드 헬퍼(MessageContainsSdkKeyword 등)와 DetermineErrorSource는 본체 AITEditorErrorTracker.cs에 있다.

using System;

namespace AppsInToss.Editor.ErrorTracker
{
    internal static partial class AITEditorErrorTracker
    {
        #region AIT Keywords

        private static readonly string[] AitKeywords =
        {
            "[AIT",
            "AIT:",
            "AppsInToss",
            "apps-in-toss",
            "ait-build",
            "AITConvertCore",
            "AITPackageBuilder",
            "AITNodeJS",
            "AITNpmRunner"
        };

        // Dev/Production Server 프로세스에서 리디렉션된 로그의 prefix 패턴.
        // 이 로그는 granite dev 프로세스의 stdout/stderr를 Unity Console로 전달한 것이며,
        // SDK 자체 에러가 아니므로 Sentry 캡처에서 제외해야 합니다.
        // "[Production Server]"는 Production Server 메뉴 제거(신규 SDK는 더 이상 방출하지 않음) 후에도
        // 구버전 SDK가 남긴 로그를 걸러내기 위해 레거시로 유지한다 (삭제 금지).
        private static readonly string[] ServerLogPrefixes =
        {
            "[Dev Server]",
            "[Production Server]"
        };

        // 100% SDK와 무관한 Unity 내부/사용자 프로젝트 메시지 패턴.
        // IsAitRelated를 통과한 메시지 중에서도 이 패턴이 매칭되면 캡처 대상에서 제외.
        //
        // 새 노이즈 패턴 추가 워크플로우:
        //   1. Sentry에서 해당 이슈가 SDK 변경 없이 재현되는지 확인 (사용자 프로젝트/Unity 내부)
        //   2. Sentry에서 해당 이슈를 ignored 처리
        //   3. 여기 NonSdkMessagePatterns에 메시지의 불변 핵심 문구를 부분 문자열로 추가
        //      (Unity 버전/환경 차이에 민감한 부분은 피할 것)
        //      단, 단일 부분 문자열이 SDK 자체 로그와 충돌할 위험이 있으면
        //      IsKnownNonSdkMessage 내에 composite AND 조건(예: "GUID [" && "conflicts with:")을 추가
        //   4. IsKnownNonSdkMessageTests*.cs(카테고리별 partial)에 positive/negative 테스트 추가
        //      (특히 AIT 키워드가 섞여도 필터링되지 않는지 negative 케이스 필수)
        private static readonly string[] NonSdkMessagePatterns =
        {
            // Unity 내부 경고
            "GfxDevice renderer is null",
            "Ignoring locale ",
            "Unable to load build report at Library/",
            "Cannot read BuildLayout header",
            "[ServicesCore]",
            "ProfileValueReference: GetValue called with empty id",
            "The Editor does not support 32-bit plugins",
            // Unity 라이프사이클 경고 — 사용자 MonoBehaviour의 Awake/OnValidate에서 발생
            "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate",
            // Unity Animator — 사용자 프로젝트의 레거시 클립 사용
            "Legacy AnimationClips are not allowed in Animator Controllers",
            // ↑ 동일 경고의 첫 줄 변형 — Unity 출력 포맷("cannot be used in the State \"X\".")과 밀착시키려
            // 따옴표를 포함해 좁힘 (SDK-KV/KT)
            "cannot be used in the State \"",
            // Unity 빌드 — 미컴파일 코드 변경 상태에서 빌드 시도 시 Unity가 직접 출력 (SDK-NE)
            "You are building a player, but you have uncompiled code changes",
            // Unity 빌드 — 사용자가 Development 옵션 없이 ConnectWithProfiler를 설정해 발생하는 Unity 표준 예외 (SDK-T3)
            // 사용자 BuildPlayerOptions 구성 문제이며 SDK 버그 아님. Unity 버전/언어와 무관하게 영문 메시지 본문이 동일.
            "Non-development build cannot allow auto-connecting the profiler",

            // 사용자 프로젝트 에셋 문제
            "matches more than one built-in atlases",
            "Warnings during import of AudioClip",
            // FMOD/오디오 — 사용자 에셋 import/포맷 문제
            "Cannot create FMOD::Sound instance for clip",
            "Failed getting load state of FSB for audio clip",
            "Cannot load audio data for audio clip",
            // Animator — 사용자 컨트롤러 설정 누락
            "doesn't have an Exit Time or any condition",

            // Unity 패키지 내부
            "Localization-String-Tables-",
            "Warning in Graph at Packages/com.unity",

            // 사용자 프로젝트 직렬화 ([Assembly-CSharp] 한정 — SDK 어셈블리의 직렬화 경고는 보호)
            "Fields serialized in [Assembly-CSharp]",
            // [Assembly-CSharp] 타입의 player/editor 직렬화 mismatch (AdSwitcher 등 사용자 코드)
            "Type '[Assembly-CSharp]",
            // 사용자 게임 코드의 player script 컴파일 실패 (스택 없이 메시지만 도착)
            "Failed to compile player scripts",
            // 사용자 코드의 사용되지 않은 필드 경고 — Unity 컴파일러가 직접 출력하는 CS0414
            "warning CS0414",

            // 외부 패키지 (Unity 버전별 괄호 유무에 관계없이 매칭되도록 핵심 문구만 추출)
            "exists but its folder",
            // .meta 파일은 있으나 대응 에셋을 못 찾을 때 Unity가 출력하는 표준 경고 — SDK 외부 패키지 노이즈.
            // 예: "A meta data file (.meta) exists but its asset 'Packages/com.wooshii.foldericons/...' can't be found ..."
            //     "A meta data file (.meta) exists but its asset can't be found. ..."
            // 위 "exists but its folder"와 동일한 Unity 메시지 계열로, asset 경로 유무·버전 차이와 무관하게
            // 핵심 문구 "exists but its asset" 만으로 두 변형을 모두 매칭. SDK는 이 문자열을 출력하지 않음.
            // 사용자가 Unity 외부에서 자산을 이동/삭제해 .meta만 고아로 남은 변형(asset 경로가 Assets/ 또는
            // Packages/...로 가변, "...please ensure that the corresponding .meta file is moved..." 안내 포함)도
            // 동일 substring으로 매칭되므로 별도 항목이 필요 없다. (이전 중복 항목 1개 제거.)
            // Sentry APPS-IN-TOSS-UNITY-SDK-ZQ, APPS-IN-TOSS-UNITY-SDK-ZS
            "exists but its asset",

            // 외부 UPM 패키지(immutable 폴더)의 에셋에 .meta 파일이 없을 때 Unity 에디터가 직접 출력하는 표준 경고.
            // 외부 서드파티 패키지(예: com.lupidan.apple-signin-unity)가 .meta를 누락한 채 배포되어 발생.
            // 예: "Asset 'Packages/com.lupidan.apple-signin-unity/AppleAuthSampleProject/ProjectSettings/...'
            //      has no meta file, but it's in an immutable folder. The asset will be ignored."
            // Sentry APPS-IN-TOSS-UNITY-SDK-10D, 10E, 10F, 10G, 10H, 10J, 10K.
            // immutable 폴더(외부 패키지)의 누락 .meta는 사용자가 조치 불가한 Unity 자체 노이즈이며,
            // 에셋 경로/패키지명이 가변이므로 불변 핵심 문구만 추출. SDK는 이 문구를 출력하지 않으므로(AitKeywords 미포함) 보호 가드와 충돌 없음.
            "has no meta file, but it's in an immutable folder",

            // Unity URP 내부
            "exceeds previous array size",

            // 사용자 Addressable 설정
            "does not have any associated AddressableAssetGroupSchemas",
            // 사용자 Addressables 콘텐츠 빌드 실패 — Unity Addressables 시스템 자체 오류 (SDK-H2)
            "Addressable content build failure",

            // 사용자 Unity 설치에 WebGL 모듈 미설치 — SDK-DD
            "Build target 'WebGL' not supported",

            // Unity AssetImporter 내부 워커/메인 에러 — SDK 외부 출처 (Unity 자체 에셋 임포트 경고).
            // 워커 prefix가 있는 변형: "[Worker2] Import Error Code:(4)" (SDK-RC/RD/RE)
            // prefix 없이 UnityWarning으로 래핑된 변형: "UnityWarning: Import Error Code:(4)" (SDK-V7, APPS-IN-TOSS-UNITY-SDK-111)
            // Unity SourceAssetDB modification time 불일치 경고로 Unity 자체 에셋 임포트 시스템에서 출력되는 외부 노이즈 — SDK 식별자 없음.
            // prefix 유무·워커 번호·코드 숫자가 모두 가변이므로 공통 핵심 문구 "Import Error Code:(" 로 일반화.
            // SDK는 이 문자열을 출력하지 않으므로(AitKeywords에 없음) 보호 가드와 충돌 없음.
            "Import Error Code:(",

            // 서브프로세스 실행 브레드크럼 — SDK가 아닌 외부(Unity Collab/CCD/사용자 도구)가 출력
            // 예: "Exec> git show -s --pretty=%D HEAD", "Exec> git log -1 --pretty=format:%h"
            // Sentry APPS-IN-TOSS-UNITY-SDK-NX, APPS-IN-TOSS-UNITY-SDK-NW
            "Exec> git ",

            // Unity 엔진 자체 경고 — WebGL은 IL2CPP "Method Name, File Name, Line Number" 스택트레이스 옵션 미지원 (SDK-8B)
            "IL2CPP stack traces is not supported on WebGL",

            // 사용자 게임 코드(외부 IAP 모듈)가 출력하는 진단. SDK 코드에 'Toss IAP' 문자열은 없음.
            // 사용자 환경(상품 구성 누락, 백엔드 연결 실패) 원인이며 SDK 분기로 해결 불가.
            // Sentry APPS-IN-TOSS-UNITY-SDK-CY.
            "Toss IAP: Initialize failed or no products",

            // 사용자 프로젝트 .meta 파일 GUID 손상 — Unity 자체 노이즈
            // 예: "The .meta file Assets/.../foo.png.meta does not have a valid GUID..."
            //     "The GUID inside 'Assets/.../foo.png.meta' cannot be extracted by the YAML Parser..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-R4, APPS-IN-TOSS-UNITY-SDK-R3
            // 두 번째 패턴은 작은따옴표를 포함시켜 .meta 경로 형식의 메시지에만 매칭되도록 좁힘
            // (다른 YAML 에셋 파싱 오류 메시지와 충돌 방지).
            "does not have a valid GUID",
            "' cannot be extracted by the YAML Parser",

            // pnpm stdout/stderr 패스스루 노이즈 — Unity가 외부 pnpm 프로세스 출력을 래핑한 라인.
            // "[pnpm] 출력:"(SDK-HA, SDK-R6)은 stdout, "[pnpm] 오류:"(SDK-VF, SDK-VA)는 stderr를
            // Unity가 UnityWarning으로 래핑한 것. 둘 다 SDK 외부(pnpm 프로세스) 출처.
            // AitKeywords에 "[pnpm]"이 없어 SDK 보호 가드는 우회되며,
            // SDK 자체 로그는 "[AIT...]" prefix와 함께 출력되므로 보호된다.
            "[pnpm] 출력:",
            "[pnpm] 오류:",

            // 사용자 코드 using 중복 — Unity 컴파일러가 직접 출력하는 CS0105.
            // 예: "warning CS0105: The using directive for 'AppsInToss' appeared previously in this namespace"
            // Sentry APPS-IN-TOSS-UNITY-SDK-SW.
            // SDK는 컴파일러 경고 메시지를 직접 출력하지 않으므로 안전.
            "warning CS0105",

            // 사용자 MonoBehaviour 라이프사이클 위반 — Unity 엔진이 OnValidate/animation event 등에서
            // 즉시 파괴를 시도할 때 직접 출력. 사용자 게임 코드 호출 흐름.
            // Sentry APPS-IN-TOSS-UNITY-SDK-T0.
            "Destroying GameObjects immediately is not permitted during",

            // Unity IL2CPP / Bee 빌드 시스템이 직접 출력하는 컴파일 단위별 빌드 실패.
            // .o 해시 파일명이 매번 달라 Sentry에서 동일 빌드 실패가 수십 개 별도 이슈로 grouping 됨.
            // 예: "Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/abcdef.o failed with output:"
            //     "Building Library/Bee/artifacts/WebGL/ManagedStripped failed with output:"
            //     "Building Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.js failed with output:"
            // Sentry APPS-IN-TOSS-UNITY-SDK-SA~SV, T7, TV 등 다수.
            // 실 SDK 빌드 실패는 "[AIT] WebGL 빌드가 실패했습니다." (SDK-8E)로 별도 캡처되므로 안전.
            "Building Library/Bee/artifacts/WebGL/",

            // git 호출 trace의 cmd wrapper 변형 — 기존 "Exec> git " 패턴이 잡지 못하는 형태.
            // 예: "Exec> cmd /c \"git\" show -s --pretty=%D HEAD", "Exec> cmd /c \"git\" log -1 ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-TE, APPS-IN-TOSS-UNITY-SDK-TF.
            "Exec> cmd /c \"git\"",

            // Unity Addressables / ScriptableBuildPipeline이 직접 출력하는 빌드 실패 메시지.
            // 사용자 프로젝트의 Addressables 그룹/스키마 설정 문제이며 SDK 영역 아님.
            // 예: "SBP ErrorError" (SDK-H4), "BuildFailedException: Failed to build Addressables content..." (SDK-S4)
            // Addressables는 동일 메시지를 다양한 prefix로 반복 출력하므로 핵심 토큰만 매칭.
            "SBP ErrorError",
            "Failed to build Addressables content",
            // Cannot read BuildLayout의 변형 — "BuildLayout has not open for a file" (SDK-EX)
            "BuildLayout has not open",

            // Unity 오디오 시스템 — 빌드/플레이 중 오디오 장치 전환 발생 시 출력 (SDK-TW)
            "Default audio device was changed",

            // Unity emscripten 압축 단계 직접 출력 — SDK 코드가 띄우는 메시지가 아님 (SDK-RV)
            // 예: "Building webgl/Build/204ccce7cc46e2cd9bd7212e664b4738.data.unityweb failed with output:"
            // 실 SDK 빌드 실패는 "[AIT] WebGL 빌드가 실패했습니다." (SDK-8E)로 별도 캡처되므로 안전.
            "Building webgl/Build/",

            // 사용자 프로젝트가 사용하는 다른 WebGL 템플릿(Fill 등)이 출력하는 진단. SDK 영역 아님.
            // 예: "[WebGL] unity-webview.js source not found: /Users/.../Assets/WebGLTemplates/Fill/TemplateData/unity-webview.js"
            //     "UnityWarning: [WebGL] unity-webview.js source not found: /Users/.../unity-webview.js"
            // Sentry APPS-IN-TOSS-UNITY-SDK-VJ ("UnityWarning: " prefix 변형 포함 — 부분 문자열 매칭).
            // SDK는 "[WebGL]" prefix를 출력하지 않으므로(grep 확인) AitKeywords 보호 가드와 충돌 없음.
            "[WebGL] unity-webview.js source not found",

            // Unity Addressables linker 누락 — 사용자 프로젝트의 Addressables 그룹 설정 문제. SDK 영역 아님.
            // 예: "BuildFailedException: Missing Addressables linker file. ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-QE.
            // SDK는 "Addressables linker" 문자열을 출력하지 않으므로(grep 확인) 안전.
            "Missing Addressables linker",

            // Unity AssetDatabase가 import 중 SaveAssets 호출 시 직접 출력 — 사용자 코드/플러그인이 import 중에 SaveAssets를 호출.
            // 예: "Calls to \"AssetDatabase.SaveAssets\" are restricted during asset importing."
            // Sentry APPS-IN-TOSS-UNITY-SDK-P4.
            // SDK는 이 문자열을 출력하지 않으므로(grep 확인) 안전.
            "\"AssetDatabase.SaveAssets\" are restricted during asset importing",

            // Unity AssetDatabase Refresh 루프 중 발생 — 사용자 프로젝트의 import 충돌. Unity 자체 진단.
            // 예: "The asset at ProjectSettings/ProjectSettings.asset has been scheduled for reimport during the Refresh loop ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-P6.
            // SDK는 이 문자열을 출력하지 않으므로(grep 확인) 안전.
            "scheduled for reimport during the Refresh loop",

            // Unity PackageManager가 immutable 패키지 변경 감지 시 직접 출력 — SDK 자동 업데이트 또는 사용자 변경.
            // 예: "The following asset(s) located in immutable packages were unexpectedly altered. ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-CH.
            // 기존 LogType.Warning 가드는 별도로 유지되며(line 432-436), 이 패턴은 Error/Exception LogType 변형도 흡수.
            // SDK 자체 코드는 이 메시지를 출력하지 않으며(주석으로만 참조) Unity 엔진이 직접 출력.
            "immutable packages were unexpectedly altered",

            // Unity AssetDatabase가 존재하지 않는 검색 폴더로 FindAssets 호출 시 직접 출력하는 엔진 경고.
            // 예: "AssetDatabase.FindAssets: Folder not found: 'Assets/Foo'"
            // SDK 자체 로그 접두사([AIT 등)가 없는 Unity 패키지 탐색 노이즈이며 사용자 프로젝트의
            // 폴더 구성/검색 경로 문제에 해당. SDK는 FindAssets를 호출하긴 하지만(AITBuildOptimizationScanner)
            // 이 경고 문자열을 직접 출력하지 않고 Unity 엔진이 출력하므로 AitKeywords 보호 가드와 충돌 없음.
            // Sentry APPS-IN-TOSS-UNITY-SDK-ZZ.
            "AssetDatabase.FindAssets: Folder not found",

            // 사용자 게임의 이미지 유틸리티가 출력하는 sprite 미할당 경고 — SDK 영역 아님.
            // 예: "[ImageUtil] Icon_1 sprite is null", "[ImageUtil] Body_7 sprite is null", "[ImageUtil] Weapon_3 sprite is null"
            // 에셋명만 가변이고 "[ImageUtil]" prefix가 불변. SDK는 이 prefix를 출력하지 않으며(grep 확인)
            // AitKeywords에도 없어 보호 가드와 충돌 없음.
            // Sentry APPS-IN-TOSS-UNITY-SDK-W1, W2, W3.
            "[ImageUtil]",

            // Unity AssetDatabase가 프로젝트 폴더 밖/절대 경로로 호출될 때 직접 출력하는 엔진 경고 — 사용자 코드의 경로 사용 오류.
            // 예: "Invalid AssetDatabase path: /Scripts/CameraController.cs. Use path relative to the project folder."
            // SDK가 잘못된 경로로 호출하면 경로에 AppsInToss/ait-build 토큰이 들어가 키워드 가드로 보호되므로 안전.
            // Sentry APPS-IN-TOSS-UNITY-SDK-QK.
            "Invalid AssetDatabase path:",

            // play mode 중 빌드/Addressables 트리거 시 Unity 엔진이 직접 출력하는 제약 에러.
            // 현행 SDK는 DoExport/DoExportAsync 진입에서 isPlayingOrWillChangePlaymode 가드로 차단
            // (AITConvertCore.cs, sentryCapture:false)하지만, 구버전 클라이언트 잔여 이벤트 및 Addressables 외
            // 경로의 변형을 backstop으로 흡수한다. Unity 엔진 영문 문구이며 SDK는 이 문자열을 직접 출력하지 않는다.
            // 예: 'Failed to build Addressables content ... "This cannot be used during play mode."'
            //     (SDK-QJ는 기존 "Failed to build Addressables content" 패턴으로도 커버됨)
            // Sentry APPS-IN-TOSS-UNITY-SDK-QJ/QH/QG.
            "This cannot be used during play mode",

            // pnpm/granite 명령 실패 — 현행 SDK는 source에서 sentryCapture:false로 차단하고(AITNpmRunner.cs:335/414)
            // 터미널 단일 캡처는 상위 CaptureBuildError가 담당한다. 구버전(≤2.4.x) 클라이언트가 캡처한 채 보낸
            // 잔여 이벤트를 backstop으로 흡수한다. "[pnpm]"은 AitKeywords에 없어 보호 가드와 충돌 없음.
            // 예: "[pnpm] 명령 실패 (Exit Code: 1): pnpm exec ait build" (SDK-RJ)
            //     "[pnpm] 비동기 명령 실패 (Exit Code: -1): pnpm exec granite build" (SDK-VG/VD/VB)
            "[pnpm] 명령 실패 (Exit Code:",
            "[pnpm] 비동기 명령 실패",

            // Android 키스토어 경로/비밀번호 잘못 설정 시 OpenJDK + sdktools.jar가 직접 출력하는 오류.
            // Unity Android 빌드 시 사용자가 Project Settings > Player > Publishing Settings에
            // 잘못된 키스토어 경로 또는 비밀번호를 입력했을 때 발생. AIT SDK 식별자 없음.
            // 예: "UnityError: Unable to list keys in the keystore. Please make sure the location
            //      and password of the keystore is correct." (APPS-IN-TOSS-UNITY-SDK-118)
            // SDK 코드는 키스토어 경로/비밀번호를 직접 다루지 않으므로 SDK 버그 아님.
            // SDK는 이 문자열을 출력하지 않음(AitKeywords에 없음).
            "Unable to list keys in the keystore",

            // Unity Editor가 Android Gradle/Java 외부 빌드 실패를 출력하는 순수 외부 노이즈.
            // 사용자 프로젝트 Android 빌드 설정 문제(JDK/SDK 경로, Gradle 버전 호환성 등)이며
            // SDK 코드와 무관. 과거 동일 패턴(S5)이 auto로 처리된 선례 있음.
            // 예: "CommandInvokationFailure: Gradle build failed." (APPS-IN-TOSS-UNITY-SDK-134)
            // SDK는 이 문자열을 출력하지 않음(AitKeywords에 없음).
            "CommandInvokationFailure: Gradle build failed",

            // 서드파티 Unity MCP 빌드 도구가 stdout/stderr에 남기는 로그 prefix. SDK 코드베이스
            // 어디에도 "[MCP Build]" 문자열이 없으며(grep 확인), 본문은 개인/사용자 프로젝트의
            // IL2CPP UnityLinker(ManagedStripped) 등 Unity 자체 툴체인 빌드 실패를 그대로 전달한 것.
            // "[Dev Server]"/"[Production Server]"(ServerLogPrefixes)와 동일한 선례 패턴이며,
            // AIT 식별자가 섞이지 않는 한 AitKeywords 보호 가드와 충돌하지 않는다.
            // 예: "UnityError: [MCP Build] ✗ Build failed: WebGL — Building Library/Bee/artifacts/WebGL/ManagedStripped failed with output:"
            // Sentry APPS-IN-TOSS-UNITY-SDK-13E.
            "[MCP Build]",

            // Unity 에디터가 로컬 Android 기기 연결(adb reverse, USB 디버깅) 시 직접 출력하는 표준 경고.
            // AIT SDK 코드는 adb/reverse를 전혀 호출하지 않으며(grep 확인), 사용자 PC의 Android SDK/adb
            // 설정 문제로 발생. "Build target 'WebGL' not supported", "Import Error Code:(" 와 동일한
            // Unity 엔진 자체 노이즈 계열. AitKeywords에 없어 보호 가드와 충돌 없음.
            // 예: "UnityWarning: Connection to Android device failed: Unable to reverse network traffic to
            //      device. Please make sure the Android SDK is installed and is properly configured in the
            //      Editor. See the Console for more details."
            // Sentry APPS-IN-TOSS-UNITY-SDK-13K.
            "Unable to reverse network traffic to device",
        };

        // DetermineErrorSource에서 메시지를 SDK로 분류하는 추가 패턴.
        // 스택트레이스로 출처 판별이 안 될 때, AitKeywords 및 "Sentry:" prefix와 함께 검사됩니다.
        // AitKeywords와 중복되는 키워드는 제외 — drift 방지.
        private static readonly string[] SdkMessagePatterns =
        {
            "[Validation]",
            "[pnpm]",
            "webgl/Build/",
        };

        // 외부(샘플/사용자 게임 코드)에서 AIT prefix를 사용하지만 SDK가 출력하지 않는 메시지.
        // AitKeywords 가드보다 먼저 매칭되어 SDK 보호 가드를 우회하고 노이즈로 분류된다.
        // 새 prefix 추가 시: SDK 코드에서 grep으로 해당 문자열이 출력되지 않음을 반드시 확인.
        // 대상 Sentry 이슈:
        //   - SDK-D2: [AIT Login][src=AIT_MOCK_OR_TIMEOUT] ...
        //   - SDK-D3: [AIT Login] InitSession failed: FORBIDDEN_ORIGIN
        //   - SDK-CF: [Toss Firebase] 게임로그인 실패: ... (사용자 게임 백엔드 통합 레이어)
        //   - SDK-PK/PJ/PF/PC/PB/QA/Q9/Q8/Q7/Q6/Q5/Q4: Assets/FTR_AppsInToss/... CS#### 사용자 코드 경고
        //     (Unity 컴파일러가 사용자 프로젝트 파일 경로 prefix로 출력 — SDK 자체 코드는 Runtime/ 또는 Editor/ 하위)
        //   - SDK-S1/SX: <color=Yellow>AITPromotion</color>: ... (사용자 게임 프로모션 로직 로그)
        //     SDK 어디에도 "AITPromotion" 문자열이 출력되지 않으며 (grep 확인),
        //     AitKeywords의 "[AIT"/"AIT:"와도 매칭되지 않으므로 ExternalAitPrefixes로 안전하게 분류.
        //     "UnityWarning:" prefix가 덧붙은 변형(SDK-S1 재발)도 IndexOf 부분 매칭으로 동일하게 드롭.
        //   - SDK-19D: [AIT Cloud Save] 게임 사용자 식별키를 받지 못했습니다. 로컬 저장으로 계속합니다.
        //     (SDK에 Cloud Save 기능 자체가 없음 — 사용자 게임이 자체 구현한 래퍼의 폴백 로그)
        private static readonly string[] ExternalAitPrefixes =
        {
            "[AIT Login]",
            "[Toss Firebase]",
            "Assets\\FTR_AppsInToss\\",
            "Assets/FTR_AppsInToss/",
            "AITPromotion</color>",
            // SDK-ZV: [AppsInTossIAPManager] IAPGetPendingOrders: null (앱 버전 미지원 등) — 사용자/샘플 IAP wrapper 클래스 로그.
            // SDK는 IAPGetPendingOrders API는 제공하지만 "[AppsInTossIAPManager]" prefix는 출력하지 않으며(grep 확인),
            // "AppsInToss"가 "IAPManager"와 붙어 단어 경계가 깨져 AitKeywords 가드에도 안 걸리므로 ExternalAitPrefixes로 분류.
            "[AppsInTossIAPManager]",
            // SDK-NB: [AIT_Auth] Custom Token 발급 실패 — 로컬 모드로 동작. 사용자 게임의 인증 래퍼 로그.
            // SDK 코드는 "[AIT_Auth]" prefix를 출력하지 않음(grep 확인). 단, "[AIT"로 시작해 AitKeywords 가드에
            // 걸려 NonSdkMessagePatterns로는 드롭 불가하므로, 가드보다 먼저 매칭되는 ExternalAitPrefixes로 분류.
            "[AIT_Auth]",
            // SDK-19D: [AIT Cloud Save] 게임 사용자 식별키를 받지 못했습니다. 로컬 저장으로 계속합니다.
            // SDK에는 Cloud Save 기능/API가 존재하지 않으며(grep 확인 — "Cloud Save", "식별키" 문자열이
            // Runtime/Editor 어디에도 없음), 사용자 게임이 AIT.Storage/GetAnonymousKey 위에 직접 구현한
            // 자체 Cloud Save 래퍼가 SDK와 동일한 "[AIT ...]" 로그 컨벤션을 흉내내 출력한 정상 폴백 로그다.
            // "[AIT"로 시작해 AitKeywords 가드에 걸려 NonSdkMessagePatterns로는 드롭 불가하므로,
            // 가드보다 먼저 매칭되는 ExternalAitPrefixes로 분류한다.
            "[AIT Cloud Save]",
        };

        #endregion

        #region Noise Filter

        /// <summary>
        /// 메시지가 확실히 SDK와 무관한 Unity 내부/사용자 프로젝트 패턴인지 판별합니다.
        /// AIT 키워드(<see cref="AitKeywords"/>)가 포함되면 절대 필터링하지 않습니다.
        /// 단, <see cref="ExternalAitPrefixes"/>는 SDK가 출력하지 않는 외부 코드 prefix로
        /// AitKeywords 가드보다 먼저 매칭되어 노이즈로 드롭됩니다.
        /// </summary>
        internal static bool IsKnownNonSdkMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // 외부 코드가 사용하는 AIT prefix는 SDK 가드를 우회하여 먼저 드롭한다.
            // SDK 코드는 이 prefix를 출력하지 않음이 보장되므로 안전.
            for (int i = 0; i < ExternalAitPrefixes.Length; i++)
            {
                if (message.IndexOf(ExternalAitPrefixes[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            // Sentry 전송 모듈 자체의 실패를 다시 Sentry로 보내면 self-loop이 발생한다.
            // 현재 main에서는 source 단에서 sentryCapture:false로 차단하지만(AITSentryTransport),
            // 이전 SDK 버전이 만든 envelope이 후속 빌드에서 흘러올 수 있어 cascade 필터도 함께 유지.
            // 4xx(인증/페이로드)와 5xx(서비스 장애) 모두 SDK 코드로 분기할 정보가 아니므로 동일 처리.
            // Sentry APPS-IN-TOSS-UNITY-SDK-T4 — [AITSentryTransport] Sentry 전송 실패 (HTTP 503)
            if (message.IndexOf("[AITSentryTransport] Sentry 전송 실패 (HTTP ", StringComparison.Ordinal) >= 0)
                return true;

            // 동기 전송(에디터 종료 시 FlushSync) 실패도 self-loop 위험. 동일 정책으로 차단.
            if (message.IndexOf("[AITSentryTransport] 동기 전송 실패", StringComparison.Ordinal) >= 0)
                return true;

            // AITSentryTransport 자체의 네트워크 오류(ConnectionError) — 사용자 환경 일시 장애.
            // Transport가 스스로의 출력을 다시 Sentry로 보내면 캐스케이드 위험이 있고,
            // 실제로 SubmitResult.Fail로 호출자에게 결과가 전달되므로 가시성도 유지됨.
            // Sentry APPS-IN-TOSS-UNITY-SDK-CZ, APPS-IN-TOSS-UNITY-SDK-KA, APPS-IN-TOSS-UNITY-SDK-RR.
            // UnityWebRequest.error 텍스트(예: "Connection refused", "Unknown Error", "Request timeout",
            // "Unable to read data")가 suffix로 붙는 다양한 변형이 동일 패턴으로 모두 매칭된다.
            if (message.IndexOf("[AITSentryTransport] 네트워크 오류", StringComparison.Ordinal) >= 0)
                return true;

            // AITSentryContextEnricher가 WebGL JS 브리지 API 호출 실패 시 출력하는 경고.
            // "is not a constant handler" 에러는 window.AppsInToss.getXxx 핸들러가 등록되지 않은
            // 환경(sandbox, 구버전 웹뷰 등)에서 발생하는 예상된 플랫폼 미지원 상황이며,
            // CollectSafe가 "unavailable"로 폴백 처리하므로 SDK 동작은 정상이다.
            // 신규 SDK 버전은 source에서 LogWarning → Log로 다운그레이드해 이 경로를 막지만,
            // 이전 버전 클라이언트의 잔여 이벤트를 backstop으로 흡수하기 위한 메시지 필터.
            // "[AITSentry]" prefix가 AitKeywords("[AIT")에 걸려 SDK 보호 가드를 통과하므로
            // 가드보다 먼저 매칭하여 Sentry 캡처를 차단한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-11J.
            if (message.IndexOf("[AITSentry]", StringComparison.Ordinal) >= 0
                && message.IndexOf("is not a constant handler", StringComparison.Ordinal) >= 0)
                return true;

            // 외부 정책 파일 fetch의 일시적 네트워크 오류 — SDK가 외부 호스트에 띄운 메시지이지만
            // SubmitResult/콘솔로 사용자 가시성은 유지되고, 재시도 시 자연 회복되는 케이스.
            // Sentry APPS-IN-TOSS-UNITY-SDK-M9.
            if (message.IndexOf("[AIT] sdk-policy.json fetch 실패", StringComparison.Ordinal) >= 0)
                return true;

            // Vite dev 서버 포트 polling 타임아웃 — 단순 폴링 종료 알림이며 실제로는 곧 브라우저가 열림.
            // SDK 흐름상 fatal하지 않고 사용자에게 안내 후 진행.
            // Sentry APPS-IN-TOSS-UNITY-SDK-QN.
            if (message.IndexOf("[AIT] Vite 포트 5173 대기 타임아웃", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 환경 포트 점유 / 외부 프로세스 비정상 종료 — actionable 가이드는 콘솔에 이미 출력.
            // "AIT: Production 서버 시작 실패"는 Production Server 메뉴 제거 후에도 구버전 SDK가
            // 여전히 방출할 수 있어 레거시로 유지한다 (삭제 금지).
            // Sentry APPS-IN-TOSS-UNITY-SDK-KP, APPS-IN-TOSS-UNITY-SDK-Q3.
            if (message.IndexOf("AIT: Dev 서버 시작 실패", StringComparison.Ordinal) >= 0
                || message.IndexOf("AIT: Production 서버 시작 실패", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 git 환경 문제(Author identity 미설정, git 프로세스 실패 등) — SDK 자동 커밋 보조 흐름.
            // 실패 시 SDK는 계속 진행하며 사용자가 수동 커밋 가능.
            // Sentry APPS-IN-TOSS-UNITY-SDK-SK, APPS-IN-TOSS-UNITY-SDK-TZ.
            if (message.IndexOf("[AIT] 자동 커밋 실패", StringComparison.Ordinal) >= 0)
                return true;

            // git 명령 타임아웃 — 짧은 타임아웃(5초)은 #591에서 source 차단(AITLog sentryCapture:false).
            // 긴 타임아웃(예: 300초 commit) 변형도 동일하게 사용자 환경 응답 지연이므로 차단.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TY, QC.
            if (message.IndexOf("[AIT] Git 명령 타임아웃", StringComparison.Ordinal) >= 0)
                return true;

            // Unity AssetDatabase가 출력하는 GUID 충돌 경고. 사용자가 SDK를 UPM이 아닌
            // Assets/ 하위로 import한 환경에서 동일 자산이 Packages/와 Assets/ 양쪽에 존재하면
            // Unity 엔진이 자동으로 새 GUID를 부여하며 이 경고를 출력한다.
            // 형식:
            //   "GUID [<hash>] for asset '<assetPath>' conflicts with:
            //     '<otherAssetPath>' (current owner)
            //   Assigning a new guid."
            // composite AND 조건으로 다른 GUID 진단 메시지와 거짓양성 충돌을 방지한다.
            // SDK 패키지 경로가 message에 들어가 AitKeywords 가드가 발동되므로 가드보다 먼저 매칭.
            // [AIT] prefix가 붙은 SDK 자체 로그는 절대 필터링하지 않으므로 별도 가드.
            // Sentry APPS-IN-TOSS-UNITY-SDK-BQ.
            if (message.IndexOf("GUID [", StringComparison.Ordinal) >= 0
                && message.IndexOf("] for asset '", StringComparison.Ordinal) >= 0
                && message.IndexOf("' conflicts with:", StringComparison.Ordinal) >= 0
                && !message.StartsWith("[AIT]", StringComparison.Ordinal))
                return true;

            // 사용자가 직접 WebGL 빌드를 취소한 정보성 경고 — SDK의 의도된 동작 경로.
            // 신규 SDK 버전은 #591에서 origin(AITLog sentryCapture:false)으로 차단되지만,
            // 이전 버전 사용자 빌드에서는 여전히 Sentry로 도달하므로 message-filter로도 흡수.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TX.
            if (message.IndexOf("사용자에 의해 WebGL 빌드가 취소", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 프로젝트(Assets/) 하위 .cs 파일의 CS0029 암묵 변환 컴파일 에러 (SDK-T2).
            // Unity 컴파일러가 사용자 코드의 타입 변환 실패를 보고할 때 출력하는 포맷:
            //   "Assets/.../Foo.cs(L,C): error CS0029: Cannot implicitly convert type 'X' to 'Y'"
            // 메시지에 SDK 타입명(예: 'AppsInToss.IapProductListItem')이 들어가 AitKeywords 가드가
            // 발동되므로, SDK 가드보다 먼저 매칭해 노이즈로 드롭한다.
            // SDK 자체 코드의 컴파일 에러는 'Packages/com.toss.apps-in-toss/...' 또는
            // 'Library/PackageCache/...' 경로로 출력되어 'Assets/' prefix가 붙지 않으므로 안전.
            if (message.IndexOf("error CS0029", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 식별자 미발견 컴파일 에러 (CS0103) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\AppsInTossCompatibilityChecker.cs(31,9): error CS0103:
            //       The name 'CheckIncompatibleComponents' does not exist in the current context"
            // Sentry APPS-IN-TOSS-UNITY-SDK-CR, APPS-IN-TOSS-UNITY-SDK-MN.
            // 사용자 식별자가 가변이라 일반화된 형태로 좁힌다(Assets/ 경로 + .cs(L,C) 패턴).
            // SDK 자체 코드는 Packages/ 또는 Library/PackageCache/ 경로로 출력되어 안전.
            if (message.IndexOf("error CS0103", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 멤버 미정의 컴파일 에러 (CS0117) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\98_Tools\BuildTool\Editor\BuildToolEditorWindow.cs(484,43): error CS0117:
            //       'AppsInTossMenu' does not contain a definition for 'Package'"
            // Sentry APPS-IN-TOSS-UNITY-SDK-80.
            // SDK 타입명을 잘못 참조한 경우라도 사용자 코드가 잘못 사용한 것이며 SDK 분기로 해결 불가.
            if (message.IndexOf("error CS0117", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 멤버/확장 메서드 미정의 컴파일 에러 (CS1061) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\AppsInToss\AppsInTossRemoteConfig.cs(21,61): error CS1061:
            //       'GameServerConfig' does not contain a definition for 'gameSettingsId' and no
            //       accessible extension method 'gameSettingsId' accepting a first argument of
            //       type 'GameServerConfig' could be found ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-13G.
            // 'GameServerConfig'는 SDK 타입이 아닌 사용자 정의 클래스이며, 파일 경로가 사용자 폴더명
            // "AppsInToss\AppsInTossRemoteConfig.cs"를 포함해 SDK 키워드 가드가 발동하므로 CS0117과
            // 동일하게 가드보다 먼저 매칭한다(Assets/ 경로 + .cs(L,C) 패턴). SDK 자체 코드는 Packages/
            // 또는 Library/PackageCache/ 경로로 출력되어 안전.
            if (message.IndexOf("error CS1061", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 async/await 미사용 경고 (CS1998) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\1. System\AppsInToss\TossManager.cs(260,43): warning CS1998:
            //       This async method lacks 'await' operators and will run synchronously. ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-Z6.
            // 사용자 폴더명에 'AppsInToss'가 포함돼 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다(Assets/ 경로 + .cs(L,C)).
            // SDK 자체 코드는 Packages/ 또는 Library/PackageCache/ 경로로 출력되어 Assets/ 가드와 충돌 없음.
            if (message.IndexOf("warning CS1998", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 obsolete API 사용 경고 (CS0618) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Editor\AppsInTossWebGLProjectSetup.cs(64,9): warning CS0618:
            //       'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: ..."
            //     "...(65,9): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ...)' is obsolete: ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-WN, APPS-IN-TOSS-UNITY-SDK-WM.
            // 현행 SDK 자체는 동일 API를 #if UNITY_6000_0_OR_NEWER로 버전 분기해 obsolete 경고를 내지 않는다
            // (AITBuildSession.cs:137-145, AITBuildInitializer.cs). 사용자 프로젝트 파일(Assets/)의 obsolete 사용만 드롭한다.
            // 파일명에 'AppsInToss'가 단어 경계 없이 붙어(AppsInTossWebGLProjectSetup) 키워드 가드를 우회하므로
            // 가드보다 먼저 Assets/ 경로 + .cs(L,C) 합성으로 좁혀 매칭한다. SDK 패키지(Packages/) 경로의 경고는
            // Assets/ 가드에 걸리지 않아 키워드 가드로 보호된다.
            if (message.IndexOf("warning CS0618", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 사용되지 않은 필드 경고 (CS0414) — Unity 컴파일러가 직접 출력.
            // 예: "UnityWarning: Assets\01_Script\AppsInToss\AppsInTossPromotionManager.cs(28,35): warning CS0414:
            //       The field 'AppsInTossPromotionManager.persistEditorMockGrantState' is assigned but its value is never used"
            // Sentry APPS-IN-TOSS-UNITY-SDK-140.
            // 위 NonSdkMessagePatterns에 "warning CS0414" substring이 이미 있으나, 사용자 폴더명이
            // 'AppsInToss'(예: Assets\01_Script\AppsInToss\...)이면 단어 경계로 SDK 키워드 가드가 먼저
            // 발동해 NonSdkMessagePatterns 루프(가드 이후)까지 도달하지 못하는 갭이 있었다.
            // CS0103/CS0117/CS1061/CS1998/CS0618과 동일한 컨벤션으로 Assets/ 경로 + .cs(L,C) 마커를
            // 합성 AND로 요구해 가드보다 먼저 매칭한다. SDK 자체 코드는 Packages/ 또는 Library/PackageCache/
            // 경로로 출력되어 안전.
            if (message.IndexOf("warning CS0414", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 'AppsInToss' 미발견 컴파일 에러 — SDK 미설치 또는 asmdef 참조 누락.
            // 예: "Assets/.../Foo.cs(L,C): error CS0246: The type or namespace name 'AppsInToss' could not be found ..."
            // 메시지에 'AppsInToss' 토큰이 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-C3, APPS-IN-TOSS-UNITY-SDK-M7 등.
            // CS0246 단독은 SDK 빌드 메시지와 충돌 위험이 있어 "'AppsInToss'"와 합성 AND + Assets/ 경로 가드로 좁힌다.
            if (message.IndexOf("error CS0246", StringComparison.Ordinal) >= 0
                && message.IndexOf("'AppsInToss'", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 using 중복(CS0105) — Unity 컴파일러가 직접 출력.
            // 예: "Assets/.../Foo.cs(L,C): warning CS0105: The using directive for 'AppsInToss' appeared previously in this namespace"
            // 예(2): "Assets\01.Scripts\Platform\AppsInToss\TossPlatformServices.cs(2,7): warning CS0105:
            //         The using directive for 'System' appeared previously in this namespace"
            // Sentry APPS-IN-TOSS-UNITY-SDK-SW, APPS-IN-TOSS-UNITY-SDK-1B1.
            // 원래는 중복 대상을 'AppsInToss'로만 한정했으나, 사용자 폴더명에 'AppsInToss' 세그먼트가
            // 포함된 파일(Platform\AppsInToss\...)에서 'System' 등 다른 using이 중복되는 경우
            // AitKeywords 보호 가드에 걸려 드롭되지 못하는 공백이 있었다. 중복 대상 식별자는 임의이므로
            // 컴파일러 고정 문구("The using directive for '" ~ "appeared previously in this namespace")로
            // 일반화하고, Assets/ 경로 + .cs(L,C) 마커 합성 AND는 기존 컨벤션대로 유지한다.
            if (message.IndexOf("warning CS0105", StringComparison.Ordinal) >= 0
                && message.IndexOf("The using directive for '", StringComparison.Ordinal) >= 0
                && message.IndexOf("appeared previously in this namespace", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 SDK 타입 인자 오용(CS1503) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\Manager\TossManager.cs(192,91): error CS1503: Argument 1: cannot convert from 'AppsInToss.GetUserKeyForGameResult' to 'string'"
            // 예(List 제네릭 변형): "Assets/.../RemoteShopInfo.cs(312,68): error CS1503: Argument 1: cannot convert from 'System.Collections.Generic.List<Studio.Common.Trident.Billing.ProductInfo>' to 'System.Collections.Generic.List<AppsInToss.IapProductListItem>'"
            // Sentry APPS-IN-TOSS-UNITY-SDK-VM/PV/PW/DA/WW/WV.
            // 메시지에 'AppsInToss.*' 타입명이 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // namespace prefix는 작은따옴표 직후('AppsInToss.) 또는 List<> 등 제네릭 인자 직후(<AppsInToss.) 두 형태를 모두 허용.
            // 두 변형 모두 점(.)을 포함해 단독 토큰 'AppsInToss'만 있는 SDK 빌드 메시지와는 충돌 없음.
            if (message.IndexOf("error CS1503", StringComparison.Ordinal) >= 0
                && (message.IndexOf("'AppsInToss.", StringComparison.Ordinal) >= 0
                    || message.IndexOf("<AppsInToss.", StringComparison.Ordinal) >= 0)
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 컴파일 에러가 SDK 식별자(AppsInToss...)를 참조하는 경우 — Unity C# 컴파일러가 직접 출력.
            // 위 CS0103/CS0246/CS1503 가드는 "Assets/" 경로 + ".cs(" 마커가 포함된 풀 라인을 요구하지만,
            // Sentry로는 파일 경로 prefix 없이 컴파일러 진단 본문만 도달하는 변형이 있어(아래 이슈) 별도로 흡수한다.
            // 진단 문구("does not exist in the current context" / "type or namespace name ... could not be found" /
            // "cannot convert from")는 C# 컴파일러만 출력하며 SDK 런타임/빌드 코드는 이 문구를 직접 출력하지 않는다.
            // SDK 자체 로그("[AIT" prefix)와 SDK 패키지 경로("com.toss.apps-in-toss")의 컴파일 에러는 방어적으로 제외해
            // 실제 SDK 버그가 묻히지 않도록 한다. 'AppsInToss' 토큰을 동반할 때만 매칭해 SDK와 무관한 컴파일 에러는 통과시킨다.

            // CS0103 변형 — "The name 'AppsInToss...' does not exist in the current context" (SDK-ZR: DreamPassDefinition.cs)
            if (message.IndexOf("does not exist in the current context", StringComparison.Ordinal) >= 0
                && message.IndexOf("AppsInToss", StringComparison.Ordinal) >= 0
                && !message.StartsWith("[AIT", StringComparison.Ordinal)
                && message.IndexOf("com.toss.apps-in-toss", StringComparison.Ordinal) < 0)
                return true;

            // CS0246 변형 — "The type or namespace name 'AppsInToss' could not be found" (SDK-103/104/105: SDK 패키지 미설치)
            if (message.IndexOf("type or namespace name", StringComparison.Ordinal) >= 0
                && message.IndexOf("could not be found", StringComparison.Ordinal) >= 0
                && message.IndexOf("AppsInToss", StringComparison.Ordinal) >= 0
                && !message.StartsWith("[AIT", StringComparison.Ordinal)
                && message.IndexOf("com.toss.apps-in-toss", StringComparison.Ordinal) < 0)
                return true;

            // CS1503 변형 — "cannot convert from 'AppsInToss.AITException' to 'string'" (SDK-102: TossAdManager.cs)
            if (message.IndexOf("cannot convert from", StringComparison.Ordinal) >= 0
                && message.IndexOf("AppsInToss", StringComparison.Ordinal) >= 0
                && !message.StartsWith("[AIT", StringComparison.Ordinal)
                && message.IndexOf("com.toss.apps-in-toss", StringComparison.Ordinal) < 0)
                return true;

            // Unity AssetDatabase가 Library/ 경로의 SDK 외부 캐시 로딩 실패 시 직접 출력.
            // 예: "Unknown error occurred while loading 'Library/AppsInToss/AITBuildSession.asset'."
            // 메시지에 'AppsInToss'/'AIT' 토큰이 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-RT.
            if (message.IndexOf("Unknown error occurred while loading 'Library/", StringComparison.Ordinal) >= 0)
                return true;

            // AITAsyncCommandRunner의 Windows powershell 실행 실패 — 사용자 환경(PATH/실행 정책) 원인.
            // 예: "[AIT Async] 명령 실행 예외: System.ComponentModel.Win32Exception (0x80004005):
            //       ApplicationName='powershell.exe', CommandLine='-ExecutionPolicy Bypass ...'"
            // [AIT Async] prefix가 SDK 키워드 가드("[AIT")에 막히므로 가드보다 먼저 매칭한다.
            // 사용자 환경 문제이며 SDK 코드 분기로 해결 불가. 합성 AND로 일반 [AIT Async] 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-VE, APPS-IN-TOSS-UNITY-SDK-VC.
            if (message.IndexOf("[AIT Async] 명령 실행 예외", StringComparison.Ordinal) >= 0
                && message.IndexOf("Win32Exception", StringComparison.Ordinal) >= 0
                && message.IndexOf("ApplicationName='powershell.exe'", StringComparison.Ordinal) >= 0)
                return true;

            // AppsInTossMenu의 'ait deploy' 실패 경로가 redirect한 stdout/stderr 본문 중
            // pnpm/npm progress bar의 ANSI escape 시퀀스(\x1b[?25l, \x1b[?25h 등 커서 hide/show)만 들어간 라인.
            // 예: "AIT: [stdout] \x1b[?25l│", "AIT: [stdout] \x1b[?25h"
            // 신규 SDK는 deploy 경로를 sentryCapture: false로 차단(D10a)했지만, 구버전 SDK 사용자는 여전히
            // 동일 메시지를 다수 fingerprint(VK/BD/T5)로 전송한다. 컨텐츠 기반 backstop.
            // 'AIT: [stdout]'/'AIT: [stderr]' prefix는 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭.
            // ANSI escape "[?25" + "AIT: [std" 합성으로 좁혀 일반 stdout/stderr 진단 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-VK/BD/T5.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("[?25", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자가 WebGL 빌드를 직접 취소한 정상 액션 — 에러가 아닌 의도된 사용자 동작 노이즈.
            // 예: "[AIT] 사용자에 의해 WebGL 빌드가 취소되었습니다." (AITConvertCore.cs, AITLog.Warning)
            //     "[AIT] 빌드가 취소되었습니다." (AITConvertCore.cs, Debug.LogWarning 변형)
            // 신규 SDK는 취소 경로를 sentryCapture: false로 차단했지만, 구버전 SDK 사용자는 여전히
            // 동일 Warning을 전송한다. 컨텐츠 기반 backstop.
            // "[AIT]" prefix가 SDK 키워드 가드("[AIT")에 막히므로 가드보다 먼저 매칭한다.
            // "[AIT" + "빌드가 취소되었습니다" 합성으로 좁혀 일반 빌드 실패 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TX.
            if (message.IndexOf("[AIT", StringComparison.Ordinal) >= 0
                && message.IndexOf("빌드가 취소되었습니다", StringComparison.Ordinal) >= 0)
                return true;

            // Unity Package Manager가 사용자 환경(네트워크/Git 인증/SSL 등) 문제로 Git 패키지 추가/제거 실패 시 직접 출력.
            // 예: "[Package Manager Window] Error adding/removing packages: https://github.com/toss/apps-in-toss-unity-sdk.git #release/v2.4.3."
            //     "[Package Manager Window] Error adding package: im.toss.apps-in-toss-unity-sdk@https://...#2.9.0"
            // URL/패키지 ID에 'apps-in-toss' 토큰이 단어 경계로 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // "[Package Manager Window]" + ("Error adding/removing packages" OR "Error adding package:") 합성으로 일반 PM 메시지와 충돌 방지.
            // 단, "[AIT" prefix로 시작하는 SDK 자체 로그는 절대 필터링하지 않으므로 별도 가드(GUID/meta 패턴과 동일 컨벤션).
            // Sentry APPS-IN-TOSS-UNITY-SDK-QQ, APPS-IN-TOSS-UNITY-SDK-7Y, APPS-IN-TOSS-UNITY-SDK-12Q.
            // SDK 자체 로그("[AIT" prefix)는 보호한다 — SDK는 Unity PM 창 출력을 "[AIT]"로 감싸지 않으므로
            // "[AIT" 로 시작하는 메시지는 이 외부 노이즈 패턴의 대상이 아니다. 이 선행 가드가 없으면
            // "[AIT] [Package Manager Window] Error adding package: ..." 같은 SDK 로그가 AitKeywords 가드보다
            // 먼저 드롭된다(#886 package-manager-noise 회귀). 회귀 테스트: ..._WithAitPrefix_NeverFiltered.
            if (!message.StartsWith("[AIT", StringComparison.Ordinal)
                && message.IndexOf("[Package Manager Window]", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Error adding/removing packages", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Error adding package:", StringComparison.Ordinal) >= 0)
                && !message.StartsWith("[AIT", StringComparison.Ordinal))
                return true;

            // Unity Package Manager가 의존성 해석 실패 시 직접 출력 — 사용자 환경 manifest.json 충돌 또는 네트워크 장애.
            // 예: "An error occurred while resolving packages:\n  Project has invalid dependencies: com.example.foo"
            // 본문에 SDK 패키지 경로(예: com.toss.apps-in-toss)가 들어가면 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // "An error occurred while resolving packages" + "Project has invalid dependencies" 합성으로 좁힌다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-V1.
            if (message.IndexOf("An error occurred while resolving packages", StringComparison.Ordinal) >= 0
                && message.IndexOf("Project has invalid dependencies", StringComparison.Ordinal) >= 0)
                return true;

            // AppsInTossMenu deploy 경로의 pnpm/ait CLI가 잘못된 인자로 호출됐을 때 redirect한 syntax error.
            // 예: "AIT: [stdout] Unknown Syntax Error: Not enough arguments to option --api-key."
            // "AIT: [stdout]" prefix가 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭한다.
            // "AIT: [std" + "Unknown Syntax Error: Not enough arguments" 합성으로 일반 stdout과 충돌 방지.
            // 사용자가 deployment key를 입력하지 않은 환경 문제 — SDK 분기로 해결 불가.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TS.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("Unknown Syntax Error: Not enough arguments", StringComparison.Ordinal) >= 0)
                return true;

            // Windows shell이 인식 못하는 명령어 호출 시 redirect한 stderr — 사용자 환경 PATH 누락 또는 인코딩 깨짐.
            // 예: "AIT: [stderr] '<corrupted-bytes>' is not recognized as an internal or external command,"
            // "AIT: [stderr]" prefix가 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭한다.
            // "AIT: [std" + "is not recognized as an internal or external command" 합성으로 좁힌다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-RG.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("is not recognized as an internal or external command", StringComparison.Ordinal) >= 0)
                return true;

            // 번들 Node.js(libuv)가 종료 시점에 출력하는 내부 assertion crash — SDK 코드로 분기/조치할 정보가 아님.
            // 예: "AIT: [stderr] Assertion failed: !(handle->flags & UV_HANDLE_CLOSING), file src\\win\\async.c, line 76"
            // "AIT: [std" prefix가 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-BE.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("Assertion failed:", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 게임의 FPS 모니터가 SDK의 "[AIT]" prefix를 그대로 사용해 출력하는 성능 경고.
            // SDK 코드는 "평균 FPS"/"목표 30+ 미달" 문자열을 출력하지 않음(grep 확인). 사용자 코드가 SDK prefix를
            // 흉내내 AitKeywords 가드("[AIT")를 우회하므로, 가드보다 먼저 두 핵심 문구의 합성으로 좁혀 드롭한다.
            // (FPS 수치는 가변이므로 불변 문구 "평균 FPS" + "미달"만 검사 — 정상 SDK 경고는 이 조합을 출력하지 않음.)
            // Sentry APPS-IN-TOSS-UNITY-SDK-WK.
            if (message.IndexOf("평균 FPS", StringComparison.Ordinal) >= 0
                && message.IndexOf("미달", StringComparison.Ordinal) >= 0)
                return true;

            // AITSentryContextEnricher의 CollectSafe가 플랫폼 API 수집 실패 시 출력하는 경고.
            // 예: "[AITSentry] GetTossAppVersion 호출 실패: Cannot read properties of undefined (reading 'getTossAppVersion')"
            //     "[AITSentry] GetDeviceId 호출 실패: ..."
            // WebGL 브리지(window.AppsInToss)가 초기화되기 전에 SDK가 먼저 호출되는 타이밍 조건이거나,
            // 에디터 PlayMode에서 WebGL jslib이 없어 발생하는 정상 실패 경로다.
            // CollectSafe는 예외를 잡아 "unavailable"를 반환하고 계속 진행하므로 SDK 동작은 중단되지 않는다.
            // Sentry로 전송하면 조치 불가한 노이즈가 되고 transport 자기참조 위험도 있으므로 차단한다.
            // "[AITSentry]" prefix는 AitKeywords("[AIT")에 막히므로 반드시 가드보다 먼저 매칭해야 한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-11G.
            if (message.IndexOf("[AITSentry]", StringComparison.Ordinal) >= 0
                && message.IndexOf("호출 실패:", StringComparison.Ordinal) >= 0)
                return true;

            // SDK 자체 immutable 패키지 폴더(im.toss.apps-in-toss-unity-sdk)의 파일이 .meta 없이 배포되어
            // Unity 에디터가 직접 출력하는 표준 경고. 브랜치 핀(dev 트리) 설치 시 CLAUDE.md/docs 등
            // .meta 미동반 파일에서 발생하며 "The asset will be ignored"로 기능 영향이 없는 노이즈다.
            // 예: "UnityError: Asset Packages/im.toss.apps-in-toss-unity-sdk/Runtime/SDK/Plugins/CLAUDE.md
            //      has no meta file, but it's in an immutable folder. The asset will be ignored."
            // 메시지에 SDK 패키지 경로(apps-in-toss)가 포함돼 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // 외부 서드파티 패키지 변형(apps-in-toss 미포함)은 아래 NonSdkMessagePatterns 루프가 그대로 처리하므로
            // 여기서 apps-in-toss 동반 조건으로 좁혀 외부 경로와 역할을 분리한다(중복 매칭 방지).
            // "[AIT" prefix가 붙은 SDK 자체 로그는 절대 필터링하지 않으므로 별도 가드.
            // Sentry APPS-IN-TOSS-UNITY-SDK-12K.
            if (message.IndexOf("has no meta file, but it's in an immutable folder", StringComparison.Ordinal) >= 0
                && message.IndexOf("apps-in-toss", StringComparison.Ordinal) >= 0
                && !message.StartsWith("[AIT", StringComparison.Ordinal))
                return true;

            // 사용자 코드의 미사용 이벤트 선언 경고 (CS0067) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\MyGame\Scripts\AppsInToss\AppsInTossStorageManager.Ranking.cs(89,31):
            //       warning CS0067: The event 'AppsInTossStorageManager.RankingResultStateChanged' is never used"
            // Sentry APPS-IN-TOSS-UNITY-SDK-1A3.
            // 사용자 클래스명(AppsInTossStorageManager)에 'AppsInToss' 토큰이 포함돼 SDK 키워드 가드가
            // 발동하므로 CS1998/CS0618과 동일하게 가드보다 먼저 매칭한다(Assets/ 경로 + .cs(L,C) 패턴).
            // SDK 자체 코드는 Packages/ 또는 Library/PackageCache/ 경로로 출력되어 Assets/ 가드와 충돌 없음.
            if (message.IndexOf("warning CS0067", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // pnpm이 content-addressable store에서 만드는 중첩 가상 스토어 경로
            // (node_modules/.pnpm/<pkg>@<ver>_<hash>/node_modules/...)를 Unity 에디터가 자체적으로
            // 삭제 시도하다 경로 검증에 실패해 출력하는 표준 경고.
            // 예: "Cannot delete asset. ait-build/node_modules/.pnpm/@granite-js+cli@1.0.4_.../node_modules/
            //      clipanion/lib/advanced/index.mjs is not a valid path."
            // ait-build는 Assets/ 밖(프로젝트 루트, UnityUtil.GetProjectPath() 참고)에 위치하고
            // SDK 코드는 이 경로를 AssetDatabase API로 다루지 않는다(grep 확인, PrepareAitBuildFolder는
            // node_modules를 보존 대상으로 두고 File/Directory API로만 정리) — Unity 자체 내부 동작에서
            // 비롯된 외부 노이즈로 판단. 메시지에 "ait-build"가 포함돼 SDK 키워드 가드가 발동하므로
            // 가드보다 먼저 매칭한다. ".pnpm" 세그먼트로 특정 패키지명/해시와 무관하게 일반화.
            // Sentry APPS-IN-TOSS-UNITY-SDK-1A9.
            if (message.IndexOf("Cannot delete asset", StringComparison.Ordinal) >= 0
                && message.IndexOf("is not a valid path", StringComparison.Ordinal) >= 0
                && message.IndexOf(".pnpm", StringComparison.Ordinal) >= 0)
                return true;

            // SDK 자체 로그는 절대 필터링하지 않음 — AitKeywords 전체를 가드로 사용
            if (MessageContainsSdkKeyword(message))
                return false;

            for (int i = 0; i < NonSdkMessagePatterns.Length; i++)
            {
                if (message.IndexOf(NonSdkMessagePatterns[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            // "Script attached to ... is missing" 패턴은 Assets/ 경로가 포함된 경우에만 사용자 프로젝트로 분류.
            // "in Assets/..." 직접 경로 형태(SDK-H1/GY), "in scene 'Assets/...'" 변형(SDK-H0/GX/GW) 모두 매칭한다.
            if (message.IndexOf("Script attached to", StringComparison.Ordinal) >= 0
                && message.IndexOf("is missing", StringComparison.Ordinal) >= 0
                && message.IndexOf("Assets/", StringComparison.Ordinal) >= 0)
                return true;

            return false;
        }

        #endregion
    }
}
