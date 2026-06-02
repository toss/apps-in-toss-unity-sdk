// ---------------------------------------------------------------------------
// FingerprintNormalizationTests.cs - BuildNormalizedFingerprint 단위 테스트
// 로그 캡처 이벤트의 가변 토큰(경로/해시/버전/숫자/glob 파일명)을 정규화해
// 동일 root cause가 단일 Sentry 이슈로 묶이는지(fingerprint explosion 방지) 검증.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class FingerprintNormalizationTests
{
    #region 같은 root cause → 동일 fingerprint (explosion 수렴)

    [Test]
    public void WindowsPathVariants_ProduceSameFingerprint()
    {
        // Sentry SDK-100 류: 사용자/머신마다 경로가 달라도 같은 예외는 하나의 이슈로 묶여야 한다.
        var a = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "IOException",
            @"[AIT] 패키지 매니저 체크 중 예외: Access to the path C:\Users\alice\AppData\node-x64-installing-12345 is denied");
        var b = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "IOException",
            @"[AIT] 패키지 매니저 체크 중 예외: Access to the path C:\Users\bob\AppData\node-x64-installing-67890 is denied");

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void GlobFileList_CollapsesToSingleFingerprint()
    {
        // Sentry SDK-ZM: "필수 파일 누락! 누락된 필수 파일: {목록}" — 누락 파일 조합이 달라도(1개 vs 여러 개) 같은 이슈로 수렴해야 한다.
        // 실제 WebGLBuildCopier.cs 오류 메시지 형식: "[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락! 누락된 필수 파일: {목록}"
        // glob 파일명 → <file>, 나열된 "<file>, <file>, ..." → <file> 로 접힘.
        var single = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "Error",
            "[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락! 누락된 필수 파일: *.framework.js");
        var many = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "Error",
            "[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락! 누락된 필수 파일: *.framework.js, *.loader.js, *.data.gz");

        Assert.IsNotNull(single);
        Assert.IsNotNull(many);
        CollectionAssert.AreEqual(single, many);
    }

    [Test]
    public void UnixPathVariants_ProduceSameFingerprint()
    {
        // Sentry Bee 패밀리: artifacts 하위 경로/오브젝트 파일명이 달라도 같은 빌드 실패로 묶여야 한다.
        var a = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "BuildError",
            "Building Library/Bee/artifacts/abc/foo.o failed");
        var b = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "BuildError",
            "Building Library/Bee/artifacts/xyz/bar.o failed");

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);
        CollectionAssert.AreEqual(a, b);
    }

    #endregion

    #region 다른 root cause → 다른 fingerprint (오버그룹화 방지)

    [Test]
    public void DifferentMessageFamilies_ProduceDifferentFingerprint()
    {
        // 누락 파일(ZM)과 폴더 삭제 실패는 서로 다른 이슈로 유지되어야 한다(과도한 병합 방지).
        // 실제 WebGLBuildCopier.cs 오류 메시지 형식 사용.
        var missing = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "Error",
            "[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락! 누락된 필수 파일: *.framework.js");
        var deleteFail = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "Error",
            @"[AIT] 이전 빌드 잔여물 정리 실패: C:\proj\ait-build");

        Assert.IsNotNull(missing);
        Assert.IsNotNull(deleteFail);
        CollectionAssert.AreNotEqual(missing, deleteFail);
    }

    [Test]
    public void DifferentExceptionType_ProduceDifferentFingerprint()
    {
        // 메시지가 동일하게 정규화돼도 예외 타입이 다르면 fingerprint도 달라야 한다(slot[1]에 타입 포함).
        var io = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "IOException", @"삭제 실패: C:\a\b\c");
        var unauthorized = AITEditorErrorTracker.BuildNormalizedFingerprint(
            "UnauthorizedAccessException", @"삭제 실패: C:\a\b\c");

        Assert.IsNotNull(io);
        Assert.IsNotNull(unauthorized);
        Assert.AreEqual(io[2], unauthorized[2]); // 정규화된 메시지는 동일
        CollectionAssert.AreNotEqual(io, unauthorized); // 그러나 타입 slot이 달라 전체는 다름
    }

    #endregion

    #region 가변 토큰 없음 / null → 기본 그룹화 유지

    [Test]
    public void CleanMessage_WithoutVariableTokens_ReturnsNull()
    {
        // 치환할 가변 토큰이 전혀 없으면 null을 반환해 Sentry 기본(메시지 텍스트) 그룹화를 그대로 둔다.
        Assert.IsNull(AITEditorErrorTracker.BuildNormalizedFingerprint("Error", "[AIT] 빌드 검증 실패"));
    }

    [Test]
    public void NullOrEmptyMessage_ReturnsNull()
    {
        Assert.IsNull(AITEditorErrorTracker.BuildNormalizedFingerprint("Error", null));
        Assert.IsNull(AITEditorErrorTracker.BuildNormalizedFingerprint("Error", ""));
    }

    #endregion

    #region fingerprint 형식

    [Test]
    public void Fingerprint_HasAitLogPrefixAndTypeSlot()
    {
        var fp = AITEditorErrorTracker.BuildNormalizedFingerprint("IOException", @"삭제 실패: C:\a\b");
        Assert.IsNotNull(fp);
        Assert.AreEqual(3, fp.Length);
        Assert.AreEqual("ait-log", fp[0]);
        Assert.AreEqual("IOException", fp[1]);
    }

    [Test]
    public void EmptyExceptionType_FallsBackToUnknownSlot()
    {
        var fp = AITEditorErrorTracker.BuildNormalizedFingerprint("", @"삭제 실패: C:\a\b");
        Assert.IsNotNull(fp);
        Assert.AreEqual("ait-log", fp[0]);
        Assert.AreEqual("unknown", fp[1]);
    }

    #endregion
}
