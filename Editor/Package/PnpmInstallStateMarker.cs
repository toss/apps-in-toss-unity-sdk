using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// pnpm install 결과 상태 마커 — 재빌드 시 불필요한 install 재실행을 건너뛰기 위한 판단 근거.
    /// 성공한 install 직후 package.json/pnpm-lock.yaml 내용 해시와 pnpm 버전을
    /// node_modules/.ait-install-state.json 에 기록하고, 다음 빌드에서 전부 일치하면 install을 스킵한다.
    ///
    /// 마커를 node_modules 내부에 두는 이유: NodeModulesValidator.CleanNodeModules가 node_modules를
    /// 통째로 삭제하므로 마커도 자동으로 함께 무효화된다 (재시도 정책의 clean 단계와 자동 정합).
    ///
    /// 판단이 불가능한 모든 케이스(마커 없음, 파싱 실패, 예외)는 "스킵 불가"로 처리한다 (fail-closed) —
    /// 잘못된 스킵(빌드 실패)의 비용이 잘못된 재설치(시간 낭비)보다 크기 때문.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class PnpmInstallStateMarker
    {
        internal const string MarkerFileName = ".ait-install-state.json";
        internal const int SchemaVersion = 1;

        /// <summary>
        /// 스킵 기능 강제 비활성화 환경변수. 배포 후 예기치 못한 문제 시 코드 수정 없이 되돌리는 킬스위치.
        /// </summary>
        internal const string KillSwitchEnvVar = "AIT_DISABLE_INSTALL_SKIP";

        /// <summary>
        /// 킬스위치 값 해석. "1"/"true"는 활성, "0"/"false"/미설정은 비활성 (대소문자·양끝 공백 무시).
        /// 그 외 값은 운영자가 스킵을 끄려던 의도로 보고 경고 로그 후 활성으로 처리한다 —
        /// 킬스위치의 존재 목적상 오타로 무력화되는 것보다 스킵이 꺼지는 쪽이 항상 안전하다 (fail-safe).
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

        internal static string GetMarkerPath(string buildProjectPath)
        {
            return Path.Combine(buildProjectPath, "node_modules", MarkerFileName);
        }

        /// <summary>
        /// 이번 빌드에서 pnpm install을 건너뛰어도 안전한지 판단한다.
        /// node_modules 존재 + 마커 유효 + 스키마/pnpm 버전 일치 + package.json·lockfile 해시 일치 +
        /// NodeModulesValidator 무결성 통과를 전부 만족할 때만 true.
        /// </summary>
        internal static bool ShouldSkipInstall(string buildProjectPath, out string reason)
        {
            reason = null;

            try
            {
                if (IsKillSwitchActive(Environment.GetEnvironmentVariable(KillSwitchEnvVar)))
                {
                    return false;
                }

                if (!Directory.Exists(Path.Combine(buildProjectPath, "node_modules")))
                {
                    return false;
                }

                string packageJsonPath = Path.Combine(buildProjectPath, "package.json");
                string lockfilePath = Path.Combine(buildProjectPath, "pnpm-lock.yaml");
                if (!File.Exists(packageJsonPath) || !File.Exists(lockfilePath))
                {
                    return false;
                }

                string markerPath = GetMarkerPath(buildProjectPath);
                if (!File.Exists(markerPath))
                {
                    return false;
                }

                var marker = MiniJson.Deserialize(File.ReadAllText(markerPath)) as Dictionary<string, object>;
                if (marker == null)
                {
                    return false;
                }

                if (!marker.TryGetValue("schemaVersion", out object schemaObj)
                    || Convert.ToInt32(schemaObj) != SchemaVersion)
                {
                    return false;
                }

                if (!marker.TryGetValue("pnpmVersion", out object pnpmObj)
                    || (pnpmObj as string) != AITPackageManagerHelper.PNPM_VERSION)
                {
                    return false;
                }

                if (!marker.TryGetValue("packageJsonHash", out object pkgHashObj)
                    || (pkgHashObj as string) != ComputeFileHash(packageJsonPath))
                {
                    return false;
                }

                if (!marker.TryGetValue("lockfileHash", out object lockHashObj)
                    || (lockHashObj as string) != ComputeFileHash(lockfilePath))
                {
                    return false;
                }

                // 해시가 일치해도 node_modules 내용이 오염됐을 수 있다 (수동 삭제, 백신 격리 등) — 이중 게이트.
                if (!NodeModulesValidator.ValidateIntegrity(buildProjectPath))
                {
                    return false;
                }

                reason = $"package.json/pnpm-lock.yaml 변경 없음 + node_modules 무결성 확인 (pnpm {AITPackageManagerHelper.PNPM_VERSION})";
                return true;
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] install 스킵 판정 중 오류 (스킵하지 않고 install 진행): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 성공한 install 직후 현재 package.json/pnpm-lock.yaml 해시를 마커에 기록한다.
        /// 기록 실패는 기능 저하가 아니라 "다음 빌드도 그냥 재설치"일 뿐이므로 모든 예외를 흡수한다.
        /// </summary>
        internal static void WriteMarkerAfterSuccessfulInstall(string buildProjectPath)
        {
            try
            {
                string packageJsonPath = Path.Combine(buildProjectPath, "package.json");
                string lockfilePath = Path.Combine(buildProjectPath, "pnpm-lock.yaml");
                string nodeModulesPath = Path.Combine(buildProjectPath, "node_modules");

                if (!Directory.Exists(nodeModulesPath) || !File.Exists(packageJsonPath) || !File.Exists(lockfilePath))
                {
                    return;
                }

                var marker = new Dictionary<string, object>
                {
                    { "schemaVersion", SchemaVersion },
                    { "pnpmVersion", AITPackageManagerHelper.PNPM_VERSION },
                    { "packageJsonHash", ComputeFileHash(packageJsonPath) },
                    { "lockfileHash", ComputeFileHash(lockfilePath) },
                    { "installedAtUtc", DateTime.UtcNow.ToString("o") },
                };

                string markerPath = GetMarkerPath(buildProjectPath);
                string tempPath = markerPath + ".tmp";
                File.WriteAllText(tempPath, MiniJson.Serialize(marker), new UTF8Encoding(false));
                if (File.Exists(markerPath)) File.Delete(markerPath);
                File.Move(tempPath, markerPath);
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] install 상태 마커 기록 실패 (무시됨 — 다음 빌드에서 install 재실행): {e.Message}");
            }
        }

        /// <summary>
        /// 파일 내용의 SHA256 해시 ("sha256:" 접두사 + 소문자 hex).
        /// </summary>
        internal static string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var sb = new StringBuilder("sha256:", 7 + hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
