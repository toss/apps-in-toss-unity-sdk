using System;
using System.Threading.Tasks;
using NUnit.Framework;
using AppsInToss.Editor;

namespace AppsInToss.Editor.EditModeTests
{
    public class AITEditorIdleWaiterTests
    {
        [TearDown]
        public void ResetProbes()
        {
            AITEditorIdleWaiter.ResetProbesForTesting();
        }

        [Test]
        public async Task WaitAsync_ReturnsTrue_WhenAlreadyIdle()
        {
            AITEditorIdleWaiter.SetProbesForTesting(
                isCompiling: () => false,
                isUpdating: () => false,
                timeoutDialog: _ => true);

            bool proceed = await AITEditorIdleWaiter.WaitAsync(timeoutSeconds: 1);
            Assert.IsTrue(proceed);
        }

        [Test]
        public async Task WaitAsync_ReturnsTrue_AfterIsCompilingBecomesFalse()
        {
            int calls = 0;
            AITEditorIdleWaiter.SetProbesForTesting(
                isCompiling: () => ++calls < 3,  // true twice, then false
                isUpdating: () => false,
                timeoutDialog: _ => true);

            bool proceed = await AITEditorIdleWaiter.WaitAsync(timeoutSeconds: 5);
            Assert.IsTrue(proceed);
            Assert.GreaterOrEqual(calls, 3);
        }

        [Test]
        public async Task WaitAsync_TimesOut_AfterConfiguredSeconds()
        {
            bool dialogShown = false;
            AITEditorIdleWaiter.SetProbesForTesting(
                isCompiling: () => true,      // never idle
                isUpdating: () => false,
                timeoutDialog: _ => { dialogShown = true; return true; });

            bool proceed = await AITEditorIdleWaiter.WaitAsync(timeoutSeconds: 1);
            Assert.IsTrue(dialogShown);
            Assert.IsTrue(proceed);  // dialog returned "continue"
        }

        [Test]
        public async Task WaitAsync_ReturnsFalse_WhenUserRejectsAfterTimeout()
        {
            AITEditorIdleWaiter.SetProbesForTesting(
                isCompiling: () => true,
                isUpdating: () => false,
                timeoutDialog: _ => false);  // user picks cancel

            bool proceed = await AITEditorIdleWaiter.WaitAsync(timeoutSeconds: 1);
            Assert.IsFalse(proceed);
        }
    }
}
