// -----------------------------------------------------------------------
// HeavyBuildVariantsTests.cs - perf heavy 빌드 변형 레지스트리(HeavyBuildVariants) 검증
//   · 이름 파싱(쉼표/공백/세미콜론, 중복 제거), all0 내장 변형, 미등록 이름 거부(아무것도 적용하지 않음)
//   · 사용자 등록·해제, 적용 순서
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;

[TestFixture]
[Category("Unit")]
public class HeavyBuildVariantsTests
{
    private AITEditorScriptObject _config;
    private readonly List<string> _registered = new List<string>();

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string name in _registered) HeavyBuildVariants.Unregister(name);
        _registered.Clear();
        if (_config != null)
        {
            UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void ParseNames_SplitsOnSeparators_AndDeduplicates()
    {
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, HeavyBuildVariants.ParseNames("a, b;c  a"));
        CollectionAssert.AreEqual(new[] { "all0" }, HeavyBuildVariants.ParseNames("all0,ALL0"));
        Assert.IsEmpty(HeavyBuildVariants.ParseNames(null));
        Assert.IsEmpty(HeavyBuildVariants.ParseNames("  ,; "));
    }

    [Test]
    public void EnvVarName_IsStable()
    {
        Assert.AreEqual("AIT_PERF_VARIANT", HeavyBuildVariants.EnvVar);
    }

    [Test]
    public void All0_IsBuiltIn_AndCannotBeUnregistered()
    {
        Assert.IsTrue(HeavyBuildVariants.IsRegistered("all0"));
        Assert.IsTrue(HeavyBuildVariants.IsRegistered("ALL0"), "이름은 대소문자를 구분하지 않는다");
        Assert.IsFalse(HeavyBuildVariants.Unregister("all0"));
        Assert.IsTrue(HeavyBuildVariants.IsRegistered("all0"));
        CollectionAssert.Contains(HeavyBuildVariants.RegisteredNames(), "all0");
    }

    [Test]
    public void All0_SetsEveryNewLeverToZero_AndDisablesCompressedPlayback()
    {
        HeavyBuildVariants.Apply(new[] { "all0" }, _config);

        Assert.AreEqual(0, _config.webglAntialiasOpt);
        Assert.AreEqual(0, _config.webglContextRecovery);
        Assert.AreEqual(0, _config.frameRateCap);
        Assert.AreEqual(0, _config.adaptiveFrameRate);
        Assert.AreEqual(0, _config.mobileLifecycle);
        Assert.AreEqual(0, _config.memoryTelemetry);
        Assert.AreEqual(0, _config.exactDataBody);
        Assert.AreEqual(0, _config.releaseConsumedData);
        Assert.AreEqual(0, _config.audioForceCompressedPlayback);
        Assert.AreEqual(0, _config.lowMemoryTier);
        Assert.AreEqual(0, _config.pageCacheDeferredPut);
        Assert.AreEqual(0, _config.audioStreamingCompressedPlayback);
    }

    [Test]
    public void Apply_UnknownName_ThrowsBeforeApplyingAnything()
    {
        var ex = Assert.Throws<ArgumentException>(() => HeavyBuildVariants.Apply(new[] { "all0", "no-such-variant" }, _config));
        StringAssert.Contains("no-such-variant", ex.Message);
        Assert.AreEqual(-1, _config.frameRateCap, "미등록 이름이 있으면 앞의 all0 도 적용하지 않아야 한다");
    }

    [Test]
    public void Apply_EmptyList_IsNoOp_EvenWithNullConfig()
    {
        Assert.IsEmpty(HeavyBuildVariants.Apply(new string[0], null));
        Assert.IsEmpty(HeavyBuildVariants.Apply(null, null));
        Assert.Throws<ArgumentNullException>(() => HeavyBuildVariants.Apply(new[] { "all0" }, null));
    }

    [Test]
    public void Register_AddsVariant_AppliedInGivenOrder()
    {
        RegisterTemp("unit-test-set-cap", c => c.frameRateCap = 1);
        RegisterTemp("unit-test-clear-cap", c => c.frameRateCap = 0);

        HeavyBuildVariants.Apply(new[] { "unit-test-set-cap", "unit-test-clear-cap" }, _config);
        Assert.AreEqual(0, _config.frameRateCap, "뒤에 적은 변형이 마지막에 적용된다");

        HeavyBuildVariants.Apply(new[] { "unit-test-clear-cap", "unit-test-set-cap" }, _config);
        Assert.AreEqual(1, _config.frameRateCap);
    }

    [Test]
    public void Register_Duplicate_Throws_AndInvalidArgumentsAreRejected()
    {
        RegisterTemp("unit-test-dup", c => { });
        Assert.Throws<InvalidOperationException>(() => HeavyBuildVariants.Register("unit-test-dup", c => { }));
        Assert.Throws<InvalidOperationException>(() => HeavyBuildVariants.Register("all0", c => { }));
        Assert.Throws<ArgumentException>(() => HeavyBuildVariants.Register("  ", c => { }));
        Assert.Throws<ArgumentNullException>(() => HeavyBuildVariants.Register("unit-test-null", null));
    }

    [Test]
    public void Unregister_RemovesVariant()
    {
        RegisterTemp("unit-test-temp", c => { });
        Assert.IsTrue(HeavyBuildVariants.IsRegistered("unit-test-temp"));
        Assert.IsTrue(HeavyBuildVariants.Unregister("unit-test-temp"));
        Assert.IsFalse(HeavyBuildVariants.IsRegistered("unit-test-temp"));
        Assert.IsFalse(HeavyBuildVariants.Unregister("unit-test-temp"));
    }

    [Test]
    public void ApplyFromEnvironment_WithUnsetVariable_ReturnsEmpty()
    {
        string previous = Environment.GetEnvironmentVariable(HeavyBuildVariants.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(HeavyBuildVariants.EnvVar, null);
            Assert.IsEmpty(HeavyBuildVariants.ApplyFromEnvironment(_config));
            Assert.AreEqual(-1, _config.exactDataBody);
        }
        finally
        {
            Environment.SetEnvironmentVariable(HeavyBuildVariants.EnvVar, previous);
        }
    }

    [Test]
    public void ApplyFromEnvironment_AppliesNamedVariants()
    {
        string previous = Environment.GetEnvironmentVariable(HeavyBuildVariants.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(HeavyBuildVariants.EnvVar, "all0");
            CollectionAssert.AreEqual(new[] { "all0" }, HeavyBuildVariants.ApplyFromEnvironment(_config));
            Assert.AreEqual(0, _config.exactDataBody);
        }
        finally
        {
            Environment.SetEnvironmentVariable(HeavyBuildVariants.EnvVar, previous);
        }
    }

    private void RegisterTemp(string name, Action<AITEditorScriptObject> apply)
    {
        HeavyBuildVariants.Register(name, apply);
        _registered.Add(name);
    }
}
