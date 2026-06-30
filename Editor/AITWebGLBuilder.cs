using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AppsInToss;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Unity WebGL 빌드 실행부 (AITConvertCore에서 분리).
    /// 빌드 캐시 유효성 검증, BuildPipeline 실행, 빌드 마커 기록,
    /// 버전 정보 JSON 기록을 한 곳에서 관리한다.
    /// AITConvertCore의 빌드 파이프라인(DoExport/DoExportAsync)이 이 클래스를 호출하며 동작은 기존과 동일하다.
    /// </summary>
    internal static class AITWebGLBuilder
    {
        /// <summary>
        /// 기존 빌드 캐시가 유효한지 검증하여 clean build 필요 여부를 판단합니다.
        /// 빌드 마커 없음, Unity 버전 불일치, 필수 파일 누락 시 true를 반환합니다.
        /// </summary>
        internal static bool ShouldForceCleanBuild(string outputPath, bool cleanBuild)
        {
            if (cleanBuild) return true;
            if (!Directory.Exists(outputPath)) return false;

            var buildInfo = AITConvertCore.ReadBuildMarker(outputPath);
            if (buildInfo == null)
            {
                AITLog.Warning("[AIT] 빌드 마커가 없습니다. Clean build를 수행합니다.", sentryCapture: false);
                return true;
            }
            if (buildInfo.unityVersion != Application.unityVersion)
            {
                AITLog.Warning($"[AIT] Unity 버전 불일치: 빌드 {buildInfo.unityVersion} vs 현재 {Application.unityVersion}. Clean build를 수행합니다.", sentryCapture: false);
                return true;
            }

            string buildDir = Path.Combine(outputPath, "Build");
            if (!Directory.Exists(buildDir) || Directory.GetFiles(buildDir, "*.loader.js").Length == 0)
            {
                AITLog.Warning("[AIT] 빌드 필수 파일이 없습니다. Clean build를 수행합니다.", sentryCapture: false);
                return true;
            }

            return false;
        }

        internal static AITConvertCore.AITExportError BuildWebGL(bool cleanBuild = false, AITBuildProfile profile = null)
        {
            // WebGL Build Support 모듈 설치 여부 사전 체크
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                // 사용자 환경 문제(Unity Hub에서 WebGL Build Support 미설치)이지 SDK 버그가 아니다.
                // 메시지 첫머리의 '[AIT' 토큰이 트래커 SDK 키워드 가드를 먼저 발동시켜 NonSdkMessagePatterns를
                // 무력화하므로, sentryCapture:false가 Sentry 차단의 유일하게 확실한 방법이다 (SDK-DB).
                AITLog.Error(
                    "[AIT] ✗ WebGL Build Support 모듈이 설치되지 않았습니다.\n" +
                    "Unity Hub를 열고 현재 Unity 버전(" + Application.unityVersion + ")에 WebGL Build Support를 추가 설치하세요.\n" +
                    "Unity Hub > Installs > Unity " + Application.unityVersion + " > Add Modules > WebGL Build Support",
                    sentryCapture: false);
                return AITConvertCore.AITExportError.BUILD_WEBGL_FAILED;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool confirm = AITPlatformHelper.ShowConfirmDialog(
                    "빌드 타겟 전환 필요",
                    $"현재 빌드 타겟이 {EditorUserBuildSettings.activeBuildTarget}입니다.\n" +
                    "WebGL 빌드를 위해 빌드 타겟을 WebGL로 전환해야 합니다.",
                    "전환",
                    "취소");

                if (!confirm)
                {
                    Debug.Log("[AIT] 사용자가 빌드 타겟 전환을 취소했습니다.");
                    return AITConvertCore.AITExportError.CANCELLED;
                }

                Debug.Log($"[AIT] 빌드 타겟을 {EditorUserBuildSettings.activeBuildTarget}에서 WebGL로 전환합니다...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                {
                    AITLog.Error("[AIT] ✗ WebGL 빌드 타겟으로 전환할 수 없습니다.", sentryCapture: false);
                    return AITConvertCore.AITExportError.BUILD_WEBGL_FAILED;
                }
            }

            string[] scenes = UnityUtil.GetBuildScenes();
            string outputPath = Path.Combine(UnityUtil.GetProjectPath(), AITConvertCore.webglDir);

            // 빌드 캐시 유효성 검증
            if (ShouldForceCleanBuild(outputPath, cleanBuild))
            {
                if (!cleanBuild)
                    Debug.Log("[AIT] 빌드 캐시 검증 실패. 자동으로 clean build를 수행합니다.");
                cleanBuild = true;
            }

            Debug.Log($"[AIT] WebGL 빌드를 시작합니다... ({(cleanBuild ? "클린 빌드" : "증분 빌드")})");

            // 클린 빌드 시에만 기존 빌드 폴더 삭제
            if (cleanBuild && Directory.Exists(outputPath))
            {
                Debug.Log("[AIT] 클린 빌드: 기존 WebGL 빌드 폴더 삭제 중...");
                Directory.Delete(outputPath, true);
            }

            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            // 빌드 옵션 설정
            BuildOptions buildOptions = BuildOptions.None;

            // Development Build 옵션 (빌드 속도 향상, 디버깅 편의)
            if (profile.developmentBuild)
            {
                buildOptions |= BuildOptions.Development;
                Debug.Log("[AIT] Development Build 활성화");
            }

            // LZ4 압축으로 빌드 속도 향상
            if (profile.enableLZ4Compression)
            {
                buildOptions |= BuildOptions.CompressWithLz4;
            }

            // Unity 2021.3에서 Bee 빌드 캐시 문제로 인한 빌드 루프 방지
            // cleanBuild 시 CleanBuildCache 옵션 추가
            if (cleanBuild)
            {
                buildOptions |= BuildOptions.CleanBuildCache;
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = buildOptions,
#if UNITY_2023_3_OR_NEWER
                // Unity 2023.3+ (Unity 6): 최신 기능 활성화 (문서 권장)
                extraScriptingDefines = new[]
                {
                    "APPSINTOS_OPTIMIZED",
                    "WEBGL_2_0",
                    "UNITY_6_FEATURES"
                }
#elif UNITY_2022_3_OR_NEWER
                // Unity 2022.3+: WebGL 2.0 최적화 (문서 권장)
                extraScriptingDefines = new[]
                {
                    "APPSINTOS_OPTIMIZED",
                    "WEBGL_2_0"
                }
#endif
            };

            // 빌드 직전: Resources JSON으로 Version/CommitHash/ReleaseDateTime 기록
            // .cs 파일 수정과 달리 .json은 스크립트 컴파일을 유발하지 않으므로 도메인 리로드 없음
            WriteVersionInfoJson();

            var config = AITConvertCore.config;
            // 빌드 직전 콘텐츠 최적화(인프로세스, opt-in): 대용량 오디오를 StreamingAssets로 외부화하고
            // 소스를 무음 스텁으로 치환 → BuildPlayer가 최적화본을 패키징. 빌드 후 finally에서 원상 복원.
            var audioStreamHandle = AITAudioStreamingProcessor.ExternalizeForBuild(config);
            // 콘텐츠 최적화 — 텍스처 crunch (빌드 산출물 .data 축소, 빌드 후 임포터 원복)
            var textureCrunchHandle = AITTextureCrunchProcessor.ApplyForBuild(config);
            // 콘텐츠 최적화 — 텍스처 크기 클램프 (maxTextureSize 캡으로 .data 축소, 빌드 후 임포터 원복)
            var textureClampHandle = AITTextureSizeClampProcessor.ApplyForBuild(config);
            // 콘텐츠 최적화 — ASTC 블록 에스컬레이션 (빌드 산출물 .data 축소, 빌드 후 임포터 원복)
            var astcBlockHandle = AITAstcBlockProcessor.ApplyForBuild(config);
            // 콘텐츠 최적화 — 폰트 CJK subset (.data 폰트 데이터 축소, 빌드 후 원본 폰트 복원)
            var fontSubsetHandle = AITFontSubsetProcessor.ApplyForBuild(config);
            // 콘텐츠 최적화 — 대형 텍스처 외부화 (초기 .data 에서 분리, 빌드 후 임포터/소스 원복)
            var textureStreamHandle = AITLargeTextureExternalizer.ExternalizeForBuild(config);
            // 콘텐츠 최적화 — 대형 폰트 deferral (초기 .data 에서 .ttf 제외, 빌드 후 임포터 원복)
            var fontStreamHandle = AITFontExternalizer.ExternalizeForBuild(config);

            UnityEditor.Build.Reporting.BuildReport result;
            try
            {
                result = BuildPipeline.BuildPlayer(buildPlayerOptions);
            }
            finally
            {
                // 빌드 완료 후 (성공/실패 무관) 콘텐츠 최적화를 원상 복원 — 사용자 프로젝트에 산출물 남기지 않음
                //
                // 반드시 "적용의 정확한 역순"으로 복원한다. 각 프로세서는 자기 변형 직전의
                // 온디스크 .meta/.ttf 스냅샷을 백업하므로, 같은 에셋을 여러 프로세서가 건드린
                // 경우 "가장 먼저 적용한" 프로세서가 진짜 원본을 갖고 있다. 역순 복원이면 그
                // 프로세서의 RestoreForBuild가 가장 마지막에 쓰여 최종 승자가 되어 원본이 보존된다.
                // (순서가 어긋나면 뒤에 적용된 프로세서가 자신의 "이미 변형된" 스냅샷을 마지막에
                //  덮어써 파트너 에셋이 매 빌드마다 영구 오염된다 — 교차-프로세서 복원 버그.)
                //
                // 적용 순서: audio → crunch → clamp → ASTC → fontSubset → texStream → fontStream
                // 복원 순서: fontStream → texStream → fontSubset → ASTC → clamp → crunch → audio
                // 콘텐츠 최적화 — 대형 폰트 deferral 원복(임포터 설정 복원) (빌드 성공/실패 무관)
                AITFontExternalizer.RestoreForBuild(fontStreamHandle);
                // 콘텐츠 최적화 — 대형 텍스처 외부화 원복(소스/임포터 복원) (빌드 성공/실패 무관)
                AITLargeTextureExternalizer.RestoreForBuild(textureStreamHandle);
                // 콘텐츠 최적화 — 폰트 subset 원본 폰트 복원 (빌드 성공/실패 무관)
                AITFontSubsetProcessor.RestoreForBuild(fontSubsetHandle);
                // ASTC 블록 에스컬레이션 복원 (성공/실패 무관)
                AITAstcBlockProcessor.RestoreForBuild(astcBlockHandle);
                // 콘텐츠 최적화 — 텍스처 크기 클램프 임포터 설정 원복 (빌드 성공/실패 무관)
                AITTextureSizeClampProcessor.RestoreForBuild(textureClampHandle);
                // 콘텐츠 최적화 — 텍스처 crunch 임포터 설정 원복 (빌드 성공/실패 무관)
                AITTextureCrunchProcessor.RestoreForBuild(textureCrunchHandle);
                // 오디오 스트리밍 외부화 원복 (StreamingAssets — 다른 에셋과 .meta 중첩 없음)
                AITAudioStreamingProcessor.RestoreForBuild(audioStreamHandle);

                // #1008: 버전 정보 JSON은 빌드 후 삭제하지 않는다 — 매 빌드 삭제/재생성하면 .meta GUID가
                // 갱신되어 무변경 재빌드에도 data 아카이브가 재빌드+재압축된다. 유지가 빌드 캐시를 보존한다.
                // (WriteVersionInfoJson 이 내용 불변 시 스킵하므로 안전.)
            }

            // 빌드 리포트를 에러 리포터에 저장 (Issue 신고 시 사용)
            AITErrorReporter.SetBuildReport(result);

            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                // 사용자가 빌드를 취소한 경우 — Unity가 BuildResult.Cancelled를 반환한다.
                // 이것은 사용자 의사이므로 SDK 결함 보고 대상이 아니다.
                if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Cancelled)
                {
                    AITLog.Warning("[AIT] 사용자에 의해 WebGL 빌드가 취소되었습니다.", sentryCapture: false);
                    return AITConvertCore.AITExportError.CANCELLED;
                }

                // 실패 진단 메시지를 step에서 평탄화 수집(에러/경고 캡 분리는 BuildFailureSummary가 처리).
                var collected = new List<(LogType type, string content)>();
                foreach (var step in result.steps)
                {
                    foreach (var message in step.messages)
                    {
                        collected.Add((message.type, message.content));
                    }
                }

                // BuildReport.steps에 에러/경고가 전혀 없으면(Bee/IL2CPP의 .o 실패 등 무진단 실패)
                // 에디터 로그 꼬리를 첨부해 단서를 남긴다. 그 외에는 step 메시지로 충분하다.
                bool hasDiagnosticMessage = false;
                foreach (var m in collected)
                {
                    if (m.type == LogType.Error || m.type == LogType.Exception
                        || m.type == LogType.Assert || m.type == LogType.Warning)
                    {
                        hasDiagnosticMessage = true;
                        break;
                    }
                }
                string logTail = hasDiagnosticMessage ? null : AITWebGLBuildDiagnostics.ReadEditorLogTail(Application.consoleLogPath);

                // BuildSummary.totalErrors/totalWarnings는 uint이므로 int 시그니처로 명시 캐스트
                // (실패 빌드의 카운트는 항상 int 범위 — overflow 우려 없음).
                string failureSummary = AITWebGLBuildDiagnostics.BuildFailureSummary(
                    result.summary.result.ToString(),
                    (int)result.summary.totalErrors,
                    (int)result.summary.totalWarnings,
                    collected,
                    logTail);

                // BuildPipeline.BuildPlayer 실패는 거의 항상 사용자 프로젝트(컴파일 에러,
                // 사용자 에셋 문제, 환경 OOM 등) 또는 Unity 자체의 비정상 종료에서 비롯되며
                // SDK 측에서 분기/수정할 수 있는 정보가 아니다. 콘솔에는 진단 메시지를 남기되
                // Sentry로는 보내지 않아 분류 노이즈를 만들지 않는다.
                AITLog.Error(failureSummary, sentryCapture: false);
                return AITConvertCore.AITExportError.BUILD_WEBGL_FAILED;
            }

            Debug.Log("[AIT] WebGL 빌드가 완료되었습니다.");

            // AIT 빌드 마커 파일 생성 (빌드 정보 기록용)
            try
            {
                var buildInfo = new AITBuildInfo
                {
                    sdkVersion = AITVersion.Version,
                    buildTime = DateTime.UtcNow.ToString("o"),
                    compressionFormat = (int)PlayerSettings.WebGL.compressionFormat,
                    decompressionFallback = PlayerSettings.WebGL.decompressionFallback,
                    profileName = profile?.developmentBuild == true ? "Development" : "Production",
                    unityVersion = Application.unityVersion
                };
                AITConvertCore.WriteBuildMarker(outputPath, buildInfo);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 빌드 마커 생성 실패 (무시됨): {e}");
            }

            return AITConvertCore.AITExportError.SUCCEED;
        }

        // Unity Resources 폴더는 사용자 프로젝트 소유 — SDK 패키지(UPM Git 설치 시 immutable)가 아니라
        // 여기에 쓰면 설치 방식에 무관하게 쓰기 가능. 플레이어 번들은 Resources/ 를 자동 포함함.
        // 이 파일은 빌드 후에도 삭제하지 않고 유지한다 — 매 빌드 삭제/재생성하면 .meta GUID가
        // 갱신되어 내용이 같아도 data 아카이브가 dirty 판정되고, 무변경 재빌드마다 아카이브
        // 재빌드+재압축(대형 프로젝트에서 수 분)이 발생하기 때문이다.
        private const string VersionInfoAssetPath = "Assets/Resources/AITVersionInfo.json";

        /// <summary>
        /// 빌드 직전 Assets/Resources/AITVersionInfo.json 에 Version/CommitHash/ReleaseDateTime 기록.
        /// .cs 파일 수정과 달리 .json은 스크립트 컴파일/도메인 리로드를 유발하지 않는다.
        ///
        /// 내용이 실제로 달라질 때(SDK 버전/커밋 변경)만 다시 기록한다. 같은 내용을 매 빌드
        /// 다시 쓰거나 삭제/재생성하면 asset dirty 판정으로 data 아카이브 증분 캐시가 무효화된다
        /// (판단 로직은 <see cref="BuildVersionInfoJson"/> 참조).
        /// </summary>
        private static void WriteVersionInfoJson()
        {
            try
            {
                string projectPath = UnityUtil.GetProjectPath();
                string absolutePath = Path.Combine(projectPath, VersionInfoAssetPath);
                string existingJson = File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : null;

                // AITVersion.Version은 EnsureLoaded 경로에 따라 "unknown"으로 초기화될 수 있어
                // 패키지의 권위 있는 소스인 package.json 에서 직접 읽는다.
                string version = ResolveSdkVersion();
                string commitHash = GetGitCommitHash();
                string json = BuildVersionInfoJson(
                    existingJson, version, commitHash,
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmm"),
                    out bool changed);

                if (!changed)
                {
                    Debug.Log("[AIT] 버전 정보 JSON 변경 없음 — 기존 파일 유지 (data 빌드 캐시 보존)");
                    return;
                }

                string directory = Path.GetDirectoryName(absolutePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(absolutePath, json);

                // Resources로 인식시키기 위해 임포트 (스크립트가 아니므로 도메인 리로드 없음)
                AssetDatabase.ImportAsset(VersionInfoAssetPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[AIT] 버전 정보 JSON 기록: Version={version}, CommitHash={commitHash}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 버전 정보 JSON 기록 실패 (무시됨): {e.Message}");
            }
        }

        /// <summary>
        /// 기록할 버전 정보 JSON(canonical 형태)을 만들고 기존 파일 내용과 비교해
        /// 재기록 필요 여부를 판단한다. 순수 함수 — 파일 IO 없음 (EditMode 테스트 대상).
        ///
        /// version/commitHash가 기존 파일과 같으면 기존 releaseDateTime을 재사용한다.
        /// releaseDateTime은 분 단위 wall-clock이라 이를 매 빌드 갱신하면 그것만으로
        /// 내용이 달라져 data 아카이브가 무효화된다. 소비처(<see cref="AITVersion.FullVersion"/>,
        /// Sentry 태그)는 표시용이므로 "이 버전/커밋 조합을 처음 빌드한 시각" 의미로 충분하다.
        /// </summary>
        /// <returns>기록할 canonical JSON. changed가 false면 기존 파일과 동일하므로 기록 불필요.</returns>
        internal static string BuildVersionInfoJson(
            string existingJson, string version, string commitHash, string nowStamp, out bool changed)
        {
            var payload = new AITVersion.VersionInfoPayload
            {
                version = version,
                releaseDateTime = nowStamp,
                commitHash = commitHash,
            };

            if (!string.IsNullOrEmpty(existingJson))
            {
                try
                {
                    var existing = JsonUtility.FromJson<AITVersion.VersionInfoPayload>(existingJson);
                    if (existing != null
                        && existing.version == version
                        && existing.commitHash == commitHash
                        && !string.IsNullOrEmpty(existing.releaseDateTime))
                    {
                        payload.releaseDateTime = existing.releaseDateTime;
                    }
                }
                catch
                {
                    // 손상된 기존 파일 → 새 값으로 canonical 재기록
                }
            }

            // JsonUtility.ToJson 으로 직렬화 — 수동 문자열 보간의 이스케이프 누락을 방지.
            // 필드명은 VersionInfoPayload 의 public field 이름과 런타임 read 경로가 공유.
            string json = JsonUtility.ToJson(payload, prettyPrint: true);
            changed = !string.Equals(existingJson, json, StringComparison.Ordinal);
            return json;
        }

        /// <summary>
        /// SDK package.json 의 version 필드를 직접 읽는다.
        /// AITVersion.Version 은 EnsureLoaded 경로 / 설치 방식에 따라 "unknown" 일 수 있으므로
        /// 빌드 산출물에 박는 값은 package.json 을 권위 있는 소스로 사용.
        /// </summary>
        private static string ResolveSdkVersion()
        {
            try
            {
                if (!AITPackagePathResolver.TryResolveFile("package.json", out string packageJsonPath))
                {
                    return AITVersion.Version;
                }

                string content = File.ReadAllText(packageJsonPath);
                // "version": "x.y.z" 최소 파싱 (JSON 라이브러리 의존성 없이)
                var match = System.Text.RegularExpressions.Regex.Match(
                    content,
                    "\"version\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : AITVersion.Version;
            }
            catch
            {
                return AITVersion.Version;
            }
        }

        /// <summary>
        /// git rev-parse --short=7 HEAD로 현재 커밋 해시를 가져옵니다.
        /// Unity 프로젝트 루트에서 실행하여 프로젝트 커밋 해시를 반환합니다.
        /// (SDK가 UPM git 패키지로 설치된 경우에도 프로젝트의 커밋 해시를 사용)
        /// </summary>
        private static string GetGitCommitHash()
        {
            try
            {
                using (var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse --short=7 HEAD",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        WorkingDirectory = UnityUtil.GetProjectPath()
                    }
                })
                {
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    bool exited = process.WaitForExit(5000);
                    return exited && process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : "unknown";
                }
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
