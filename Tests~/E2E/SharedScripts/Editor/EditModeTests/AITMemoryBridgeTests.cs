// -----------------------------------------------------------------------
// AITMemoryBridgeTests.cs - 메모리 텔레메트리 브릿지(AITMemoryBridge) 순수 로직 검증
// Level 0: ait-mem.js 가 SendMessage 로 보내는 JSON 의 파싱과 이벤트 발행 규칙을 고정한다.
//   - TryParse   : JS 가 실제로 내는 형태의 JSON, 모르는 키 무시, 깨진/빈 입력 거부(예외 없음)
//   - ParseLevel : level 문자열 → enum (대소문자 무시, 모르는 값은 Ok)
//   - Dispatch   : Latest 갱신, 이벤트 발행, 구독자 예외 격리
//   - 기본값     : 자동 UnloadUnusedAssets 는 꺼짐
// 실제 SendMessage 경로(WebGL)는 EditMode 로 검증할 수 없다 — Playwright(perf-pacing.test.js)가 JS 쪽을 커버한다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;

[TestFixture]
public class AITMemoryBridgeTests
{
    // ait-mem.js 의 snapshot('pressure') 와 같은 키 구성
    private const string PressureJson =
        "{\"type\":\"pressure\",\"level\":\"high\",\"heapBytes\":268435456,\"peakBytes\":268435456," +
        "\"growCount\":8,\"growFailures\":0,\"growTotalMs\":123.45,\"growMaxMs\":40.1,\"crashCount\":2,\"bgKillCount\":1}";

    private AITMemoryInfo _lastEvent;
    private int _eventCount;
    private Action<AITMemoryInfo> _handler;

    [SetUp]
    public void SetUp()
    {
        _lastEvent = null;
        _eventCount = 0;
        _handler = info => { _lastEvent = info; _eventCount++; };
        AITMemoryBridge.OnMemoryEvent += _handler;
    }

    [TearDown]
    public void TearDown()
    {
        AITMemoryBridge.OnMemoryEvent -= _handler;
        AITMemoryBridge.AutoUnloadUnusedAssets = false;
    }

    // =====================================================
    // TryParse
    // =====================================================

    [Test]
    public void TryParse_FullPayload_MapsEveryField()
    {
        Assert.IsTrue(AITMemoryBridge.TryParse(PressureJson, out var info));
        Assert.AreEqual("pressure", info.type);
        Assert.AreEqual("high", info.level);
        Assert.AreEqual(AITMemoryLevel.High, info.Level);
        Assert.AreEqual(268435456L, info.heapBytes);
        Assert.AreEqual(268435456L, info.peakBytes);
        Assert.AreEqual(8, info.growCount);
        Assert.AreEqual(0, info.growFailures);
        Assert.AreEqual(123.45f, info.growTotalMs, 0.01f);
        Assert.AreEqual(40.1f, info.growMaxMs, 0.01f);
        Assert.AreEqual(2, info.crashCount);
        Assert.AreEqual(1, info.bgKillCount);
        Assert.AreEqual(256.0, info.HeapMegabytes, 0.0001);
    }

    [Test]
    public void TryParse_LargeHeapBytes_DoesNotOverflow()
    {
        // 2GB 를 넘는 값도 long 으로 받아야 한다(wasm32 최대 4GB)
        Assert.IsTrue(AITMemoryBridge.TryParse("{\"type\":\"grow\",\"heapBytes\":3221225472}", out var info));
        Assert.AreEqual(3221225472L, info.heapBytes);
    }

    [Test]
    public void TryParse_SnapshotWithExtraDiagnosticKeys_IgnoresUnknownFields()
    {
        // getState()/snapshotJson() 은 진단용 키(prevSession, wrapped, bridgeReady, jsHeapUsedBytes ...)를 더 싣는다
        const string json =
            "{\"type\":\"snapshot\",\"level\":\"ok\",\"heapBytes\":0,\"peakBytes\":0,\"growCount\":0,\"growFailures\":0," +
            "\"growTotalMs\":0,\"growMaxMs\":0,\"crashCount\":0,\"bgKillCount\":0," +
            "\"prevSession\":\"exit\",\"wrapped\":true,\"initialBytes\":0,\"bridgeReady\":true,\"jsHeapUsedBytes\":12345678}";
        Assert.IsTrue(AITMemoryBridge.TryParse(json, out var info));
        Assert.AreEqual("snapshot", info.type);
        Assert.AreEqual(AITMemoryLevel.Ok, info.Level);
    }

    [Test]
    public void TryParse_MissingOptionalFields_UsesDefaults()
    {
        Assert.IsTrue(AITMemoryBridge.TryParse("{\"type\":\"crash\"}", out var info));
        Assert.AreEqual("crash", info.type);
        Assert.AreEqual(0, info.crashCount);
        Assert.AreEqual(0L, info.heapBytes);
        Assert.AreEqual(AITMemoryLevel.Ok, info.Level);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json")]
    [TestCase("{")]
    [TestCase("[]")]
    [TestCase("{}")]                   // type 이 없으면 이벤트가 아니다
    [TestCase("{\"type\":\"\"}")]
    public void TryParse_InvalidInput_ReturnsFalseWithoutThrowing(string json)
    {
        AITMemoryInfo info = new AITMemoryInfo();
        bool ok = false;
        Assert.DoesNotThrow(() => ok = AITMemoryBridge.TryParse(json, out info));
        Assert.IsFalse(ok);
        Assert.IsNull(info, "실패 시 out 값은 null 이어야 한다.");
    }

    // =====================================================
    // ParseLevel
    // =====================================================

    [TestCase("ok", AITMemoryLevel.Ok)]
    [TestCase("high", AITMemoryLevel.High)]
    [TestCase("critical", AITMemoryLevel.Critical)]
    [TestCase("HIGH", AITMemoryLevel.High)]       // 대소문자 무시
    [TestCase("Critical", AITMemoryLevel.Critical)]
    [TestCase("", AITMemoryLevel.Ok)]
    [TestCase(null, AITMemoryLevel.Ok)]
    [TestCase("urgent", AITMemoryLevel.Ok)]       // 모르는 값은 Ok(과잉 반응 방지)
    public void ParseLevel_MapsStringToEnum(string level, AITMemoryLevel expected)
    {
        Assert.AreEqual(expected, AITMemoryBridge.ParseLevel(level));
    }

    // =====================================================
    // Dispatch
    // =====================================================

    [Test]
    public void Dispatch_ValidJson_UpdatesLatestAndRaisesEvent()
    {
        Assert.IsTrue(AITMemoryBridge.Dispatch(PressureJson));
        Assert.AreEqual(1, _eventCount);
        Assert.IsNotNull(_lastEvent);
        Assert.AreEqual(AITMemoryLevel.High, _lastEvent.Level);
        Assert.AreSame(_lastEvent, AITMemoryBridge.Latest);
    }

    [Test]
    public void Dispatch_InvalidJson_ReturnsFalseAndKeepsLatest()
    {
        Assert.IsTrue(AITMemoryBridge.Dispatch(PressureJson));
        var before = AITMemoryBridge.Latest;

        Assert.IsFalse(AITMemoryBridge.Dispatch("garbage"));
        Assert.AreEqual(1, _eventCount, "잘못된 입력은 이벤트를 만들지 않는다.");
        Assert.AreSame(before, AITMemoryBridge.Latest);
    }

    [Test]
    public void Dispatch_SubscriberThrows_OtherSubscribersStillRun()
    {
        var order = new List<string>();
        Action<AITMemoryInfo> thrower = _ => { order.Add("thrower"); throw new InvalidOperationException("테스트용 예외"); };
        Action<AITMemoryInfo> after = _ => order.Add("after");
        AITMemoryBridge.OnMemoryEvent += thrower;
        AITMemoryBridge.OnMemoryEvent += after;
        try
        {
            Assert.DoesNotThrow(() => Assert.IsTrue(AITMemoryBridge.Dispatch(PressureJson)));
        }
        finally
        {
            AITMemoryBridge.OnMemoryEvent -= thrower;
            AITMemoryBridge.OnMemoryEvent -= after;
        }

        CollectionAssert.AreEqual(new[] { "thrower", "after" }, order);
        Assert.AreEqual(1, _eventCount, "SetUp 에서 건 핸들러도 호출돼야 한다.");
    }

    [Test]
    public void Dispatch_CriticalWithAutoUnloadOff_DoesNotThrow()
    {
        // 기본값(꺼짐)에서는 UnloadUnusedAssets 경로를 타지 않는다.
        Assert.IsFalse(AITMemoryBridge.AutoUnloadUnusedAssets);
        const string json = "{\"type\":\"pressure\",\"level\":\"critical\",\"heapBytes\":402653184}";
        Assert.DoesNotThrow(() => AITMemoryBridge.Dispatch(json));
        Assert.AreEqual(AITMemoryLevel.Critical, _lastEvent.Level);
    }

    // =====================================================
    // 기본값
    // =====================================================

    [Test]
    public void AutoUnloadUnusedAssets_DefaultsToFalse()
    {
        Assert.IsFalse(AITMemoryBridge.AutoUnloadUnusedAssets);
    }

    [Test]
    public void TryGetSnapshot_OutsideWebGL_ReturnsFalse()
    {
        // 에디터에서는 JS 가 없으므로 false(예외 없음)
        Assert.IsFalse(AITMemoryBridge.TryGetSnapshot(out var info));
        Assert.IsNull(info);
    }
}
