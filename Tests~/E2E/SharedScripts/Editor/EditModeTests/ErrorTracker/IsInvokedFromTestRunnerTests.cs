// ---------------------------------------------------------------------------
// IsInvokedFromTestRunnerTests.cs - 테스트 러너 스택 감지 단위 테스트
// EditMode 테스트가 발생시키는 Debug.LogWarning을 Sentry에서 제외하기 위한
// 호출 스택 기반 가드의 정상/예외 케이스를 검증.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class IsInvokedFromTestRunnerTests
{
    [Test]
    public void NullStack_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsInvokedFromTestRunner(null));
    }

    [Test]
    public void EmptyStack_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsInvokedFromTestRunner(""));
    }

    [Test]
    public void PureSdkStack_ReturnsFalse()
    {
        string stack =
            "AppsInToss.Editor.AITDeprecationChecker.ParseMinVersionFromJson (System.String json) (at Editor/AITDeprecationChecker.cs:150)\n" +
            "AppsInToss.Editor.AITDeprecationChecker.FetchMinVersion () (at Editor/AITDeprecationChecker.cs:131)\n";
        Assert.IsFalse(AITEditorErrorTracker.IsInvokedFromTestRunner(stack));
    }

    [Test]
    public void NUnitFrameworkInStack_ReturnsTrue()
    {
        // NUnit.Framework가 호출 스택에 나타나면 테스트 실행 중으로 판단
        string stack =
            "AppsInToss.Editor.AITDeprecationChecker.ParseMinVersionFromJson (System.String json) (at Editor/AITDeprecationChecker.cs:150)\n" +
            "DeprecationCheckerTests.ParseMinVersionFromJson_EmptyJson_ReturnsNull () (at Tests~/.../DeprecationCheckerTests.cs:198)\n" +
            "NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object instance)\n";
        Assert.IsTrue(AITEditorErrorTracker.IsInvokedFromTestRunner(stack));
    }

    [Test]
    public void UnityTestRunnerInStack_ReturnsTrue()
    {
        // Unity의 TestRunner 내부 네임스페이스가 있으면 테스트 실행 중으로 판단
        string stack =
            "AppsInToss.Editor.Foo.Bar () (at Editor/Foo.cs:10)\n" +
            "UnityEngine.TestRunner.NUnitExtensions.Runner.EnumerableTestMethodCommand+<ExecuteEnumerable>d__3.MoveNext ()\n";
        Assert.IsTrue(AITEditorErrorTracker.IsInvokedFromTestRunner(stack));
    }

    [Test]
    public void UnityEditorTestToolsInStack_ReturnsTrue()
    {
        string stack =
            "AppsInToss.Editor.Foo.Bar () (at Editor/Foo.cs:10)\n" +
            "UnityEditor.TestTools.TestRunner.EditModeLauncher+<DoRunSomeTests>d__4.MoveNext ()\n";
        Assert.IsTrue(AITEditorErrorTracker.IsInvokedFromTestRunner(stack));
    }

    [Test]
    public void MessageMentioningNunitText_ReturnsFalse()
    {
        // 메시지 본문에 'NUnit' 문자열이 있을 수 있으나 stackTrace 인자에 없으면 false
        // (stackTrace 기반 판정이라는 불변 조건 확인)
        string stack = "AppsInToss.Editor.Foo.Bar () (at Editor/Foo.cs:10)";
        Assert.IsFalse(AITEditorErrorTracker.IsInvokedFromTestRunner(stack));
    }
}
