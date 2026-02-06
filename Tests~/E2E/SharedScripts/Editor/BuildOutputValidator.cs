// -----------------------------------------------------------------------
// BuildOutputValidator.cs - 빌드 산출물 검증
// Level 1: WebGL 빌드 후 C# 레벨에서 산출물 구조를 검증
// build-validation.json을 생성하여 Playwright에서 재사용
// -----------------------------------------------------------------------

using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class BuildOutputValidator
{
    [Serializable]
    public class ValidationResult
    {
        public bool passed;
        public float buildSizeMB;
        public string compressionFormat;
        public int fileCount;
        public string[] errors;
        public string[] warnings;
        public FileDetail[] files;

        // Asset Streaming 관련 필드
        public bool hasStreamingAssets;
        public int streamingAssetBundleCount;
        public float streamingAssetsSizeMB;
        public float dataSizeMB;
    }

    [Serializable]
    public class FileDetail
    {
        public string name;
        public long sizeBytes;
        public string type;
    }

    /// <summary>
    /// 빌드 산출물 전체 검증
    /// </summary>
    public static ValidationResult ValidateAll(string projectPath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var files = new List<FileDetail>();

        string aitBuildPath = Path.Combine(projectPath, "ait-build");
        string distWebPath = Path.Combine(aitBuildPath, "dist", "web");
        string distBuildPath = Path.Combine(distWebPath, "Build");

        // 1. ait-build/ 디렉토리 존재 확인
        if (!Directory.Exists(aitBuildPath))
        {
            errors.Add("ait-build/ directory not found");
            return BuildResult(false, 0, "unknown", 0, errors, warnings, files);
        }

        // 2. package.json 존재 확인
        string packageJsonPath = Path.Combine(aitBuildPath, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            errors.Add("ait-build/package.json not found");
        }

        // 3. granite.config.ts 존재 + 플레이스홀더 확인
        string graniteConfigPath = Path.Combine(aitBuildPath, "granite.config.ts");
        if (File.Exists(graniteConfigPath))
        {
            string content = File.ReadAllText(graniteConfigPath);
            var placeholders = CheckForPlaceholders(content);
            if (placeholders.Count > 0)
            {
                errors.Add($"granite.config.ts has unsubstituted placeholders: {string.Join(", ", placeholders)}");
            }
        }
        else
        {
            warnings.Add("granite.config.ts not found");
        }

        // 4. node_modules/ 존재 확인
        string nodeModulesPath = Path.Combine(aitBuildPath, "node_modules");
        if (!Directory.Exists(nodeModulesPath))
        {
            errors.Add("ait-build/node_modules/ not found (npm install may have failed)");
        }

        // 5. dist/web/ 존재 확인
        if (!Directory.Exists(distWebPath))
        {
            errors.Add("ait-build/dist/web/ not found");
            return BuildResult(false, 0, "unknown", 0, errors, warnings, files);
        }

        // 6. dist/web/Build/ 존재 확인
        if (!Directory.Exists(distBuildPath))
        {
            errors.Add("ait-build/dist/web/Build/ not found");
            return BuildResult(false, 0, "unknown", 0, errors, warnings, files);
        }

        // 7. index.html 존재 + 플레이스홀더 확인
        string indexPath = Path.Combine(distWebPath, "index.html");
        if (File.Exists(indexPath))
        {
            string content = File.ReadAllText(indexPath);
            var placeholders = CheckForPlaceholders(content);
            if (placeholders.Count > 0)
            {
                errors.Add($"index.html has unsubstituted placeholders: {string.Join(", ", placeholders)}");
            }
        }
        else
        {
            errors.Add("dist/web/index.html not found");
        }

        // 8. 필수 빌드 파일 확인
        string[] buildFiles = Directory.GetFiles(distBuildPath);
        bool hasLoader = false;
        bool hasWasm = false;
        bool hasData = false;
        string compressionFormat = "unknown";

        foreach (string file in buildFiles)
        {
            string fileName = Path.GetFileName(file);
            var info = new FileInfo(file);

            files.Add(new FileDetail
            {
                name = fileName,
                sizeBytes = info.Length,
                type = DetectFileType(fileName)
            });

            if (fileName.EndsWith(".loader.js")) hasLoader = true;

            if (fileName.Contains(".wasm"))
            {
                hasWasm = true;
                compressionFormat = DetectCompression(fileName);
            }

            if (fileName.Contains(".data"))
            {
                hasData = true;
            }
        }

        if (!hasLoader) errors.Add("Missing loader.js in Build/");
        if (!hasWasm) errors.Add("Missing .wasm file in Build/");
        if (!hasData) errors.Add("Missing .data file in Build/");

        // 9. 빌드 크기 계산
        float buildSizeMB = GetDirectorySizeMB(distWebPath);
        int fileCount = files.Count;

        // 10. .data 파일 크기 측정
        float dataSizeMB = 0f;
        foreach (var file in files)
        {
            if (file.type == "data")
            {
                dataSizeMB += file.sizeBytes / (1024f * 1024f);
            }
        }

        // 11. StreamingAssets 검증
        bool hasStreamingAssets = false;
        int streamingAssetBundleCount = 0;
        float streamingAssetsSizeMB = 0f;

        string streamingAssetsPath = Path.Combine(distWebPath, "StreamingAssets");
        if (Directory.Exists(streamingAssetsPath))
        {
            hasStreamingAssets = true;
            string[] bundleFiles = Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories);
            foreach (string bundleFile in bundleFiles)
            {
                string ext = Path.GetExtension(bundleFile).ToLower();
                // .meta 파일 등 제외, 번들 파일만 카운트
                if (ext != ".meta" && ext != ".json")
                {
                    streamingAssetBundleCount++;
                    streamingAssetsSizeMB += new FileInfo(bundleFile).Length / (1024f * 1024f);
                }
            }
            Debug.Log($"[BuildOutputValidator] StreamingAssets: {streamingAssetBundleCount} bundles, {streamingAssetsSizeMB:F2} MB");
        }

        bool passed = errors.Count == 0;
        var result = BuildResult(passed, buildSizeMB, compressionFormat, fileCount, errors, warnings, files);
        result.hasStreamingAssets = hasStreamingAssets;
        result.streamingAssetBundleCount = streamingAssetBundleCount;
        result.streamingAssetsSizeMB = streamingAssetsSizeMB;
        result.dataSizeMB = dataSizeMB;
        return result;
    }

    private static ValidationResult BuildResult(bool passed, float buildSizeMB, string compressionFormat,
        int fileCount, List<string> errors, List<string> warnings, List<FileDetail> files)
    {
        return new ValidationResult
        {
            passed = passed,
            buildSizeMB = buildSizeMB,
            compressionFormat = compressionFormat,
            fileCount = fileCount,
            errors = errors.ToArray(),
            warnings = warnings.ToArray(),
            files = files.ToArray()
        };
    }

    /// <summary>
    /// 플레이스홀더 패턴 검사
    /// </summary>
    public static List<string> CheckForPlaceholders(string content)
    {
        var found = new List<string>();
        var patterns = new[] { @"%UNITY_[A-Z_]+%", @"%AIT_[A-Z_]+%" };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(content, pattern))
            {
                if (!found.Contains(match.Value))
                {
                    found.Add(match.Value);
                }
            }
        }

        return found;
    }

    private static string DetectCompression(string fileName)
    {
        if (fileName.EndsWith(".br")) return "brotli";
        if (fileName.EndsWith(".gz")) return "gzip";
        if (fileName.EndsWith(".unityweb")) return "unityweb";
        return "disabled";
    }

    private static string DetectFileType(string fileName)
    {
        if (fileName.Contains(".wasm")) return "wasm";
        if (fileName.Contains(".data")) return "data";
        if (fileName.Contains(".framework.js")) return "framework";
        if (fileName.EndsWith(".loader.js")) return "loader";
        return "other";
    }

    private static float GetDirectorySizeMB(string dirPath)
    {
        long totalSize = 0;
        try
        {
            foreach (string file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                totalSize += new FileInfo(file).Length;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildOutputValidator] Failed to calculate directory size: {e.Message}");
        }
        return totalSize / (1024f * 1024f);
    }
}
