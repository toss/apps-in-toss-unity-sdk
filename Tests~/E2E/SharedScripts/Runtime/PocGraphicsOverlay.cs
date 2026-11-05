using UnityEngine;

// WebGPU PoC: shows the graphics API Unity actually picked at runtime.
public class PocGraphicsOverlay : MonoBehaviour
{
    static string text;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        text = $"GFX: {SystemInfo.graphicsDeviceType}\n{SystemInfo.graphicsDeviceVersion}\n{SystemInfo.graphicsDeviceName}";
        Debug.Log("[AIT-POC] " + text.Replace("\n", " | "));
        var go = new GameObject("PocGraphicsOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<PocGraphicsOverlay>();
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(18, Screen.height / 40), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = SystemInfo.graphicsDeviceType.ToString().Contains("WebGPU") ? Color.green : Color.yellow;
        }
        GUI.depth = -1000;
        float w = Screen.width * 0.9f, h = style.fontSize * 4.2f;
        GUI.Box(new Rect((Screen.width - w) / 2, Screen.height * 0.45f, w, h), text, style);
    }
}
