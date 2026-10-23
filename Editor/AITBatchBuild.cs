using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// batchmode 빌드 결과를 담는 JSON 스키마 (<c>ait-build-result.json</c>).
    /// 경로는 모두 Unity 프로젝트 루트 기준 상대 경로이며 구분자는 '/'입니다.
    /// 문자열 필드가 비어 있으면 해당 값이 없다는 뜻입니다 (예: 실패 시 aitPath).
    /// </summary>
    [Serializable]
    internal sealed class AITBatchBuildResult
    {
        /// <summary>스키마 버전. 필드가 바뀌어 기존 소비자가 깨질 때만 올립니다.</summary>
        public int schemaVersion = AITBatchBuild.ResultSchemaVersion;

        public bool success;

        /// <summary>생성된 .ait 파일의 프로젝트 상대 경로. 실패하면 빈 문자열.</summary>
        public string aitPath = "";

        /// <summary>.ait 파일 크기(바이트). 실패하면 0.</summary>
        public long sizeBytes;

        /// <summary><see cref="AITConvertCore.AITExportError"/> 이름. 성공하면 빈 문자열.</summary>
        public string errorCode = "";

        /// <summary>실패 원인 설명. 성공하면 빈 문자열.</summary>
        public string error = "";

        /// <summary>진입점 시작부터 종료 직전까지 걸린 시간(밀리초).</summary>
        public long durationMs;

        /// <summary>빌드에 쓴 SDK 패키지 버전 (package.json의 version).</summary>
        public string sdkVersion = "";

        public string ToJson() => JsonUtility.ToJson(this, true);
    }

    /// <summary>
    /// 사용자 프로젝트에서 <c>-executeMethod AppsInToss.Editor.AITBatchBuild.Build</c>로 호출하는
    /// batchmode 빌드 진입점. 새 빌드 로직 없이 <see cref="AITConvertCore.DoExport"/>를 그대로 부르고,
    /// 결과를 JSON 파일과 프로세스 종료 코드로 알립니다.
    ///
    /// 사용 예:
    ///   Unity -batchmode -nographics -projectPath &lt;프로젝트&gt; \
    ///         -executeMethod AppsInToss.Editor.AITBatchBuild.Build -logFile &lt;로그 파일&gt;
    ///
    /// - 종료 코드: 성공 0, 실패 1. batchmode에서는 진입점이 직접 <see cref="EditorApplication.Exit"/>를
    ///   부르므로 <c>-quit</c>가 필요 없습니다. Editor GUI에서 부르면 종료하지 않고 로그와 JSON만 남깁니다.
    /// - 결과 파일: 기본 <c>&lt;프로젝트&gt;/ait-build/ait-build-result.json</c>.
    ///   <c>-aitResultPath &lt;경로&gt;</c>로 바꿀 수 있습니다 (상대 경로는 프로젝트 루트 기준).
    ///   시작할 때 이전 결과 파일을 지우므로, 파일이 없으면 Unity가 진입점 도중 죽었다는 뜻입니다.
    /// - <c>-aitCleanBuild</c>를 주면 클린 빌드로 실행합니다 (기본은 증분 빌드).
    /// - 빌드 프로필은 AIT 설정의 Production 프로필이고, <c>AIT_COMPRESSION_FORMAT</c> 등
    ///   환경 변수 오버라이드는 DoExport가 기존대로 적용합니다.
    /// - 자격증명과 배포는 다루지 않습니다.
    /// </summary>
    public static class AITBatchBuild
    {
        internal const int ResultSchemaVersion = 1;
        internal const string ResultFileName = "ait-build-result.json";
        internal const string ResultPathArg = "-aitResultPath";
        internal const string CleanBuildArg = "-aitCleanBuild";

        /// <summary>batchmode 빌드 진입점. 종료 코드: 성공 0, 실패 1.</summary>
        public static void Build()
        {
            var stopwatch = Stopwatch.StartNew();
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string[] args = Environment.GetCommandLineArgs();
            string resultPath = ResolveResultPath(projectPath, args);
            bool cleanBuild = HasFlag(args, CleanBuildArg);
            string sdkVersion = SafeResolveSdkVersion();

            // 이전 실행의 결과가 남아 있으면 이번 실행이 도중에 죽었을 때 성공으로 오인된다.
            TryDeleteFile(resultPath);

            AITBatchBuildResult result;
            try
            {
                Debug.Log($"[AIT] batch 빌드를 시작합니다 (cleanBuild: {cleanBuild})");
                var export = AITConvertCore.DoExport(
                    buildWebGL: true,
                    doPackaging: true,
                    cleanBuild: cleanBuild);
                result = CreateResult(export, projectPath, stopwatch.ElapsedMilliseconds, sdkVersion);
            }
            catch (Exception e)
            {
                result = CreateExceptionResult(e, stopwatch.ElapsedMilliseconds, sdkVersion);
            }

            bool written = TryWriteResult(resultPath, result, projectPath);
            if (result.success && !written)
            {
                // 소비자는 JSON을 기준으로 판정하므로 결과를 못 남긴 성공은 실패로 취급한다.
                result.success = false;
                result.errorCode = "RESULT_WRITE_FAILED";
                result.error = "빌드는 끝났지만 결과 JSON 파일을 쓰지 못했습니다.";
            }

            if (result.success)
            {
                Debug.Log($"[AIT] batch 빌드 성공: {result.aitPath} ({result.sizeBytes} bytes, {result.durationMs}ms)");
            }
            else
            {
                AITLog.Error($"[AIT] batch 빌드 실패: {result.errorCode} - {result.error}", sentryCapture: false);
            }

            Exit(result.success ? 0 : 1);
        }

        /// <summary>DoExport 반환값과 산출물 상태에서 결과 객체를 만든다.</summary>
        internal static AITBatchBuildResult CreateResult(
            AITConvertCore.AITExportError export, string projectPath, long durationMs, string sdkVersion)
        {
            if (export != AITConvertCore.AITExportError.SUCCEED)
            {
                return CreateFailure(export.ToString(), AITConvertCore.GetErrorCause(export), durationMs, sdkVersion);
            }

            string[] aitFiles = AITBuildValidator.FindAitFiles(Path.Combine(projectPath, "ait-build"));
            if (aitFiles.Length == 0)
            {
                var missing = AITConvertCore.AITExportError.AIT_FILE_MISSING;
                return CreateFailure(missing.ToString(), AITConvertCore.GetErrorCause(missing), durationMs, sdkVersion);
            }

            // 보통 .ait는 하나다. 여러 개면 경로 순서가 아니라 가장 최근에 쓴 파일을 고른다.
            string aitFile = aitFiles[0];
            foreach (string candidate in aitFiles)
            {
                if (File.GetLastWriteTimeUtc(candidate) > File.GetLastWriteTimeUtc(aitFile))
                    aitFile = candidate;
            }

            return new AITBatchBuildResult
            {
                success = true,
                aitPath = ToProjectRelativePath(projectPath, aitFile),
                sizeBytes = new FileInfo(aitFile).Length,
                durationMs = durationMs,
                sdkVersion = sdkVersion ?? "",
            };
        }

        internal static AITBatchBuildResult CreateFailure(
            string errorCode, string error, long durationMs, string sdkVersion)
        {
            return new AITBatchBuildResult
            {
                success = false,
                errorCode = errorCode ?? "",
                error = error ?? "",
                durationMs = durationMs,
                sdkVersion = sdkVersion ?? "",
            };
        }

        internal static AITBatchBuildResult CreateExceptionResult(Exception e, long durationMs, string sdkVersion)
        {
            // DoExport는 내부 예외를 직접 잡아 BUILD_WEBGL_FAILED로 돌려주므로, 여기까지 오는 예외는 드물다.
            return CreateFailure(
                AITConvertCore.AITExportError.BUILD_WEBGL_FAILED.ToString(),
                $"{e.GetType().Name}: {e.Message}",
                durationMs,
                sdkVersion);
        }

        /// <summary>
        /// 결과 파일 경로를 정한다. <c>-aitResultPath &lt;경로&gt;</c>가 있으면 그 값(상대 경로는 프로젝트 루트 기준),
        /// 없으면 <c>&lt;프로젝트&gt;/ait-build/ait-build-result.json</c>.
        /// </summary>
        internal static string ResolveResultPath(string projectPath, string[] args)
        {
            string value = GetArgValue(args, ResultPathArg);
            if (string.IsNullOrEmpty(value))
                return Path.Combine(projectPath, "ait-build", ResultFileName);

            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(projectPath, value));
        }

        internal static string GetArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }
            return null;
        }

        internal static bool HasFlag(string[] args, string name)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// 프로젝트 루트 기준 상대 경로('/' 구분자)로 바꾼다. 프로젝트 밖의 경로면 입력을 그대로 돌려준다.
        /// </summary>
        internal static string ToProjectRelativePath(string projectPath, string fullPath)
        {
            string root = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(fullPath);
            if (!full.StartsWith(root, StringComparison.Ordinal)) return fullPath;
            return full.Substring(root.Length).Replace('\\', '/');
        }

        private static bool TryWriteResult(string resultPath, AITBatchBuildResult result, string projectPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(resultPath, result.ToJson());
                Debug.Log($"[AIT] batch 빌드 결과를 기록했습니다: {ToProjectRelativePath(projectPath, resultPath)}");
                return true;
            }
            catch (Exception e)
            {
                AITLog.Error($"[AIT] batch 빌드 결과 파일을 쓰지 못했습니다: {e.Message}", sentryCapture: false);
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 이전 batch 빌드 결과 파일을 지우지 못했습니다: {e.Message}");
            }
        }

        private static string SafeResolveSdkVersion()
        {
            try { return AITWebGLBuilder.ResolveSdkVersion(); }
            catch { return ""; }
        }

        private static void Exit(int code)
        {
            // GUI Editor에서 직접 호출했을 때 Editor를 닫지 않는다.
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
