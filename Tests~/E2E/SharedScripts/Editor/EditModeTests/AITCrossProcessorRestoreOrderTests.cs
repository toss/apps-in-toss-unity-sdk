// AITCrossProcessorRestoreOrderTests
//
// 교차-프로세서 복원 순서 회귀 가드 (Level 1 — 실 AssetDatabase).
//
// 배경: AITConvertCore.BuildWebGL 의 finally 블록은 콘텐츠 최적화 프로세서들을
// "적용의 정확한 역순"으로 RestoreForBuild 해야 한다. 각 프로세서는 자기 변형 직전의
// 온디스크 .meta 를 verbatim 백업하므로, 같은 에셋을 둘 이상이 건드린 경우
// "가장 먼저 적용한" 프로세서가 진짜 원본을 갖고 있다. 역순 복원이면 그 프로세서의
// 복원이 마지막에 쓰여 원본이 보존된다. 순서가 어긋나면(예: 먼저 적용한 프로세서를
// 먼저 복원) 뒤에 적용한 프로세서가 자신의 "이미 변형된" 스냅샷을 마지막에 덮어써
// 파트너 에셋의 .meta 가 매 빌드마다 영구 오염된다.
//
// 이 테스트는 clamp → texStream 순으로 같은 텍스처의 .meta 를 변형한 뒤
// (texStream → clamp = 적용 역순) 으로 복원하면 .meta 가 바이트 단위로 원본과
// 동일해짐을 검증한다. 복원 순서가 뒤집히면(clamp 먼저) 최종 .meta 에
// clamp 변형값(maxTextureSize 캡)이 남아 이 테스트가 RED 가 된다.
//
// clamp/texStream 쌍을 고른 이유: clamp 은 crunch 와 달리 ASTC 서브타겟 게이트가
// 없어 EditMode 에서 안정적으로 활성화된다(maxTextureSize > clampMax 면 동작).

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITCrossProcessorRestoreOrderTests
{
    private const string TempDir = "Assets/AITTest_CrossProcessorRestore";
    private const string TempTexPath = TempDir + "/cross_restore_probe.png";

    private string _projectRoot;
    private string _metaAbsPath;
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        _metaAbsPath = Path.Combine(_projectRoot, TempTexPath + ".meta");

        string dirAbs = Path.Combine(_projectRoot, TempDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        // 64x64 RGBA PNG — texStream 의 minBytes/차원 게이트를 넉넉히 통과.
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var px = new Color32[64 * 64];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = new Color32((byte)(i & 0xFF), (byte)((i >> 2) & 0xFF), (byte)((i >> 4) & 0xFF), 255);
        }
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(_projectRoot, TempTexPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TempTexPath, ImportAssetOptions.ForceSynchronousImport);

        // 결정성: maxTextureSize 를 명시적으로 2048 로 고정(>clampMax 1024). Unity 기본값
        // 변화에 비의존. 이 상태의 .meta 가 "원본"이다.
        var ti = (TextureImporter)AssetImporter.GetAtPath(TempTexPath);
        Assert.IsNotNull(ti, "임시 텍스처 임포터가 존재해야 함");
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();

        // 프로세서 2종을 임시 폴더로 스코프 + 작은 텍스처가 자격을 갖도록 임계값 하향.
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();

        _config.textureSizeClamp = 1;          // ON
        _config.textureClampMaxSize = 1024;    // 2048 > 1024 → clamp 동작
        _config.textureClampMinBytes = 0;      // 크기 필터 없음
        _config.textureClampDirs = TempDir;
        _config.textureClampExcludeDirs = string.Empty;

        _config.textureStreaming = 1;          // ON
        _config.textureStreamingMinBytes = 1;  // 어떤 파일이든 자격
        _config.textureStreamingDirs = TempDir;
    }

    [TearDown]
    public void TearDown()
    {
        // 잔존 백업 파일 정리(테스트 실패 시에도 샘플 프로젝트를 더럽히지 않도록).
        string assetsAbs = Application.dataPath;
        foreach (var suffix in new[] { "*.aittexclampbak", "*.aittexstreammetabak", "*.aittexstreambak" })
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

    [Test]
    public void RestoreInReverseApplyOrder_TwoProcessorsOnSameMeta_RestoresVerbatim()
    {
        Assert.IsTrue(File.Exists(_metaAbsPath), "사전조건: 원본 .meta 가 존재해야 함");
        string originalMeta = File.ReadAllText(_metaAbsPath);
        Assert.IsFalse(string.IsNullOrEmpty(originalMeta), "원본 .meta 는 비어있지 않아야 함");

        // ── 적용(AITConvertCore.BuildWebGL 과 동일 순서: clamp → texStream) ──
        var clampHandle = AITTextureSizeClampProcessor.ApplyForBuild(_config);
        var streamHandle = AITLargeTextureExternalizer.ExternalizeForBuild(_config);

        // 두 프로세서가 실제로 활성화되어 교차 변형이 일어났는지 확인 — 그렇지 않으면
        // 이 테스트는 무의미하므로 명시적으로 실패 처리해 "조용한 no-op"을 방지한다.
        Assert.IsTrue(clampHandle != null && clampHandle.Active,
            "clamp 프로세서가 활성화되어야 함(maxTextureSize 2048 > clampMax 1024). " +
            "비활성이면 교차-프로세서 시나리오가 성립하지 않음.");
        Assert.IsTrue(streamHandle != null && streamHandle.Active,
            "texStream 프로세서가 활성화되어야 함(minBytes=1). 비활성이면 교차 시나리오 미성립.");

        Assert.IsTrue(File.Exists(_metaAbsPath + ".aittexclampbak"),
            "clamp 적용 후 .aittexclampbak(원본 .meta 스냅샷)이 존재해야 함");
        Assert.IsTrue(File.Exists(_metaAbsPath + ".aittexstreammetabak"),
            "texStream 적용 후 .aittexstreammetabak(clamp-변형 .meta 스냅샷)이 존재해야 함");

        // ── 복원: 적용의 역순(texStream 먼저, clamp 나중) ──
        // 이 순서가 검증 대상이다. 만약 clamp 를 먼저 복원하면 clamp 가 원본을 쓴 뒤
        // texStream 이 자신의 clamp-변형 스냅샷(.aittexstreammetabak)을 마지막에 덮어써
        // 최종 .meta 에 maxTextureSize 캡(1024)이 남는다 → 아래 AreEqual 이 실패.
        AITLargeTextureExternalizer.RestoreForBuild(streamHandle);
        AITTextureSizeClampProcessor.RestoreForBuild(clampHandle);
        AssetDatabase.Refresh();

        // ── 검증: .meta 가 원본과 바이트 단위로 동일 ──
        Assert.IsTrue(File.Exists(_metaAbsPath), "복원 후 .meta 가 존재해야 함");
        string restoredMeta = File.ReadAllText(_metaAbsPath);
        Assert.AreEqual(originalMeta, restoredMeta,
            "역순 복원 후 .meta 는 원본과 동일해야 한다. 불일치는 교차-프로세서 복원 순서 " +
            "버그(뒤에 적용한 프로세서의 변형 스냅샷이 마지막에 남음)를 의미한다.");

        // 소스가 원위치로 복귀했는지 + 백업 잔존물이 없는지 확인.
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(TempTexPath),
            "texStream 이 외부화한 소스가 원위치로 복원되어야 함");
        Assert.IsFalse(File.Exists(_metaAbsPath + ".aittexclampbak"),
            "복원 후 clamp 백업이 삭제되어야 함");
        Assert.IsFalse(File.Exists(_metaAbsPath + ".aittexstreammetabak"),
            "복원 후 texStream meta 백업이 삭제되어야 함");
    }
}
