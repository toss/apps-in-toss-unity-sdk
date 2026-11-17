using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using Debug = UnityEngine.Debug;

/// <summary>
/// 실기기 측정용 HUD 변형. 등록 규약은 <see cref="HeavyBuildVariants"/> 참조.
/// perf.yml 의 workflow_dispatch <c>variant</c> 입력에 <c>device-hud</c> 를 적는다(쉼표로 다른 변형과 조합: <c>lto-off,device-hud</c>).
///
/// 화면 우상단에 반투명 복사 버튼을 띄워, 사용자가 실기기(토스 앱 WebView, 디버거 없음)에서 측정 결과를 클립보드로 복사해 붙여넣게 한다.
/// 클립보드와 localStorage 만 쓰고 네트워크 전송은 없다.
///
/// 주입 방식: 샘플 프로젝트로 복사된 WebGL 템플릿(Assets/WebGLTemplates/AITTemplate/index.html)의 USER_HEAD 영역에 HUD 스크립트를 넣는다.
/// 패키지 원본 템플릿은 건드리지 않는다. WebGLBuildCopier 가 빌드마다 이 영역을 읽어 출력 index.html 에 합치므로(.ait 패키징 이전) 최종 번들에 실린다.
/// 변형을 지정하지 않은 빌드는 <see cref="CleanUpUnlessRequested"/> 가 남은 HUD 영역을 비운다(재사용 작업 공간 대비).
/// </summary>
public static class HeavyDeviceHudVariant
{
    public const string VariantName = "device-hud";

    /// <summary>USER_HEAD 영역에 HUD 가 들어 있음을 나타내는 표식(영역 시작 주석 안에 둔다).</summary>
    internal const string Marker = "AIT_DEVICE_HUD";

    private const string BuildToken = "__AIT_HUD_BUILD_JSON__";

    [Serializable]
    private class HudBuildInfo
    {
        public string variant;
        public string posture;
        public string unity;
        public string sha;
    }

    [HeavyVariant(VariantName)]
    public static void ApplyDeviceHud(AITEditorScriptObject config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        string script = File.ReadAllText(ResolveScriptPath());
        string buildJson = JsonUtility.ToJson(new HudBuildInfo
        {
            variant = Environment.GetEnvironmentVariable(HeavyBuildVariants.EnvVar) ?? "",
            posture = Environment.GetEnvironmentVariable("AIT_PERF_POSTURE") ?? "",
            unity = Application.unityVersion,
            sha = ResolveSha(),
        });
        // </script> 로 인라인 블록이 끊기지 않게 한다.
        buildJson = buildJson.Replace("</", "<\\/");
        if (!script.Contains(BuildToken)) throw new InvalidOperationException($"[heavy] device-hud: HUD 스크립트에 {BuildToken} 토큰이 없습니다.");
        script = script.Replace(BuildToken, buildJson);

        string section = "<!-- USER_HEAD_START - " + Marker + " -->\n<script>\n" + script + "\n</script>\n<!-- USER_HEAD_END -->";
        string indexPath = EnsureProjectIndexPath();
        string content = File.ReadAllText(indexPath);
        string updated = AITTemplateManager.ReplaceHtmlUserSection(
            content, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END, section);
        if (updated == content && !content.Contains(Marker))
            throw new InvalidOperationException($"[heavy] device-hud: USER_HEAD 마커를 찾지 못했습니다: {indexPath}");
        File.WriteAllText(indexPath, updated);
        AssetDatabase.ImportAsset(ToAssetPath(indexPath), ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[AIT-Variant] device-hud: HUD 스크립트 {script.Length}자를 {indexPath} 의 USER_HEAD 에 주입 (build={buildJson})");
    }

    /// <summary>
    /// device-hud 가 요청되지 않은 빌드에서, 이전 device-hud 빌드가 프로젝트 템플릿에 남긴 HUD 영역을 비운다.
    /// 프로젝트 템플릿이 없거나 표식이 없으면 아무것도 하지 않는다.
    /// </summary>
    internal static void CleanUpUnlessRequested(System.Collections.Generic.IEnumerable<string> requested)
    {
        foreach (string n in requested)
            if (string.Equals(n, VariantName, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            string indexPath = ProjectIndexPath();
            if (!File.Exists(indexPath)) return;
            string content = File.ReadAllText(indexPath);
            if (!content.Contains(Marker)) return;
            string empty = AITTemplateManager.HTML_USER_HEAD_START + " - 이 영역에 사용자 커스텀 스크립트/스타일을 추가하세요 -->\n    " + AITTemplateManager.HTML_USER_HEAD_END;
            File.WriteAllText(indexPath, AITTemplateManager.ReplaceHtmlUserSection(
                content, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END, empty));
            Debug.Log("[AIT-Variant] device-hud 미지정: 이전 빌드가 남긴 HUD 영역을 비웠습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AIT-Variant] device-hud 잔여 영역 정리 실패(무시): {ex.Message}");
        }
    }

    private static string ProjectIndexPath()
        => Path.Combine(Application.dataPath, "WebGLTemplates", "AITTemplate", "index.html");

    /// <summary>프로젝트 템플릿 index.html 이 없으면 SDK 정본에서 복사해 만든 뒤 경로를 돌려준다(빌드 초기화가 하는 동기화와 같은 함수).</summary>
    private static string EnsureProjectIndexPath()
    {
        string path = ProjectIndexPath();
        if (!File.Exists(path)) AITTemplateManager.EnsureWebGLTemplatesExist();
        if (!File.Exists(path)) throw new InvalidOperationException($"[heavy] device-hud: 프로젝트 WebGL 템플릿 index.html 을 만들 수 없습니다: {path}");
        return path;
    }

    private static string ToAssetPath(string fullPath)
    {
        string full = Path.GetFullPath(fullPath).Replace('\\', '/');
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/') + "/";
        return full.StartsWith(project, StringComparison.Ordinal) ? full.Substring(project.Length) : full;
    }

    private static string ResolveScriptPath()
    {
        string here = CallerDir();
        string path = string.IsNullOrEmpty(here) ? null : Path.GetFullPath(Path.Combine(here, "DeviceHud~", "device-hud.js"));
        if (path == null || !File.Exists(path))
            throw new FileNotFoundException($"[heavy] device-hud: HUD 스크립트를 찾을 수 없습니다: {path}");
        return path;
    }

    private static string ResolveSha()
    {
        string sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(sha)) return sha.Trim().Length > 8 ? sha.Trim().Substring(0, 8) : sha.Trim();
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --short=8 HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Application.dataPath,
            };
            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                if (p.ExitCode == 0 && o.Length > 0) return o;
            }
        }
        catch
        {
            // git 이 없으면 라벨에 sha 를 비운다.
        }
        return "";
    }

    private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);
}
