// -----------------------------------------------------------------------
// PortResolverParserTests.cs
// Level 0: PortResolver.IsPortConflictError 순수 파서 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.Menu;

namespace AppsInToss.Editor.Menu.Tests
{
    [TestFixture]
    public class PortResolverParserTests
    {
        [Test]
        public void IsPortConflictError_EaddrInUse_ReturnsTrue()
        {
            Assert.IsTrue(PortResolver.IsPortConflictError("Error: listen EADDRINUSE: address already in use :::5173"));
        }

        [Test]
        public void IsPortConflictError_PortIsAlreadyInUseMessage_ReturnsTrue()
        {
            Assert.IsTrue(PortResolver.IsPortConflictError("Port is already in use"));
        }

        [Test]
        public void IsPortConflictError_AddressAlreadyInUseMessage_ReturnsTrue()
        {
            Assert.IsTrue(PortResolver.IsPortConflictError("bind: address already in use"));
        }

        [Test]
        public void IsPortConflictError_NormalOutput_ReturnsFalse()
        {
            Assert.IsFalse(PortResolver.IsPortConflictError("Build succeeded in 1.2s"));
        }

        [Test]
        public void IsPortConflictError_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(PortResolver.IsPortConflictError(string.Empty));
        }

        [Test]
        public void IsPortConflictError_Null_ReturnsFalse()
        {
            Assert.IsFalse(PortResolver.IsPortConflictError(null));
        }
    }
}
