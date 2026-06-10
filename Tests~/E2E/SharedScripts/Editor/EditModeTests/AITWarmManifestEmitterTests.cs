// -----------------------------------------------------------------------
// AITWarmManifestEmitterTests.cs - EditMode warm manifest 산출기 테스트
// Level 0: AITWarmManifestEmitter.WriteManifest 의 게이팅/스키마/인코딩/
//          결정성/플레이스홀더 안전 검증 (순수 파일 I/O, 빌드 불필요)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITWarmManifestEmitterTests
{
    // 테스트용 파일명 상수
    private const string LoaderFile    = "game.loader.js";
    private const string DataFile      = "game.data";
    private const string FrameworkFile = "game.framework.js";
    private const string WasmFile      = "game.wasm";
    private const string SymbolsFile   = "game.symbols.json";

    // 알려진 gzip raw 크기 검증용
    private const int KnownRawSize = 1024; // 바이트

    private string _tempDir;
    private string _buildDir;
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        // 임시 디렉토리 생성 (GUID 기반으로 충돌 방지)
        _tempDir = Path.Combine(Path.GetTempPath(), "AITWarmManifestTests_" + Guid.NewGuid().ToString("N"));
        _buildDir = Path.Combine(_tempDir, "Build");
        Directory.CreateDirectory(_buildDir);

        // 가짜 빌드 파일 생성
        CreateGzipFile(Path.Combine(_buildDir, DataFile), KnownRawSize);
        CreatePlainFile(Path.Combine(_buildDir, WasmFile),      256);
        CreatePlainFile(Path.Combine(_buildDir, FrameworkFile), 128);
        CreatePlainFile(Path.Combine(_buildDir, LoaderFile),     64);
        CreatePlainFile(Path.Combine(_buildDir, SymbolsFile),    32);

        // config 생성
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.enablePageCache  = true;
        _config.emitWarmManifest = true;
        _config.pageCacheName    = "ait-page-cache";
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        // 임시 디렉토리 정리
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // 정리 실패는 무시
        }
    }

    // ===== 케이스 1: emitWarmManifest=false → 파일 미산출, stale 파일 삭제 =====

    [Test]
    public void Disabled_NoFileEmitted_AndStaleDeleted()
    {
        // stale 파일을 미리 생성
        string manifestPath = Path.Combine(_tempDir, AITWarmManifestEmitter.FileName);
        File.WriteAllText(manifestPath, "stale", Encoding.UTF8);
        Assert.IsTrue(File.Exists(manifestPath), "SetUp: stale 파일이 있어야 한다");

        _config.emitWarmManifest = false;
        WriteManifest();

        Assert.IsFalse(File.Exists(manifestPath),
            "emitWarmManifest=false 이면 기존 stale 파일도 삭제되어야 한다");
    }

    // ===== 케이스 2: emitWarmManifest=true + enablePageCache=false → 경고 + 미산출 =====

    [Test]
    public void EnabledManifest_ButPageCacheDisabled_WarnsAndNoFile()
    {
        _config.enablePageCache  = false;
        _config.emitWarmManifest = true;

        // 경고 로그가 발생해야 한다
        LogAssert.Expect(LogType.Warning, new Regex("ait-warm-manifest\\.json 미산출"));

        WriteManifest();

        string manifestPath = Path.Combine(_tempDir, AITWarmManifestEmitter.FileName);
        Assert.IsFalse(File.Exists(manifestPath),
            "enablePageCache=false 이면 manifest 를 산출하지 않아야 한다");
    }

    // ===== 케이스 3: 정상 산출 — 파일 존재, 경로 포함, cacheName 에코 =====

    [Test]
    public void NormalEmit_FileExists_ContainsBuildPaths()
    {
        WriteManifest();

        string manifestPath = Path.Combine(_tempDir, AITWarmManifestEmitter.FileName);
        Assert.IsTrue(File.Exists(manifestPath), "manifest 파일이 존재해야 한다");

        string json = File.ReadAllText(manifestPath, Encoding.UTF8);

        StringAssert.Contains("Build/" + DataFile,      json, "data 파일 경로가 포함되어야 한다");
        StringAssert.Contains("Build/" + FrameworkFile, json, "framework 파일 경로가 포함되어야 한다");
        StringAssert.Contains("Build/" + WasmFile,      json, "wasm 파일 경로가 포함되어야 한다");
    }

    [Test]
    public void NormalEmit_CacheName_IsEchoed()
    {
        _config.pageCacheName = "my-custom-cache";
        WriteManifest();

        string json = ReadManifest();
        StringAssert.Contains("my-custom-cache", json, "지정한 cacheName 이 manifest 에 포함되어야 한다");
    }

    [Test]
    public void NormalEmit_EmptyCacheName_FallsBackToDefault()
    {
        _config.pageCacheName = "";
        WriteManifest();

        string json = ReadManifest();
        StringAssert.Contains(AITPageCacheEmitter.DefaultCacheName, json,
            "pageCacheName 이 비면 기본값 ait-page-cache 로 보정되어야 한다");
    }

    // ===== 케이스 4: wireBytes == 실제 파일 크기 =====

    [Test]
    public void NormalEmit_WireBytes_MatchActualFileSize()
    {
        WriteManifest();
        string json = ReadManifest();

        // wasm 파일은 plain bytes 이므로 wireBytes == 256 이어야 한다
        long expectedWire = new FileInfo(Path.Combine(_buildDir, WasmFile)).Length;
        StringAssert.Contains("\"wireBytes\": " + expectedWire, json,
            "wireBytes 는 실제 파일 크기와 일치해야 한다");
    }

    // ===== 케이스 5: gzip 파일 rawBytes == 기지 raw 크기, encoding == "gzip" =====

    [Test]
    public void NormalEmit_GzipDataFile_HasCorrectRawBytesAndEncoding()
    {
        WriteManifest();
        string json = ReadManifest();

        // data 파일은 gzip 으로 만들었으므로 encoding "gzip" 이어야 한다
        StringAssert.Contains("\"encoding\": \"gzip\"", json,
            "gzip 파일은 encoding 이 gzip 이어야 한다");

        // rawBytes 는 KnownRawSize 와 같아야 한다
        StringAssert.Contains("\"rawBytes\": " + KnownRawSize, json,
            "gzip rawBytes 는 알려진 raw 크기와 일치해야 한다");
    }

    // ===== 케이스 6: loader/symbols 는 excluded 에만 있고 assets 에 없음 =====

    [Test]
    public void NormalEmit_Loader_OnlyInExcluded_NotInAssets()
    {
        WriteManifest();
        string json = ReadManifest();

        // excluded 에 loader 경로가 있어야 한다
        StringAssert.Contains("\"role\": \"loader\"", json, "loader 가 excluded 에 있어야 한다");

        // assets 배열에는 loader 경로가 없어야 한다.
        // assets 블록과 excluded 블록을 분리해서 검증.
        int assetsStart = json.IndexOf("\"assets\"", StringComparison.Ordinal);
        int excludedStart = json.IndexOf("\"excluded\"", StringComparison.Ordinal);
        Assert.IsTrue(assetsStart >= 0 && excludedStart > assetsStart,
            "assets 와 excluded 섹션 순서가 올바라야 한다");

        string assetsSection = json.Substring(assetsStart, excludedStart - assetsStart);
        StringAssert.DoesNotContain(LoaderFile, assetsSection,
            "assets 섹션에 loader 파일이 포함되면 안 된다");
    }

    [Test]
    public void NormalEmit_Symbols_OnlyInExcluded_NotInAssets()
    {
        WriteManifest();
        string json = ReadManifest();

        StringAssert.Contains("\"role\": \"symbols\"", json, "symbols 가 excluded 에 있어야 한다");

        int assetsStart = json.IndexOf("\"assets\"", StringComparison.Ordinal);
        int excludedStart = json.IndexOf("\"excluded\"", StringComparison.Ordinal);
        string assetsSection = json.Substring(assetsStart, excludedStart - assetsStart);
        StringAssert.DoesNotContain(SymbolsFile, assetsSection,
            "assets 섹션에 symbols 파일이 포함되면 안 된다");
    }

    // ===== 케이스 7: %[A-Z_]+% 토큰 부재 =====

    [Test]
    public void NormalEmit_NoUnreplacedPlaceholderTokens()
    {
        WriteManifest();
        string json = ReadManifest();

        var matches = Regex.Matches(json, @"%[A-Z0-9_]+%");
        Assert.AreEqual(0, matches.Count,
            "manifest 에 %대문자_퍼센트% 토큰이 있으면 ValidatePlaceholderSubstitution 이 빌드를 실패시킨다");
    }

    // ===== 케이스 8: 결정성 — 2회 호출 산출물이 byte-identical =====

    [Test]
    public void NormalEmit_Deterministic_TwiceProducesBytesIdenticalOutput()
    {
        WriteManifest();
        string first = ReadManifest();

        WriteManifest();
        string second = ReadManifest();

        Assert.AreEqual(first, second,
            "같은 입력에 대해 두 번 호출하면 byte-identical 산출물이어야 한다(타임스탬프/랜덤 없음)");
    }

    // ===== 케이스 9: schemaVersion, generator 필드 존재 =====

    [Test]
    public void NormalEmit_ContainsSchemaVersionAndGenerator()
    {
        WriteManifest();
        string json = ReadManifest();

        StringAssert.Contains("\"schemaVersion\": 1", json,
            "schemaVersion 이 1 이어야 한다");
        StringAssert.Contains("\"generator\":", json,
            "generator 필드가 있어야 한다");
        StringAssert.Contains("apps-in-toss.unity", json,
            "generator 에 apps-in-toss.unity 가 포함되어야 한다");
    }

    // ===== 케이스 10: totals.wireBytes 가 파일 크기의 합 =====

    [Test]
    public void NormalEmit_TotalsWireBytes_IsSumOfAssets()
    {
        WriteManifest();
        string json = ReadManifest();

        long expectedTotal =
            new FileInfo(Path.Combine(_buildDir, DataFile)).Length +
            new FileInfo(Path.Combine(_buildDir, FrameworkFile)).Length +
            new FileInfo(Path.Combine(_buildDir, WasmFile)).Length;

        // totals 섹션 이후의 부분 문자열에서 wireBytes 를 탐색해
        // per-asset wireBytes 와 우연히 일치하는 false-positive 를 방지한다.
        int totalsIdx = json.IndexOf("\"totals\"", StringComparison.Ordinal);
        Assert.Greater(totalsIdx, -1, "totals 섹션이 존재해야 한다");
        string totalsSection = json.Substring(totalsIdx);
        StringAssert.Contains("\"wireBytes\": " + expectedTotal, totalsSection,
            "totals.wireBytes 는 assets wireBytes 의 합이어야 한다");
    }

    // -----------------------------------------------------------------------
    // 헬퍼
    // -----------------------------------------------------------------------

    private void WriteManifest()
    {
        AITWarmManifestEmitter.WriteManifest(
            _config,
            _tempDir,
            LoaderFile,
            DataFile,
            FrameworkFile,
            WasmFile,
            SymbolsFile
        );
    }

    private string ReadManifest()
    {
        string manifestPath = Path.Combine(_tempDir, AITWarmManifestEmitter.FileName);
        return File.ReadAllText(manifestPath, Encoding.UTF8);
    }

    /// <summary>
    /// 알려진 raw 크기(<paramref name="rawSize"/> 바이트)를 gzip 으로 압축한 파일을 생성합니다.
    /// gzip rawBytes 검증용.
    /// </summary>
    private static void CreateGzipFile(string path, int rawSize)
    {
        byte[] rawData = new byte[rawSize];
        // 검증 가능한 패턴으로 채움 (0 만으로 채우면 압축률이 너무 높아도 무관)
        for (int i = 0; i < rawSize; i++)
        {
            rawData[i] = (byte)(i % 251);
        }

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var gz = new GZipStream(fs, CompressionMode.Compress, leaveOpen: false))
        {
            gz.Write(rawData, 0, rawData.Length);
        }
    }

    /// <summary>
    /// <paramref name="size"/> 바이트의 plain 파일을 생성합니다.
    /// </summary>
    private static void CreatePlainFile(string path, int size)
    {
        byte[] data = new byte[size];
        for (int i = 0; i < size; i++)
        {
            // 1 offset: i=0 → 1(0x01), i=1 → 2(0x02), ...
            // 첫 바이트=0x01(≠0x1f), 두 번째 바이트=0x02(≠0x8b) → gzip 매직 바이트(0x1f 0x8b) 절대 불일치.
            data[i] = (byte)(i % 127 + 1);
        }
        File.WriteAllBytes(path, data);
    }
}
