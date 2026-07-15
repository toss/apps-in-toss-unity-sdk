// AITFontSubsetProcessorRestoreTests
//
// 폰트 CJK subset(기본 ON) 프로세서의 apply→restore 왕복이 모든 변형을 완전히 되돌리는지 검증하는
// 회귀 가드 (Level 1 — 실 AssetDatabase / 실 파일 왕복).
//
// AITConvertCore.BuildWebGL 은 BuildPipeline.BuildPlayer 직전에 AITFontSubsetProcessor.ApplyForBuild 를,
// try/finally 의 finally 에서 AITFontSubsetProcessor.RestoreForBuild 를 호출한다(오디오 스트리밍과 동일 패턴).
// APPLY 의 비파괴 계약(프로세서 소스 주석에 문서화):
//   - 소스 원본을 <src>.aitfontbak 로 백업한 뒤, 소스 .ttf/.otf 바이트를 subset 산출물로 치환한다.
//   - 진행 마커(Assets/.ait-fontsubset-active)를 남긴다(비정상 종료 시 다음 로드 안전망 트리거용).
// RESTORE 계약:
//   - Assets 트리의 모든 *.aitfontbak 를 원본으로 되돌리고 백업 파일을 삭제한다.
//   - 진행 마커를 제거한다.
//
// ★ 이 가드가 없으면: RESTORE 가 깨질 경우 파트너의 소스 폰트가 "보존 범위만 남긴 subset"으로
//   빌드마다 영구 치환되고(16MB CJK → ~0.1MB), .aitfontbak/마커 잔존물이 파트너 repo 를 오염시킨다.
//   기존 폰트 테스트(AITFontSubsetAutoTests.cs)는 Level 0 순수 로직(블록 완성/범위 포맷/스캐너)만 다루고
//   ApplyForBuild/RestoreForBuild 왕복은 전혀 커버하지 않는다 — 이 파일이 그 공백을 메운다.
//
// ── 픽스처 전략 ──
//   실 subset(harfbuzz/subset-font)은 SDK 내장 Node.js + npm 모듈을 요구하는 외부 도구라 오프라인
//   batchmode 에서 결정적으로 실행되지 않는다. 그래서 검증을 둘로 나눈다:
//     (1) RestoreForBuild_AfterStagedSubset_...  — 외부 도구 없이 항상 실행되는 결정적 복원 가드.
//         ApplyForBuild 가 남기는 on-disk 상태(백업 + subset 치환 소스 + 마커)를 그 문서화된 계약대로
//         충실히 재현한 뒤, 실 공개 진입점 RestoreForBuild 가 소스 바이트·.meta 를 원본과 바이트 단위로
//         되돌리고 백업/마커를 제거하는지 단언한다(harfbuzz 불필요 — 복원은 파일 복사+reimport 이므로).
//     (2) ApplyForBuild_RealEntryPoint_...       — 내장 Node.js 가 이미 준비된 환경에서만 실 APPLY
//         진입점을 그대로 호출해 진짜 subset→복원 왕복을 end-to-end 로 검증한다(다운로드 미유발 프로브로
//         게이트; 미준비 시 Ignore). 도구 미준비 시엔 apply 가 '절반만 적용된' 위험 상태를 남기지 않는
//         graceful degradation 계약을 검증한다.
//
//   두 테스트 모두 저장소의 실 폰트를 건드리지 않는다 — 샘플 CJK 폰트 바이트를 임시 에셋으로 복사한
//   뒤 그 사본에서만 동작하고 TearDown 에서 정리한다.

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITFontSubsetProcessorRestoreTests
{
    private const string TempDir = "Assets/AITTest_FontSubsetRestore";
    private const string FixturePath = TempDir + "/font_restore_fixture.otf";

    // 프로세서 소스에 문서화된 백업 접미사 / 진행 마커(테스트가 아는 계약값 — 오디오 테스트가
    // .aitstreambak / .ait-audioreencode-active 를 아는 것과 동일).
    private const string BackupSuffix = ".aitfontbak";
    private const string Marker = "Assets/.ait-fontsubset-active";

    private string _projectRoot;
    private byte[] _originalFontBytes;   // 샘플 폰트 원본 바이트(못 찾으면 null → 테스트 Ignore)
    private string _sampleFontAssetPath; // 진단 로그용
    private AITEditorScriptObject _config;

    private string FixtureFull => Path.Combine(_projectRoot, FixturePath);
    private string FixtureMetaFull => FixtureFull + ".meta";

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;

        // 샘플 프로젝트에 동봉된 실 CJK 폰트 바이트를 확보(경로는 프로젝트 변형마다 다를 수 있어 GUID 검색).
        _originalFontBytes = TryLoadSampleFontBytes(_projectRoot, out _sampleFontAssetPath);
        if (_originalFontBytes == null)
        {
            return; // 폰트 픽스처 부재 → 각 테스트가 Ignore 처리
        }

        Directory.CreateDirectory(Path.Combine(_projectRoot, TempDir));

        // 유효한 실 폰트 바이트를 임시 에셋으로 복사 → Unity 가 클린 임포트(무-에러) 후 실 .meta 생성.
        File.WriteAllBytes(FixtureFull, _originalFontBytes);
        AssetDatabase.ImportAsset(FixturePath, ImportAssetOptions.ForceSynchronousImport);

        // 프로세서를 임시 폰트로 스코프. 수동 대상 → DetectAutoTargets/1MB 크기 게이트/자동 TMP 제외 우회.
        // 수동 보존 범위 → AITFontUsedCharScanner.ScanProject(프로젝트 전수 스캔) 우회로 결정성 확보.
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.fontSubset = 1;                          // 명시적 ON
        _config.fontSubsetTargetPaths = FixturePath;     // 수동 대상(override)
        _config.fontSubsetUnicodeRanges = "U+0020-007E,U+AC00-AC01"; // ASCII + 한글 일부(NotoSans 에 존재)
        _config.fontSubsetExtraRanges = string.Empty;
        _config.fontSubsetExcludeTargetPaths = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        // 실패 테스트에서도 샘플 프로젝트를 더럽히지 않도록 잔존물을 전부 제거(best-effort).
        try
        {
            string assetsAbs = Application.dataPath;
            foreach (var bak in Directory.GetFiles(assetsAbs, "*" + BackupSuffix, SearchOption.AllDirectories))
            {
                try { File.Delete(bak); } catch { /* best-effort */ }
            }
        }
        catch { /* best-effort */ }

        if (_projectRoot != null)
        {
            string marker = Path.Combine(_projectRoot, Marker);
            try { if (File.Exists(marker)) { File.Delete(marker); } } catch { /* best-effort */ }
        }

        AssetDatabase.DeleteAsset(FixturePath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

        if (_config != null)
        {
            UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (1) 결정적 복원 가드 — 외부 도구 불필요, 항상 실행, 비-공허.
    //     ApplyForBuild 가 남기는 on-disk 상태를 재현 → 실 RestoreForBuild 가 verbatim 복원하는지 검증.
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void RestoreForBuild_AfterStagedSubset_RestoresSourceBytesAndMetaVerbatim()
    {
        RequireFixture();

        byte[] original = File.ReadAllBytes(FixtureFull);
        string originalMeta = File.ReadAllText(FixtureMetaFull);
        Assert.IsFalse(string.IsNullOrEmpty(originalMeta), "사전조건: 폰트 .meta 가 존재해야 함");

        string bak = FixtureFull + BackupSuffix;
        string markerFull = Path.Combine(_projectRoot, Marker);

        // ── APPLY 재현: ApplyForBuild 의 문서화된 파일 효과를 그대로 스테이징 ──
        //   (1) 원본을 <src>.aitfontbak 로 백업  (2) 소스를 subset 산출물로 치환  (3) 진행 마커 생성.
        //   harfbuzz subset 자체는 외부 도구라 batchmode 결정성이 없으므로 '그 파일 효과'만 재현한다.
        //   (subset 치환 소스는 reimport 하지 않는다 — 복원이 원본을 되돌린 뒤 reimport 하므로 임포트
        //    노이즈가 없다. 복원 계약 검증에는 on-disk 상태 재현으로 충분하다.)
        File.Copy(FixtureFull, bak, true);
        byte[] subsetStub = MakeSmallerDistinctPayload(original); // 실 subset: 16MB → ~0.1MB 축소를 모사
        File.WriteAllBytes(FixtureFull, subsetStub);
        File.WriteAllText(markerFull, "active");

        // ── 비-공허 전제: '적용됨' 상태가 실제로 성립했는지(변환이 진짜 일어났는지) ──
        Assert.IsTrue(File.Exists(bak), "APPLY 재현: 원본 백업(.aitfontbak)이 존재해야 함");
        Assert.IsFalse(AreBytesEqual(original, File.ReadAllBytes(FixtureFull)),
            "APPLY 재현: 소스가 subset 으로 치환되어 원본과 달라야 함(테스트가 공허하지 않다는 전제).");
        Assert.Less(new FileInfo(FixtureFull).Length, original.Length,
            "APPLY 재현: subset 소스는 원본보다 작아야 함");
        Assert.IsTrue(File.Exists(markerFull), "APPLY 재현: 진행 마커가 존재해야 함");

        // ── 검증 대상: 실 공개 복원 진입점(BuildWebGL 의 finally 가 호출하는 바로 그 메서드) ──
        //   핸들은 Active/Count 만 운반하며 실제 복원은 on-disk *.aitfontbak 로 구동된다.
        AITFontSubsetProcessor.RestoreForBuild(
            new AITFontSubsetProcessor.FontHandle { Active = true, Count = 1 });

        // ── VERBATIM 복원 단언 ──
        Assert.IsTrue(AreBytesEqual(original, File.ReadAllBytes(FixtureFull)),
            "복원 후 폰트 소스는 원본과 바이트 단위로 동일해야 함(subset 잔존 = 파트너 폰트 영구 절단 버그).");
        Assert.AreEqual(originalMeta, File.ReadAllText(FixtureMetaFull),
            "복원 후 .meta 는 원본과 동일해야 함(임포트 설정 오염 방지).");
        Assert.IsFalse(File.Exists(bak),
            "복원 후 백업(.aitfontbak)이 삭제되어야 함(잔존 = 파트너 repo 에 백업 파일 오염).");
        Assert.IsFalse(File.Exists(markerFull),
            "복원 후 진행 마커가 제거되어야 함(잔존 = 다음 에디터 로드에서 안전망 오발동).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (2) 실 APPLY 진입점 왕복 — 내장 Node.js 준비 환경에서만 진짜 subset→복원을 end-to-end 검증.
    //     미준비(오프라인 batchmode) 시 Ignore. 도구 실패 시 graceful no-op 안전 계약 검증.
    // ─────────────────────────────────────────────────────────────────────────
    [Test]
    public void ApplyForBuild_RealEntryPoint_SubsetsThenRestoresVerbatim_OrGracefullyNoOps()
    {
        RequireFixture();

        // 실 subset 은 내장 Node.js + subset-font(npm)를 요구한다. 다운로드를 유발하지 않는 프로브로 게이트.
        string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: false);
        if (string.IsNullOrEmpty(npm))
        {
            Assert.Ignore("내장 Node.js 미준비(오프라인 batchmode) — 실 subset 왕복 생략. " +
                "결정적 복원 가드는 RestoreForBuild_AfterStagedSubset_RestoresSourceBytesAndMetaVerbatim 가 담당한다.");
        }

        // 2차 게이트: subset-font(npm) 모듈이 아직 캐시되지 않았다면 ApplyForBuild → EnsureTool 이
        // 1회 `npm install`(네트워크, 최대 180s)을 유발한다. 이 테스트는 "다운로드 미유발" 프로브 계약이므로
        // 모듈이 이미 설치된 환경에서만 실 왕복을 검증하고, 미설치면 Ignore 한다(EditMode 테스트의 네트워크
        // 접근·비결정성 방지). 경로는 프로세서 GetHomeToolDir()(~/.ait-unity-sdk/font-subset) 미러.
        string homeToolBase = AITPlatformHelper.IsWindows
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string subsetFontInstalled = Path.Combine(
            homeToolBase, ".ait-unity-sdk", "font-subset", "node_modules", "subset-font", "package.json");
        if (!File.Exists(subsetFontInstalled))
        {
            Assert.Ignore("subset-font(npm) 모듈 미캐시 — 실 subset 호출 시 네트워크 npm install 유발 위험으로 생략. " +
                "결정적 복원 가드는 RestoreForBuild_AfterStagedSubset_RestoresSourceBytesAndMetaVerbatim 가 담당한다.");
        }

        byte[] original = File.ReadAllBytes(FixtureFull);
        string originalMeta = File.ReadAllText(FixtureMetaFull);
        string bak = FixtureFull + BackupSuffix;
        string markerFull = Path.Combine(_projectRoot, Marker);

        // ── 실 APPLY 진입점 호출 ──
        var handle = AITFontSubsetProcessor.ApplyForBuild(_config);
        Assert.IsNotNull(handle, "ApplyForBuild 는 항상 non-null 핸들을 반환해야 함");

        if (!handle.Active)
        {
            // subset-font 모듈 미설치 + 오프라인 등으로 도구 준비 실패 → graceful degradation.
            // 핵심 안전 계약: '절반만 적용된' 위험 상태(백업 없이 소스만 치환)를 절대 남기지 않는다.
            Assert.IsTrue(AreBytesEqual(original, File.ReadAllBytes(FixtureFull)),
                "graceful no-op: subset 불가 시 소스 폰트는 원본 그대로여야 함(무백업 치환 금지).");
            Assert.IsFalse(File.Exists(bak), "graceful no-op: 백업이 생성되지 않아야 함");
            Assert.IsFalse(File.Exists(markerFull), "graceful no-op: 진행 마커가 남지 않아야 함");
            Debug.Log($"[AITFontSubsetProcessorRestoreTests] subset 도구 미준비 → graceful no-op 계약만 검증 " +
                $"(픽스처: {_sampleFontAssetPath}).");
            return;
        }

        // ── 실 subset 성공: 변환이 실제로 일어났는지 단언(비-공허) → finally 에서 실 복원 ──
        try
        {
            Assert.GreaterOrEqual(handle.Count, 1, "최소 1개 폰트가 subset 되어야 함");
            Assert.IsTrue(File.Exists(bak), "subset 후 원본 백업(.aitfontbak)이 존재해야 함");
            Assert.IsFalse(AreBytesEqual(original, File.ReadAllBytes(FixtureFull)),
                "subset 후 소스 폰트 바이트가 실제로 바뀌어야 함(변환이 진짜 일어났다는 전제).");
            Assert.Less(new FileInfo(FixtureFull).Length, original.Length,
                "subset 후 소스 폰트는 원본보다 작아야 함(CJK 전체 → 보존 범위만).");
            Assert.IsTrue(File.Exists(markerFull), "subset 진행 중 마커가 존재해야 함");
        }
        finally
        {
            // ── 검증 대상: 실 공개 복원 진입점 ──
            AITFontSubsetProcessor.RestoreForBuild(handle);
        }

        // ── VERBATIM 복원 단언 ──
        Assert.IsTrue(AreBytesEqual(original, File.ReadAllBytes(FixtureFull)),
            "복원 후 폰트 소스는 원본과 바이트 단위로 동일해야 함(subset 잔존 = 파트너 폰트 영구 절단).");
        Assert.AreEqual(originalMeta, File.ReadAllText(FixtureMetaFull),
            "복원 후 .meta 는 원본과 동일해야 함");
        Assert.IsFalse(File.Exists(bak), "복원 후 백업(.aitfontbak)이 삭제되어야 함");
        Assert.IsFalse(File.Exists(markerFull), "복원 후 진행 마커가 제거되어야 함");
    }

    // ─────────────────────────── 헬퍼 ───────────────────────────

    private void RequireFixture()
    {
        if (_originalFontBytes == null)
        {
            Assert.Ignore("샘플 CJK 폰트(NotoSansKR-Regular)를 찾지 못함 — 이 프로젝트 변형에는 " +
                "폰트 픽스처가 없어 검증을 생략한다.");
        }
    }

    /// <summary>샘플 프로젝트에 동봉된 실 폰트(.ttf/.otf) 바이트를 GUID 검색으로 확보. 없으면 null.</summary>
    private static byte[] TryLoadSampleFontBytes(string projectRoot, out string foundAssetPath)
    {
        foundAssetPath = null;
        foreach (var guid in AssetDatabase.FindAssets("NotoSansKR-Regular t:Font"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(p))
            {
                continue;
            }

            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (ext != ".otf" && ext != ".ttf")
            {
                continue;
            }

            string full = Path.Combine(projectRoot, p);
            if (File.Exists(full))
            {
                foundAssetPath = p;
                return File.ReadAllBytes(full);
            }
        }

        return null;
    }

    /// <summary>
    /// 원본보다 확실히 작고 내용이 다른 바이트 페이로드(실 subset 산출물의 스탠드인).
    /// 프리픽스 복사 후 첫 바이트를 반전해 길이·내용 모두 원본과 다름을 보장한다.
    /// </summary>
    private static byte[] MakeSmallerDistinctPayload(byte[] original)
    {
        int len = Math.Min(120000, Math.Max(1, original.Length / 4));
        var stub = new byte[len];
        Array.Copy(original, stub, len);
        stub[0] = (byte)(stub[0] ^ 0xFF);
        return stub;
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
