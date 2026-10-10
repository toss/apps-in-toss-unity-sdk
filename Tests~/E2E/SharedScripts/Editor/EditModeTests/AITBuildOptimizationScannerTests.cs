// -----------------------------------------------------------------------
// AITBuildOptimizationScannerTests.cs - Read/Write 활성 에셋 스캐너 검증
// Level 0: EstimateTextureCpuBytes / ClampToMaxTextureSize / IsReadableIssue 순수 계산
// Level 1: 임시 readable PNG·OBJ 를 만들어 ScanReadable 이 나열하고 기본 미선택·임계치 미만은 차단하지 않음을 확인,
//          EstimateReadableCpuBytes 가 WebGL 압축 포맷 오버라이드를 포맷 기준으로 계산함을 확인
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss.Editor;

[TestFixture]
public class AITBuildOptimizationScannerTests
{
    private const string TempFolder = "Assets/__AITScanTest";

    [TearDown]
    public void TearDown()
    {
        if (AssetDatabase.IsValidFolder(TempFolder))
        {
            AssetDatabase.DeleteAsset(TempFolder);
        }
        AssetDatabase.Refresh();
    }

    [Test]
    public void EstimateTextureCpuBytes_2048WithMips_About22Mb()
    {
        long bytes = AITBuildOptimizationScanner.EstimateTextureCpuBytes(2048, 2048, true);
        // 2048*2048*4*4/3 = 22,369,621 B (십진 MB 기준 22.37)
        Assert.AreEqual(22369621L, bytes);
        Assert.AreEqual(22.37, bytes / 1e6, 0.01);
    }

    [Test]
    public void EstimateTextureCpuBytes_1024x512NoMips_2Mb()
    {
        long bytes = AITBuildOptimizationScanner.EstimateTextureCpuBytes(1024, 512, false);
        // 1024*512*4 = 2 MiB
        Assert.AreEqual(2L * 1024 * 1024, bytes);
    }

    [Test]
    public void ClampToMaxTextureSize_ScalesLongestSideProportionally()
    {
        int w = 4096, h = 2048;
        AITBuildOptimizationScanner.ClampToMaxTextureSize(ref w, ref h, 1024);
        Assert.AreEqual(1024, w);
        Assert.AreEqual(512, h);
    }

    [Test]
    public void ClampToMaxTextureSize_SmallerThanMax_Unchanged()
    {
        int w = 512, h = 256;
        AITBuildOptimizationScanner.ClampToMaxTextureSize(ref w, ref h, 2048);
        Assert.AreEqual(512, w);
        Assert.AreEqual(256, h);
    }

    [Test]
    public void ScanReadable_ListsReadableTexture_UnselectedByDefault()
    {
        if (!AssetDatabase.IsValidFolder(TempFolder))
        {
            AssetDatabase.CreateFolder("Assets", "__AITScanTest");
        }

        string path = TempFolder + "/readable64.png";
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.SaveAndReimport();

        var issue = AITBuildOptimizationScanner.ScanReadable();

        Assert.AreEqual(OptimizationIssueType.ReadWriteEnabled, issue.type);
        Assert.IsFalse(issue.isSelected, "Read/Write 해제는 사용자 에셋을 수정하므로 기본 미선택이어야 한다");
        Assert.Contains(path, issue.assetPaths);
    }

    private static string CreateReadablePng(string name, int size)
    {
        if (!AssetDatabase.IsValidFolder(TempFolder))
        {
            AssetDatabase.CreateFolder("Assets", "__AITScanTest");
        }

        string path = TempFolder + "/" + name;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return path;
    }

    [Test]
    public void IsReadableIssue_ModelsAlone_DoNotBlock()
    {
        Assert.IsFalse(AITBuildOptimizationScanner.IsReadableIssue(0.0, 3), "모델만 있으면 빌드 차단 이슈가 아니다");
        Assert.IsFalse(AITBuildOptimizationScanner.IsReadableIssue(7.9, 100));
        Assert.IsTrue(AITBuildOptimizationScanner.IsReadableIssue(8.0, 0));
        Assert.IsTrue(AITBuildOptimizationScanner.IsReadableIssue(22.4, 5));
    }

    [Test]
    public void ScanReadable_SmallReadableTexture_IsAlreadyOptimalButStillListed()
    {
        string path = CreateReadablePng("readableSmall64.png", 64);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.SaveAndReimport();

        var issue = AITBuildOptimizationScanner.ScanReadable();

        Assert.Contains(path, issue.assetPaths);
        Assert.IsFalse(issue.isSelected);
        // 프로젝트에 이미 큰 readable 에셋이 있으면 이 단언은 의미가 없다.
        if (issue.assetPaths.Count != 1)
        {
            Assert.Inconclusive("테스트 프로젝트에 다른 Read/Write 에셋이 있어 단독 상태를 검증할 수 없음");
        }
        Assert.AreEqual(OptimizationStatus.AlreadyOptimal, issue.status, "64² 텍스처 하나는 8 MB 임계치보다 훨씬 작다");
    }

    [Test]
    public void ScanReadable_ReadableModelAlone_DoesNotRaiseIssue()
    {
        if (!AssetDatabase.IsValidFolder(TempFolder))
        {
            AssetDatabase.CreateFolder("Assets", "__AITScanTest");
        }

        string path = TempFolder + "/readableTri.obj";
        File.WriteAllText(path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        AssetDatabase.ImportAsset(path);

        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Assert.Inconclusive("OBJ 가 ModelImporter 로 임포트되지 않음");
        }
        importer.isReadable = true;
        importer.SaveAndReimport();

        var issue = AITBuildOptimizationScanner.ScanReadable();

        Assert.Contains(path, issue.assetPaths, "차단하지 않아도 목록에는 남아야 한다");
        if (issue.assetPaths.Count != 1)
        {
            Assert.Inconclusive("테스트 프로젝트에 다른 Read/Write 에셋이 있어 단독 상태를 검증할 수 없음");
        }
        Assert.AreEqual(OptimizationStatus.AlreadyOptimal, issue.status, "Read/Write 모델 1개만으로 빌드 차단 모달을 띄우면 안 된다");
        StringAssert.Contains("모델 1개", issue.description);
    }

    [Test]
    public void EstimateReadableCpuBytes_WebGLAstc6x6Override_UsesFormatNotRgba32()
    {
        string path = CreateReadablePng("astc2048.png", 2048);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.mipmapEnabled = true;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "WebGL",
            overridden = true,
            maxTextureSize = 2048,
            format = TextureImporterFormat.ASTC_6x6
        });
        importer.SaveAndReimport();

        importer = (TextureImporter)AssetImporter.GetAtPath(path);
        long bytes = AITBuildOptimizationScanner.EstimateReadableCpuBytes(importer, 2048, 2048);

        // ASTC 6x6 = 16 B / 36 px ≈ 0.44 B/px → 밉맵 포함 약 2.5 MB. 비압축 추정(22 MB)이면 안 된다.
        Assert.Greater(bytes / 1e6, 1.6);
        Assert.Less(bytes / 1e6, 3.0);
    }

    [Test]
    public void EstimateReadableCpuBytes_UncompressedNoOverride_KeepsRgba32Estimate()
    {
        string path = CreateReadablePng("raw2048.png", 2048);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "WebGL", overridden = false });
        importer.SaveAndReimport();

        importer = (TextureImporter)AssetImporter.GetAtPath(path);
        long bytes = AITBuildOptimizationScanner.EstimateReadableCpuBytes(importer, 2048, 2048);

        Assert.AreEqual(AITBuildOptimizationScanner.EstimateTextureCpuBytes(2048, 2048, true), bytes);
    }

    [Test]
    public void EstimateReadableCpuBytes_DefaultCompressedNoOverride_OneBytePerPixel()
    {
        string path = CreateReadablePng("comp2048.png", 2048);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "WebGL", overridden = false });
        importer.SaveAndReimport();

        importer = (TextureImporter)AssetImporter.GetAtPath(path);
        long bytes = AITBuildOptimizationScanner.EstimateReadableCpuBytes(importer, 2048, 2048);

        Assert.AreEqual(2048L * 2048L, bytes);
    }
}
