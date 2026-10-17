// AITAudioCrossProcessorRestoreTests
//
// 오디오 스트리밍(외부화) ↔ 오디오 재인코딩 교차-프로세서 통합 가드 (Level 1 — 실 AssetDatabase).
//
// AITWebGLBuilder.BuildWebGL 의 오디오 파이프라인은 다음 계약을 갖는다:
//   적용:  audioStream(ExternalizeForBuild) → audioReencode(ApplyForBuild)
//   복원:  audioReencode(RestoreForBuild) → audioStream(RestoreForBuild)   ← 적용의 정확한 역순
//   상호배제: 재인코딩은 스트리밍이 외부화(무음 스텁 치환)한 클립을 건너뛴다
//             (<src>.aitstreambak 존재 여부로 판별 — 무음 스텁 재인코딩은 무의미·오염 위험).
//
// 이 테스트는 실제 .wav 에셋 2개(대용량=스트리밍 대상 / 소용량=재인코딩 대상)로
// 위 계약 전체를 검증한다: 상호배제가 지켜지는지, 역순 복원 후 소스 바이트와 .meta 가
// 원본과 바이트 단위로 동일한지, 백업/마커/StreamingAssets 잔존물이 없는지.
//
// 참고: 텍스처 쌍(clamp↔texStream)의 동일 가드는 AITCrossProcessorRestoreOrderTests 참조.

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITAudioCrossProcessorRestoreTests
{
    private const string TempDir = "Assets/AITTest_AudioCrossRestore";
    private const string BigPath = TempDir + "/audio_cross_big.wav";    // 스트리밍 외부화 대상
    private const string SmallPath = TempDir + "/audio_cross_small.wav"; // 재인코딩 대상

    // 스트리밍 크기 게이트: big(≈400KB)만 통과, small(≈10KB)은 미달.
    // (audioStreamingMinBytes 필드가 int 라 int 로 선언)
    private const int StreamMinBytes = 100000;

    private const string StreamRootAssets = "Assets/StreamingAssets/ait-stream-audio";
    private const string StreamBakSuffix = ".aitstreambak";
    private const string ReencodeBakSuffix = ".aitaudioreencodebak";
    private const string ReencodeMarker = "Assets/.ait-audioreencode-active";

    private string _projectRoot;
    private AITEditorScriptObject _config;

    private string BigFull => Path.Combine(_projectRoot, BigPath);
    private string SmallFull => Path.Combine(_projectRoot, SmallPath);
    private string BigMetaFull => BigFull + ".meta";
    private string SmallMetaFull => SmallFull + ".meta";

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string dirAbs = Path.Combine(_projectRoot, TempDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        // 유음(사인 톤) PCM WAV 생성 — 무음 스텁 치환 여부를 바이트 비교로 판별 가능해야 한다.
        File.WriteAllBytes(BigFull, MakeToneWav(200000));  // ≈400KB > StreamMinBytes
        File.WriteAllBytes(SmallFull, MakeToneWav(5000));  // ≈10KB  < StreamMinBytes

        AssetDatabase.ImportAsset(BigPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(SmallPath, ImportAssetOptions.ForceSynchronousImport);

        // 결정성: 두 클립 모두 base(default) 임포트 설정을 PCM 으로 고정.
        //  - small: 자동 모드 재인코딩(PCM→Vorbis)의 실 대상.
        //  - big:   포맷상으로는 재인코딩 자격이 있지만 스트리밍 외부화로 제외되어야 함(상호배제 검증).
        ForceDefaultPcm(BigPath);
        ForceDefaultPcm(SmallPath);

        // 두 프로세서를 임시 폴더로 스코프. 재인코딩은 -1(자동) — tri-state 자동 게이트
        // (GetDefaultAudioReencode()=true)까지 end-to-end 로 함께 검증한다.
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();

        _config.audioStreaming = 1;                    // ON
        _config.audioStreamingMinBytes = StreamMinBytes;
        _config.audioStreamingDirs = TempDir;

        _config.audioReencode = -1;                    // 자동(기본 ON)
        _config.audioReencodeQuality = 0.7f;
        _config.audioReencodeMinBytes = 0;             // 크기 필터 없음
        _config.audioReencodeDirs = TempDir;
        _config.audioReencodeExcludeDirs = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        // 테스트 실패 시에도 샘플 프로젝트를 더럽히지 않도록 잔존물을 전부 제거(베스트 에포트).
        string assetsAbs = Application.dataPath;
        foreach (var pattern in new[] { "*" + StreamBakSuffix, "*" + ReencodeBakSuffix })
        {
            foreach (var bak in Directory.GetFiles(assetsAbs, pattern, SearchOption.AllDirectories))
            {
                try { File.Delete(bak); } catch { /* best-effort */ }
            }
        }

        string marker = Path.Combine(_projectRoot, ReencodeMarker);
        try { if (File.Exists(marker)) { File.Delete(marker); } } catch { /* best-effort */ }

        AssetDatabase.DeleteAsset(BigPath);
        AssetDatabase.DeleteAsset(SmallPath);
        AssetDatabase.DeleteAsset(TempDir);

        // 스트리밍이 만드는 StreamingAssets 사본과, 비어 있게 된 상위 폴더 정리.
        AssetDatabase.DeleteAsset(StreamRootAssets);
        string streamingAssetsAbs = Path.Combine(_projectRoot, "Assets/StreamingAssets");
        try
        {
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
    public void StreamThenReencode_MutualExclusion_AndReverseRestore_RestoresVerbatim()
    {
        byte[] bigOriginalBytes = File.ReadAllBytes(BigFull);
        string bigOriginalMeta = File.ReadAllText(BigMetaFull);
        string smallOriginalMeta = File.ReadAllText(SmallMetaFull);
        Assert.IsFalse(string.IsNullOrEmpty(bigOriginalMeta), "사전조건: big .meta 존재");
        Assert.IsFalse(string.IsNullOrEmpty(smallOriginalMeta), "사전조건: small .meta 존재");

        // ── 적용(AITWebGLBuilder.BuildWebGL 과 동일 순서: stream → reencode) ──
        var streamHandle = AITAudioStreamingProcessor.ExternalizeForBuild(_config);
        var reencodeHandle = AITAudioReencodeProcessor.ApplyForBuild(_config);

        try
        {
            // 스트리밍: big 만 외부화(크기 게이트). "조용한 no-op" 방지를 위해 명시 단언.
            Assert.IsTrue(streamHandle != null && streamHandle.Active,
                "스트리밍 프로세서가 활성화되어야 함(모듈 미존재/스텁 미발견이면 교차 시나리오 미성립).");
            Assert.AreEqual(1, streamHandle.Count,
                "big(≈400KB)만 외부화되어야 함 — small(≈10KB)은 minBytes 미달.");
            Assert.IsTrue(File.Exists(BigFull + StreamBakSuffix),
                "외부화 후 big 소스 백업(.aitstreambak)이 존재해야 함");
            Assert.IsFalse(AreBytesEqual(bigOriginalBytes, File.ReadAllBytes(BigFull)),
                "외부화 후 big 소스는 무음 스텁으로 치환되어 원본과 달라야 함");

            // 재인코딩: small 만 처리. big 은 .aitstreambak 존재로 건너뛰어야 한다(상호배제 핵심).
            Assert.IsTrue(reencodeHandle != null && reencodeHandle.Active,
                "재인코딩 프로세서가 자동 모드(-1)에서 활성화되어야 함(PCM small 이 대상).");
            Assert.AreEqual(1, reencodeHandle.ClipCount,
                "small(PCM)만 재인코딩되어야 함 — 외부화된 big 은 상호배제로 제외.");
            Assert.IsTrue(File.Exists(SmallMetaFull + ReencodeBakSuffix),
                "재인코딩 후 small .meta 백업(.aitaudioreencodebak)이 존재해야 함");
            Assert.IsFalse(File.Exists(BigMetaFull + ReencodeBakSuffix),
                "상호배제: 외부화된 big 에는 재인코딩 백업이 없어야 함(무음 스텁 재인코딩 금지).");

            // small 의 유효 임포트 설정이 실제로 Vorbis 로 바뀌었는지. WebGL 은 per-platform
            // 오디오 오버라이드 미지원이므로 WebGL 빌드가 ship 하는 것은 base(defaultSampleSettings)다.
            var smallImporter = (AudioImporter)AssetImporter.GetAtPath(SmallPath);
            Assert.IsNotNull(smallImporter, "small 임포터가 존재해야 함");
            var applied = smallImporter.defaultSampleSettings;
            Assert.AreEqual(AudioCompressionFormat.Vorbis, applied.compressionFormat,
                "재인코딩 적용 후 small 의 base compressionFormat 은 Vorbis 여야 함(WebGL ship 값).");
            Assert.AreEqual(0.7f, applied.quality, 0.01f,
                "재인코딩 적용 후 small 의 base quality 는 설정값(0.7)이어야 함");
            Assert.IsFalse(smallImporter.ContainsSampleSettingsOverride("WebGL"),
                "WebGL 은 오디오 per-platform 오버라이드 미지원 — 유령 오버라이드를 만들려 시도하면 안 됨.");

            // StreamingAssets 사본 + 매니페스트 존재(런타임 스트리밍 소스).
            Assert.IsTrue(File.Exists(Path.Combine(_projectRoot, StreamRootAssets, "manifest.json")),
                "외부화 후 매니페스트가 존재해야 함");
        }
        finally
        {
            // ── 복원: 적용의 정확한 역순(reencode 먼저, stream 나중) — 검증 대상 그 자체 ──
            AITAudioReencodeProcessor.RestoreForBuild(reencodeHandle);
            AITAudioStreamingProcessor.RestoreForBuild(streamHandle);
            AssetDatabase.Refresh();
        }

        // ── 검증: 소스/메타가 바이트 단위로 원본과 동일 + 잔존물 전무 ──
        Assert.IsTrue(AreBytesEqual(bigOriginalBytes, File.ReadAllBytes(BigFull)),
            "복원 후 big 소스는 원본과 바이트 단위로 동일해야 함(무음 스텁 잔존 = 영구 무음 버그).");
        Assert.AreEqual(bigOriginalMeta, File.ReadAllText(BigMetaFull),
            "복원 후 big .meta 는 원본과 동일해야 함");
        Assert.AreEqual(smallOriginalMeta, File.ReadAllText(SmallMetaFull),
            "복원 후 small .meta 는 원본과 동일해야 함(재인코딩 오버라이드가 남으면 영구 오염).");

        var smallAfter = (AudioImporter)AssetImporter.GetAtPath(SmallPath);
        Assert.IsNotNull(smallAfter, "복원 후 small 임포터가 존재해야 함");
        Assert.AreEqual(AudioCompressionFormat.PCM, smallAfter.defaultSampleSettings.compressionFormat,
            "복원 후 small 의 base compressionFormat 은 원래 값(PCM)으로 돌아와야 함.");

        Assert.IsFalse(File.Exists(BigFull + StreamBakSuffix), "복원 후 스트리밍 백업이 삭제되어야 함");
        Assert.IsFalse(File.Exists(SmallMetaFull + ReencodeBakSuffix), "복원 후 재인코딩 백업이 삭제되어야 함");
        Assert.IsFalse(Directory.Exists(Path.Combine(_projectRoot, StreamRootAssets)),
            "복원 후 StreamingAssets 사본 디렉토리가 제거되어야 함");
        Assert.IsFalse(File.Exists(Path.Combine(_projectRoot, ReencodeMarker)),
            "복원 후 재인코딩 마커가 제거되어야 함");
    }

    [Test]
    public void Reencode_Alone_PcmDefault_AppliesVorbisOverride_AndRestoresVerbatim()
    {
        // 스트리밍 없이 재인코딩 단독 경로 격리 — 결합 테스트 실패 시 원인 판별용.
        _config.audioStreaming = 0;

        string smallOriginalMeta = File.ReadAllText(SmallMetaFull);
        string bigOriginalMeta = File.ReadAllText(BigMetaFull);

        var handle = AITAudioReencodeProcessor.ApplyForBuild(_config);
        try
        {
            Assert.IsTrue(handle != null && handle.Active, "PCM 클립 2개가 대상이므로 활성화되어야 함");
            Assert.AreEqual(2, handle.ClipCount,
                "스트리밍이 꺼져 있으면 PCM 클립 2개(big/small) 모두 재인코딩 대상이어야 함");

            foreach (var p in new[] { SmallPath, BigPath })
            {
                var ai = (AudioImporter)AssetImporter.GetAtPath(p);
                Assert.AreEqual(AudioCompressionFormat.Vorbis,
                    ai.defaultSampleSettings.compressionFormat,
                    $"{p}: base(defaultSampleSettings)가 Vorbis 로 변경되어야 함(WebGL ship 값)");
            }
        }
        finally
        {
            AITAudioReencodeProcessor.RestoreForBuild(handle);
            AssetDatabase.Refresh();
        }

        Assert.AreEqual(smallOriginalMeta, File.ReadAllText(SmallMetaFull),
            "복원 후 small .meta 는 원본과 동일해야 함");
        Assert.AreEqual(bigOriginalMeta, File.ReadAllText(BigMetaFull),
            "복원 후 big .meta 는 원본과 동일해야 함");
        Assert.IsFalse(File.Exists(Path.Combine(_projectRoot, ReencodeMarker)),
            "복원 후 마커가 제거되어야 함");
    }

    // ─────────────────────────── 헬퍼 ───────────────────────────

    /// <summary>클립의 base(default) 임포트 설정을 PCM 으로 고정하고 동기 reimport 한다.</summary>
    private static void ForceDefaultPcm(string assetPath)
    {
        var ai = (AudioImporter)AssetImporter.GetAtPath(assetPath);
        Assert.IsNotNull(ai, $"오디오 임포터가 존재해야 함: {assetPath}");
        var ss = ai.defaultSampleSettings;
        ss.compressionFormat = AudioCompressionFormat.PCM;
        ai.defaultSampleSettings = ss;
        ai.SaveAndReimport();
    }

    /// <summary>16-bit mono 44.1kHz 사인 톤 PCM WAV 바이트를 생성한다(sampleCount * 2 bytes data).</summary>
    private static byte[] MakeToneWav(int sampleCount)
    {
        const int sampleRate = 44100;
        int dataSize = sampleCount * 2;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);               // PCM fmt chunk size
            bw.Write((short)1);         // audio format: PCM
            bw.Write((short)1);         // channels: mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);   // byte rate (mono 16-bit)
            bw.Write((short)2);         // block align
            bw.Write((short)16);        // bits per sample
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            for (int i = 0; i < sampleCount; i++)
            {
                bw.Write((short)(Math.Sin(i * 0.05) * 8000)); // 유음 톤(무음 스텁과 구분)
            }

            return ms.ToArray();
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
