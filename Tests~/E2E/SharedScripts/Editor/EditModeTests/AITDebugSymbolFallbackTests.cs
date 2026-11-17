// -----------------------------------------------------------------------
// AITDebugSymbolFallbackTests.cs - External 심볼 로더 전처리 실패 서명 판정 검증
// Level 0: IsSignature 순수 함수만 다룬다(재빌드·EditorPrefs 기록은 Perf Build Heavy 가 실제로 확인).
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITDebugSymbolFallbackTests
{
    [Test]
    public void LoaderPreprocessFailure_IsSignature()
    {
        const string log =
            "Building Library/Bee/artifacts/WebGLwebgl.loader.js failed with output:\n" +
            "Preprocessor error \"ReferenceError: SYMBOLS_FILENAME is not defined\" occured in file " +
            "\"UnityLoader/UnityLoader.js\" at line 39 when evaluating expression \"#if SYMBOLS_FILENAME\"";

        Assert.IsTrue(AITDebugSymbolFallback.IsSignature(log));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Preprocessor error \"ReferenceError: DATA_FILENAME is not defined\"")]
    [TestCase("wasm-ld: Killed: 9")]
    public void OtherFailures_AreNotSignature(string log)
    {
        Assert.IsFalse(AITDebugSymbolFallback.IsSignature(log));
    }
}
