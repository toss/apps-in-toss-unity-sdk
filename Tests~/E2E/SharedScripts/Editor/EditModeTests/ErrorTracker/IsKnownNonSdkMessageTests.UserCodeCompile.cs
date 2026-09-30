// IsKnownNonSdkMessageTests.UserCodeCompile.cs - 사용자 코드 CS#### 컴파일 에러·경고
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

public partial class IsKnownNonSdkMessageTests
{
    #region 사용자 코드 CS0029 암묵 변환 컴파일 에러 (SDK-T2)

    [Test]
    public void UserCodeCS0029_IapProductListItem_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T2 실측 메시지 — 사용자 IAP 매니저 코드가 SDK 반환 배열을
        // List<T>에 암묵 변환하려다 발생한 컴파일 에러. SDK 타입명('AppsInToss.IapProductListItem')이
        // 포함되어 AitKeywords 가드를 발동시키지만, composite AND 가드가 그보다 먼저 매칭되어 노이즈로 드롭됨.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/02_Scripts/02_Manager/IAP/IAPServiceToss.cs(24,29): error CS0029: Cannot implicitly convert type 'AppsInToss.IapProductListItem[]' to 'System.Collections.Generic.List<AppsInToss.IapProductListItem>'"));
    }

    [Test]
    public void UserCodeCS0029_OtherUserPath_ReturnsTrue()
    {
        // CS0029 + Assets/ + .cs( 조합은 SDK 타입 참조 여부와 무관하게 사용자 코드 컴파일 에러로 분류.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Scripts/GameLogic.cs(42,10): error CS0029: Cannot implicitly convert type 'int' to 'string'"));
    }

    [Test]
    public void Cs0029WithoutAssetsPrefix_AitProtected_ReturnsFalse()
    {
        // 'Assets/' 경로가 없으면 composite AND 가드의 한 조건이 빠지므로 매칭되지 않고,
        // AitKeywords 가드가 정상 동작해 SDK 자체 로그로 간주됨 (필터링 안 함).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] error CS0029 reported from SDK build pipeline"));
    }

    [Test]
    public void SdkPackageCS0029_ReturnsFalse()
    {
        // SDK 자체 .cs 파일의 컴파일 에러는 'Packages/com.toss.apps-in-toss/...' 경로로 출력되어
        // 'Assets/' prefix가 붙지 않으므로 composite AND 가드가 매칭되지 않아야 함 (SDK 보호).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Runtime/Foo.cs(10,5): error CS0029: Cannot implicitly convert type 'int' to 'string'"));
    }

    [Test]
    public void OtherCSErrorInUserAssets_ReturnsFalse()
    {
        // 가드가 등록되지 않은 다른 CS 에러 코드(예: CS0535)는 composite 가드가 매칭되지 않아 통과해야 함.
        // 본 SDK는 CS0029/CS0103/CS0105/CS0117/CS0246/CS1503/CS1061 한정으로 가드.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Scripts/GameLogic.cs(42,10): error CS0535: 'X' does not implement interface member 'Y'"));
    }

    #endregion

    #region 사용자 코드 CS0103/CS0117/CS1061 컴파일 에러 (SDK-CR, SDK-MN, SDK-80, SDK-13G)

    [Test]
    public void UserCodeCS0103_AssetsBackslashPath_ReturnsTrue()
    {
        // Sentry SDK-CR — Windows 경로 + 사용자 식별자 미발견.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Scripts\\AppsInTossCompatibilityChecker.cs(31,9): error CS0103: The name 'CheckIncompatibleComponents' does not exist in the current context"));
    }

    [Test]
    public void UserCodeCS0103_AssetsForwardSlashPath_ReturnsTrue()
    {
        // Sentry SDK-MN POSIX 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/AppsInToss/01_Script/CreateObstacle.cs(22,54): error CS0103: The name 'touchPos' does not exist in the current context"));
    }

    [Test]
    public void UserCodeCS0117_AppsInTossMenu_ReturnsTrue()
    {
        // Sentry SDK-80 — 사용자 BuildTool 코드가 SDK 타입의 존재하지 않는 멤버 참조.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\98_Tools\\BuildTool\\Editor\\BuildToolEditorWindow.cs(484,43): error CS0117: 'AppsInTossMenu' does not contain a definition for 'Package'"));
    }

    [Test]
    public void UserCodeCS1061_GameServerConfigMissingMember_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-13G — 사용자 정의 클래스 'GameServerConfig'(SDK 타입 아님)의
        // 존재하지 않는 멤버/확장 메서드 참조. 파일 경로에 "AppsInToss" 폴더명이 포함돼 SDK 키워드
        // 가드가 발동하므로 CS0117과 동일하게 가드보다 먼저 매칭되어야 한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Assets\\Scripts\\AppsInToss\\AppsInTossRemoteConfig.cs(21,61): error CS1061: 'GameServerConfig' does not contain a definition for 'gameSettingsId' and no accessible extension method 'gameSettingsId' accepting a first argument of type 'GameServerConfig' could be found"));
    }

    [Test]
    public void UserCodeCS1061_ForwardSlashPath_ReturnsTrue()
    {
        // POSIX 경로 변형도 동일 composite 가드(error CS1061 + Assets/ + .cs()로 매칭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Scripts/Manager/InventoryManager.cs(58,20): error CS1061: 'ItemData' does not contain a definition for 'stackSize'"));
    }

    [Test]
    public void Cs1061_SdkPackagePath_NotFiltered()
    {
        // SDK 자체 .cs의 CS1061은 Packages/com.toss.apps-in-toss 경로로 출력되어 Assets/ prefix 미포함 → 보호됨.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): error CS1061: 'Bar' does not contain a definition for 'baz'"));
    }

    [Test]
    public void Cs1061WithoutAssetsPrefix_AitProtected_ReturnsFalse()
    {
        // SDK 자체 진단 라인(Assets/ 경로 미포함)은 [AIT] prefix로 보호됨.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS1061 reported in SDK fallback path"));
    }

    [Test]
    public void Cs0103WithoutAssetsPrefix_AitProtected_ReturnsFalse()
    {
        // SDK 자체 진단 라인(Assets/ 경로 미포함)은 [AIT] prefix로 보호됨.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS0103 reported in SDK fallback path"));
    }

    [Test]
    public void Cs0103_SdkPackagePath_NotFiltered()
    {
        // SDK 자체 .cs의 CS0103은 Packages/com.toss.apps-in-toss 경로로 출력되어 Assets/ prefix 미포함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): error CS0103: The name 'bar' does not exist in the current context"));
    }

    [Test]
    public void Cs0117_SdkPackagePath_NotFiltered()
    {
        // CS0117도 동일 — SDK 자체 코드 보호.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): error CS0117: 'Bar' does not contain a definition for 'baz'"));
    }

    #endregion

    #region 사용자 코드 CS0414 미사용 필드 경고 — AppsInToss 폴더명 경로 가드 갭 (APPS-IN-TOSS-UNITY-SDK-140)

    [Test]
    public void UserCodeCS0414_AppsInTossFolderPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-140 — 사용자 폴더명이 'AppsInToss'라 기존 NonSdkMessagePatterns의
        // "warning CS0414" substring이 SDK 키워드 가드(MessageContainsSdkKeyword)에 먼저 막혀 도달하지
        // 못했던 갭. CS0103/CS0117/CS1061/CS1998/CS0618과 동일한 composite AND 가드로 매칭돼야 한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Assets\\01_Script\\AppsInToss\\AppsInTossPromotionManager.cs(28,35): warning CS0414: The field 'AppsInTossPromotionManager.persistEditorMockGrantState' is assigned but its value is never used"));
    }

    [Test]
    public void UserCodeCS0414_ForwardSlashPath_ReturnsTrue()
    {
        // POSIX 경로 변형 — AppsInToss 폴더명이 없어도 Assets/ + .cs(L,C) 합성 가드로 동일하게 매칭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Scripts/PlayerController.cs(12,17): warning CS0414: The field 'PlayerController.unusedFlag' is assigned but its value is never used"));
    }

    [Test]
    public void Cs0414_SdkPackagePath_NeverFiltered()
    {
        // SDK 자체 .cs의 CS0414는 Packages/com.toss.apps-in-toss 경로로 출력 → Assets/ 가드 미충족 +
        // "apps-in-toss" 키워드 가드로 보호되어 절대 드롭되지 않아야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): warning CS0414: The field 'Foo.bar' is assigned but its value is never used"));
    }

    [Test]
    public void Cs0414_NoAssetsPath_StillFilteredByLegacyPattern()
    {
        // Assets/ 경로도 .cs(L,C)도 없어 새 composite 가드는 매칭되지 않지만, 기존 NonSdkMessagePatterns의
        // "warning CS0414" substring(가드 회귀 방지)이 여전히 드롭한다 — 기존 동작 불변 확인.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SomeLib.dll: warning CS0414: 'Z' is assigned but its value is never used"));
    }

    [Test]
    public void Cs0414WithoutAssetsPrefix_AitProtected_ReturnsFalse()
    {
        // SDK 자체 진단 라인(Assets/ 경로 미포함)은 [AIT] prefix로 보호됨 — CS0103/CS1061과 동일 컨벤션.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: warning CS0414 reported in SDK fallback path"));
    }

    [Test]
    public void UserCodeCS0414_StorageManagerAppsInTossPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-15T — 사용자 스크립트 AppsInTossStorageManager.cs의 CS0414 경고.
        // 위 140 composite AND 가드는 파일명을 특정하지 않고 warning CS0414 + Assets 경로 + .cs(L,C)로
        // 일반화돼 있어 이 메시지도 이미 매칭한다(회귀 확인용, 새 패턴 불필요).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Assets\\ArrowPuzzle\\Scripts\\AppsInToss\\AppsInTossStorageManager.cs(159,49): warning CS0414: The field 'AppsInTossStorageManager.timeoutSeconds' is assigned but its value is never used"));
    }

    [Test]
    public void UserCodeCS0414_IAPManagerAppsInTossPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-15S — 사용자 스크립트 AppsInTossIAPManager.cs의 CS0414 경고.
        // 동일하게 140 composite AND 가드가 파일명 무관하게 이미 매칭한다(회귀 확인용, 새 패턴 불필요).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Assets\\ArrowPuzzle\\Scripts\\AppsInToss\\AppsInTossIAPManager.cs(18,49): warning CS0414: The field 'AppsInTossIAPManager.purchaseResultTimeoutSeconds' is assigned but its value is never used"));
    }

    #endregion

    #region 사용자 코드 컴파일러 경고/에러 (SDK-SW, SDK-T0, SDK-C3/M7)

    [Test]
    public void UsingDirectiveAppearedPreviously_AppsInToss_ReturnsTrue()
    {
        // Sentry SDK-SW — 사용자 코드의 using AppsInToss; 중복 (CS0105).
        // Unity 컴파일러가 직접 출력하므로 SDK 외부 메시지로 드롭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Script\\AITIAPManager.cs(11,7): warning CS0105: The using directive for 'AppsInToss' appeared previously in this namespace"));
    }

    [Test]
    public void UsingDirectiveAppearedPreviously_OtherTargetInAppsInTossFolder_ReturnsTrue()
    {
        // Sentry SDK-1B1 — 사용자 코드(TossPlatformServices.cs)의 using System; 중복 (CS0105).
        // 중복 대상이 'AppsInToss'가 아닌 'System'이지만, 파일 경로에 'AppsInToss' 세그먼트가
        // 포함돼 있어 AitKeywords 보호 가드가 먼저 발동할 뻔한 케이스. 컴파일러 고정 문구
        // ("The using directive for '" ~ "appeared previously in this namespace")로 일반화한
        // composite 조건이 가드보다 먼저 매칭해 드롭해야 한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Assets\\01.Scripts\\Platform\\AppsInToss\\TossPlatformServices.cs(2,7): warning CS0105: The using directive for 'System' appeared previously in this namespace"));
    }

    [Test]
    public void UsingDirectiveAppearedPreviously_AitKeywordProtected()
    {
        // SDK 자체 로그가 "[AIT ...] warning CS0105 ..." 형태로 캡처될 가능성 보호.
        // AitKeywords 가드로 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] internal: warning CS0105 collision"));
    }

    [Test]
    public void DestroyingGameObjectsImmediately_ReturnsTrue()
    {
        // Sentry SDK-T0 — 사용자 MonoBehaviour의 OnValidate/animation event 등에서 즉시 파괴 시도.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Destroying GameObjects immediately is not permitted during physics trigger/contact, animation event callbacks, rendering callbacks or OnValidate. You must use Destroy instead."));
    }

    [Test]
    public void DestroyingGameObjects_AitKeywordProtected()
    {
        // SDK가 동일 문구를 진단/리포트로 출력하더라도 [AIT] prefix가 붙으면 보호되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: Destroying GameObjects immediately is not permitted during teardown"));
    }

    #endregion

    #region 'AppsInToss' 식별자 미발견 컴파일 에러 (SDK-C3, SDK-M7, SDK-PV)

    [Test]
    public void UserCode_AppsInTossNamespaceMissing_ReturnsTrue()
    {
        // Sentry SDK-C3 — 사용자 스크립트가 AppsInToss namespace를 import하지 못함.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Script\\Tutorial_Howtoplay.cs(4,7): error CS0246: The type or namespace name 'AppsInToss' could not be found (are you missing a using directive or an assembly reference?)"));
    }

    [Test]
    public void UserCode_AppsInTossNamespaceMissing_PosixPath_ReturnsTrue()
    {
        // Sentry SDK-M7 — POSIX 경로 변형
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/CommonScripts/AdManager.cs(3,7): error CS0246: The type or namespace name 'AppsInToss' could not be found (are you missing a using directive or an assembly reference?)"));
    }

    [Test]
    public void Cs0246_WithoutAppsInTossToken_NotFiltered()
    {
        // CS0246 단독은 SDK 빌드 메시지/다른 namespace 미발견과 충돌할 수 있으므로
        // 'AppsInToss' 식별자가 동반될 때만 노이즈로 분류한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Foo.cs(1,1): error CS0246: The type or namespace name 'SomethingElse' could not be found"));
    }

    [Test]
    public void Cs0246_AppsInTossToken_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS0246: The type or namespace name 'AppsInToss' could not be found in fallback build"));
    }

    #endregion

    #region SDK 타입 인자 오용 컴파일 에러 CS1503 (SDK-VM, SDK-PV, SDK-PW, SDK-DA)

    [Test]
    public void UserCode_Cs1503_AppsInTossTypeMisuse_ReturnsTrue()
    {
        // Sentry SDK-VM — TossManager.cs에서 GetUserKeyForGameResult를 string으로 잘못 사용.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Scripts\\Manager\\TossManager.cs(192,91): error CS1503: Argument 1: cannot convert from 'AppsInToss.GetUserKeyForGameResult' to 'string'"));
    }

    [Test]
    public void UserCode_Cs1503_AppsInTossTypeMisuse_PosixPath_ReturnsTrue()
    {
        // Sentry SDK-PV — POSIX 경로 변형 (IapProductListItem 잘못 사용).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/02.Script/Platform/AIT/AITBridge.IAP.cs(17,26): error CS1503: Argument 1: cannot convert from 'AppsInToss.IapProductListItem' to 'string'"));
    }

    [Test]
    public void UserCode_Cs1503_AitException_ReturnsTrue()
    {
        // Sentry SDK-PW — modules 경로의 사용자 코드, AITException 잘못 사용.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/modules/unity-trident/Runtime/FeatureService/Billing/Billing.cs(398,42): error CS1503: Argument 1: cannot convert from 'AppsInToss.AITException' to 'string'"));
    }

    [Test]
    public void Cs1503_WithoutAppsInTossNamespace_NotFiltered()
    {
        // CS1503 단독은 SDK와 무관한 컴파일 에러도 잡을 수 있으므로 'AppsInToss.' namespace prefix가
        // 동반될 때만 노이즈로 분류. 'AppsInToss.' 점(.) 위치가 중요 (단독 토큰 'AppsInToss'는 통과).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Foo.cs(1,1): error CS1503: Argument 1: cannot convert from 'System.Int32' to 'string'"));
    }

    [Test]
    public void UserCode_Cs1503_ListGenericAppsInTossType_ReturnsTrue()
    {
        // Sentry SDK-WW — List<T> 제네릭 인자로 SDK 타입을 잘못 사용. namespace prefix가 '<AppsInToss.' 형태로 등장.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/02.Script/Match3/Event/TravelPass/TravelPassEventModel.cs(203,60): error CS1503: Argument 1: cannot convert from 'System.Collections.Generic.List<Studio.Common.Trident.Billing.ProductInfo>' to 'System.Collections.Generic.List<AppsInToss.IapProductListItem>'"));
    }

    [Test]
    public void UserCode_Cs1503_ListGenericAppsInTossType_PosixPath_ReturnsTrue()
    {
        // Sentry SDK-WV — POSIX 경로 + List<T> 제네릭 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/02.Script/Match3/Data/RemoteShopInfo.cs(312,68): error CS1503: Argument 1: cannot convert from 'System.Collections.Generic.List<Studio.Common.Trident.Billing.ProductInfo>' to 'System.Collections.Generic.List<AppsInToss.IapProductListItem>'"));
    }

    [Test]
    public void Cs1503_AppsInTossTypeMisuse_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: 합성 가드(error CS1503 + 'AppsInToss.' + Assets/ + .cs()를 모두 만족하지 않으면
        // 다음 단계인 SDK 키워드 가드에서 [AIT] prefix로 보호된다. 여기서는 Assets/와 .cs( 없이
        // SDK가 진단/리포트로 같은 토큰을 출력하는 케이스를 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS1503 fallback: 'AppsInToss.Result' conversion mismatch detected"));
    }

    #endregion

    #region 사용자 코드 컴파일 에러 — 파일 경로 prefix 없는 진단 본문 변형 (SDK-ZR, SDK-102~105)

    [Test]
    public void BareDiagnostic_Cs0103_AppsInTossIdentifier_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-ZR — DreamPassDefinition.cs에서 'AppsInTossProductSkus' 식별자 미존재(CS0103).
        // "Assets/.../...cs(L,C): error CS0103:" 경로 prefix 없이 진단 본문만 Sentry에 도달한 변형.
        // 위 Assets/ 기반 CS0103 가드가 잡지 못하므로 진단 문구 + AppsInToss 토큰 합성 가드로 흡수.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The name 'AppsInTossProductSkus' does not exist in the current context"));
    }

    [Test]
    public void BareDiagnostic_Cs0246_AppsInTossNamespace_InGameUI_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-105 — InGameUI.cs에서 AppsInToss 네임스페이스 미발견(CS0246).
        // SDK 패키지 미설치/asmdef 참조 누락이라는 사용자측 환경 문제. 경로 prefix 없는 본문 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The type or namespace name 'AppsInToss' could not be found (are you missing a using directive or an assembly reference?)"));
    }

    [Test]
    public void BareDiagnostic_Cs0246_AppsInTossNamespace_IngameTower_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-104 — IngameTower.cs 변형. 동일 패턴이 항목별 evidence를 모두 커버하는지 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The type or namespace name 'AppsInToss' could not be found"));
    }

    [Test]
    public void BareDiagnostic_Cs0246_AppsInTossNamespace_VersionCheckManager_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-103 — VersionCheckManager.cs 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "VersionCheckManager.cs: The type or namespace name 'AppsInToss' could not be found"));
    }

    [Test]
    public void BareDiagnostic_Cs1503_AitException_TossAdManager_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-102 — TossAdManager.cs에서 AITException을 string 자리에 전달(CS1503).
        // 경로 prefix 없이 본문만 도달한 변형이라 위 'AppsInToss.'/Assets/ 합성 가드가 잡지 못해 별도 흡수.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Argument 1: cannot convert from 'AppsInToss.AITException' to 'string'"));
    }

    [Test]
    public void BareDiagnostic_Cs0103_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드: [AIT] prefix가 붙은 동일 본문은 SDK 자체 로그로 간주되어 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The name 'AppsInTossProductSkus' does not exist in the current context"));
    }

    [Test]
    public void BareDiagnostic_Cs0246_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The type or namespace name 'AppsInToss' could not be found"));
    }

    [Test]
    public void BareDiagnostic_Cs1503_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Argument 1: cannot convert from 'AppsInToss.AITException' to 'string'"));
    }

    [Test]
    public void BareDiagnostic_Cs0246_SdkPackagePath_NotFiltered()
    {
        // SDK 자체 .cs의 실제 컴파일 에러는 'com.toss.apps-in-toss' 패키지 경로로 출력되므로
        // 진단 본문 가드가 이를 흡수해 실 SDK 버그를 묻으면 안 된다 (패키지 경로 제외 가드 검증).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Runtime/Foo.cs(10,5): error CS0246: The type or namespace name 'AppsInToss' could not be found"));
    }

    [Test]
    public void BareDiagnostic_Cs1503_WithoutAppsInTossToken_NotFiltered()
    {
        // 'AppsInToss' 토큰이 없으면 SDK와 무관한 일반 컴파일 에러이므로 진단 본문 가드가 매칭되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Argument 1: cannot convert from 'System.Int32' to 'string'"));
    }

    [Test]
    public void BareDiagnostic_Cs0103_WithoutAppsInTossToken_NotFiltered()
    {
        // 동일 — AppsInToss 토큰 없는 일반 CS0103 진단 본문은 통과(노이즈로 단정하지 않음).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The name 'someLocalVariable' does not exist in the current context"));
    }

    #endregion
}
