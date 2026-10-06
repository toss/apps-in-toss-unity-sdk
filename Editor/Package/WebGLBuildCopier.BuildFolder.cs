using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    internal static partial class WebGLBuildCopier
    {
        /// <summary>
        /// ait-build 폴더 준비 (기존 결과물 정리).
        /// node_modules/설정 파일과 public/(정확히는 <see cref="MirroredPublicDirectories"/>)은 유지하고
        /// 나머지 이전 결과물(dist, index.html 등)을 지운다. public/을 통째로 지우면 매 빌드마다
        /// 수백 MB의 Unity 산출물을 다시 복사해야 해 CopyWebGLToPublic의 변경분 미러 복사가 항상
        /// 무효화되므로, 미러 대상만 남기고 그 외 public/ 항목은 <see cref="PrunePublicFolder"/>가 정리한다.
        /// internal 승격: facade(AITPackageBuilder) 및 EditMode 테스트(리플렉션)에서 호출하기 위함.
        /// </summary>
        /// <param name="preservePackagingOutputs">
        /// true면 이전 패키징 산출물(dist/ 와 ait-build 루트의 *.ait)을 정리 대상에서 제외해
        /// 보존한다. fastBuild(Deploy (Test)) 경로에서만 true로 전달해야 한다 —
        /// Package.PackageBuildStateMarker.ShouldSkipPackageBuild가 .ait 산출물 존재를 스킵
        /// 조건으로 검사하는데, 이 판정 전에 산출물이 지워지면 스킵이 절대 발동하지 않는다
        /// (모든 빌드 진입점이 이 함수를 가장 먼저 호출하기 때문).
        ///
        /// ⚠️ 루트 *.ait도 함께 보존해야 한다: web-framework 3.x의 `ait build`는 .ait를 dist/가
        /// 아니라 ait-build 루트에 emit하고(dist/에는 vite 산출물인 dist/web만 생성됨), 2.x도
        /// 루트에 emit한다. dist/만 보존하면 실제 산출물인 루트 *.ait가 여기서 지워져 스킵이
        /// 영원히 발동하지 않는다 (AITBuildValidator.ValidateDistOutput의 "루트 → dist/" 탐색
        /// 순서와 동일한 위치 규약).
        ///
        /// 스킵 판정이 결국 false로 나오면 호출부가 <see cref="DeletePreviousPackagingOutputs"/>로
        /// 실제 빌드 시작 직전 dist/와 루트 *.ait를 명시적으로 삭제해 "빌드는 항상 빈 산출물
        /// 상태에서 시작한다" 불변식을 복원한다. 기본값(false)은 Production/Build &amp; Package
        /// 경로의 기존 동작과 동일 — 산출물은 항상 이 함수에서 정리된다.
        /// </param>
        internal static void PrepareAitBuildFolder(string buildProjectPath, bool preservePackagingOutputs = false)
        {
            if (!Directory.Exists(buildProjectPath))
            {
                Directory.CreateDirectory(buildProjectPath);
                Debug.Log("[AIT] ait-build 폴더 생성");
            }
            else
            {
                Debug.Log("[AIT] 기존 빌드 결과물 정리 중... (node_modules·설정 파일과 public/ 미러 대상은 유지)");

                var itemsToKeep = new List<string>
                {
                    "node_modules",
                    "package.json",
                    "package-lock.json",
                    "pnpm-lock.yaml",
                    "pnpm-workspace.yaml",
                    "granite.config.ts",
                    "apps-in-toss.config.ts",
                    "vite.config.ts",
                    "tsconfig.json",
                    // public/은 CopyWebGLToPublic이 미러로 관리한다 — 여기서 통째로 지우면
                    // 변경분 복사가 매번 전량 복사로 퇴화한다. 미러 대상 밖 항목은 아래 PrunePublicFolder가 정리.
                    "public"
                };

                if (preservePackagingOutputs)
                {
                    itemsToKeep.Add("dist");
                }

                foreach (string item in Directory.GetFileSystemEntries(buildProjectPath))
                {
                    string itemName = Path.GetFileName(item);

                    bool shouldKeep = false;
                    foreach (string keepItem in itemsToKeep)
                    {
                        if (itemName == keepItem)
                        {
                            shouldKeep = true;
                            break;
                        }
                    }

                    // 루트 *.ait는 web-framework 2.x/3.x가 실제로 .ait를 emit하는 위치라
                    // itemsToKeep(고정 이름 매칭)으로는 표현할 수 없다 — 파일명이 appName에
                    // 따라 달라지므로 확장자로 판별한다.
                    if (!shouldKeep && preservePackagingOutputs && IsAitArchive(item))
                    {
                        shouldKeep = true;
                    }

                    if (shouldKeep) continue;

                    // SafeDelete/DeleteDirectory는 예외를 던지지 않고 실패 시 내부에서 경고 로그를 남김
                    if (Directory.Exists(item))
                    {
                        AITFileUtils.DeleteDirectory(item);
                    }
                    else if (File.Exists(item))
                    {
                        AITFileSystemHelper.SafeDelete(item);
                    }
                }

                PrunePublicFolder(Path.Combine(buildProjectPath, "public"));
            }
        }

        /// <summary>
        /// 이전 패키징 산출물(ait-build/dist 와 ait-build 루트의 *.ait)을 삭제한다.
        /// <see cref="PrepareAitBuildFolder"/>(preservePackagingOutputs: true)가 보존해둔 이전
        /// 산출물을, Package.PackageBuildStateMarker.ShouldSkipPackageBuild 판정이 결국 false로
        /// 나와 실제 vite/ait build를 새로 시작하기 직전에 호출해 지운다 — "빌드는 항상 빈
        /// 산출물 상태에서 시작한다" 불변식을 복원해 옛 .ait와 새 .ait가 공존하는 것을
        /// 막는다(예: appName/version 변경으로 산출물 파일명이 달라지는 경우).
        /// preservePackagingOutputs:false 경로에서는 PrepareAitBuildFolder가 이미 둘 다
        /// 지웠으므로 이 호출은 언제나 안전한 no-op이다.
        /// </summary>
        internal static void DeletePreviousPackagingOutputs(string buildProjectPath)
        {
            string distPath = Path.Combine(buildProjectPath, "dist");
            if (Directory.Exists(distPath))
            {
                AITFileUtils.DeleteDirectory(distPath);
            }

            if (!Directory.Exists(buildProjectPath)) return;

            foreach (string item in Directory.GetFiles(buildProjectPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsAitArchive(item))
                {
                    AITFileSystemHelper.SafeDelete(item);
                }
            }
        }

        /// <summary>
        /// ait-build 루트에 emit된 패키징 산출물(.ait) 여부. 파일명은 appName에 따라 달라지므로
        /// 확장자로만 판별한다. Windows의 8.3 단축명 때문에 <c>Directory.GetFiles(dir, "*.ait")</c>가
        /// .aitxxx 같은 더 긴 확장자까지 잡을 수 있어, 열거는 "*"로 하고 여기서 정확히 검사한다.
        /// </summary>
        private static bool IsAitArchive(string path)
        {
            return File.Exists(path)
                && string.Equals(Path.GetExtension(path), AitArchiveExtension, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// public/에서 미러 대상(<see cref="MirroredPublicDirectories"/>)만 남기고 나머지를 지운다.
        /// 미러 대상은 CopyWebGLToPublic이 변경분 복사 + stale 제거로 최종 상태를 보장하므로 보존해도
        /// 잔여물이 남지 않는다. 그 외 항목(사용자 BuildConfig~의 public/ 파일 등)은 매 빌드 다시
        /// 복사되므로 예전처럼 여기서 지워야 소스에서 삭제된 파일이 계속 서빙되지 않는다.
        /// </summary>
        private static void PrunePublicFolder(string publicPath)
        {
            if (!Directory.Exists(publicPath)) return;

            var mirrored = new HashSet<string>(MirroredPublicDirectories, System.StringComparer.Ordinal);

            foreach (string item in Directory.GetFileSystemEntries(publicPath))
            {
                string itemName = Path.GetFileName(item);

                if (Directory.Exists(item))
                {
                    if (mirrored.Contains(itemName)) continue;
                    AITFileUtils.DeleteDirectory(item);
                }
                else if (File.Exists(item))
                {
                    AITFileSystemHelper.SafeDelete(item);
                }
            }
        }
    }
}
