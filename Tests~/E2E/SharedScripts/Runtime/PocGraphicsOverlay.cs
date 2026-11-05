using System.Runtime.InteropServices;
using UnityEngine;

// WebGPU PoC: shows the graphics API Unity actually picked at runtime, plus load timing
// (ms since page navigation start) so builds can be compared on devices without a web inspector.
public class PocGraphicsOverlay : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string PocTiming_Snapshot();
#else
    static string PocTiming_Snapshot() => $"{Mathf.RoundToInt(Time.realtimeSinceStartup * 1000)}|0|0|0";
#endif

    static string gfx;
    static long sceneMs, engineStartMs, dlEndMs, dlBytes, dlCount;
    long firstFrameMs = -1;
    float maxDtMs;
    int hitches, frames, fps;
    float fpsWindowStart, fpsFrames;
    bool logged;
    string text;
    GUIStyle style;

    static long[] Snap()
    {
        var p = PocTiming_Snapshot().Split('|');
        var r = new long[4];
        for (int i = 0; i < 4 && i < p.Length; i++) long.TryParse(p[i], out r[i]);
        return r;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        gfx = $"GFX: {SystemInfo.graphicsDeviceType}\n{SystemInfo.graphicsDeviceVersion}\n{SystemInfo.graphicsDeviceName}";
        var s = Snap();
        sceneMs = s[0];
        engineStartMs = s[0] - Mathf.RoundToInt(Time.realtimeSinceStartup * 1000);
        dlEndMs = s[1]; dlBytes = s[2]; dlCount = s[3];
        Debug.Log("[AIT-POC] " + gfx.Replace("\n", " | "));
        var go = new GameObject("PocGraphicsOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<PocGraphicsOverlay>();
    }

    void Update()
    {
        frames++;
        if (firstFrameMs < 0) { firstFrameMs = Snap()[0]; fpsWindowStart = Time.realtimeSinceStartup; }
        float dt = Time.unscaledDeltaTime * 1000f;
        float sinceFirst = Time.realtimeSinceStartup - fpsWindowStart;
        if (frames > 1 && sinceFirst < 10f)
        {
            if (dt > maxDtMs) maxDtMs = dt;
            if (dt > 50f) hitches++;
        }
        fpsFrames++;
        if (Time.realtimeSinceStartup - fpsWindowStart >= 1f && sinceFirst > 0f)
        {
            fps = Mathf.RoundToInt(fpsFrames / (Time.realtimeSinceStartup - fpsWindowStart));
            fpsFrames = 0; fpsWindowStart = Time.realtimeSinceStartup;
        }
        text = $"{gfx}\n"
             + $"dl done {dlEndMs}ms ({dlBytes / 1048576f:F1}MB x{dlCount})\n"
             + $"engine start {engineStartMs}ms\n"
             + $"scene {sceneMs}ms / 1st frame {firstFrameMs}ms\n"
             + $"hitch(10s) max {maxDtMs:F0}ms, >50ms x{hitches}\n"
             + $"fps {fps}";
        if (!logged && Time.realtimeSinceStartup * 1000 > (sceneMs - engineStartMs) + 10000)
        {
            logged = true;
            Debug.Log("[AIT-POC] TIMING " + text.Replace("\n", " | "));
        }
    }

    void OnGUI()
    {
        if (text == null) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(16, Screen.height / 52), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = SystemInfo.graphicsDeviceType.ToString().Contains("WebGPU") ? Color.green : Color.yellow;
        }
        GUI.depth = -1000;
        float w = Screen.width * 0.94f, h = style.fontSize * 10f;
        GUI.Box(new Rect((Screen.width - w) / 2, Screen.height * 0.40f, w, h), text, style);
    }
}
