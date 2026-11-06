// -----------------------------------------------------------------------
// AITFontExternalizerPackagesSourceGateTests.cs - 소스 폰트 Packages/ 게이트 회귀 가드
//
// 버그: AITFontExternalizer 의 후보 선정(GetFontStreamingCandidates)·계획 수립(ExternalizeForBuild
// Phase A) 은 TMP_FontAsset 경로에는 Assets/ 게이트를 걸면서, 실제로 바이트를 파괴하는 대상인
// ★소스 .ttf/.otf 경로★에는 같은 게이트가 없었다. Assets/ 의 TMP 폰트가 Packages/(UPM 패키지)의
// 대형 소스 폰트를 참조하면, fontStreaming(기본 자동 ON)이 패키지 원본 파일을 612B 스텁으로
// 치환하고 백업(.aitfontsrcbak~)은 Assets/ 전용 복원 스캔(RestoreAllBackups, Application.dataPath
// 하위 재귀)이 영원히 찾지 못하는 위치(Packages/)에 남는다 — 해당 폰트 전역 tofu + 복원 불가.
//
// 고침: AITFontExternalizer.IsSourceFontPathAllowed(srcFontPath) 를 "순수 내부 헬퍼" 로 추출하고,
// (1) GetFontStreamingCandidates() 의 preFiltered 루프, (2) ExternalizeForBuild Phase A(plan.Add
// 직전) 양쪽에서 소스가 Assets/ 로 시작하지 않으면 후보/계획에서 제외한다. 파괴적 쓰기
// (SwapSourceToStub, Phase C)의 유일한 호출 경로는 Phase A 가 채운 plan → built 이므로, 이 두
// 게이트 뒤에 있으면 파괴적 쓰기 경로 전부가 커버된다.
//
// Level 0: IsSourceFontPathAllowed 순수 로직(AssetDatabase 비의존, 문자열 판정만).
// Level 1: 실 AssetDatabase — 이 저장소 테스트 패키지의 비임포트 원본 폰트
//   (Packages/im.toss.sdk-test-scripts/Runtime/Fonts~/NotoSansKR-Regular.otf, ~16MB — "패키지
//   Runtime/Resources/ 는 무조건 빌드 포함" 규칙을 피하려 비임포트 "~" 폴더에 있어 AssetDatabase 로
//   직접 로드할 수 없다)를 SetUp에서 이 패키지 내 "임포트되는" 경로
//   (Runtime/GateTestFixtures/gate_test_font.otf, "~" 접미 아님)로 File.Copy + AssetDatabase.Refresh
//   해 스테이징한다(로컬 file: 패키지는 mutable이라 가능). 그 사본(자동 스캔 1MB 임계값을 실제로 넘어
//   이 게이트가 없으면 정말로 후보에 들어갔을 것임을 보장)을 소스로 하는 TMP_FontAsset 을 생성해,
//   자동 스캔 후보/수동 모드 계획 양쪽에서 제외되는지 검증한다. TearDown에서 스테이징한 사본 +
//   .meta + 빈 폴더를 best-effort 로 제거한다(2차 안전망: Tests~/E2E/SharedScripts/.gitignore 의
//   Runtime/GateTestFixtures/ 규칙). TMP_FontAsset 생성 리플렉션 패턴은 DeployProbeBuildRunner.cs 의
//   TryGenerateProbeFontAsset(약 :732-822)/EnsureTmpEssentialResources(약 :824-873) 를 그대로
//   따른다. 원본(Fonts~)은 ★절대 수정하지 않는다★(읽기만).

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

// ─────────────────────────── Level 0: 순수 로직 ───────────────────────────

[TestFixture]
public class AITFontExternalizerIsSourceFontPathAllowedTests
{
    [Test]
    public void IsSourceFontPathAllowed_AssetsPrefixedPath_ReturnsTrue()
    {
        Assert.IsTrue(AITFontExternalizer.IsSourceFontPathAllowed("Assets/Fonts/Foo.ttf"),
            "Assets/ 로 시작하는 소스 경로는 허용되어야 함.");
    }

    [Test]
    public void IsSourceFontPathAllowed_PackagesPrefixedPath_ReturnsFalse()
    {
        Assert.IsFalse(
            AITFontExternalizer.IsSourceFontPathAllowed(
                "Packages/im.toss.sdk-test-scripts/Runtime/Resources/Fonts/NotoSansKR-Regular.otf"),
            "Packages/ 로 시작하는 소스 경로는 거부되어야 함 — 이 저장소의 모든 콘텐츠 최적화 레버는 " +
            "Assets/ 하위만 대상이라는 계약을 위반함.");
    }

    [Test]
    public void IsSourceFontPathAllowed_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(AITFontExternalizer.IsSourceFontPathAllowed(null), "null 은 거부되어야 함.");
        Assert.IsFalse(AITFontExternalizer.IsSourceFontPathAllowed(string.Empty), "빈 문자열은 거부되어야 함.");
    }

    [Test]
    public void IsSourceFontPathAllowed_NonAssetsRelativePath_ReturnsFalse()
    {
        // Assets/ 도 Packages/ 도 아닌(예: 프로젝트 루트 상대) 경로 — 보수적으로 거부.
        Assert.IsFalse(AITFontExternalizer.IsSourceFontPathAllowed("SomeFolder/Foo.ttf"),
            "Assets/ 접두가 없는 경로는 거부되어야 함(화이트리스트 방식).");
    }
}

// ─────────────────────────── Level 1: 실 AssetDatabase ───────────────────────────

[TestFixture]
public class AITFontExternalizerPackagesSourceCandidateExclusionTests
{
    // 이 저장소 테스트 패키지(Tests~/E2E/SharedScripts, package.json name=im.toss.sdk-test-scripts)의
    // 비임포트 원본(대형 ~16MB, 절대 수정하지 않는다 — 읽기만) 및 SetUp이 스테이징하는 임포트 가능한 사본.
    private const string PackagesRawFontRelativePath = "Runtime/Fonts~/NotoSansKR-Regular.otf";
    private const string GateFixtureRelativePath = "Runtime/GateTestFixtures/gate_test_font.otf";
    private const string PackagesSourceFontPath = "Packages/im.toss.sdk-test-scripts/" + GateFixtureRelativePath;

    private const string TempDir = "Assets/AITTest_FontExternalizerPackagesGate";
    private const string TmpFontAssetPath = TempDir + "/PackagesSourceProbeFontAsset.asset";

    private const string SrcBackupSuffix = ".aitfontsrcbak~";
    private const string Marker = "Assets/.ait-fontstream-active";

    private string _projectRoot;
    private AITEditorScriptObject _config;
    private bool _fixtureReady;
    private string _skipReason;

    /// <summary>SetUp이 스테이징한 게이트 픽스처의 실 파일시스템 경로(TearDown 정리 대상).</summary>
    private string _gateFixturePhysicalPath;

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        _fixtureReady = false;
        _skipReason = null;
        _gateFixturePhysicalPath = null;

        Type fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
        if (fontAssetType == null)
        {
            _skipReason = "TMP(Unity.TextMeshPro) 미설치 — TMP_FontAsset 생성 불가.";
            return;
        }

        if (!TryStagePackagesSourceFixture(out _skipReason))
        {
            return;
        }

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(PackagesSourceFontPath);
        if (sourceFont == null)
        {
            _skipReason = $"게이트 테스트 픽스처 로드 실패: {PackagesSourceFontPath} " +
                "(im.toss.sdk-test-scripts 패키지가 이 환경에 해석되지 않음).";
            return;
        }

        EnsureTmpEssentialResources(fontAssetType);
        if (Shader.Find("TextMeshPro/Distance Field") == null &&
            Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
        {
            _skipReason = "TMP SDF 셰이더 미가용(Essential Resources 임포트 미반영) — TMP_FontAsset 생성 불가.";
            return;
        }

        string dirAbs = Path.Combine(_projectRoot, TempDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        var createMethod = fontAssetType.GetMethod(
            "CreateFontAsset",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(Font) },
            null);
        if (createMethod == null)
        {
            _skipReason = "TMP_FontAsset.CreateFontAsset(Font) 오버로드를 찾지 못함.";
            return;
        }

        object fontAssetObj;
        try
        {
            fontAssetObj = createMethod.Invoke(null, new object[] { sourceFont });
        }
        catch (Exception e)
        {
            Exception root = e;
            while (root is TargetInvocationException tie && tie.InnerException != null)
            {
                root = tie.InnerException;
            }
            _skipReason = $"TMP_FontAsset 생성 예외({root.GetType().Name}: {root.Message}) — 이 TMP 버전에서 " +
                "CreateFontAsset(Font) 리플렉션 호출 불가.";
            return;
        }

        var mainAsset = fontAssetObj as UnityEngine.Object;
        if (mainAsset == null)
        {
            _skipReason = "TMP_FontAsset 생성 실패(null 반환).";
            return;
        }

        AssetDatabase.CreateAsset(mainAsset, TmpFontAssetPath);
        EditorUtility.SetDirty(mainAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(TmpFontAssetPath, ImportAssetOptions.ForceSynchronousImport);

        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.fontStreaming = 1; // 수동 모드 — Phase A 게이트를 직접 겨냥.
        _config.fontStreamingTargetPaths = TmpFontAssetPath;
        _config.fontStreamingMaxConcurrent = 2;

        _fixtureReady = true;
    }

    [TearDown]
    public void TearDown()
    {
        // SetUp이 패키지 내부에 스테이징한 게이트 픽스처(사본) 정리 — best-effort. 원본(Fonts~)은
        // 건드리지 않는다(_gateFixturePhysicalPath는 그 사본만 가리킨다).
        if (!string.IsNullOrEmpty(_gateFixturePhysicalPath))
        {
            try { if (File.Exists(_gateFixturePhysicalPath)) File.Delete(_gateFixturePhysicalPath); }
            catch { /* best-effort */ }

            try
            {
                string metaPath = _gateFixturePhysicalPath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
            catch { /* best-effort */ }

            try
            {
                string dirAbs = Path.GetDirectoryName(_gateFixturePhysicalPath);
                if (!string.IsNullOrEmpty(dirAbs) && Directory.Exists(dirAbs) &&
                    Directory.GetFileSystemEntries(dirAbs).Length == 0)
                {
                    Directory.Delete(dirAbs);
                    string dirMeta = dirAbs + ".meta";
                    if (File.Exists(dirMeta)) File.Delete(dirMeta);
                }
            }
            catch { /* best-effort */ }

            _gateFixturePhysicalPath = null;
            AssetDatabase.Refresh();
        }

        // Packages/ 원본(Fonts~)은 절대 건드리지 않는다 — Assets/ 쪽 픽스처/잔존물만 정리(베스트 에포트).
        try
        {
            string assetsAbs = Application.dataPath;
            foreach (var bak in Directory.GetFiles(assetsAbs, "*" + SrcBackupSuffix, SearchOption.AllDirectories))
            {
                try { File.Delete(bak); } catch { /* best-effort */ }
            }
        }
        catch { /* best-effort */ }

        try
        {
            string markerFull = Path.Combine(_projectRoot ?? Directory.GetParent(Application.dataPath).FullName, Marker);
            if (File.Exists(markerFull)) { File.Delete(markerFull); }
        }
        catch { /* best-effort */ }

        AssetDatabase.DeleteAsset(TmpFontAssetPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

        if (_config != null)
        {
            UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void GetFontStreamingCandidates_ExcludesTmpFontAssetWithPackagesSource()
    {
        if (!_fixtureReady)
        {
            Assert.Ignore(_skipReason);
        }

        // 사전조건: 대상 TMP_FontAsset 이 실제로 Packages/ 소스를 ★직접 의존성★으로 갖는지 확인
        // (ResolveSourceFont 와 동일 조건 — 안 그러면 이 테스트는 아무것도 증명하지 못함).
        string[] deps = AssetDatabase.GetDependencies(TmpFontAssetPath, false);
        bool hasPackagesSource = Array.IndexOf(deps, PackagesSourceFontPath) >= 0;
        if (!hasPackagesSource)
        {
            Assert.Ignore("이 TMP 버전에서 생성된 TMP_FontAsset 이 소스 Font 를 직접 의존성으로 노출하지 않음 " +
                "— 픽스처 구성 불가로 판단, 거짓 실패 방지를 위해 Ignore.");
        }

        // 사전조건: 게이트가 없다면 이 자산이 진짜로 후보였을 것임을 보장(비-공허 테스트) — 크기 ≥ 1MB.
        // (SetUp이 스테이징한 실 파일 경로를 사용 — Packages/ 가상 경로가 항상 프로젝트 루트 기준 실
        //  파일시스템 경로로 그대로 결합된다고 가정하지 않는다.)
        Assert.IsTrue(File.Exists(_gateFixturePhysicalPath),
            $"사전조건: 게이트 픽스처 실파일 확인({_gateFixturePhysicalPath}).");
        Assert.GreaterOrEqual(new FileInfo(_gateFixturePhysicalPath).Length, 1 * 1024 * 1024,
            "사전조건: 게이트 픽스처(원본 폰트 사본)가 자동 스캔 1MB 임계값 이상이어야 이 테스트가 비-공허함.");

        // ── 검증 대상 그 자체: 자동 스캔 후보 목록 ──
        string[] candidates = AITFontExternalizer.GetFontStreamingCandidates();

        Assert.IsFalse(Array.IndexOf(candidates, TmpFontAssetPath) >= 0,
            "소스가 Packages/ 인 TMP_FontAsset 은 자동 스캔 후보에서 제외되어야 함 " +
            "(회귀 시: 패키지 원본이 스텁으로 치환되고 Assets/ 전용 복원 스캔이 못 찾아 영구 파손).");
    }

    [Test]
    public void ExternalizeForBuild_ManualMode_SkipsPackagesSourcedTarget()
    {
        if (!_fixtureReady)
        {
            Assert.Ignore(_skipReason);
        }

        string[] deps = AssetDatabase.GetDependencies(TmpFontAssetPath, false);
        if (Array.IndexOf(deps, PackagesSourceFontPath) < 0)
        {
            Assert.Ignore("이 TMP 버전에서 생성된 TMP_FontAsset 이 소스 Font 를 직접 의존성으로 노출하지 않음 " +
                "— 픽스처 구성 불가로 판단, 거짓 실패 방지를 위해 Ignore.");
        }

        // ── 검증 대상: 수동 모드(fontStreamingTargetPaths=대상 1건) — Phase A 게이트를 직접 겨냥 ──
        var handle = AITFontExternalizer.ExternalizeForBuild(_config);
        try
        {
            Assert.IsFalse(handle != null && handle.Active,
                "Packages/ 소스만 있는 대상은 계획 수립 단계(Phase A)에서 제외되어 외부화가 비활성이어야 함.");
            Assert.AreEqual(0, handle?.Count ?? 0, "외부화된 폰트 개수는 0이어야 함(대상이 게이트에 걸림).");

            // 파괴적 쓰기(SwapSourceToStub)가 전혀 시도되지 않았어야 함 — 백업 사이드카가 어디에도 없어야 함.
            string[] backups = Directory.GetFiles(Application.dataPath, "*" + SrcBackupSuffix, SearchOption.AllDirectories);
            Assert.AreEqual(0, backups.Length,
                "소스 스텁 치환 시도가 없어야 하므로 .aitfontsrcbak~ 백업이 하나도 생기면 안 됨.");
        }
        finally
        {
            AITFontExternalizer.RestoreForBuild(handle);
            AssetDatabase.Refresh();
        }
    }

    // ─────────────────────────── 헬퍼: 게이트 픽스처 스테이징 ───────────────────────────

    /// <summary>
    /// 패키지 비임포트 원본(Runtime/Fonts~/NotoSansKR-Regular.otf)을 이 패키지 내 "임포트되는" 경로
    /// (Runtime/GateTestFixtures/gate_test_font.otf)로 복사해 AssetDatabase 가 Font 로 인식하게
    /// 만든다. 원본이 "패키지 Runtime/Resources/ 는 무조건 빌드 포함" 규칙을 피하려 비임포트
    /// "~" 폴더로 옮겨져 AssetDatabase.LoadAssetAtPath 로 직접 로드할 수 없기 때문이다. 성공 시
    /// _gateFixturePhysicalPath 를 채워 TearDown 정리 대상으로 남긴다. 실패 시 skipReason 을 채우고
    /// false 반환.
    /// </summary>
    private bool TryStagePackagesSourceFixture(out string skipReason)
    {
        skipReason = null;

        var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
            typeof(AITFontExternalizerPackagesSourceCandidateExclusionTests).Assembly);
        if (pkg == null || string.IsNullOrEmpty(pkg.resolvedPath))
        {
            skipReason = "im.toss.sdk-test-scripts 패키지의 물리 경로(resolvedPath) 해석 실패 — " +
                "게이트 픽스처 스테이징 불가.";
            return false;
        }

        string rawFontPath = ResolveNotoSansKrRawPath(pkg.resolvedPath);
        if (string.IsNullOrEmpty(rawFontPath))
        {
            skipReason = $"패키지 비임포트 원본을 찾지 못함({PackagesRawFontRelativePath}).";
            return false;
        }

        string destAbs = Path.Combine(
            pkg.resolvedPath, GateFixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string destDirAbs = Path.GetDirectoryName(destAbs);
        try
        {
            if (!string.IsNullOrEmpty(destDirAbs) && !Directory.Exists(destDirAbs))
            {
                Directory.CreateDirectory(destDirAbs);
            }
            File.Copy(rawFontPath, destAbs, overwrite: true);
        }
        catch (Exception e)
        {
            skipReason = $"게이트 픽스처 스테이징(File.Copy) 실패: {e.Message}";
            return false;
        }

        _gateFixturePhysicalPath = destAbs;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return true;
    }

    /// <summary>패키지 비임포트 원본(Runtime/Fonts~/NotoSansKR-Regular.otf)의 실 파일시스템 경로를
    /// 해석한다(AITSfntLiteTests.ResolveNotoSansKrPath 와 동일 관용구 — pkgResolvedPath 기준 우선,
    /// 실패 시 이 테스트 파일 자신의 물리적 위치 기준 CallerFilePath 상대 경로 폴백).</summary>
    private static string ResolveNotoSansKrRawPath(string pkgResolvedPath)
    {
        const string fileName = "NotoSansKR-Regular.otf";

        if (!string.IsNullOrEmpty(pkgResolvedPath))
        {
            string viaPkg = Path.Combine(pkgResolvedPath, "Runtime", "Fonts~", fileName);
            if (File.Exists(viaPkg))
            {
                return viaPkg;
            }
        }

        string thisFileDir = Path.GetDirectoryName(CallerFilePath());
        if (string.IsNullOrEmpty(thisFileDir))
        {
            return null;
        }

        // Editor/EditModeTests/ → (상위 2단계) → SharedScripts/ → Runtime/Fonts~/
        string viaSharedScriptsRelative = Path.GetFullPath(Path.Combine(
            thisFileDir, "..", "..", "Runtime", "Fonts~", fileName));
        return File.Exists(viaSharedScriptsRelative) ? viaSharedScriptsRelative : null;
    }

    private static string CallerFilePath(
        [System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    // ─────────────────────────── 헬퍼(DeployProbeBuildRunner.EnsureTmpEssentialResources 이관) ───────────────────────────

    /// <summary>TMP Essential Resources 를 1회 임포트(headless 안전, CI 결정성). 이미 임포트됐으면 멱등 skip.</summary>
    private static void EnsureTmpEssentialResources(Type fontAssetType)
    {
        try
        {
            const string marker = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(marker) != null)
            {
                return; // 이미 임포트됨.
            }

            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(fontAssetType.Assembly);
            if (pkg == null || string.IsNullOrEmpty(pkg.resolvedPath))
            {
                return;
            }

            string unityPackagePath = Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(unityPackagePath))
            {
                return;
            }

            var importImmediately = typeof(AssetDatabase).GetMethod(
                "ImportPackageImmediately",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { typeof(string) },
                null);
            if (importImmediately != null)
            {
                importImmediately.Invoke(null, new object[] { unityPackagePath });
            }
            else
            {
                AssetDatabase.ImportPackage(unityPackagePath, false);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AITFontExternalizerPackagesSourceGateTests] TMP Essential Resources 임포트 예외(무시): {e.Message}");
        }
    }
}
