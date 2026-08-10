// -----------------------------------------------------------------------
// MenuRedesignTests.cs - EditMode 빌드 메뉴 재설계 검증 테스트
// Level 0: Unity Editor 리플렉션을 통해 메뉴 구조 및 제거된 메서드 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using AppsInToss;

[TestFixture]
public class MenuRedesignTests
{
    private Type menuType;

    [SetUp]
    public void Setup()
    {
        menuType = typeof(AppsInTossMenu);
        Assert.IsNotNull(menuType, "AppsInTossMenu type should exist");
    }

    // =====================================================
    // Test 1: AIT/Build 메서드 제거 확인 (BuildAndPackage로 대체됨)
    // =====================================================

    [Test]
    public void Build_MenuItem_Should_Not_Exist()
    {
        var method = menuType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(method, "Build() method should have been removed (replaced by BuildAndPackage)");
    }

    // =====================================================
    // Test 2: AIT/Package 메서드 제거 확인 (BuildAndPackage로 대체됨)
    // =====================================================

    [Test]
    public void Package_MenuItem_Should_Not_Exist()
    {
        var method = menuType.GetMethod("Package", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(method, "Package() method should have been removed (replaced by BuildAndPackage)");
    }

    // =====================================================
    // Test 3: AIT/Build & Package 메뉴 존재
    // =====================================================

    [Test]
    public void BuildAndPackage_MenuItem_Should_Exist()
    {
        var method = menuType.GetMethod("BuildAndPackage", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "BuildAndPackage() method should exist");

        var menuItemAttr = method.GetCustomAttributes(typeof(MenuItem), false)
            .Cast<MenuItem>()
            .FirstOrDefault();
        Assert.IsNotNull(menuItemAttr, "BuildAndPackage() should have MenuItem attribute");
        Assert.AreEqual("AIT/Build & Package", menuItemAttr.menuItem);
    }

    // =====================================================
    // Test 4: Restart Server 이름 변경 확인 (auto 접미사 제거)
    // =====================================================

    [Test]
    public void DevServer_RestartServer_Should_Not_Have_Auto_Suffix()
    {
        var method = menuType.GetMethod("MenuRestartDevServer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "MenuRestartDevServer() method should exist");

        var menuItemAttr = method.GetCustomAttributes(typeof(MenuItem), false)
            .Cast<MenuItem>()
            .FirstOrDefault();
        Assert.IsNotNull(menuItemAttr, "MenuRestartDevServer() should have MenuItem attribute");
        Assert.AreEqual("AIT/Dev Server/Restart Server", menuItemAttr.menuItem,
            "Dev Restart Server menu should not have (auto) suffix");
    }

    [Test]
    public void ProdServer_RestartServer_Method_Should_Not_Exist()
    {
        // Production Server 메뉴 전체 제거 확인 (샌드박스 앱 테스트 불가로 존재 이유 상실)
        var method = menuType.GetMethod("MenuRestartProdServer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(method, "MenuRestartProdServer() method should have been removed (Production Server removed)");
    }

    // =====================================================
    // Test 4b: AIT/Publish 메뉴 제거, Deploy (Test)/(Production) 메뉴로 대체 확인
    // =====================================================

    [Test]
    public void Publish_MenuItem_Should_Not_Exist()
    {
        var method = menuType.GetMethod("Publish", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(method, "Publish() method should have been removed (replaced by DeployTest/DeployProduction)");
    }

    [Test]
    public void DeployTest_MenuItem_Should_Exist()
    {
        var method = menuType.GetMethod("DeployTest", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "DeployTest() method should exist");

        var menuItemAttr = method.GetCustomAttributes(typeof(MenuItem), false)
            .Cast<MenuItem>()
            .FirstOrDefault();
        Assert.IsNotNull(menuItemAttr, "DeployTest() should have MenuItem attribute");
        Assert.AreEqual("AIT/Deploy (Test)", menuItemAttr.menuItem);
    }

    [Test]
    public void DeployProduction_MenuItem_Should_Exist()
    {
        var method = menuType.GetMethod("DeployProduction", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "DeployProduction() method should exist");

        var menuItemAttr = method.GetCustomAttributes(typeof(MenuItem), false)
            .Cast<MenuItem>()
            .FirstOrDefault();
        Assert.IsNotNull(menuItemAttr, "DeployProduction() should have MenuItem attribute");
        Assert.AreEqual("AIT/Deploy (Production)", menuItemAttr.menuItem);
    }

    [Test]
    public void DeployTest_MenuItem_Priority_Should_Precede_DeployProduction()
    {
        // 메뉴 순서: Deploy (Test)가 위, Deploy (Production)이 아래 (기존 AIT/Publish 자리 유지)
        var testMethod = menuType.GetMethod("DeployTest", BindingFlags.Public | BindingFlags.Static);
        var productionMethod = menuType.GetMethod("DeployProduction", BindingFlags.Public | BindingFlags.Static);

        var testAttr = testMethod.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>().First();
        var productionAttr = productionMethod.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>().First();

        Assert.Less(testAttr.priority, productionAttr.priority,
            "AIT/Deploy (Test) should appear above AIT/Deploy (Production) in the menu");
    }

    // =====================================================
    // Test 5: Repackage & Restart 메뉴 제거 확인
    // =====================================================

    [Test]
    public void RepackageAndRestart_Methods_Should_Not_Exist()
    {
        var allMethods = menuType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        var repackageMethods = allMethods
            .Where(m => m.Name.Contains("Repackage"))
            .Select(m => m.Name)
            .ToArray();

        Assert.IsEmpty(repackageMethods,
            $"No Repackage methods should exist, found: {string.Join(", ", repackageMethods)}");
    }

    [Test]
    public void No_MenuItem_Should_Contain_Repackage()
    {
        var allMethods = menuType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        foreach (var method in allMethods)
        {
            var menuItemAttrs = method.GetCustomAttributes(typeof(MenuItem), false)
                .Cast<MenuItem>();
            foreach (var attr in menuItemAttrs)
            {
                Assert.IsFalse(attr.menuItem.Contains("Repackage"),
                    $"MenuItem should not contain 'Repackage': {attr.menuItem}");
            }
        }
    }

    // =====================================================
    // Test 6: REQUIRES_FULL_BUILD 에러 코드 제거 확인
    // =====================================================

    [Test]
    public void REQUIRES_FULL_BUILD_Enum_Should_Not_Exist()
    {
        var enumType = typeof(AITConvertCore.AITExportError);
        var names = Enum.GetNames(enumType);

        Assert.IsFalse(names.Contains("REQUIRES_FULL_BUILD"),
            "REQUIRES_FULL_BUILD should have been removed from AITExportError enum");
    }

    // =====================================================
    // Test 7: Package Only 관련 dead code 제거 확인
    // =====================================================

    [Test]
    public void ExecutePackageOnly_Should_Not_Exist()
    {
        var method = menuType.GetMethod("ExecutePackageOnly",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(method, "ExecutePackageOnly() should have been removed");
    }

    [Test]
    public void ExecuteWebGLBuildOnly_Should_Not_Exist()
    {
        var method = menuType.GetMethod("ExecuteWebGLBuildOnly",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(method, "ExecuteWebGLBuildOnly() should have been removed");
    }

    [Test]
    public void NeedUnityRebuild_Should_Not_Exist()
    {
        var method = menuType.GetMethod("NeedUnityRebuild",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(method, "NeedUnityRebuild() should have been removed");
    }

    // =====================================================
    // Test 8: ValidateBuildMarker 제거 확인
    // =====================================================

    [Test]
    public void ValidateBuildMarker_Should_Not_Exist()
    {
        // AITPackageBuilder는 internal이므로 어셈블리에서 직접 검색
        var editorAssembly = typeof(AITConvertCore).Assembly;
        var builderType = editorAssembly.GetType("AppsInToss.Editor.AITPackageBuilder");
        Assert.IsNotNull(builderType, "AITPackageBuilder type should exist in editor assembly");

        var method = builderType.GetMethod("ValidateBuildMarker",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNull(method, "ValidateBuildMarker() should have been removed from AITPackageBuilder");
    }

    // =====================================================
    // Test 9: DoExport 시그니처 검증 (named parameter 호환성)
    // =====================================================

    [Test]
    public void DoExport_Should_Have_Named_Parameters()
    {
        var method = typeof(AITConvertCore).GetMethod("DoExport",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "DoExport() method should exist");

        var parameters = method.GetParameters();
        var paramNames = parameters.Select(p => p.Name).ToArray();

        Assert.Contains("buildWebGL", paramNames, "DoExport should have buildWebGL parameter");
        Assert.Contains("doPackaging", paramNames, "DoExport should have doPackaging parameter");
    }
}
