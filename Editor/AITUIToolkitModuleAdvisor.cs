using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 내장 모듈 com.unity.modules.uielements(UI Toolkit)를 끌 수 있는지 판단하고 켜고 끄는 것을 돕는다.
    ///
    /// 이 모듈이 켜져 있으면 게임이 UI Toolkit을 안 써도 빌드에 UIElements/IMGUI/TextCore 등이 남아
    /// wasm이 커지고 첫 프레임 전에 UIElementsInitialization이 돈다(빈 앱 CI 실측: wasm 0.45~1.06MB(brotli),
    /// 첫 프레임 149~550ms). SDK 자체는 런타임에 UI Toolkit을 쓰지 않는다.
    ///
    /// 모듈을 끈 뒤에도 이 파일이 컴파일돼야 하므로 UnityEngine.UIElements 타입은 직접 참조하지 않고
    /// 전체 이름 문자열로만 비교한다.
    /// </summary>
    internal static class AITUIToolkitModuleAdvisor
    {
        internal const string ModuleName = "com.unity.modules.uielements";
        internal const string SectionName = "UI Toolkit 모듈";

        private const string UIElementsAssemblyName = "UnityEngine.UIElementsModule";
        private const int MaxListedAssets = 5;

        private static readonly HashSet<string> UIToolkitAssetTypes = new HashSet<string>
        {
            "UnityEngine.UIElements.PanelSettings",
            "UnityEngine.UIElements.VisualTreeAsset",
            "UnityEngine.UIElements.StyleSheet",
            "UnityEngine.UIElements.ThemeStyleSheet",
        };

        internal class Result
        {
            public bool ModuleEnabled;
            public List<string> DependentPackages = new List<string>();
            public List<string> RuntimeUsages = new List<string>();
            public bool Failed;

            public bool Recommended =>
                !Failed && ModuleEnabled && DependentPackages.Count == 0 && RuntimeUsages.Count == 0;
        }

        internal static Result Analyze()
        {
            var result = new Result();
            try
            {
                result.ModuleEnabled = IsModuleInManifest();
                if (!result.ModuleEnabled)
                    return result;

                result.DependentPackages = FindDependentPackages();
                result.RuntimeUsages.AddRange(FindContentUsages());
                result.RuntimeUsages.AddRange(FindCodeUsages());
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-UIToolkit] 검사 중 예외 발생(권장하지 않음으로 처리): {e.Message}", sentryCapture: false);
                result.Failed = true;
            }
            return result;
        }

        private static bool IsModuleInManifest()
        {
            string manifestPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            var root = MiniJson.Deserialize(File.ReadAllText(manifestPath)) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("dependencies", out var deps))
                return false;
            var depMap = deps as Dictionary<string, object>;
            return depMap != null && depMap.ContainsKey(ModuleName);
        }

        private static List<string> FindDependentPackages()
        {
            var list = new List<string>();
            foreach (var package in PackageInfo.GetAllRegisteredPackages())
            {
                if (package.name == ModuleName || package.dependencies == null)
                    continue;
                if (package.dependencies.Any(d => d.name == ModuleName))
                    list.Add(package.name);
            }
            return list;
        }

        private static IEnumerable<string> FindContentUsages()
        {
            var roots = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                    roots.Add(scene.path);
            }

            // Resources 폴더는 참조 없이도 빌드에 들어간다(Editor 폴더 아래는 제외).
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;
                if (path.IndexOf("/Resources/", StringComparison.Ordinal) < 0 || path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                    continue;
                if (AssetDatabase.IsValidFolder(path))
                    continue;
                roots.Add(path);
            }

            var found = new List<string>();
            // UIDocument 컴포넌트는 네이티브 클래스라 의존성에 스크립트가 잡히지 않는다.
            // 대신 UIDocument가 항상 참조하는 PanelSettings/VisualTreeAsset 에셋으로 간접 감지한다.
            foreach (var path in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type != null && UIToolkitAssetTypes.Contains(type.FullName))
                    found.Add(path);
            }

            if (found.Count == 0)
                yield break;

            string sample = string.Join(", ", found.Take(MaxListedAssets));
            string more = found.Count > MaxListedAssets ? $" 외 {found.Count - MaxListedAssets}개" : "";
            yield return $"빌드에 포함되는 UI Toolkit 에셋: {sample}{more}";
        }

        private static IEnumerable<string> FindCodeUsages()
        {
            var loaded = new Dictionary<string, System.Reflection.Assembly>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                loaded[asm.GetName().Name] = asm;

            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                // UGUI/TextMeshPro/Input System 같은 Unity 패키지는 조건부 상호운용 코드로만 UI Toolkit을 참조하고,
                // 모듈이 빠지면 그 코드가 컴파일에서 제외된다.
                if (assembly.sourceFiles.Length > 0 &&
                    assembly.sourceFiles.All(f => f.StartsWith("Packages/com.unity.", StringComparison.Ordinal)))
                    continue;

                if (!loaded.TryGetValue(assembly.name, out var asm))
                    continue;

                // 에디터 도메인에 로드된 런타임 어셈블리는 #if UNITY_EDITOR 구간의 참조까지 포함할 수 있어
                // 과대 보고될 수 있다. 보수적으로 사용 중으로 취급한다.
                if (asm.GetReferencedAssemblies().Any(r => r.Name == UIElementsAssemblyName))
                    yield return $"어셈블리 {assembly.name}이(가) UI Toolkit({UIElementsAssemblyName})을 참조합니다.";
            }
        }

        internal static void Disable()
        {
            if (!EditorUtility.DisplayDialog(
                    "UI Toolkit 모듈 끄기",
                    "내장 모듈 com.unity.modules.uielements를 Packages/manifest.json에서 제거합니다.\n\n" +
                    "게임이 UI Toolkit을 쓰지 않으면 빌드가 작아지고 첫 화면이 빨라집니다. " +
                    "이 창의 같은 위치에서 언제든 다시 켤 수 있습니다.\n\n" +
                    "주의: UI Toolkit을 쓰는 에디터 스크립트나 패키지는 모듈을 끈 뒤 컴파일 오류가 날 수 있습니다.",
                    "끄기", "취소"))
                return;

            Track(Client.Remove(ModuleName), "제거");
        }

        internal static void Enable()
        {
            if (!EditorUtility.DisplayDialog(
                    "UI Toolkit 모듈 켜기",
                    "내장 모듈 com.unity.modules.uielements를 Packages/manifest.json에 다시 추가합니다.\n\n" +
                    "빌드에 UI Toolkit 관련 엔진 코드가 다시 포함됩니다.",
                    "켜기", "취소"))
                return;

            Track(Client.Add(ModuleName), "추가");
        }

        private static void Track(Request request, string verb)
        {
            AITLog.Info($"[AIT-UIToolkit] {ModuleName} {verb} 요청을 보냈습니다. 도메인 리로드가 일어날 수 있습니다.");
            void Poll()
            {
                if (!request.IsCompleted)
                    return;
                EditorApplication.update -= Poll;
                if (request.Status == StatusCode.Failure)
                    AITLog.Warning($"[AIT-UIToolkit] {ModuleName} {verb} 실패: {request.Error?.message}", sentryCapture: false);
            }
            EditorApplication.update += Poll;
        }
    }

    /// <summary>
    /// WebGL 빌드 시작 때 UI Toolkit 모듈을 끌 수 있으면 안내 로그를 한 줄 남긴다. 순수 안내이며 빌드를 바꾸지 않는다.
    /// </summary>
    internal class AITUIToolkitBuildAdvisory : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                if (report == null || report.summary.platform != BuildTarget.WebGL)
                    return;

                if (!AITUIToolkitModuleAdvisor.Analyze().Recommended)
                    return;

                AITLog.Info(
                    "[AIT-UIToolkit] 런타임에서 UI Toolkit을 쓰지 않는데 모듈(com.unity.modules.uielements)이 켜져 있습니다. " +
                    "빈 앱 기준 실측으로 끄면 첫 화면이 약 0.15~0.55초 빨라지고 wasm이 약 0.5~1MB 줄었습니다. " +
                    $"AIT Configuration 창 > 고급 설정 > '{AITUIToolkitModuleAdvisor.SectionName}'에서 끌 수 있습니다.");
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-UIToolkit] 안내 실행 중 예외 발생(무시): {e.Message}", sentryCapture: false);
            }
        }
    }
}
