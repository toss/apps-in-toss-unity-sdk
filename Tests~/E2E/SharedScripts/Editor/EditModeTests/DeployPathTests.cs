// -----------------------------------------------------------------------
// DeployPathTests.cs - Deploy 메뉴 PATH 구성 회귀 검증
// Level 0: AITDeployManager.ExecuteDeploy가 node_modules/.bin을 PATH에 포함하도록
//          AITNpmRunner.BuildAdditionalPaths를 경유하는지 검증한다. Unity/pnpm 실행 없이 동작.
//
// 회귀 대상: Sentry APPS-IN-TOSS-UNITY-SDK-12J
//   "[Windows] AIT SDK — 배포(Deploy) 메뉴가 'ait' is not recognized로 실패"
//   ExecuteDeploy가 additionalPaths로 new[] { npmDir }만 전달해 node_modules/.bin을 누락한 버그.
//   build 경로(RunNpmCommandWithCache)는 BuildAdditionalPaths로 node_modules/.bin을 포함하지만
//   deploy 경로만 npmDir 단독을 전달하던 비대칭이 원인.
//
// 메모: 이 파일은 AppsInTossEditModeTests 어셈블리에 속한다(EditModeTests/ 루트, MenuTests/ 하위
//   AppsInTossMenuTests 어셈블리와 분리). 해당 어셈블리는 InternalsVisibleTo로 internal
//   AITDeployManager/AITNpmRunner에 접근 가능하다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.IO;
using UnityEditor;
using AppsInToss.Editor;       // AITNpmRunner (internal)
using AppsInToss.Editor.Menu;  // AITDeployManager (internal, .Menu 하위 네임스페이스)

[TestFixture]
public class DeployPathTests
{
    // =====================================================
    // 동작 검증: BuildAdditionalPaths가 node_modules/.bin을 포함하는가
    // =====================================================

    /// <summary>
    /// workingDirectory 하위에 node_modules/.bin이 실제로 존재하면 BuildAdditionalPaths가
    /// 그 경로를 PATH 목록에 포함해야 한다. ExecuteDeploy가 buildPath를 workingDirectory로
    /// 전달하므로, deploy 시 ait CLI(node_modules/.bin)가 PATH에 올라간다.
    /// </summary>
    [Test]
    public void BuildAdditionalPaths_WithWorkingDir_IncludesNodeModulesBin_WhenExists()
    {
        string tempDir = Path.Combine(Path.GetTempPath(),
            "ait-deploy-path-test-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        string nodeModulesBin = Path.Combine(tempDir, "node_modules", ".bin");

        try
        {
            Directory.CreateDirectory(nodeModulesBin);

            // npmPath는 임의 값 — 파일 존재 여부를 검증하지 않음
            string fakeNpmPath = Path.Combine(tempDir, "node", "npm");

            var paths = AITNpmRunner.BuildAdditionalPaths(fakeNpmPath, tempDir);

            Assert.IsNotNull(paths, "BuildAdditionalPaths 결과가 null이면 안 됨.");
            Assert.IsTrue(paths.Contains(nodeModulesBin),
                $"BuildAdditionalPaths는 node_modules/.bin({nodeModulesBin})을 포함해야 함. " +
                $"실제 반환 목록: [{string.Join(", ", paths)}]");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// node_modules/.bin이 존재하지 않으면 BuildAdditionalPaths는 그 경로를 추가하지 않는다
    /// (Directory.Exists 가드). 존재하지 않는 경로를 PATH에 넣어 노이즈가 되는 것을 방지한다.
    /// </summary>
    [Test]
    public void BuildAdditionalPaths_WithoutNodeModulesBin_DoesNotIncludeIt()
    {
        string tempDir = Path.Combine(Path.GetTempPath(),
            "ait-deploy-path-test-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        string nodeModulesBin = Path.Combine(tempDir, "node_modules", ".bin");

        try
        {
            Directory.CreateDirectory(tempDir); // node_modules/.bin은 만들지 않음

            string fakeNpmPath = Path.Combine(tempDir, "node", "npm");
            var paths = AITNpmRunner.BuildAdditionalPaths(fakeNpmPath, tempDir);

            Assert.IsNotNull(paths);
            Assert.IsFalse(paths.Contains(nodeModulesBin),
                "node_modules/.bin이 없으면 PATH 목록에 포함되면 안 됨.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // =====================================================
    // 배선 검증: ExecuteDeploy가 BuildAdditionalPaths를 경유하는가
    // =====================================================

    /// <summary>
    /// AITDeployManager.ExecuteDeploy() 본문이 additionalPaths 구성에 BuildAdditionalPaths를
    /// 사용하는지 소스 정적 검증으로 고정한다.
    ///
    /// 과거 버그: ExecuteDeploy는 additionalPaths로 new[] { npmDir }만 전달했다. 이 경우 Windows에서
    /// pnpm exec ait deploy 실행 시 node_modules/.bin이 PATH에 없어 'ait' is not recognized 오류 발생.
    /// 수정: BuildAdditionalPaths(npmPath, buildPath)로 node_modules/.bin을 PATH에 포함.
    /// </summary>
    [Test]
    public void ExecuteDeploy_UsesBuildAdditionalPaths_NotRawNpmDir()
    {
        string sourcePath = LocateDeployManagerSource();
        Assert.IsNotNull(sourcePath, "AITDeployManager.cs 소스 경로를 찾을 수 없음.");
        Assert.IsTrue(File.Exists(sourcePath), $"AITDeployManager.cs가 존재해야 함: {sourcePath}");

        string source = File.ReadAllText(sourcePath);
        string deployBody = ExtractMethodBody(source, "private static void ExecuteDeploy()");
        Assert.IsNotNull(deployBody,
            "AITDeployManager.ExecuteDeploy() 메서드를 소스에서 찾을 수 없음.");

        bool usesRawNpmDirArray =
            deployBody.Contains("new[] { npmDir }") ||
            deployBody.Contains("new string[] { npmDir }");
        Assert.IsFalse(usesRawNpmDirArray,
            "AITDeployManager.ExecuteDeploy()는 additionalPaths에 new[] { npmDir }을 직접 전달하면 안 됨. " +
            "node_modules/.bin이 누락되어 Windows에서 'ait' is not recognized 발생 " +
            "(Sentry APPS-IN-TOSS-UNITY-SDK-12J). BuildAdditionalPaths(npmPath, buildPath)를 사용할 것.");

        Assert.IsTrue(deployBody.Contains("BuildAdditionalPaths("),
            "AITDeployManager.ExecuteDeploy()는 AITNpmRunner.BuildAdditionalPaths(npmPath, buildPath)를 " +
            "호출하여 node_modules/.bin을 포함한 PATH를 구성해야 한다(build 경로와 동일한 동작 보장).");
    }

    // =====================================================
    // 헬퍼
    // =====================================================

    private static string LocateDeployManagerSource()
    {
        // 1) MonoScript를 통해 AITDeployManager 타입이 정의된 .cs를 찾는다(메서드가 이동해도 타입을 추적).
        var guids = AssetDatabase.FindAssets("AITDeployManager t:MonoScript");
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) continue;
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (ms != null && ms.GetClass() == typeof(AITDeployManager))
                return assetPath;
        }

        // 2) 알려진 상대 경로 후보
        string[] candidates =
        {
            "Editor/Menu/AITDeployManager.cs",
            "Packages/im.toss.apps-in-toss-unity-sdk/Editor/Menu/AITDeployManager.cs",
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        // 3) PackageCache 패턴
        string packageCache = "Library/PackageCache";
        if (Directory.Exists(packageCache))
        {
            var dirs = Directory.GetDirectories(packageCache, "im.toss.apps-in-toss-unity-sdk*");
            foreach (var d in dirs)
            {
                string p = Path.Combine(d, "Editor", "Menu", "AITDeployManager.cs");
                if (File.Exists(p)) return p;
            }
        }

        return null;
    }

    private static string ExtractMethodBody(string source, string signatureSubstring)
    {
        int sigIdx = source.IndexOf(signatureSubstring);
        if (sigIdx < 0) return null;

        int openIdx = source.IndexOf('{', sigIdx);
        if (openIdx < 0) return null;

        int depth = 0;
        for (int i = openIdx; i < source.Length; i++)
        {
            char ch = source[i];
            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(openIdx + 1, i - openIdx - 1);
            }
        }
        return null;
    }
}
