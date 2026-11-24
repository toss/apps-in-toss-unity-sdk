// -----------------------------------------------------------------------
// AITTextureStreamMemoryGateTests.cs - 텍스처 외부화 빌드 단계의 메모리 예산 게이트와 매니페스트 플래그 통합 검증
// Level 1 (에디터 임포터 사용): 실제 임시 PNG 를 임포트해 AITLargeTextureExternalizer.ExternalizeForBuild 를 돌린다.
//   - 원본 isReadable=false → 매니페스트 엔트리에 "nonReadable":1, readable → 필드 없음
//   - auto(-1) + 예산 초과 → 외부화 제외(ExcludedBudget), 예산 0 → 제한 없음, 명시 1 → 예산 무시
//   - 매니페스트 maxConcurrent 기본 1
// 순수 계산(예산 선택/크기 추정)은 AITTextureStreamPlannerTests 가 맡는다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureStreamMemoryGateTests
{
    private const string TempDir = "Assets/AITTest_TexStreamMemGate";
    private const string TempTexPath = TempDir + "/memgate_probe.png";
    private const string ManifestRel = "Assets/StreamingAssets/ait-stream-texture/manifest.json";

    private string _projectRoot;
    private AITEditorScriptObject _config;
    private AITLargeTextureExternalizer.TextureStreamHandle _handle;

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string dirAbs = Path.Combine(_projectRoot, TempDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        // 1024² 노이즈 PNG. 압축 포맷(DXT/ASTC) 임포트면 스텁 RGBA32 가 원본 GPU 보다 수 MB 크다.
        var tex = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        var px = new Color32[1024 * 1024];
        uint s = 12345u;
        for (int i = 0; i < px.Length; i++)
        {
            s = s * 1664525u + 1013904223u;
            px[i] = new Color32((byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), 255);
        }

        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(_projectRoot, TempTexPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TempTexPath, ImportAssetOptions.ForceSynchronousImport);

        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.textureStreaming = 1;
        _config.textureStreamingMinBytes = 1;
        _config.textureStreamingDirs = TempDir;
        _config.textureStreamingExcludeDirs = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        AITLargeTextureExternalizer.RestoreForBuild(_handle);
        _handle = null;

        string assetsAbs = Application.dataPath;
        foreach (var suffix in new[] { "*.aittexstreammetabak", "*.aittexstreambak" })
        {
            foreach (var bak in Directory.GetFiles(assetsAbs, suffix, SearchOption.AllDirectories))
            {
                try { File.Delete(bak); } catch { /* best-effort */ }
            }
        }

        AssetDatabase.DeleteAsset(TempTexPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    private void ConfigureImporter(bool readable)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(TempTexPath);
        Assert.IsNotNull(ti, "임시 텍스처 임포터가 존재해야 함");
        ti.isReadable = readable;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 2048;
        ti.crunchedCompression = false;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.SaveAndReimport();
    }

    private string ReadManifest()
    {
        string abs = Path.Combine(_projectRoot, ManifestRel);
        Assert.IsTrue(File.Exists(abs), "외부화 뒤 매니페스트가 있어야 함");
        return File.ReadAllText(abs);
    }

    [Test]
    public void NonReadableOriginal_ManifestEntry_CarriesNonReadableFlag()
    {
        ConfigureImporter(readable: false);
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsTrue(_handle.Active, "명시 1 은 외부화되어야 함");
        StringAssert.Contains("\"nonReadable\":1", ReadManifest());
    }

    [Test]
    public void ReadableOriginal_ManifestEntry_OmitsNonReadableFlag()
    {
        ConfigureImporter(readable: true);
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsTrue(_handle.Active);
        StringAssert.DoesNotContain("nonReadable", ReadManifest());
    }

    [Test]
    public void Manifest_MaxConcurrent_DefaultsToOne_WhenConfigUnset()
    {
        ConfigureImporter(readable: false);
        _config.textureStreamingMaxConcurrent = 0;
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsTrue(_handle.Active);
        StringAssert.Contains("\"maxConcurrent\":1,", ReadManifest());
    }

    [Test]
    public void Auto_OverBudget_ExcludesTexture()
    {
        ConfigureImporter(readable: false);
        _config.textureStreaming = -1;            // auto
        _config.textureStreamingMemoryBudgetMB = 1;
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsFalse(_handle.Active, "1024² 압축 텍스처의 RGBA32 팽창(수 MB)은 1MB 예산을 넘으므로 auto 는 외부화하지 않는다.");
        Assert.AreEqual(1, _handle.ExcludedBudget);
        Assert.AreEqual(0, _handle.Count);
    }

    [Test]
    public void Auto_ZeroBudget_MeansNoLimit()
    {
        ConfigureImporter(readable: false);
        _config.textureStreaming = -1;
        _config.textureStreamingMemoryBudgetMB = 0;
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsTrue(_handle.Active, "예산 0 은 제한 없음.");
        Assert.AreEqual(0, _handle.ExcludedBudget);
    }

    [Test]
    public void Auto_LargeBudget_Externalizes()
    {
        ConfigureImporter(readable: false);
        _config.textureStreaming = -1;
        _config.textureStreamingMemoryBudgetMB = 256;
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsTrue(_handle.Active);
        Assert.AreEqual(0, _handle.ExcludedBudget);
    }

    [Test]
    public void Explicit_IgnoresBudget()
    {
        ConfigureImporter(readable: false);
        _config.textureStreaming = 1;
        _config.textureStreamingMemoryBudgetMB = 1;
        _handle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);
        Assert.IsTrue(_handle.Active, "명시 1 은 예산과 무관하게 전부 외부화한다(기존 동작).");
        Assert.AreEqual(0, _handle.ExcludedBudget);
    }
}
