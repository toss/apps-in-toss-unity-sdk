// -----------------------------------------------------------------------
// DeployPathRegressionTests.cs - ExecuteDeploy 경로 누락 회귀 검증
// Level 0: AppsInTossMenu.cs 소스를 정적으로 읽어 ExecuteDeploy가
//          node_modules/.bin을 PATH에 포함하도록 BuildAdditionalPaths를
//          사용하는지 확인한다. Unity/pnpm 실행 없이 동작.
//
// 회귀 대상:
//   - techchat#4138: "유니티 AIT sdk Publish 배포실패 문제"
//   - APPS-IN-TOSS-UNITY-SDK-12J: "[Windows] AIT SDK 2.9.0 — 배포(Deploy) 메뉴가
//     'ait' is not recognized로 실패"
//
// 근본 원인:
//   AppsInTossMenu.ExecuteDeploy()가 AITPlatformHelper.ExecuteCommand에
//   additionalPaths로 new[] { npmDir }만 전달해 node_modules/.bin을 누락.
//   AITNpmRunner.BuildAdditionalPaths는 node_modules/.bin을 최우선으로 추가하므로
//   deploy 경로도 동일하게 BuildAdditionalPaths를 경유해야 한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;
using AppsInToss;

[TestFixture]
public class DeployPathRegressionTests
{
    /// <summary>
    /// ExecuteDeploy 본문이 additionalPaths 구성에 BuildAdditionalPaths를 사용하는지 검증.
    ///
    /// 핵심 가드: `new[] { npmDir }` 같은 리터럴 배열로 npmDir만 전달하면 안 된다.
    /// BuildAdditionalPaths(npmPath, buildPath)를 통해 node_modules/.bin을 포함시켜야 한다.
    ///
    /// Windows에서 pnpm exec ait deploy 실행 시 PATH에 node_modules/.bin이 없으면
    /// 'ait' is not recognized as an internal or external command 오류가 발생한다.
    /// </summary>
    [Test]
    public void ExecuteDeploy_Uses_BuildAdditionalPaths_InsteadOf_NpmDirOnly()
    {
        string sourcePath = LocateAppsInTossMenuSource();
        Assert.IsNotNull(sourcePath,
            "AppsInTossMenu.cs 소스 경로를 찾을 수 없음.");
        Assert.IsTrue(File.Exists(sourcePath),
            $"AppsInTossMenu.cs가 존재해야 함: {sourcePath}");

        string source = File.ReadAllText(sourcePath);
        string deployBody = ExtractMethodBody(source, "private static void ExecuteDeploy()");
        Assert.IsNotNull(deployBody,
            "AppsInTossMenu.ExecuteDeploy() 메서드를 소스에서 찾을 수 없음.");

        // 핵심 회귀 가드 #1:
        //   `new[] { npmDir }` 패턴이 deploy 본문에 있으면 node_modules/.bin이 누락된 상태.
        //   techchat#4138 / APPS-IN-TOSS-UNITY-SDK-12J 재발 지점.
        Assert.IsFalse(deployBody.Contains("new[] { npmDir }"),
            "AppsInTossMenu.ExecuteDeploy()가 new[] { npmDir }를 additionalPaths로 전달하고 있음. " +
            "node_modules/.bin이 PATH에서 누락되어 Windows에서 'ait' is not recognized 오류가 발생한다 " +
            "(techchat#4138, APPS-IN-TOSS-UNITY-SDK-12J). " +
            "AITNpmRunner.BuildAdditionalPaths(npmPath, buildPath)를 사용할 것.");

        // 핵심 회귀 가드 #2:
        //   BuildAdditionalPaths 호출이 실제로 deploy 본문에 있어야 한다.
        Assert.IsTrue(deployBody.Contains("BuildAdditionalPaths("),
            "AppsInTossMenu.ExecuteDeploy()가 AITNpmRunner.BuildAdditionalPaths()를 호출해야 함. " +
            "deploy 명령도 build와 동일하게 node_modules/.bin을 PATH에 포함시켜야 한다.");
    }

    /// <summary>
    /// BuildAdditionalPaths 호출이 workingDirectory(buildPath)를 인자로 전달하는지 확인.
    /// workingDirectory 없이 호출하면 node_modules/.bin 검색 자체가 스킵된다.
    /// </summary>
    [Test]
    public void ExecuteDeploy_PassesBuildPath_To_BuildAdditionalPaths()
    {
        string sourcePath = LocateAppsInTossMenuSource();
        Assert.IsNotNull(sourcePath, "AppsInTossMenu.cs 소스 경로를 찾을 수 없음.");

        string source = File.ReadAllText(sourcePath);
        string deployBody = ExtractMethodBody(source, "private static void ExecuteDeploy()");
        Assert.IsNotNull(deployBody, "AppsInTossMenu.ExecuteDeploy() 메서드를 소스에서 찾을 수 없음.");

        // BuildAdditionalPaths(npmPath, buildPath) 형태로 buildPath가 두 번째 인자로 전달되어야 한다.
        // buildPath가 빠지면 workingDirectory=null → node_modules/.bin 스킵 → Windows 오류 재발.
        Assert.IsTrue(
            deployBody.Contains("BuildAdditionalPaths(npmPath, buildPath)"),
            "AppsInTossMenu.ExecuteDeploy()에서 BuildAdditionalPaths 호출 시 " +
            "두 번째 인자로 buildPath를 전달해야 한다. " +
            "buildPath 없이 호출하면 node_modules/.bin이 PATH에 추가되지 않는다.");
    }

    private static string LocateAppsInTossMenuSource()
    {
        // 1) AssetDatabase MonoScript 경로로 탐색
        var guids = AssetDatabase.FindAssets("AppsInTossMenu t:MonoScript");
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) continue;
            if (Path.GetFileName(assetPath) == "AppsInTossMenu.cs" && File.Exists(assetPath))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (ms != null && ms.GetClass() == typeof(AppsInTossMenu))
                    return assetPath;
            }
        }

        // 2) 알려진 후보 경로 순회
        string[] candidates = new[]
        {
            "Editor/AppsInTossMenu.cs",
            "Packages/im.toss.apps-in-toss-unity-sdk/Editor/AppsInTossMenu.cs",
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
                string p = Path.Combine(d, "Editor", "AppsInTossMenu.cs");
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
