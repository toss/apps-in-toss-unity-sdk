// AITDiagnosticsAggregationTests
//
// AITDataBreakdownReport.Aggregate / AITBootSceneDiagnostics.ComputeBootShare 순수 집계 함수 검증
// (Level 0 — 순수 로직). 두 진단 후처리기는 UnityEditor.Build.Reporting.BuildReport를 읽어서 동작하는데
// BuildReport는 테스트에서 생성할 수 없다(실제 빌드 산출물로만 채워짐) — 그래서 검증 시도조차 되지 않은
// 채로 남아 있었다. 이 테스트가 검증하는 두 메서드는 BuildReport 의존을 걷어내고 플레인 데이터(튜플
// 목록/딕셔너리)만 받는 순수 함수로 분리된 것으로, 어댑터(OnPostprocessBuild → Run)가 BuildReport에서
// 이 입력을 뽑아 넘기기만 한다.
//
// SetUp/TearDown 없음 — 두 메서드 모두 static이고 Unity 객체/에셋 상태에 의존하지 않는다.

using System.Collections.Generic;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITDiagnosticsAggregationTests
{
    // ─────────────────────────── AITDataBreakdownReport.Aggregate ───────────────────────────

    [Test]
    public void Aggregate_TypeBreakdown_SumsBytesAndComputesPercent()
    {
        var entries = new List<(string SourcePath, string TypeName, long Bytes)>
        {
            ("Assets/a.png", "Texture2D", 300),
            ("Assets/b.png", "Texture2D", 100),
            ("Assets/c.wav", "AudioClip", 600),
        };

        var result = AITDataBreakdownReport.Aggregate(entries, grandTotalBytes: 1000, topN: 20, tagMap: null);

        Assert.AreEqual(1000, result.DataTotalBytes, ".data 총계는 입력 바이트 합과 같아야 함.");
        Assert.AreEqual(2, result.ByType.Count, "타입 2종(Texture2D/AudioClip)으로 그룹핑되어야 함.");

        var texture = result.ByType.Find(t => t.Type == "Texture2D");
        Assert.AreEqual(2, texture.Count, "Texture2D 항목 2개가 합산되어야 함.");
        Assert.AreEqual(400, texture.Bytes, "Texture2D 바이트 합은 300+100=400이어야 함.");
        Assert.AreEqual(40.0, texture.PercentOfData, 0.001, "Texture2D 비중은 400/1000=40%여야 함.");

        var audio = result.ByType.Find(t => t.Type == "AudioClip");
        Assert.AreEqual(600, audio.Bytes, "AudioClip 바이트는 600이어야 함.");
        Assert.AreEqual(60.0, audio.PercentOfData, 0.001, "AudioClip 비중은 600/1000=60%여야 함.");

        Assert.AreEqual("AudioClip", result.ByType[0].Type,
            "타입 목록은 바이트 내림차순으로 정렬되어야 함(AudioClip 600 > Texture2D 400).");
    }

    [Test]
    public void Aggregate_ZeroTotal_DoesNotDivideByZero()
    {
        var entries = new List<(string SourcePath, string TypeName, long Bytes)>
        {
            ("Assets/a.png", "Texture2D", 0),
            ("Assets/b.png", "Texture2D", 0),
        };

        var result = AITDataBreakdownReport.Aggregate(entries, grandTotalBytes: 0, topN: 20, tagMap: null);

        Assert.AreEqual(0, result.DataTotalBytes);
        Assert.AreEqual(1, result.ByType.Count);
        Assert.AreEqual(0.0, result.ByType[0].PercentOfData, "총계가 0이면 비중은 0으로 나누기 예외 없이 0%여야 함.");
        foreach (var a in result.TopAssets)
        {
            Assert.AreEqual(0.0, a.PercentOfData, "TOP 자산 비중도 총계 0일 때 0%여야 함.");
        }
    }

    [Test]
    public void Aggregate_TopN_OrdersByBytesDescending()
    {
        var entries = new List<(string SourcePath, string TypeName, long Bytes)>
        {
            ("Assets/small.png", "Texture2D", 10),
            ("Assets/big.png", "Texture2D", 1000),
            ("Assets/mid.png", "Texture2D", 500),
        };

        var result = AITDataBreakdownReport.Aggregate(entries, grandTotalBytes: 1510, topN: 2, tagMap: null);

        Assert.AreEqual(2, result.TopAssets.Count, "topN=2이면 상위 2개만 반환되어야 함.");
        Assert.AreEqual("Assets/big.png", result.TopAssets[0].SourcePath, "1위는 가장 큰 자산이어야 함.");
        Assert.AreEqual("Assets/mid.png", result.TopAssets[1].SourcePath, "2위는 그 다음으로 큰 자산이어야 함.");
    }

    [Test]
    public void Aggregate_TopN_LargerThanInput_ReturnsAllWithoutError()
    {
        var entries = new List<(string SourcePath, string TypeName, long Bytes)>
        {
            ("Assets/a.png", "Texture2D", 10),
            ("Assets/b.png", "Texture2D", 20),
        };

        var result = AITDataBreakdownReport.Aggregate(entries, grandTotalBytes: 30, topN: 100, tagMap: null);

        Assert.AreEqual(2, result.TopAssets.Count, "topN이 입력 개수보다 커도 예외 없이 입력 개수만큼만 반환되어야 함.");
    }

    [Test]
    public void Aggregate_ExternalizedAsset_IsTaggedAndNotMistakenAsStillInside()
    {
        var entries = new List<(string SourcePath, string TypeName, long Bytes)>
        {
            ("Assets/streamed.png", "Texture2D", 5),   // 외부화된 자산 — packedAssets엔 스텁 크기(5B)만 남음
            ("Assets/kept.png", "Texture2D", 900),     // 외부화되지 않은 자산 — 여전히 .data 안에 그대로 있음
        };
        var tagMap = new Dictionary<string, string>
        {
            ["Assets/streamed.png"] = "텍스처 스트리밍",
        };

        var result = AITDataBreakdownReport.Aggregate(entries, grandTotalBytes: 905, topN: 20, tagMap: tagMap);

        var streamed = result.TopAssets.Find(a => a.SourcePath == "Assets/streamed.png");
        Assert.IsTrue(streamed.Externalized, "외부화 매니페스트에 태깅된 자산은 Externalized=true여야 함.");
        Assert.AreEqual("텍스처 스트리밍", streamed.ExternalizedLever, "외부화 레버 라벨이 그대로 전달되어야 함.");

        var kept = result.TopAssets.Find(a => a.SourcePath == "Assets/kept.png");
        Assert.IsFalse(kept.Externalized,
            "태그 맵에 없는 자산은 Externalized=false여야 함 — 외부화된 자산이 '아직 .data 안에 그대로 있다'고 오귀속되면 안 됨.");
        Assert.IsNull(kept.ExternalizedLever);
    }

    [Test]
    public void Aggregate_NullOrEmptyInputs_DoNotThrow()
    {
        Assert.DoesNotThrow(() => AITDataBreakdownReport.Aggregate(null, 0, 20, null),
            "null 입력(dataEntries/tagMap)에서도 예외 없이 처리되어야 함.");

        var result = AITDataBreakdownReport.Aggregate(null, 0, 20, null);
        Assert.AreEqual(0, result.DataTotalBytes);
        Assert.AreEqual(0, result.ByType.Count);
        Assert.AreEqual(0, result.TopAssets.Count);

        Assert.DoesNotThrow(
            () => AITDataBreakdownReport.Aggregate(new List<(string, string, long)>(), 100, -1, null),
            "topN이 음수여도 예외 없이 처리되어야 함(빈 TOP 목록으로 취급).");
    }

    // ─────────────────────────── AITBootSceneDiagnostics.ComputeBootShare ───────────────────────────

    [Test]
    public void ComputeBootShare_NormalCase_ComputesShareAndWarnFlag()
    {
        var bootDeps = new List<string> { "Assets/Boot.unity", "Assets/BootTexture.png" };
        var byPath = new Dictionary<string, long>
        {
            ["Assets/Boot.unity"] = 100,
            ["Assets/BootTexture.png"] = 400,
            ["Assets/OtherLevelTexture.png"] = 500, // 부팅 씬이 참조하지 않는 자산 — 합산에서 제외되어야 함
        };

        var share = AITBootSceneDiagnostics.ComputeBootShare(bootDeps, byPath, grandTotalBytes: 1000, warnThresholdPct: 50.0);

        Assert.AreEqual(500, share.BootBytes, "부팅 씬 의존성 바이트는 100+400=500이어야 함(비의존 자산 500은 제외).");
        Assert.AreEqual(50.0, share.PercentOfTotal, 0.001, "비중은 500/1000=50%여야 함.");
        Assert.IsTrue(share.ExceedsWarnThreshold, "50%는 기준값(50%) 이상이므로 경고 플래그가 켜져야 함.");
    }

    [Test]
    public void ComputeBootShare_ZeroTotal_DoesNotDivideByZero()
    {
        var bootDeps = new List<string> { "Assets/Boot.unity" };
        var byPath = new Dictionary<string, long> { ["Assets/Boot.unity"] = 100 };

        var share = AITBootSceneDiagnostics.ComputeBootShare(bootDeps, byPath, grandTotalBytes: 0, warnThresholdPct: 50.0);

        Assert.AreEqual(0.0, share.PercentOfTotal, "총계가 0이면 비중은 0으로 나누기 예외 없이 0%여야 함.");
        Assert.IsFalse(share.ExceedsWarnThreshold, "총계를 알 수 없는 상태에서는 경고를 내지 않아야 함(오탐 방지).");
    }

    [Test]
    public void ComputeBootShare_EmptyBootSet_ReturnsZeroBootBytes()
    {
        var byPath = new Dictionary<string, long> { ["Assets/Other.png"] = 900 };

        var share = AITBootSceneDiagnostics.ComputeBootShare(
            new List<string>(), byPath, grandTotalBytes: 1000, warnThresholdPct: 50.0);

        Assert.AreEqual(0, share.BootBytes, "부팅 씬 의존성이 없으면 부팅 바이트는 0이어야 함.");
        Assert.AreEqual(0.0, share.PercentOfTotal, 0.001);
        Assert.IsFalse(share.ExceedsWarnThreshold);
    }

    [Test]
    public void ComputeBootShare_NullInputs_DoNotThrow()
    {
        Assert.DoesNotThrow(() => AITBootSceneDiagnostics.ComputeBootShare(null, null, 1000, 50.0),
            "bootDependencyPaths/packedBytesByPath가 null이어도 예외 없이 처리되어야 함.");

        var share = AITBootSceneDiagnostics.ComputeBootShare(null, null, 1000, 50.0);
        Assert.AreEqual(0, share.BootBytes);
        Assert.AreEqual(0.0, share.PercentOfTotal);
    }

    [Test]
    public void ComputeBootShare_PathNotInPackedMap_IsIgnoredNotThrown()
    {
        // AssetDatabase.GetDependencies는 소스 경로를 주지만 packedAssets에 대응 항목이 없을 수 있다
        // (예: 스크립트 등 .data 패킹 대상이 아닌 의존성) — 조회 실패(0 취급)로 처리되고 예외가 나면 안 됨.
        var bootDeps = new List<string> { "Assets/Boot.unity", "Assets/SomeScript.cs" };
        var byPath = new Dictionary<string, long> { ["Assets/Boot.unity"] = 100 };

        var share = AITBootSceneDiagnostics.ComputeBootShare(bootDeps, byPath, grandTotalBytes: 1000, warnThresholdPct: 50.0);

        Assert.AreEqual(100, share.BootBytes, "packedAssets에 없는 경로는 0 바이트로 취급되고 조회된 것만 합산되어야 함.");
    }
}
