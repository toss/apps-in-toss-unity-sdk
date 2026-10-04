using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Unity 6000.3 이상의 물리 백엔드(PhysX)를 끌 수 있는지 판단하고 켜고 끄는 것을 돕는다.
    ///
    /// 6000.3부터 ProjectSettings/DynamicsManager.asset의 m_CurrentBackendId가 PhysX id(기본값이며 키가 없어도 같다)이면
    /// 앱이 물리를 안 써도 빌드가 "PhysicsBackendPhysX Module"(약 0.8MB(brotli) wasm)을 항상 포함한다.
    /// 이 id를 0xdecafbad(none, 대체 통합)로 바꾸면 PhysX와 Physics 모듈이 빠진다(CI 실측).
    ///
    /// 빌드 중에 SerializedObject로 바꾸면 반영되지 않았고 에디터가 로드하기 전에 에셋 파일에 있어야 하므로,
    /// 이 클래스는 에셋 파일 텍스트를 직접 고치고 에디터 재시작 뒤에 적용된다고 안내한다.
    /// 2D 물리(Physics2DModule)는 별개라 이 검사 대상이 아니다.
    ///
    /// 구버전에서도 컴파일돼야 하므로 UnityEngine.PhysicsModule 타입은 직접 참조하지 않고 이름 문자열로만 비교한다.
    /// </summary>
    internal static class AITPhysicsBackendAdvisor
    {
        internal const string SectionName = "물리 백엔드(PhysX)";

        internal const long PhysXBackendId = 4072204805;   // 0xf2b8ea05
        internal const long NoneBackendId = 3737844653;    // 0xdecafbad

        private const string BackendKey = "m_CurrentBackendId";
        private const string PhysicsAssemblyName = "UnityEngine.PhysicsModule";
        private const int MaxListedAssets = 5;

        // 3D 물리 컴포넌트·에셋의 YAML 클래스 id(Rigidbody, Collider 계열, Joint 계열, CharacterController, ConstantForce, PhysicMaterial, ArticulationBody).
        private static readonly HashSet<int> PhysicsClassIds = new HashSet<int>
        {
            54, 56, 57, 59, 64, 65, 75, 134, 135, 136, 138, 143, 144, 145, 146, 153, 154, 171,
        };

        private static readonly HashSet<string> PhysicsAssetTypes = new HashSet<string>
        {
            "UnityEngine.PhysicMaterial",
            "UnityEngine.PhysicsMaterial",
        };

        private static readonly Regex ClassIdRegex = new Regex(@"^--- !u!(\d+) &", RegexOptions.Multiline);
        private static readonly Regex BackendLineRegex = new Regex(@"^([ \t]*)m_CurrentBackendId:[ \t]*(\d+)[ \t]*$", RegexOptions.Multiline);

        internal class Result
        {
            public bool Applicable;
            public bool PhysXEnabled;
            public List<string> RuntimeUsages = new List<string>();
            public bool Failed;

            public bool Recommended => Applicable && PhysXEnabled && RuntimeUsages.Count == 0 && !Failed;
        }

        private static string AssetFilePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "ProjectSettings", "DynamicsManager.asset");

        internal static Result Analyze()
        {
            var result = new Result();
#if UNITY_6000_3_OR_NEWER
            result.Applicable = true;
            try
            {
                result.PhysXEnabled = IsPhysXEnabled(File.Exists(AssetFilePath) ? File.ReadAllText(AssetFilePath) : "");
                if (!result.PhysXEnabled)
                    return result;

                result.RuntimeUsages.AddRange(FindContentUsages());
                result.RuntimeUsages.AddRange(FindCodeUsages());
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-Physics] 검사 중 예외 발생(권장하지 않음으로 처리): {e.Message}", sentryCapture: false);
                result.Failed = true;
            }
#endif
            return result;
        }

        /// <summary>에셋 텍스트에서 현재 백엔드 id를 읽는다. 키가 없으면 기본값(PhysX)이다.</summary>
        internal static long ReadBackendId(string assetText)
        {
            var match = BackendLineRegex.Match(assetText ?? "");
            if (match.Success && long.TryParse(match.Groups[2].Value, out long id))
                return id;
            return PhysXBackendId;
        }

        internal static bool IsPhysXEnabled(string assetText)
        {
            return ReadBackendId(assetText) == PhysXBackendId;
        }

        /// <summary>
        /// 에셋 텍스트의 m_CurrentBackendId 줄을 targetId로 바꾼다. 줄이 없으면 형제 키와 같은 2칸 들여쓰기로 끝에 덧붙인다.
        /// 기존 줄바꿈(LF/CRLF)은 유지한다.
        /// </summary>
        internal static string RewriteBackendId(string assetText, long targetId)
        {
            assetText = assetText ?? "";
            if (BackendLineRegex.IsMatch(assetText))
                return BackendLineRegex.Replace(assetText, m => $"{m.Groups[1].Value}{BackendKey}: {targetId}", 1);

            string newline = assetText.Contains("\r\n") ? "\r\n" : "\n";
            string text = assetText;
            if (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal))
                text += newline;
            return text + $"  {BackendKey}: {targetId}{newline}";
        }

#if UNITY_6000_3_OR_NEWER
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
            foreach (var path in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                // 물리 머티리얼 에셋은 메인 타입으로 바로 감지한다.
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type != null && PhysicsAssetTypes.Contains(type.FullName))
                {
                    found.Add(path);
                    continue;
                }

                // 씬/프리팹의 물리 컴포넌트는 YAML 클래스 id로 감지한다. 바이너리로 직렬화된 에셋은 건너뛴다.
                if ((path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    && HasPhysicsComponentInYaml(path))
                    found.Add(path);
            }

            if (found.Count == 0)
                yield break;

            string sample = string.Join(", ", found.Take(MaxListedAssets));
            string more = found.Count > MaxListedAssets ? $" 외 {found.Count - MaxListedAssets}개" : "";
            yield return $"빌드에 포함되는 물리 컴포넌트/에셋: {sample}{more}";
        }

        private static bool HasPhysicsComponentInYaml(string assetPath)
        {
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", assetPath);
            if (!File.Exists(fullPath))
                return false;

            string text = File.ReadAllText(fullPath);
            if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                return false;

            foreach (Match m in ClassIdRegex.Matches(text))
            {
                if (int.TryParse(m.Groups[1].Value, out int id) && PhysicsClassIds.Contains(id))
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> FindCodeUsages()
        {
            var loaded = new Dictionary<string, System.Reflection.Assembly>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                loaded[asm.GetName().Name] = asm;

            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                // Unity 패키지는 조건부 상호운용 코드로 물리 모듈을 참조하는 경우가 많아 제외한다.
                if (assembly.sourceFiles.Length > 0 &&
                    assembly.sourceFiles.All(f => f.StartsWith("Packages/com.unity.", StringComparison.Ordinal)))
                    continue;

                if (!loaded.TryGetValue(assembly.name, out var asm))
                    continue;

                // 에디터 도메인에 로드된 런타임 어셈블리는 #if UNITY_EDITOR 구간의 참조까지 포함할 수 있어
                // 과대 보고될 수 있다. 보수적으로 사용 중으로 취급한다.
                if (asm.GetReferencedAssemblies().Any(r => r.Name == PhysicsAssemblyName))
                    yield return $"어셈블리 {assembly.name}이(가) 물리({PhysicsAssemblyName})를 참조합니다.";
            }
        }
#endif

        private const string LaunchBackendKey = "AIT_PhysicsBackendAtLaunch";

        /// <summary>
        /// 이번 에디터 세션이 처음 로드될 때의 백엔드 id를 기록한다(SessionState는 에디터 재시작 때만 지워진다).
        /// 도메인 리로드마다 불려도 첫 값만 남긴다.
        /// </summary>
        internal static void RecordLaunchBackend()
        {
            try
            {
                if (SessionState.GetString(LaunchBackendKey, "") != "")
                    return;
                string text = File.Exists(AssetFilePath) ? File.ReadAllText(AssetFilePath) : "";
                SessionState.SetString(LaunchBackendKey, ReadBackendId(text).ToString());
            }
            catch (Exception)
            {
                // 기록 실패는 안내 문구에만 영향이 있어 무시한다.
            }
        }

        /// <summary>파일에는 none인데 지금 실행 중인 에디터는 PhysX로 떠 있으면 true(재시작 대기).</summary>
        internal static bool IsRestartPending()
        {
            string launched = SessionState.GetString(LaunchBackendKey, "");
            if (launched != PhysXBackendId.ToString())
                return false;
            string text = File.Exists(AssetFilePath) ? File.ReadAllText(AssetFilePath) : "";
            return !IsPhysXEnabled(text);
        }

        /// <summary>대화상자 없이 설정 파일만 다시 쓴다. 성공하면 true. 효과는 에디터 재시작 뒤에 나타난다.</summary>
        internal static bool ApplyBackendSilently(long targetId)
        {
#if UNITY_6000_3_OR_NEWER
            try
            {
                string path = AssetFilePath;
                if (!File.Exists(path))
                {
                    AITLog.Warning($"[AIT-Physics] {path} 파일이 없어 변경하지 못했습니다.", sentryCapture: false);
                    return false;
                }

                string original = File.ReadAllText(path);
                File.WriteAllText(path, RewriteBackendId(original, targetId));
                AITLog.Info($"[AIT-Physics] m_CurrentBackendId를 {targetId}로 바꿨습니다. 에디터를 다시 시작해야 적용됩니다.");
                return true;
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-Physics] 설정 파일 수정 실패: {e.Message}", sentryCapture: false);
                return false;
            }
#else
            return false;
#endif
        }

        internal static void Disable()
        {
            Apply(NoneBackendId,
                "PhysX 끄기",
                "ProjectSettings/DynamicsManager.asset의 물리 백엔드(m_CurrentBackendId)를 none으로 바꿉니다.\n\n" +
                "게임이 3D 물리를 쓰지 않으면 PhysX와 Physics 모듈이 빌드에서 빠져 wasm이 약 0.8MB(brotli) 줄어듭니다. " +
                "이 창의 같은 위치에서 언제든 다시 켤 수 있습니다.\n\n" +
                "주의: 끈 뒤 3D 물리(Rigidbody, Collider, Physics.Raycast 등)는 동작하지 않습니다. 2D 물리는 영향이 없습니다.",
                "끄기");
        }

        internal static void Enable()
        {
            Apply(PhysXBackendId,
                "PhysX 켜기",
                "ProjectSettings/DynamicsManager.asset의 물리 백엔드(m_CurrentBackendId)를 PhysX로 되돌립니다.\n\n" +
                "빌드에 PhysX 엔진 코드가 다시 포함됩니다. 에디터 로드 시 자동으로 끄는 기능도 함께 꺼집니다.",
                "켜기");
        }

        private static void Apply(long targetId, string title, string message, string okLabel)
        {
#if UNITY_6000_3_OR_NEWER
            if (!EditorUtility.DisplayDialog(title, message, okLabel, "취소"))
                return;

            if (!ApplyBackendSilently(targetId))
                return;

            // 사용자가 직접 켠 것을 자동 적용이 다시 끄지 않도록 옵트아웃한다.
            if (targetId == PhysXBackendId)
                AITAutoOptimizer.OptOut(c => c.physicsBackendAutoDisable = 0);

            if (EditorUtility.DisplayDialog(
                    "에디터 재시작 필요",
                    "물리 백엔드 변경은 에디터를 다시 시작한 뒤 적용됩니다. 지금 프로젝트를 다시 열까요?\n\n" +
                    "'나중에'를 누르면 직접 에디터를 껐다 켜야 합니다. 그 전에 빌드하면 이전 설정으로 빌드됩니다.",
                    "지금 재시작", "나중에"))
            {
                if (EditorUtility.DisplayDialog(
                        "저장 확인",
                        "재시작 전에 저장하지 않은 씬과 프로젝트 설정을 저장합니다. 계속할까요?",
                        "계속", "취소"))
                {
                    if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                }
            }
#endif
        }
    }

    /// <summary>
    /// WebGL 빌드 시작 때 PhysX 백엔드를 끌 수 있으면 안내 로그를 한 줄 남긴다. 순수 안내이며 빌드를 바꾸지 않는다.
    /// </summary>
    internal class AITPhysicsBackendBuildAdvisory : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                if (report == null || report.summary.platform != BuildTarget.WebGL)
                    return;

                if (AITPhysicsBackendAdvisor.IsRestartPending())
                {
                    AITLog.Info(
                        "[AIT-Physics] 물리 백엔드(PhysX)를 끄도록 설정 파일을 바꿨지만 이 에디터는 아직 PhysX로 실행 중이라 " +
                        "이번 빌드에는 PhysX가 포함됩니다. 에디터를 다시 시작하면 적용됩니다.");
                    return;
                }

                if (!AITPhysicsBackendAdvisor.Analyze().Recommended)
                    return;

                AITLog.Info(
                    "[AIT-Physics] 런타임에서 3D 물리를 쓰지 않는데 물리 백엔드(PhysX)가 켜져 있습니다. " +
                    "끄면 wasm이 약 0.8MB(brotli) 줄어듭니다(에디터 재시작 필요). " +
                    $"AIT Configuration 창 > 고급 설정 > '{AITPhysicsBackendAdvisor.SectionName}'에서 끌 수 있습니다.");
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-Physics] 안내 실행 중 예외 발생(무시): {e.Message}", sentryCapture: false);
            }
        }
    }
}
