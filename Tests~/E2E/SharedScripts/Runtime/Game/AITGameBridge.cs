using System.Runtime.InteropServices;

/// <summary>
/// game posture 픽스처 전용 JS 브릿지. 상태 JSON 을 window.__AIT_GAME 으로 올린다(AITGameBridge.jslib).
/// 에디터/비 WebGL 에서는 호출만 세고 아무것도 하지 않는다.
/// </summary>
public static class AITGameBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AITGame_Report(string json);
#endif

    public static int ReportCount;

    public static void Report(string json)
    {
        ReportCount++;
#if UNITY_WEBGL && !UNITY_EDITOR
        AITGame_Report(json);
#endif
    }
}
