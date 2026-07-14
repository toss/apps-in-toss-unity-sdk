// AITTextureCrunchProcessorRestoreTests
//
// 텍스처 crunch 프로세서 apply→restore 왕복 회귀 가드 (Level 1 — 실 AssetDatabase).
//
// 배경: AITTextureCrunchProcessor 는 기본 ON(자동) 빌드 단계 프로세서로, 빌드 직전
// 대상 Texture2D 의 임포터 설정(crunchedCompression / compressionQuality / maxTextureSize)을
// 일시적으로 crunch 로 바꿔 reimport 하고, 빌드 종료 후 원본 임포트 설정으로 복원한다.
// 복원 메커니즘은 변형 직전의 온디스크 .meta 를 <path>.meta.aittexbak 로 verbatim 백업했다가,
// RestoreForBuild 에서 그 백업을 File.Copy 로 되돌리는 것이다(비파괴 계약).
//
// ⚠ 이 복원이 깨지면(백업 미복원/부분 복원) 파트너의 텍스처 임포터 설정과 .meta 가
// 매 빌드마다 영구히 crunch 상태로 오염된다(작업 트리에 잔존). 이 테스트가 그 회귀의
// 유일한 가드다: 실제 PNG 에셋 1개로 apply→restore 왕복을 돌려
//   (1) apply 가 실제로 crunch 로 변형했는지(비-vacuous),
//   (2) restore 후 .meta 가 원본과 바이트 단위로 동일하고 임포터 필드가 원복됐는지,
//   (3) 백업/마커 잔존물이 없는지
// 를 검증한다.
//
// 참고: 텍스처 왕복 가드의 교차-프로세서 순서 변형은 AITCrossProcessorRestoreOrderTests,
// 오디오 파이프라인 왕복은 AITAudioCrossProcessorRestoreTests 참조.
//
// crunch 는 ASTC 서브타겟에서 skip 되므로(DXT 위 압축이라 ASTC 환경은 무효), SetUp 에서
// WebGL 서브타겟을 DXT 로 고정해 게이트를 통과시키고 TearDown 에서 원복한다.

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureCrunchProcessorRestoreTests
{
    private const string TempDir = "Assets/AITTest_TextureCrunchRestore";
    private const string TempTexPath = TempDir + "/crunch_restore_probe.png";

    // 프로세서 내부 상수와 동일해야 한다(백업/마커 잔존물 검증용).
    private const string BackupSuffix = ".aittexbak";
    private const string MarkerRelative = "Assets/.ait-texcrunch-active";

    // 결정적 원본 상태(Unity 기본값 변화에 비의존).
    private const int OriginalQuality = 50;   // crunch 미적용 상태의 compressorQuality(Normal)
    private const int OriginalMaxSize = 2048;
    private const int CrunchQuality = 100;    // apply 시 적용할 crunch 품질(원본 50 과 명확히 다름)

    private string _projectRoot;
    private string _metaAbsPath;
    private AITEditorScriptObject _config;

#if UNITY_2022_3_OR_NEWER
    private WebGLTextureSubtarget _prevSubtarget;
#endif

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

        // 64x64 RGBA PNG(알파 포함 → Automatic 이 DXT5 계열로 매핑). crunch 는 크기 게이트가
        // 없고 임포터 상태(wouldChange)로만 동작하므로 작은 텍스처로 충분하다.
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

        // 결정성: 원본을 "crunch 미적용 + Compressed + quality 50 + maxSize 2048" 로 고정.
        // 이 상태의 .meta 가 verbatim 복원 대상 "원본"이다. crunch 프로세서는 !crunchedCompression
        // 또는 compressionQuality 불일치를 wouldChange 로 보므로 반드시 변형이 일어난다.
        var ti = (TextureImporter)AssetImporter.GetAtPath(TempTexPath);
        Assert.IsNotNull(ti, "임시 텍스처 임포터가 존재해야 함");
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.crunchedCompression = false;
        ti.compressionQuality = OriginalQuality;
        ti.maxTextureSize = OriginalMaxSize;
        ti.SaveAndReimport();

        // crunch 의 ASTC 서브타겟 게이트를 통과시키기 위해 DXT 로 고정(TearDown 에서 원복).
#if UNITY_2022_3_OR_NEWER
        _prevSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
#endif

        // crunch 프로세서를 임시 폴더로 스코프. maxSize 캡은 0(끔) 으로 두어 crunch 플래그/품질
        // 변형만 격리한다(size-clamp 는 별도 프로세서 소관). 아틀라스 경로는 비활성.
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.textureCrunch = 1;                 // 명시적 ON
        _config.textureCrunchMaxSize = 0;          // 캡 안 함(maxTextureSize 는 변형 대상 아님)
        _config.textureCrunchQuality = CrunchQuality;
        _config.textureCrunchAtlas = false;        // 아틀라스 경로 제외(Texture2D 왕복만 검증)
        _config.textureCrunchAtlasMaxSize = 0;
        _config.textureCrunchDirs = TempDir;       // 임시 폴더로 스코프
    }

    [TearDown]
    public void TearDown()
    {
        // 잔존 백업 파일 정리(테스트 실패 시에도 샘플 프로젝트를 더럽히지 않도록 — 베스트 에포트).
        string assetsAbs = Application.dataPath;
        foreach (var bak in Directory.GetFiles(assetsAbs, "*" + BackupSuffix, SearchOption.AllDirectories))
        {
            try { File.Delete(bak); } catch { /* best-effort */ }
        }

        string marker = Path.Combine(_projectRoot, MarkerRelative);
        try { if (File.Exists(marker)) { File.Delete(marker); } } catch { /* best-effort */ }

        AssetDatabase.DeleteAsset(TempTexPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

#if UNITY_2022_3_OR_NEWER
        EditorUserBuildSettings.webGLBuildSubtarget = _prevSubtarget;
#endif

        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void ApplyThenRestore_TextureCrunch_RestoresMetaVerbatim()
    {
        Assert.IsTrue(File.Exists(_metaAbsPath), "사전조건: 원본 .meta 가 존재해야 함");
        string originalMeta = File.ReadAllText(_metaAbsPath);
        Assert.IsFalse(string.IsNullOrEmpty(originalMeta), "원본 .meta 는 비어있지 않아야 함");

        var before = (TextureImporter)AssetImporter.GetAtPath(TempTexPath);
        Assert.IsNotNull(before, "사전조건: 텍스처 임포터가 존재해야 함");
        Assert.IsFalse(before.crunchedCompression, "사전조건: 원본은 crunch 미적용 상태여야 함");
        Assert.AreEqual(OriginalQuality, before.compressionQuality, "사전조건: 원본 compressionQuality=50");

        // ── 적용(AITConvertCore.BuildWebGL 이 BuildPlayer 직전에 호출하는 실 진입점) ──
        var handle = AITTextureCrunchProcessor.ApplyForBuild(_config);

        // "조용한 no-op" 방지: 프로세서가 실제로 활성화되어 변형이 일어났는지 명시 단언.
        // (ASTC 게이트로 skip 되면 handle.Active=false → 이 테스트는 무의미하므로 실패 처리)
        Assert.IsTrue(handle != null && handle.Active,
            "crunch 프로세서가 활성화되어야 함(textureCrunch=1, WebGL 서브타겟 DXT). " +
            "비활성이면 apply→restore 왕복이 성립하지 않음.");
        Assert.AreEqual(1, handle.TextureCount,
            "TempDir 로 스코프된 텍스처 1개만 crunch 처리되어야 함.");

        string backupPath = _metaAbsPath + BackupSuffix;
        Assert.IsTrue(File.Exists(backupPath),
            "적용 후 원본 .meta 스냅샷(.aittexbak)이 존재해야 함(복원 소스).");

        // 변형이 실제로 일어났는지(비-vacuous): crunch 플래그 on + 품질 변경 + 온디스크 .meta 변화.
        var applied = (TextureImporter)AssetImporter.GetAtPath(TempTexPath);
        Assert.IsNotNull(applied, "적용 후 임포터가 존재해야 함");
        Assert.IsTrue(applied.crunchedCompression,
            "적용 후 crunchedCompression=true 여야 함(crunch 가 실제로 적용됐다는 실증).");
        Assert.AreEqual(CrunchQuality, applied.compressionQuality,
            "적용 후 compressionQuality 는 설정값(100)으로 바뀌어야 함(변형 실증).");
        Assert.AreNotEqual(originalMeta, File.ReadAllText(_metaAbsPath),
            "적용 후 온디스크 .meta 는 원본과 달라야 함(변형이 디스크에 기록됨).");

        // ── 복원(이 테스트의 검증 대상 그 자체) ──
        AITTextureCrunchProcessor.RestoreForBuild(handle);
        AssetDatabase.Refresh();

        // ── 검증: .meta 가 원본과 바이트 단위로 동일 + 임포터 필드 원복 + 잔존물 전무 ──
        Assert.IsTrue(File.Exists(_metaAbsPath), "복원 후 .meta 가 존재해야 함");
        string restoredMeta = File.ReadAllText(_metaAbsPath);
        Assert.AreEqual(originalMeta, restoredMeta,
            "복원 후 .meta 는 원본과 바이트 단위로 동일해야 한다. 불일치는 crunch 프로세서의 " +
            "복원이 파트너 에셋의 임포터 설정을 영구 오염시킨다는 의미다.");

        var restored = (TextureImporter)AssetImporter.GetAtPath(TempTexPath);
        Assert.IsNotNull(restored, "복원 후 임포터가 존재해야 함");
        Assert.IsFalse(restored.crunchedCompression,
            "복원 후 crunchedCompression 는 원래 false 로 돌아와야 함.");
        Assert.AreEqual(OriginalQuality, restored.compressionQuality,
            "복원 후 compressionQuality 는 원래 값(50)으로 돌아와야 함.");

        Assert.IsFalse(File.Exists(backupPath), "복원 후 백업(.aittexbak)이 삭제되어야 함");
        Assert.IsFalse(File.Exists(Path.Combine(_projectRoot, MarkerRelative)),
            "복원 후 crunch 마커(Assets/.ait-texcrunch-active)가 제거되어야 함");
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(TempTexPath),
            "복원 후 텍스처 에셋이 정상 로드되어야 함");
    }
}
