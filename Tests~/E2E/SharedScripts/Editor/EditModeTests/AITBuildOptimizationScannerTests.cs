// -----------------------------------------------------------------------
// AITBuildOptimizationScannerTests.cs - Read/Write 활성 에셋 스캐너 검증
// Level 0: EstimateTextureCpuBytes / ClampToMaxTextureSize 순수 계산
// Level 1: 임시 readable PNG 를 만들어 ScanReadable 이 나열하고 기본 미선택임을 확인
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
}
