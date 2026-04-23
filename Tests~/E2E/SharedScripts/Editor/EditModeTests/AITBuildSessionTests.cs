using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss.Editor;

namespace AppsInToss.Editor.EditModeTests
{
    public class PlayerSettingsSnapshotTests
    {
        [Test]
        public void Capture_RoundTrips_WebGLCompressionFormat()
        {
            var originalCompression = PlayerSettings.WebGL.compressionFormat;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                var snapshot = PlayerSettingsSnapshot.Capture();

                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                Assert.AreNotEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);

                snapshot.Restore();
                Assert.AreEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = originalCompression;
            }
        }

        [Test]
        public void IsSerializable_CanRoundTripThroughJsonUtility()
        {
            var snapshot = PlayerSettingsSnapshot.Capture();
            string json = JsonUtility.ToJson(snapshot);
            var restored = JsonUtility.FromJson<PlayerSettingsSnapshot>(json);
            Assert.AreEqual(snapshot.webGLTemplate, restored.webGLTemplate);
        }
    }
}
