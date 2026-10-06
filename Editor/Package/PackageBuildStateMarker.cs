using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// vite/ait build(패키징 산출물 생성) 결과 상태 마커 — 재빌드 시 불필요한 vite/ait build
    /// 재실행을 건너뛰기 위한 판단 근거. <see cref="PnpmInstallStateMarker"/>와 동일한
    /// 해시 마커 + 킬스위치 + fail-closed 패턴을 패키징 단계(granite/ait build)에 적용한다.
    ///
    /// 마커를 node_modules/ 안(node_modules/.ait-package-build-state.json)에 두는 이유는
    /// PnpmInstallStateMarker와 동일하다: 모든 빌드 진입점이 가장 먼저 호출하는
    /// WebGLBuildCopier.PrepareAitBuildFolder의 정리 대상 제외 목록(itemsToKeep)에
    /// node_modules가 이미 포함돼 있어 마커가 자연히 생존하고, Package.NodeModulesValidator.
    /// CleanNodeModules가 node_modules를 통째로 지우는 재시도 정책과도 자동으로 정합된다
    /// (node_modules가 사라지면 마커도 함께 사라져 fail-closed).
    ///
    /// 성공한 빌드 직후 (1) ait-build/public 트리 상태(경로·크기·mtime), (2) ait-build 설정
    /// 파일들의 내용 해시, (3) UNITY_METADATA 중 .ait 헤더에 반영되는 필드의 해시를
    /// node_modules/.ait-package-build-state.json 에 기록하고, 다음 빌드에서 전부 일치하며
    /// 산출물(.ait)이 여전히 존재하면 vite/ait build 자체를 건너뛴다. .ait의 위치는
    /// ait build CLI 버전에 따라 ait-build 루트(2.x·3.x 현행) 또는 dist/이므로
    /// <see cref="AITBuildValidator.ValidateDistOutput"/>과 동일하게 두 곳을 모두 본다.
    ///
    /// public/ 트리는 내용을 다시 읽지 않고 (경로, 길이, mtimeTicks)만 본다 — WebGLBuildCopier.
    /// CopyFileIfChanged가 내용이 같은 파일은 mtime을 보존한 채 복사를 스킵하므로, mtime이
    /// 바뀌지 않았다는 것은 곧 내용이 바뀌지 않았다는 뜻이다(대용량 .data/.wasm를 매번 전부
    /// 읽지 않아도 되는 이유). 반대로 mtime은 "변경 감지 시에만" 전진하므로(동일 내용으로
    /// 되돌려도 재복사되며 mtime이 갱신됨), 과거 상태로 우연히 되돌아가 스킵이 잘못
    /// 재활성화되는 시나리오는 구조적으로 발생하지 않는다.
    ///
    /// ait-build/index.html은 public/ 밖(프로젝트 루트)에 있고 WebGLBuildCopier가 매 빌드
    /// 무조건 File.WriteAllText로 다시 쓰기 때문에(mtime이 항상 전진) 위 mtime 트릭을 쓸 수
    /// 없다 — 대신 설정 파일들과 함께 "내용" 해시로 configFilesHash에 포함시킨다. index.html은
    /// PlayerSettings/Configuration/BuildProfile 값(표시 이름·아이콘·색상·디버그 콘솔 여부 등)이
    /// 반영된 최종 산출물이므로, 이 값들을 개별적으로 해시 입력에 나열하지 않고도 index.html
    /// 내용 해시 하나로 그 변경들을 함께 커버한다.
    ///
    /// 판단이 불가능한 모든 케이스(마커 없음, 파싱 실패, 예외)는 "스킵 불가"로 처리한다
    /// (fail-closed) — 잘못된 스킵(스테일 .ait 배포)의 비용이 잘못된 재빌드(시간 낭비)보다
    /// 훨씬 크기 때문.
    ///
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서
    /// 접근됩니다.
    /// </summary>
    internal static class PackageBuildStateMarker
    {
        internal const string MarkerFileName = ".ait-package-build-state.json";
        internal const int SchemaVersion = 1;

        /// <summary>
        /// 스킵 기능 강제 비활성화 환경변수. 배포 후 예기치 못한 문제 시 코드 수정 없이
        /// 되돌리는 킬스위치.
        /// </summary>
        internal const string KillSwitchEnvVar = "AIT_DISABLE_PACKAGE_BUILD_SKIP";

        /// <summary>
        /// ait-build/ 직하에서 내용 해시로 추적하는 설정/스크립트/엔트리 파일 목록.
        /// package.json·pnpm-lock.yaml·pnpm-workspace.yaml은 설치될 web-framework
        /// 버전(및 ait-patch-cli.mjs가 패치할 cli.js)을 고정하고, vite/granite/apps-in-toss
        /// config·tsconfig는 빌드 동작 자체를 바꾸며, unity-bridge.ts는 번들에 포함되는 코드,
        /// ait-patch-cli.mjs는 빌드 직전 실행되는 패치 스크립트, index.html은 위 클래스 주석의
        /// 이유로 여기 포함된다. appName/version 등 Configuration 값은 이 파일들(특히
        /// apps-in-toss.config.ts/index.html) 생성 결과에 반영되므로 별도 입력 없이
        /// 자동으로 커버된다.
        /// </summary>
        private static readonly string[] TrackedRootFiles =
        {
            "package.json",
            "pnpm-lock.yaml",
            "pnpm-workspace.yaml",
            "vite.config.ts",
            "granite.config.ts",
            "apps-in-toss.config.ts",
            "tsconfig.json",
            "unity-bridge.ts",
            "ait-patch-cli.mjs",
            "index.html",
        };

        /// <summary>
        /// ait-build/src/ 하위 전체 파일도 추적 대상이다(현재 템플릿은 이 디렉토리를 만들지
        /// 않지만, 향후 web-framework 템플릿이 src/ 구조로 바뀌더라도 자동으로 커버되도록
        /// 존재 시에만 반영한다 — 부재는 해시에 영향을 주지 않는 순수 no-op).
        /// </summary>
        private const string TrackedSrcDirName = "src";

        /// <summary>
        /// 킬스위치 값 해석. "1"/"true"는 활성, "0"/"false"/미설정은 비활성 (대소문자·양끝
        /// 공백 무시). 그 외 값은 운영자가 스킵을 끄려던 의도로 보고 경고 로그 후 활성으로
        /// 처리한다 — 킬스위치의 존재 목적상 오타로 무력화되는 것보다 스킵이 꺼지는 쪽이
        /// 항상 안전하다 (fail-safe).
        /// </summary>
        internal static bool IsKillSwitchActive(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (normalized == "1" || normalized.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (normalized == "0" || normalized.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Debug.LogWarning(
                $"[AIT] {KillSwitchEnvVar}='{value}' 값을 인식할 수 없어 킬스위치 활성(스킵 비활성화)으로 처리합니다. " +
                "인식되는 값: 1/true (활성), 0/false (비활성).");
            return true;
        }

        internal static string GetMarkerPath(string aitBuildPath)
        {
            return Path.Combine(aitBuildPath, "node_modules", MarkerFileName);
        }

        /// <summary>
        /// 이번 빌드에서 vite/ait build(패키징)를 건너뛰어도 안전한지 판단한다.
        /// 킬스위치 비활성 + 마커 유효 + 스키마 일치 + web-framework 메이저 버전 일치 +
        /// public 매니페스트/설정 파일/Unity 메타데이터 해시 일치 + .ait 산출물(ait-build 루트
        /// 또는 dist/) 존재를 전부 만족할 때만 true. 호출부는 fastBuild(Deploy (Test) 등 빠른
        /// 반복 경로)에서만 이 메서드를 호출해야 한다 — Production 배포는 항상 전량 재빌드한다
        /// (안전 우선). 거절 시에는 사유를 Debug.Log로 남긴다 (조용한 fail-closed 금지).
        /// </summary>
        internal static bool ShouldSkipPackageBuild(string aitBuildPath, out string reason)
        {
            reason = null;

            try
            {
                string blocker = FindSkipBlocker(aitBuildPath);
                if (blocker != null)
                {
                    // 거절 사유를 항상 남긴다 — fail-closed 분기가 조용히 false를 반환하면
                    // "스킵이 왜 안 걸리는지" 를 로그만으로 진단할 수 없어(실제로 .ait 위치
                    // 규약 불일치가 이 방식으로 장기간 은폐됐다) 회귀를 놓치게 된다.
                    Debug.Log($"[AIT] 패키징 스킵 조건 불충족 — vite/ait build를 실행합니다 ({blocker})");
                    return false;
                }

                reason = "ait-build/public + 설정 파일 + Unity 메타데이터 변경 없음 + .ait 산출물 존재";
                return true;
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] 패키징 빌드 스킵 판정 중 오류 (스킵하지 않고 vite/ait build 진행): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 스킵을 막는 첫 번째 사유를 사람이 읽을 수 있는 문자열로 반환한다. 스킵 가능하면 null.
        /// <see cref="ShouldSkipPackageBuild"/>의 fail-closed 분기를 진단 가능한 형태로 분리한 것.
        /// </summary>
        private static string FindSkipBlocker(string aitBuildPath)
        {
            if (IsKillSwitchActive(Environment.GetEnvironmentVariable(KillSwitchEnvVar)))
            {
                return $"킬스위치 {KillSwitchEnvVar} 활성";
            }

            string markerPath = GetMarkerPath(aitBuildPath);
            if (!File.Exists(markerPath))
            {
                return "이전 성공 빌드 마커 없음";
            }

            var marker = MiniJson.Deserialize(File.ReadAllText(markerPath)) as Dictionary<string, object>;
            if (marker == null)
            {
                return "마커 파싱 실패";
            }

            if (!marker.TryGetValue("schemaVersion", out object schemaObj)
                || Convert.ToInt32(schemaObj) != SchemaVersion)
            {
                return "마커 schemaVersion 불일치";
            }

            int currentMajor = GraniteBuildRunner.GetWebFrameworkMajor(aitBuildPath);
            if (!marker.TryGetValue("webFrameworkMajor", out object majorObj)
                || Convert.ToInt32(majorObj) != currentMajor)
            {
                return $"web-framework 메이저 버전 변경 (현재 {currentMajor})";
            }

            if (!marker.TryGetValue("publicManifestHash", out object publicHashObj)
                || (publicHashObj as string) != ComputePublicManifestHash(aitBuildPath))
            {
                return "ait-build/public 변경";
            }

            if (!marker.TryGetValue("configFilesHash", out object configHashObj)
                || (configHashObj as string) != ComputeConfigFilesHash(aitBuildPath))
            {
                return "빌드 설정 파일(index.html 포함) 변경";
            }

            if (!marker.TryGetValue("metadataHash", out object metadataHashObj)
                || (metadataHashObj as string) != ComputeMetadataHash())
            {
                return "Unity 메타데이터(sdkVersion/unityVersion 등) 변경";
            }

            // 해시가 전부 일치해도 산출물이 사라졌으면(수동 삭제, Clean 메뉴 등) 스킵 불가.
            // AITBuildValidator.ValidateDistOutput은 실패 시 진단용 Debug.LogError를 남겨
            // (Sentry로도 캡처됨) 성공으로 끝날 스킵 판정에서 유령 에러가 발생하므로 여기서는
            // 호출하지 않고, 로그를 남기지 않는 조용한 존재 확인만 한다.
            if (!HasAitArchive(aitBuildPath))
            {
                return ".ait 산출물 없음 (ait-build 루트/dist 모두)";
            }

            return null;
        }

        /// <summary>
        /// 패키징 산출물(.ait)이 존재하는지 조용히 확인한다. 탐색 위치와 순서는
        /// <see cref="AITBuildValidator.ValidateDistOutput"/>과 동일해야 한다 — ait build CLI는
        /// 버전에 따라 ait-build 루트(2.x, 3.x 현행) 또는 dist/에 .ait를 emit하기 때문이다.
        /// 여기서 dist/만 보면 실제 산출물이 루트에 있는 현행 3.x에서는 스킵이 영원히 발동하지
        /// 않는다 (#1094가 실측에서 걸리지 않은 원인).
        /// </summary>
        private static bool HasAitArchive(string aitBuildPath)
        {
            if (ContainsAitArchive(aitBuildPath))
            {
                return true;
            }

            return ContainsAitArchive(Path.Combine(aitBuildPath, "dist"));
        }

        private static bool ContainsAitArchive(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }

            // Windows의 8.3 단축명 때문에 "*.ait" glob이 .aitxxx까지 잡을 수 있어
            // 확장자를 명시적으로 비교한다.
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetExtension(path), WebGLBuildCopier.AitArchiveExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 실제 vite/ait build를 시작하기 직전에 호출한다. 이번 빌드가 성공하기 전까지는
        /// (즉 <see cref="RecordSuccessfulBuild"/>가 새로 쓰기 전까지는) 마커를 무효화해,
        /// 빌드가 중간에 실패해 dist/가 일관되지 않은 상태로 남더라도 다음 판정이 스테일
        /// 마커로 잘못 스킵하지 않도록 한다. 실패해도(예: 파일 잠금) 조용히 무시한다 —
        /// 이 호출이 실패해도 ShouldSkipPackageBuild의 해시/산출물 비교 자체가 이미
        /// fail-closed이므로 안전성이 이 호출 하나에 의존하지 않는다.
        /// </summary>
        internal static void InvalidateMarker(string aitBuildPath)
        {
            try
            {
                string markerPath = GetMarkerPath(aitBuildPath);
                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] 패키징 상태 마커 무효화 실패 (무시됨): {e.Message}");
            }
        }

        /// <summary>
        /// 성공한 vite/ait build 직후(dist 산출물 검증까지 통과한 뒤) 현재 상태를 마커에
        /// 기록한다. 기록 실패는 기능 저하가 아니라 "다음 빌드도 그냥 재실행"일 뿐이므로
        /// 모든 예외를 흡수한다.
        /// </summary>
        internal static void RecordSuccessfulBuild(string aitBuildPath, int webFrameworkMajor)
        {
            try
            {
                var marker = new Dictionary<string, object>
                {
                    { "schemaVersion", SchemaVersion },
                    { "publicManifestHash", ComputePublicManifestHash(aitBuildPath) },
                    { "configFilesHash", ComputeConfigFilesHash(aitBuildPath) },
                    { "metadataHash", ComputeMetadataHash() },
                    { "webFrameworkMajor", webFrameworkMajor },
                    { "lastBuildSucceededUtc", DateTime.UtcNow.ToString("o") },
                };

                string markerPath = GetMarkerPath(aitBuildPath);
                string tempPath = markerPath + ".tmp";
                File.WriteAllText(tempPath, MiniJson.Serialize(marker), new UTF8Encoding(false));
                if (File.Exists(markerPath)) File.Delete(markerPath);
                File.Move(tempPath, markerPath);
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] 패키징 상태 마커 기록 실패 (무시됨 — 다음 빌드에서 vite/ait build 재실행): {e.Message}");
            }
        }

        /// <summary>
        /// ait-build/public 트리를 재귀 순회해 각 파일의 (상대경로, 길이, mtimeTicks)를
        /// 상대경로 오름차순으로 정렬한 목록의 SHA256. 내용을 읽지 않으므로 대용량
        /// .data/.wasm가 섞여 있어도 저렴하다. public/이 없으면 빈 매니페스트로 취급한다
        /// (WebGL 출력 복사가 아직 실행되지 않은 비정상 상태 — .ait 산출물 존재 검사가
        /// 별도로 막는다).
        /// </summary>
        internal static string ComputePublicManifestHash(string aitBuildPath)
        {
            string publicPath = Path.Combine(aitBuildPath, "public");
            var sb = new StringBuilder();

            if (Directory.Exists(publicPath))
            {
                var entries = new List<(string relPath, long length, long mtimeTicks)>();
                foreach (var filePath in Directory.EnumerateFiles(publicPath, "*", SearchOption.AllDirectories))
                {
                    string relPath = NormalizeRelativePath(publicPath, filePath);
                    var info = new FileInfo(filePath);
                    entries.Add((relPath, info.Length, info.LastWriteTimeUtc.Ticks));
                }

                entries.Sort((a, b) => string.CompareOrdinal(a.relPath, b.relPath));

                foreach (var entry in entries)
                {
                    sb.Append(entry.relPath).Append('|').Append(entry.length).Append('|').Append(entry.mtimeTicks).Append('\n');
                }
            }

            return ComputeStringHash(sb.ToString());
        }

        /// <summary>
        /// <see cref="TrackedRootFiles"/> + (존재하면) ait-build/src/ 하위 전체 파일의 "내용"
        /// SHA256을 상대경로 오름차순으로 결합한 뒤 다시 SHA256. 파일이 없으면 내용 대신
        /// "absent" 문자열을 써서 존재→부재 전환도 해시 변화로 감지한다.
        /// </summary>
        internal static string ComputeConfigFilesHash(string aitBuildPath)
        {
            var entries = new List<(string relPath, string contentHash)>();

            foreach (var relPath in TrackedRootFiles)
            {
                string fullPath = Path.Combine(aitBuildPath, relPath);
                entries.Add((relPath, File.Exists(fullPath) ? ComputeFileContentHash(fullPath) : "absent"));
            }

            string srcDir = Path.Combine(aitBuildPath, TrackedSrcDirName);
            if (Directory.Exists(srcDir))
            {
                foreach (var filePath in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
                {
                    string relPath = TrackedSrcDirName + "/" + NormalizeRelativePath(srcDir, filePath);
                    entries.Add((relPath, ComputeFileContentHash(filePath)));
                }
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.relPath, b.relPath));

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.Append(entry.relPath).Append(':').Append(entry.contentHash).Append('\n');
            }

            return ComputeStringHash(sb.ToString());
        }

        /// <summary>
        /// UNITY_METADATA 중 .ait 헤더에 그대로 반영되는 필드(sdkVersion, sdkCommitHash,
        /// unityVersion 등 — buildTimestamp 제외)의 해시. public/과 설정 파일 내용이 우연히
        /// 이전 빌드와 동일해도, SDK를 업데이트했거나 Unity 버전이 바뀌었다면 스킵되지 않도록
        /// 막는다. buildTimestamp는 매 빌드마다 항상 바뀌므로 포함하면 스킵이 영원히 발동하지
        /// 않게 되어 <see cref="AITUnityMetadata.BuildContentMetadataJson"/>에서 제외한다.
        /// </summary>
        internal static string ComputeMetadataHash()
        {
            return ComputeStringHash(AITUnityMetadata.BuildContentMetadataJson());
        }

        /// <summary>
        /// basePath 기준 상대경로를 OS 무관하게 '/' 구분자로 정규화한다.
        /// </summary>
        private static string NormalizeRelativePath(string basePath, string fullPath)
        {
            string relative = fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        /// <summary>
        /// 파일 내용의 SHA256 해시 (소문자 hex, 접두사 없음 — 내부 결합용 원시 값).
        /// </summary>
        private static string ComputeFileContentHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                return ToHex(sha256.ComputeHash(stream));
            }
        }

        /// <summary>
        /// 문자열의 SHA256 해시 ("sha256:" 접두사 + 소문자 hex) — 마커에 저장되는 최종 값 형식.
        /// </summary>
        private static string ComputeStringHash(string content)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
                return "sha256:" + ToHex(hash);
            }
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
