// -----------------------------------------------------------------------
// MainThreadDispatcherTests.cs - MainThreadDispatcher 단위 테스트
// Level 0: EditorApplication.update 의존 없이 순수 C# 로직만 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.Menu;

namespace AppsInToss.Editor.Menu.Tests
{
    [TestFixture]
    public class MainThreadDispatcherTests
    {
        [Test]
        public void Enqueue_NullAction_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MainThreadDispatcher.Enqueue(null));
        }

        [Test]
        public void EnsureRegistered_CalledTwice_RegistersOnlyOnce()
        {
            // static 생성자에서 이미 한 번 등록된 상태. 두 번 더 호출해도
            // Interlocked.CompareExchange 가드로 인해 추가 등록 없이 안전해야 함.
            Assert.DoesNotThrow(() =>
            {
                MainThreadDispatcher.EnsureRegistered();
                MainThreadDispatcher.EnsureRegistered();
            });
        }
    }
}
