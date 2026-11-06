using System.Runtime.InteropServices;

/// <summary>
/// mobilegame posture 픽스처 전용 JS 브릿지. 상태 JSON 을 window.__AIT_RUN 으로 올린다(Plugins/AITRunBridge.jslib).
/// 에디터/비 WebGL 에서는 아무것도 하지 않는다.
/// </summary>
public static class AITRunBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AITRun_Report(string json);
#endif

    public static void Report(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AITRun_Report(json);
#endif
    }
}
