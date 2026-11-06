// AITFontExternalizerRestoreTests
//
// 폰트 외부화(default-ON) 프로세서의 적용→복원 왕복 회귀 가드 (Level 1 — 실 AssetDatabase).
//
// AITConvertCore.BuildWebGL 는 빌드 직전 AITFontExternalizer.ExternalizeForBuild 를 호출하고,
// finally 에서 RestoreForBuild 로 원상 복원한다. 외부화는 파트너 프로젝트의 소스 .ttf/.otf
// 바이트를 612B 스텁으로 ★인플레이스 치환★하고(원본은 <src>.aitfontsrcbak~ 로 백업),
// 빌드 종료 시 원본 바이트를 되돌린다. 복원이 누락/파손되면 파트너의 폰트 파일이 매 빌드마다
// 영구히 612B 스텁으로 남아(= 영구 □/두부문자) 소스 트리가 파괴된다 — 이 테스트가 막는 회귀다.
//
// 이 테스트는 실제 .ttf 픽스처를 만들고, 그 폰트를 ★직접 참조하는★ 에셋(GUISkin.font)을 대상으로
// 수동 모드(fontStreaming=1) 외부화를 실행한 뒤, 복원 후 소스 .ttf 바이트/.meta 가 원본과
// 바이트 단위로 동일해지는지, 백업/마커/StreamingAssets 잔존물이 전무한지 검증한다.
//
// 왜 GUISkin 인가(대상 에셋의 역할): AITFontExternalizer 는 대상 에셋의 ★직접 의존성★에서 첫
//   .ttf/.otf 를 소스로 해석한다(ResolveSourceFont). 맨 .ttf 를 대상으로 주면 유일한 의존성이
//   자기 자신이라 스킵되어 소스 해석이 실패한다. 따라서 폰트를 참조하는 에셋이 필요한데, GUISkin
//   은 (a) UnityEngine 기본 타입이라 TMP/리플렉션 없이 컴파일되고(테스트 asmdef 는 TMP 미참조),
//   (b) font 필드로 .ttf 를 직접 참조해 의존성 해석을 성립시키며, (c) TMP_FontAsset 처럼 OnEnable
//   에서 material/atlas 부재로 LogError 를 내지 않는다. 프로덕션의 정식 대상은 TMP_FontAsset 이지만,
//   본 테스트가 지키는 ★소스 .ttf 스텁 치환→복원★ 경로(SwapSourceToStub / RestoreAllBackups)는
//   대상 타입과 무관하게 동일하다(비-TMP 대상은 fallback strip 만 건너뜀 — 그 strip 은 적용 내부
//   Phase B 에서 즉시 자기복원되므로 RestoreForBuild 의 소스-.ttf 복원 책임과 무관).
//
// 참고: 오디오/텍스처 프로세서의 동일 왕복 가드는 AITAudioCrossProcessorRestoreTests /
//       AITCrossProcessorRestoreOrderTests 참조.

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITFontExternalizerRestoreTests
{
    private const string TempDir = "Assets/AITTest_FontExternalizerRestore";
    private const string TtfPath = TempDir + "/font_externalizer_probe.ttf";      // 스텁 치환 대상 소스 폰트
    private const string SkinPath = TempDir + "/font_externalizer_ref.guiskin";   // 소스 .ttf 를 직접 참조하는 대상 에셋

    // AITFontExternalizer 의 상수 미러(프로덕션 값과 일치해야 함).
    private const string SrcBackupSuffix = ".aitfontsrcbak~";
    private const string StreamRootAssets = "Assets/StreamingAssets/ait-stream-font";
    private const string Marker = "Assets/.ait-fontstream-active";

    // AITFontExternalizer.StubTtfBase64 의 사본(612B, .notdef+space 만 든 최소 유효 TTF).
    // 프로세서가 소스를 치환하는 바로 그 바이트 — 적용 후 소스가 이 값과 같아졌는지로 "치환 발생"을 판정한다.
    private const string StubTtfBase64 =
        "AAEAAAAKAIAAAwAgT1MvMkkYTDAAAAEoAAAAYGNtYXAADABzAAABkAAAADRnbHlmAAAAAAAAAcwAAAABaGVhZCupKBAAAACsAAAANmhoZWEGaAZoAAAA5AAAACRobXR4CAAAAAAAAYgAAAAGbG9jYQAAAAAAAAHEAAAABm1heHAAAwACAAABCAAAACBuYW1lpGT3tQAAAdAAAABscG9zdAAHAAAAAAI8AAAAJgABAAAAAQAAVk+3Zl8PPPUAAwgAAAAAAOZJcYwAAAAA5klxjAAAAAAAAAAAAAAAAwACAAAAAAAAAAEAAAZm/mYAAAgAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAEAAAACAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAwgAAZAABQAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAPz8/PwAAACAAIAZm/mYAAAZmAZoAAAAAAAAAAAAAAAAAAAAgAAAIAAAAAAAAAAAAAAIAAAADAAAAFAADAAEAAAAUAAQAIAAAAAQABAABAAAAIP//AAAAIP///+EAAQAAAAAAAAAAAAAAAAAAAAAAAAAEADYAAQAAAAAAAQALAAAAAQAAAAAAAgAHAAsAAwABBAkAAQAWABIAAwABBAkAAgAOAChBSVRGb250U3R1YlJlZ3VsYXIAQQBJAFQARgBvAG4AdABTAHQAdQBiAFIAZQBnAHUAbABhAHIAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAwAA";

    private string _projectRoot;
    private AITEditorScriptObject _config;

    // SetUp 결과: 픽스처가 실제로 원본(스텁)보다 큰 바이트로 구성됐는지(=바이트 왕복 단언이 비-공허한지).
    private bool _sourceIsPadded;

    private string TtfFull => Path.Combine(_projectRoot, TtfPath);
    private string TtfMetaFull => TtfFull + ".meta";
    private string SkinMetaFull => Path.Combine(_projectRoot, SkinPath) + ".meta";
    private string BakFull => TtfFull + SrcBackupSuffix;
    private string MarkerFull => Path.Combine(_projectRoot, Marker);
    private string StreamRootFull => Path.Combine(_projectRoot, StreamRootAssets);
    private string ManifestFull => Path.Combine(StreamRootFull, "manifest.json");

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string dirAbs = Path.Combine(_projectRoot, TempDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        byte[] stub = Convert.FromBase64String(StubTtfBase64);

        // 소스 .ttf 를 "스텁 + 무해한 트레일링 패딩(16B 0x00)"으로 만들어 원본을 스텁과 ★바이트가 다르게★
        // 한다. sfnt 테이블 오프셋은 파일 선두 기준 절대값이라 끝에 붙인 바이트는 어떤 테이블에도
        // 참조되지 않아 FreeType 임포트에 무해하다(원본 612+16=628B ≠ 스텁 612B). 이 차이가 있어야
        // "복원 후 == 원본" 단언이 공허하지 않다(스텁이 남으면 길이부터 어긋나 RED).
        byte[] padded = new byte[stub.Length + 16];
        Array.Copy(stub, padded, stub.Length);
        File.WriteAllBytes(TtfFull, padded);
        AssetDatabase.ImportAsset(TtfPath, ImportAssetOptions.ForceSynchronousImport);

        // 방어: 만약 이 환경의 FreeType 가 트레일링 패딩을 거부해 Font 임포트가 실패하면,
        // 순수 스텁(항상 유효)으로 폴백한다. 이 경우 바이트 왕복 단언은 공허해지지만(원본==스텁),
        // 백업/마커/스트림루트 잔존물 단언이 skip-복원을 여전히 잡아낸다(아래 would-fail 근거 참조).
        _sourceIsPadded = true;
        if (AssetDatabase.LoadAssetAtPath<Font>(TtfPath) == null)
        {
            File.WriteAllBytes(TtfFull, stub);
            AssetDatabase.ImportAsset(TtfPath, ImportAssetOptions.ForceSynchronousImport);
            _sourceIsPadded = false;
        }

        // 소스 .ttf 를 직접 참조하는 대상 에셋(GUISkin.font). 프리시 인스턴스라 기본 스타일 폰트는
        // 내장 리소스(파일 확장자 .ttf 아님)라 소스 해석에 걸리지 않는다 — 우리가 심은 .ttf 만 매칭.
        var skin = ScriptableObject.CreateInstance<GUISkin>();
        AssetDatabase.CreateAsset(skin, SkinPath);
        skin.font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(SkinPath, ImportAssetOptions.ForceSynchronousImport);

        // 수동 모드: 대상 화이트리스트에 GUISkin 경로만 지정(자동 스캔의 1MB/부팅씬 게이트 회피, 결정성).
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.fontStreaming = 1;                 // 1 = 수동(fontStreamingTargetPaths 만 외부화)
        _config.fontStreamingTargetPaths = SkinPath;
        _config.fontStreamingMaxConcurrent = 2;
    }

    [TearDown]
    public void TearDown()
    {
        // 실패 시에도 샘플 프로젝트를 더럽히지 않도록 잔존물 전부 정리(베스트 에포트).
        string assetsAbs = Application.dataPath;
        try
        {
            foreach (var bak in Directory.GetFiles(assetsAbs, "*" + SrcBackupSuffix, SearchOption.AllDirectories))
            {
                try { File.Delete(bak); } catch { /* best-effort */ }
            }
        }
        catch { /* best-effort */ }

        try { if (File.Exists(MarkerFull)) { File.Delete(MarkerFull); } } catch { /* best-effort */ }
        try { if (Directory.Exists(StreamRootFull)) { Directory.Delete(StreamRootFull, true); } } catch { /* best-effort */ }
        try
        {
            string bundleTemp = Path.Combine(_projectRoot, "Library/ait-fontbundle");
            if (Directory.Exists(bundleTemp)) { Directory.Delete(bundleTemp, true); }
        }
        catch { /* best-effort */ }

        AssetDatabase.DeleteAsset(SkinPath);
        AssetDatabase.DeleteAsset(TtfPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.DeleteAsset(StreamRootAssets);

        // 비어 있게 된 StreamingAssets 상위 폴더 정리.
        try
        {
            string streamingAssetsAbs = Path.Combine(_projectRoot, "Assets/StreamingAssets");
            if (Directory.Exists(streamingAssetsAbs)
                && Directory.GetFileSystemEntries(streamingAssetsAbs).Length == 0)
            {
                AssetDatabase.DeleteAsset("Assets/StreamingAssets");
            }
        }
        catch { /* best-effort */ }

        AssetDatabase.Refresh();

        if (_config != null)
        {
            UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void ExternalizeThenRestore_SwapsSourceToStub_AndRestoresVerbatim()
    {
        // ── 환경 능력 게이트 ──
        // 외부화의 적용 경로는 대상 에셋을 WebGL AssetBundle 로 빌드한다(BuildFontBundle →
        // BuildPipeline.BuildAssetBundles(BuildTarget.WebGL)). WebGL 빌드 지원 모듈이 없으면 번들
        // 빌드가 실패해 외부화가 no-op 이 되어 왕복을 증명할 수 없다 — 거짓 GREEN/RED 대신 Ignore.
        // (E2E CI 러너는 WebGL 을 빌드하므로 이 경로가 실제로 실행되어 가드가 작동한다.)
        if (!WebGLBuildSupportInstalled())
        {
            Assert.Ignore("WebGL 빌드 지원 모듈 미설치 — 폰트 외부화 번들 빌드 불가. WebGL 지원 러너(CI)에서 검증됨.");
        }

        // 픽스처 사전조건: 소스 .ttf 가 Font 로 임포트되어 대상 에셋의 직접 의존성으로 노출돼야 한다.
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Font>(TtfPath),
            "사전조건: 소스 .ttf 가 Font 로 임포트되어야 함(SwapSourceToStub 대상).");
        string[] deps = AssetDatabase.GetDependencies(SkinPath, false);
        if (Array.IndexOf(deps, TtfPath) < 0)
        {
            // 이 Unity 버전에서 GUISkin.font 가 GetDependencies 에 노출되지 않으면 소스 해석이
            // 성립하지 않는다 — 픽스처 구성 불가로 보고 Ignore(거짓 실패 방지).
            Assert.Ignore("GUISkin.font 가 GetDependencies(direct) 에 노출되지 않음 — 소스 .ttf 해석 불가로 픽스처 구성 불가.");
        }

        // ── 원본 스냅샷 ──
        byte[] originalTtfBytes = File.ReadAllBytes(TtfFull);
        string originalTtfMeta = File.ReadAllText(TtfMetaFull);
        string originalSkinMeta = File.ReadAllText(SkinMetaFull);
        byte[] stubBytes = Convert.FromBase64String(StubTtfBase64);
        Assert.IsFalse(string.IsNullOrEmpty(originalTtfMeta), "사전조건: 소스 .ttf .meta 존재");
        Assert.IsFalse(File.Exists(BakFull), "사전조건: 아직 백업(.aitfontsrcbak~)이 없어야 함");

        // ── 적용(실 공개 진입점: ExternalizeForBuild) ──
        var handle = AITFontExternalizer.ExternalizeForBuild(_config);
        try
        {
            // "조용한 no-op" 방지: 외부화가 실제로 1건 수행됐는지 명시 단언(WebGL 지원 환경에서 필수 통과).
            Assert.IsTrue(handle != null && handle.Active,
                "외부화가 활성화되어야 함 — 비활성이면 번들 빌드/소스 해석 실패로 왕복 시나리오 미성립.");
            Assert.AreEqual(1, handle.Count, "대상 1건(GUISkin 이 참조하는 소스 .ttf)만 외부화되어야 함.");

            // 변형이 ACTUALLY 일어났는지: 백업 생성 + 소스가 스텁 바이트로 치환 + 매니페스트 동봉.
            Assert.IsTrue(File.Exists(BakFull),
                "적용 후 소스 백업(.aitfontsrcbak~)이 존재해야 함(원본 바이트 보관).");
            byte[] appliedBytes = File.ReadAllBytes(TtfFull);
            Assert.IsTrue(AreBytesEqual(stubBytes, appliedBytes),
                "적용 후 소스 .ttf 는 612B 스텁으로 치환되어야 함(.data 에서 폰트 바이트 제거).");
            if (_sourceIsPadded)
            {
                Assert.IsFalse(AreBytesEqual(originalTtfBytes, appliedBytes),
                    "적용 후 소스는 원본(패딩본)과 달라야 함 — 바이트 왕복 단언이 비-공허함을 보장.");
            }
            Assert.IsTrue(File.Exists(ManifestFull), "적용 후 StreamingAssets 매니페스트가 존재해야 함.");
            Assert.IsTrue(File.Exists(MarkerFull), "적용 중 마커(.ait-fontstream-active)가 존재해야 함.");
        }
        finally
        {
            // ── 복원(실 공개 진입점: RestoreForBuild) — 검증 대상 그 자체 ──
            AITFontExternalizer.RestoreForBuild(handle);
            AssetDatabase.Refresh();
        }

        // ── 검증: 소스 .ttf 바이트/.meta 가 원본과 바이트 단위로 동일 + 잔존물 전무 ──
        byte[] restoredBytes = File.ReadAllBytes(TtfFull);
        Assert.IsTrue(AreBytesEqual(originalTtfBytes, restoredBytes),
            "복원 후 소스 .ttf 는 원본과 바이트 단위로 동일해야 함(스텁 잔존 = 파트너 폰트 영구 파손 버그).");
        Assert.AreEqual(originalTtfMeta, File.ReadAllText(TtfMetaFull),
            "복원 후 소스 .ttf .meta 는 원본과 동일해야 함(임포터 설정 오염 금지).");
        Assert.AreEqual(originalSkinMeta, File.ReadAllText(SkinMetaFull),
            "복원 후 대상 에셋(GUISkin) .meta 는 원본과 동일해야 함(assetBundleName 등 오염 금지).");

        // skip/파손 복원의 네거티브 컨트롤 — 아래가 하나라도 남으면 복원이 안 된 것.
        Assert.IsFalse(File.Exists(BakFull), "복원 후 백업(.aitfontsrcbak~)이 삭제되어야 함.");
        Assert.IsFalse(File.Exists(MarkerFull), "복원 후 마커(.ait-fontstream-active)가 제거되어야 함.");
        Assert.IsFalse(Directory.Exists(StreamRootFull),
            "복원 후 StreamingAssets/ait-stream-font 디렉토리가 제거되어야 함.");
    }

    // ─────────────────────────── 헬퍼 ───────────────────────────

    /// <summary>WebGL 빌드 지원(플레이백 엔진) 설치 여부. 미설치면 빈 경로를 반환한다(공개 API).</summary>
    private static bool WebGLBuildSupportInstalled()
    {
        try
        {
            string dir = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.WebGL, BuildOptions.None);
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
        }
        catch
        {
            // 능력 판별 불가 시 보수적으로 "설치됨"으로 보고 진행(하드 단언에 맡김).
            return true;
        }
    }

    private static bool AreBytesEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }
}
