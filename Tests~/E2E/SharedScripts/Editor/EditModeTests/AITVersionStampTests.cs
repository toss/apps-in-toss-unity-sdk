// -----------------------------------------------------------------------
// AITVersionStampTests.cs - package.json description 릴리스 스탬프 파싱 검증
// Level 0: 이전 스탬프(리비전 없음)는 Revision 0, 새 스탬프의 +N은 Revision N
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss;

[TestFixture]
public class AITVersionStampTests
{
    private static void Parse(string description, out string dateTime, out string hash, out int revision)
    {
        AITVersion.ParseReleaseStamp(description, out dateTime, out hash, out revision);
    }

    [Test]
    public void OldStamp_HasNoRevision()
    {
        Parse("Apps in Toss SDK (Released: 20260126_1204, e89a387)", out var dt, out var hash, out var rev);
        Assert.AreEqual("20260126_1204", dt);
        Assert.AreEqual("e89a387", hash);
        Assert.AreEqual(0, rev);
    }

    [Test]
    public void NewStamp_ReadsRevision()
    {
        Parse("Apps in Toss SDK (Released: 20260126_1204, e89a387, +2)", out var dt, out var hash, out var rev);
        Assert.AreEqual("20260126_1204", dt);
        Assert.AreEqual("e89a387", hash);
        Assert.AreEqual(2, rev);
    }

    [Test]
    public void StampWithoutHash_HasNoRevision()
    {
        Parse("(Released: 20260126_1204)", out var dt, out var hash, out var rev);
        Assert.AreEqual("20260126_1204", dt);
        Assert.IsNull(hash);
        Assert.AreEqual(0, rev);
    }

    [Test]
    public void StampWithRevisionButNoHash_ReadsRevision()
    {
        Parse("(Released: 20260126_1204, +2)", out var dt, out var hash, out var rev);
        Assert.AreEqual("20260126_1204", dt);
        Assert.IsNull(hash);
        Assert.AreEqual(2, rev);
    }

    [TestCase("(Released: 20260126_1204, e89a387, +0)")]
    [TestCase("(Released: 20260126_1204, e89a387, +02)")]
    [TestCase("(Released: 20260126_1204, e89a387, +x)")]
    public void MalformedRevision_DoesNotMatchStamp(string description)
    {
        // 정규식 전체가 맞지 않으면 일시·해시도 비우고 리비전 0
        Parse(description, out var dt, out var hash, out var rev);
        Assert.IsNull(dt);
        Assert.IsNull(hash);
        Assert.AreEqual(0, rev);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("no stamp here")]
    public void NoStamp_AllEmpty(string description)
    {
        Parse(description, out var dt, out var hash, out var rev);
        Assert.IsNull(dt);
        Assert.IsNull(hash);
        Assert.AreEqual(0, rev);
    }
}
