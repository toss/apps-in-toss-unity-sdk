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

        [Test]
        public void IsPortConflictError_AllUppercase_ReturnsTrue()
        {
            // ToLowerInvariant() 계약 고정: 완전 대문자 입력도 매칭되어야 함
            Assert.IsTrue(PortResolver.IsPortConflictError("PORT IS ALREADY IN USE"));
        }

        [Test]
        public void IsPortConflictError_SubstringInMultiLineOutput_ReturnsTrue()
        {
            // 실제 툴 출력은 다중 라인 스택트레이스일 수 있음
            string output = "Starting dev server...\n  at node (internal)\nError: listen EADDRINUSE on :::5173\n  at Server.listen";
            Assert.IsTrue(PortResolver.IsPortConflictError(output));
        }

        [Test]
        public void IsPortConflictError_NearMissPhrase_ReturnsFalse()
        {
            // 느슨한 문구 매칭으로 false positive가 생기지 않아야 함
            Assert.IsFalse(PortResolver.IsPortConflictError("the address has already been used"));
        }

        [Test]
        public void IsPortConflictError_WhitespaceOnly_ReturnsFalse()
        {
            Assert.IsFalse(PortResolver.IsPortConflictError("   "));
            Assert.IsFalse(PortResolver.IsPortConflictError("\n"));
            Assert.IsFalse(PortResolver.IsPortConflictError("\t"));
        }
    }
}
