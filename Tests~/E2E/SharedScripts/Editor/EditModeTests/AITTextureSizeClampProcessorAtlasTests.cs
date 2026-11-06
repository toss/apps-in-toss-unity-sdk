// -----------------------------------------------------------------------
// AITTextureSizeClampProcessorAtlasTests.cs - 텍스처 size clamp SpriteAtlas 경로 회귀 가드
// Level 1: 실 AssetDatabase 기반 SpriteAtlas apply→restore 왕복 검증.
//
// 배경: AITTextureSizeClampProcessor.ApplySpriteAtlas 는 SpriteAtlas 의 DefaultTexturePlatform
// (마스터) + WebGL 오버라이드 platform settings 중 maxTextureSize 만 캡 이하로 낮추고, 빌드 종료 후
// 에셋 본체 파일(.spriteatlas)을 BackupAtlasAsset 이 만든 .aittexclampbak 스냅샷으로 복원한다.
// 텍스처와 달리 platform settings 가 아틀라스 에셋 자체에 직렬화되므로 .meta 백업만으로는
// 복원이 불가능하다 — 이 왕복이 깨지면 아틀라스 임포트 설정이 캡 상태로 영구 오염된다.
//
// 두 아틀라스(캡 초과/캡 이하)를 같은 ApplyForBuild 호출로 동시에 스코프해 캡 게이트가
// "캡을 넘는 것만" 건드리는지(비-vacuous 변형 + no-op 보존)를 한 번에 검증한다.
//
// PackAllAtlases 는 프로세서 내부에서 무조건 호출되지만(아틀라스 1개 이상 변경 시), 패킹 결과물
// (실제 압축 텍스처)까지는 검증하지 않는다 — platform settings 값만으로 캡 적용 여부를 판단한다.

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureSizeClampProcessorAtlasTests
{
    private const string TempDir = "Assets/AITTest_TextureSizeClampAtlas";
    private const string OverCapAtlasPath = TempDir + "/clamp_atlas_overcap.spriteatlas";
    private const string UnderCapAtlasPath = TempDir + "/clamp_atlas_undercap.spriteatlas";

    // 프로세서 내부 상수와 동일해야 한다(백업/마커 잔존물 검증용).
    private const string BackupSuffix = ".aittexclampbak";
    private const string MarkerRelative = "Assets/.ait-texclamp-active";

    // 결정적 원본 상태(Unity 기본값 변화에 비의존).
    private const int OverCapMasterSize = 4096;
    private const int OverCapWebGLSize = 4096;
    private const int UnderCapMasterSize = 512;
    private const int ClampCap = 1024;

    private string _projectRoot;
    private string _overCapAbsPath;
    private string _underCapAbsPath;
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        _overCapAbsPath = Path.Combine(_projectRoot, OverCapAtlasPath);
        _underCapAbsPath = Path.Combine(_projectRoot, UnderCapAtlasPath);

        string dirAbs = Path.Combine(_projectRoot, TempDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        // 캡(1024) 초과 아틀라스: 마스터 4096 + WebGL 오버라이드 4096.
        var overCap = new SpriteAtlas();
        AssetDatabase.CreateAsset(overCap, OverCapAtlasPath);
        var overDef = overCap.GetPlatformSettings("DefaultTexturePlatform");
        overDef.maxTextureSize = OverCapMasterSize;
        overCap.SetPlatformSettings(overDef);
        var overWeb = overCap.GetPlatformSettings("WebGL");
        overWeb.overridden = true;
        overWeb.maxTextureSize = OverCapWebGLSize;
        overCap.SetPlatformSettings(overWeb);
        EditorUtility.SetDirty(overCap);

        // 캡 이하 아틀라스: 마스터 512, WebGL 오버라이드 없음(변경 대상 아님을 검증).
        var underCap = new SpriteAtlas();
        AssetDatabase.CreateAsset(underCap, UnderCapAtlasPath);
        var underDef = underCap.GetPlatformSettings("DefaultTexturePlatform");
        underDef.maxTextureSize = UnderCapMasterSize;
        underCap.SetPlatformSettings(underDef);
        EditorUtility.SetDirty(underCap);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // size-clamp 프로세서를 임시 폴더로 스코프(명시적 ON, 캡=1024).
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.textureSizeClamp = 1;              // 명시적 ON
        _config.textureClampMaxSize = ClampCap;
        _config.textureClampMinBytes = 0;
        _config.textureClampDirs = TempDir;         // 임시 폴더로 스코프
        _config.textureClampExcludeDirs = "";
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

        AssetDatabase.DeleteAsset(OverCapAtlasPath);
        AssetDatabase.DeleteAsset(UnderCapAtlasPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void ApplyThenRestore_SpriteAtlas_ClampsOverCapPreservesUnderCapAndRestoresVerbatim()
    {
        Assert.IsTrue(File.Exists(_overCapAbsPath), "사전조건: 캡 초과 아틀라스 에셋이 존재해야 함");
        Assert.IsTrue(File.Exists(_underCapAbsPath), "사전조건: 캡 이하 아틀라스 에셋이 존재해야 함");
        byte[] overCapOriginalBytes = File.ReadAllBytes(_overCapAbsPath);
        byte[] underCapOriginalBytes = File.ReadAllBytes(_underCapAbsPath);

        var overBefore = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(OverCapAtlasPath);
        Assert.IsNotNull(overBefore, "사전조건: 캡 초과 아틀라스가 로드되어야 함");
        var overDefBefore = overBefore.GetPlatformSettings("DefaultTexturePlatform");
        Assert.AreEqual(OverCapMasterSize, overDefBefore.maxTextureSize, "사전조건: 마스터 maxTextureSize=4096");
        var overWebBefore = overBefore.GetPlatformSettings("WebGL");
        Assert.IsTrue(overWebBefore.overridden, "사전조건: WebGL 오버라이드가 설정되어 있어야 함");
        Assert.AreEqual(OverCapWebGLSize, overWebBefore.maxTextureSize, "사전조건: WebGL 오버라이드 maxTextureSize=4096");

        // ── 적용(AITConvertCore.BuildWebGL 이 BuildPlayer 직전에 호출하는 실 진입점) ──
        var handle = AITTextureSizeClampProcessor.ApplyForBuild(_config);

        // "조용한 no-op" 방지: 프로세서가 실제로 활성화되어 캡 초과 아틀라스 1개만 처리했는지 명시 단언.
        Assert.IsTrue(handle != null && handle.Active,
            "size-clamp 프로세서가 활성화되어야 함(textureSizeClamp=1, 캡 초과 아틀라스 존재). " +
            "비활성이면 apply→restore 왕복이 성립하지 않음.");
        Assert.AreEqual(1, handle.AtlasCount,
            "TempDir 로 스코프된 아틀라스 중 캡 초과 1개만 처리되어야 함(캡 이하는 no-op).");
        Assert.AreEqual(0, handle.TextureCount, "이 테스트는 SpriteAtlas 만 다루므로 Texture2D 처리 건수는 0이어야 함.");

        // ── 캡 초과 아틀라스: 실제로 캡까지 내려갔는지(비-vacuous) ──
        string overBackupPath = _overCapAbsPath + BackupSuffix;
        Assert.IsTrue(File.Exists(overBackupPath), "적용 후 캡 초과 아틀라스의 원본 백업(.aittexclampbak)이 존재해야 함.");

        var overApplied = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(OverCapAtlasPath);
        Assert.IsNotNull(overApplied, "적용 후 캡 초과 아틀라스가 로드되어야 함");
        var overDefApplied = overApplied.GetPlatformSettings("DefaultTexturePlatform");
        Assert.AreEqual(ClampCap, overDefApplied.maxTextureSize,
            "적용 후 마스터 maxTextureSize 는 캡(1024) 이하로 내려가야 함(캡 적용 실증).");
        var overWebApplied = overApplied.GetPlatformSettings("WebGL");
        Assert.IsTrue(overWebApplied.overridden, "적용 후에도 WebGL 오버라이드 플래그는 보존되어야 함.");
        Assert.AreEqual(ClampCap, overWebApplied.maxTextureSize,
            "적용 후 WebGL 오버라이드 maxTextureSize 도 캡(1024) 이하로 내려가야 함(빌드는 오버라이드를 우선하므로).");

        // ── 캡 이하 아틀라스: 전혀 건드려지지 않았는지(백업 미생성 포함) ──
        string underBackupPath = _underCapAbsPath + BackupSuffix;
        Assert.IsFalse(File.Exists(underBackupPath), "캡 이하 아틀라스는 백업이 생성되지 않아야 함(변경 대상 아님).");
        Assert.AreEqual(underCapOriginalBytes, File.ReadAllBytes(_underCapAbsPath),
            "캡 이하 아틀라스는 온디스크 바이트가 원본과 동일해야 함(무변경).");
        var underApplied = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(UnderCapAtlasPath);
        var underDefApplied = underApplied.GetPlatformSettings("DefaultTexturePlatform");
        Assert.AreEqual(UnderCapMasterSize, underDefApplied.maxTextureSize,
            "캡 이하 아틀라스의 마스터 maxTextureSize 는 원래 값(512)을 유지해야 함.");

        // ── 복원(이 테스트의 검증 대상 그 자체) ──
        AITTextureSizeClampProcessor.RestoreForBuild(handle);
        AssetDatabase.Refresh();

        // ── 검증: 캡 초과 아틀라스가 원본과 바이트 단위로 동일 + platform settings 원복 + 잔존물 전무 ──
        Assert.IsTrue(File.Exists(_overCapAbsPath), "복원 후 캡 초과 아틀라스 에셋이 존재해야 함");
        Assert.AreEqual(overCapOriginalBytes, File.ReadAllBytes(_overCapAbsPath),
            "복원 후 캡 초과 아틀라스는 원본과 바이트 단위로 동일해야 한다. 불일치는 size-clamp 프로세서의 " +
            "복원이 파트너 에셋의 아틀라스 설정을 영구 오염시킨다는 의미다.");

        var overRestored = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(OverCapAtlasPath);
        Assert.IsNotNull(overRestored, "복원 후 아틀라스가 정상 로드되어야 함");
        var overDefRestored = overRestored.GetPlatformSettings("DefaultTexturePlatform");
        Assert.AreEqual(OverCapMasterSize, overDefRestored.maxTextureSize,
            "복원 후 마스터 maxTextureSize 는 원래 값(4096)으로 돌아와야 함.");
        var overWebRestored = overRestored.GetPlatformSettings("WebGL");
        Assert.IsTrue(overWebRestored.overridden, "복원 후에도 WebGL 오버라이드 플래그는 유지되어야 함.");
        Assert.AreEqual(OverCapWebGLSize, overWebRestored.maxTextureSize,
            "복원 후 WebGL 오버라이드 maxTextureSize 는 원래 값(4096)으로 돌아와야 함.");

        Assert.IsFalse(File.Exists(overBackupPath), "복원 후 백업(.aittexclampbak)이 삭제되어야 함");
        Assert.IsFalse(File.Exists(Path.Combine(_projectRoot, MarkerRelative)),
            "복원 후 size-clamp 마커(Assets/.ait-texclamp-active)가 제거되어야 함");
    }
}
