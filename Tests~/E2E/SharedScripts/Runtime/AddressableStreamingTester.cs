#if AIT_ADDRESSABLES_INSTALLED
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 런타임 Addressable 에셋 로드 테스트
/// WebGL에서 StreamingAssets의 AssetBundle 로드를 검증
/// </summary>
public class AddressableStreamingTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendStreamingTestResults(string json);
#else
    private static void SendStreamingTestResults(string json)
    {
        UnityEngine.Debug.Log($"[StreamingTest] Results: {json}");
    }
#endif

    [Serializable]
    private class StreamingTestResult
    {
        public bool success;
        public int textureWidth;
        public int textureHeight;
        public long loadTimeMs;
        public string error;
    }

    /// <summary>
    /// JavaScript에서 호출: window.TriggerStreamingTest()
    /// → SendMessage('BenchmarkManager', 'TriggerStreamingTest')
    /// </summary>
    public void TriggerStreamingTest()
    {
        UnityEngine.Debug.Log("[AddressableStreamingTester] Streaming test triggered");
        RunStreamingTests();
    }

    /// <summary>
    /// Addressable 에셋 로드 테스트 실행
    /// </summary>
    public void RunStreamingTests()
    {
        UnityEngine.Debug.Log("[AddressableStreamingTester] Starting streaming tests...");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var handle = Addressables.LoadAssetAsync<Texture2D>("Assets/TestStreamingAssets/LargeTexture.png");
            handle.Completed += (op) =>
            {
                stopwatch.Stop();
                var result = new StreamingTestResult();

                if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                {
                    result.success = true;
                    result.textureWidth = op.Result.width;
                    result.textureHeight = op.Result.height;
                    result.loadTimeMs = stopwatch.ElapsedMilliseconds;
                    result.error = "";
                    UnityEngine.Debug.Log($"[AddressableStreamingTester] Texture loaded: {op.Result.width}x{op.Result.height} in {stopwatch.ElapsedMilliseconds}ms");
                }
                else
                {
                    result.success = false;
                    result.textureWidth = 0;
                    result.textureHeight = 0;
                    result.loadTimeMs = stopwatch.ElapsedMilliseconds;
                    result.error = op.OperationException?.Message ?? "Unknown error";
                    UnityEngine.Debug.LogError($"[AddressableStreamingTester] Load failed: {result.error}");
                }

                string json = JsonUtility.ToJson(result);
                SendStreamingTestResults(json);

                // 리소스 해제
                Addressables.Release(handle);
            };
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            var result = new StreamingTestResult
            {
                success = false,
                textureWidth = 0,
                textureHeight = 0,
                loadTimeMs = stopwatch.ElapsedMilliseconds,
                error = e.Message
            };
            string json = JsonUtility.ToJson(result);
            UnityEngine.Debug.LogError($"[AddressableStreamingTester] Exception: {e.Message}");
            SendStreamingTestResults(json);
        }
    }
}
#endif
