using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// WebGPU PoC 벤치마크. 씬 로드 후 자동 실행되며 결과는 window.__pocBench / localStorage 'pocBenchRuns'에 남는다.
//   S: 빌드마다 소금(salt)이 달라지는 커스텀 셰이더 변형 40개 첫 표시(기기 셰이더 캐시가 없는 콜드 컴파일)
//   C: Standard 셰이더 변형 80개 첫 표시(셰이더 컴파일 부담)
//   A: 드로우콜 N개(머티리얼 인스턴스가 모두 다른 큐브) 250~4000
//   B: 화면 전체 반투명 쿼드 K겹 4~32
// 각 단계는 워밍업 2초 + 측정 5초. 주소에 nobench 가 있으면 실행하지 않는다.
[DefaultExecutionOrder(-32000)]
public class PocBench : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern double PocBench_Now();
    [DllImport("__Internal")] static extern void PocBench_Publish(string json);
#else
    static double PocBench_Now() => Time.realtimeSinceStartup * 1000.0;
    static void PocBench_Publish(string json) { }
#endif

    [Serializable]
    public class Stage
    {
        public string id, group;
        public int param, frames;
        public float fps, showMs;
        public float dtAvg, dtP50, dtP95, dtMax;
        public float cpuAvg, cpuP50, cpuP95, cpuMax;
    }

    [Serializable]
    public class Report
    {
        public string runId, gfx, gfxVersion, gfxName, unityVersion, current;
        public int screenW, screenH, targetFrameRate;
        public double firstUpdateMs, firstFrameEndMs, benchStartMs;
        public bool done;
        public List<Stage> stages = new List<Stage>();
    }

    const float WarmupMs = 2000f, MeasureMs = 5000f;
    static readonly int[] DrawCalls = { 250, 500, 1000, 2000, 4000 };
    static readonly int[] Overdraw = { 4, 8, 16, 32, 64, 128, 256 };

    readonly Report report = new Report();
    long frameStartTicks;
    float lastDtMs;
    bool firstUpdateSeen;
    string status = "starting";
    GUIStyle style;

    Camera cam;
    Light benchLight;
    GameObject root;
    readonly List<Material> temp = new List<Material>();
    Action<float> tick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Application.absoluteURL != null && Application.absoluteURL.Contains("nobench")) return;
#endif
        var go = new GameObject("PocBench");
        DontDestroyOnLoad(go);
        go.AddComponent<PocBench>();
    }

    void Start()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        report.runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        report.gfx = SystemInfo.graphicsDeviceType.ToString();
        report.gfxVersion = SystemInfo.graphicsDeviceVersion;
        report.gfxName = SystemInfo.graphicsDeviceName;
        report.unityVersion = Application.unityVersion;
        report.screenW = Screen.width;
        report.screenH = Screen.height;
        report.targetFrameRate = Application.targetFrameRate;
        StartCoroutine(Run());
    }

    // 프레임 시작: 가장 이른 실행순서에서 스톱워치 시작, 프레임 끝(WaitForEndOfFrame)에서 종료한다.
    void Update()
    {
        frameStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        lastDtMs = Time.unscaledDeltaTime * 1000f;
        if (!firstUpdateSeen) { firstUpdateSeen = true; report.firstUpdateMs = PocBench_Now(); }
        if (tick != null) tick(Time.unscaledDeltaTime);
    }

    float CpuMs() => (System.Diagnostics.Stopwatch.GetTimestamp() - frameStartTicks) * 1000f / System.Diagnostics.Stopwatch.Frequency;

    IEnumerator Run()
    {
        yield return new WaitForEndOfFrame();
        report.firstFrameEndMs = PocBench_Now();
        yield return new WaitForSecondsRealtime(1f);
        report.benchStartMs = PocBench_Now();

        foreach (var other in Camera.allCameras) other.enabled = false;
        var camGo = new GameObject("PocBenchCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = new Vector3(0, 1000, -10);
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
        cam.depth = 100;
        var lightGo = new GameObject("PocBenchLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.rotation = Quaternion.Euler(40, 30, 0);
        benchLight = lightGo.AddComponent<Light>();
        benchLight.type = LightType.Directional;
        benchLight.shadows = LightShadows.None;

        Publish();
        int coldCount = Resources.LoadAll<Material>("PocBench/Cold").Length;
        if (coldCount > 0) yield return RunStage("S" + coldCount, "S", coldCount);
        yield return RunStage("C80", "C", PocBenchVariantCount());
        foreach (var n in DrawCalls) yield return RunStage("A" + n, "A", n);
        foreach (var k in Overdraw) yield return RunStage("B" + k, "B", k);

        report.current = "";
        report.done = true;
        status = "done";
        Publish();
        Debug.Log("[AIT-POC] BENCH DONE " + JsonUtility.ToJson(report));
        if (cam != null) cam.enabled = false;
    }

    static int PocBenchVariantCount()
    {
        var all = Resources.LoadAll<Material>("PocBench/Variants");
        return all.Length;
    }

    IEnumerator RunStage(string id, string group, int param)
    {
        report.current = id;
        status = $"{id} setup";
        Publish();
        yield return null;

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Setup(group, param);
        yield return new WaitForEndOfFrame();
        var st = new Stage { id = id, group = group, param = param };
        st.showMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000f / System.Diagnostics.Stopwatch.Frequency;

        float acc = 0;
        status = $"{id} warmup";
        while (acc < WarmupMs) { yield return new WaitForEndOfFrame(); acc += lastDtMs; }

        status = $"{id} measure";
        var dts = new List<float>(2048);
        var cpus = new List<float>(2048);
        acc = 0;
        while (acc < MeasureMs)
        {
            yield return new WaitForEndOfFrame();
            dts.Add(lastDtMs); cpus.Add(CpuMs()); acc += lastDtMs;
        }
        st.frames = dts.Count;
        st.fps = st.frames * 1000f / acc;
        Fill(dts, out st.dtAvg, out st.dtP50, out st.dtP95, out st.dtMax);
        Fill(cpus, out st.cpuAvg, out st.cpuP50, out st.cpuP95, out st.cpuMax);
        report.stages.Add(st);

        Teardown();
        Publish();
        yield return null;
        yield return null;
    }

    static void Fill(List<float> v, out float avg, out float p50, out float p95, out float max)
    {
        v.Sort();
        int n = v.Count;
        float sum = 0; foreach (var x in v) sum += x;
        avg = sum / n;
        p50 = v[(n - 1) / 2];
        p95 = v[Mathf.Clamp(Mathf.CeilToInt(0.95f * n) - 1, 0, n - 1)];
        max = v[n - 1];
    }

    void Setup(string group, int param)
    {
        root = new GameObject("PocBenchStage");
        root.transform.position = new Vector3(0, 1000, 0);
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
        cam.aspect = aspect;
        float viewH = cam.orthographicSize * 2f, viewW = viewH * aspect;

        if (group == "A")
        {
            var shader = Resources.Load<Shader>("PocBench/PocUnlit");
            var mesh = PrimitiveMesh(PrimitiveType.Cube);
            int cols = Mathf.CeilToInt(Mathf.Sqrt(param * aspect));
            int rows = Mathf.CeilToInt(param / (float)cols);
            float cell = Mathf.Min(viewW / cols, viewH / rows);
            var spinners = new Transform[param];
            var axes = new Vector3[param];
            for (int i = 0; i < param; i++)
            {
                var go = new GameObject("c" + i);
                go.transform.SetParent(root.transform, false);
                int cx = i % cols, cy = i / cols;
                go.transform.localPosition = new Vector3((cx + 0.5f - cols / 2f) * cell, (cy + 0.5f - rows / 2f) * cell, 0);
                go.transform.localScale = Vector3.one * cell * 0.6f;
                go.transform.localRotation = Quaternion.Euler(30, 45, 0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var m = new Material(shader);
                m.SetColor("_Color", Color.HSVToRGB((i * 0.618f) % 1f, 0.7f, 0.95f));
                temp.Add(m);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                spinners[i] = go.transform;
                axes[i] = new Vector3(0.3f + (i % 5) * 0.2f, 1f, 0.2f * (i % 3));
            }
            tick = dt => { for (int i = 0; i < spinners.Length; i++) spinners[i].Rotate(axes[i] * (90f * dt), Space.Self); };
        }
        else if (group == "B")
        {
            var shader = Resources.Load<Shader>("PocBench/PocOverdraw");
            var mesh = PrimitiveMesh(PrimitiveType.Quad);
            for (int i = 0; i < param; i++)
            {
                var go = new GameObject("q" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0, 0, 1f + i * 0.01f);
                go.transform.localScale = new Vector3(viewW * 1.05f, viewH * 1.05f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var m = new Material(shader);
                var c = Color.HSVToRGB((i * 0.137f) % 1f, 0.5f, 0.9f); c.a = 0.05f;
                m.SetColor("_Color", c);
                temp.Add(m);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
            tick = null;
        }
        else
        {
            // S/C: 변형 머티리얼마다 구 하나. 첫 표시 때 셰이더 변형이 처음 쓰인다.
            var mats = Resources.LoadAll<Material>(group == "S" ? "PocBench/Cold" : "PocBench/Variants");
            var mesh = PrimitiveMesh(PrimitiveType.Sphere);
            int cols = Mathf.CeilToInt(Mathf.Sqrt(mats.Length * aspect));
            int rows = Mathf.CeilToInt(mats.Length / (float)cols);
            float cell = Mathf.Min(viewW / cols, viewH / rows);
            for (int i = 0; i < mats.Length; i++)
            {
                var go = new GameObject("v" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3((i % cols + 0.5f - cols / 2f) * cell, (i / cols + 0.5f - rows / 2f) * cell, 0);
                go.transform.localScale = Vector3.one * cell * 0.8f;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mats[i];
            }
            tick = null;
        }
    }

    static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
    static Mesh PrimitiveMesh(PrimitiveType t)
    {
        if (meshes.TryGetValue(t, out var m) && m != null) return m;
        var tmp = GameObject.CreatePrimitive(t);
        m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        meshes[t] = m;
        return m;
    }

    void Teardown()
    {
        tick = null;
        if (root != null) Destroy(root);
        foreach (var m in temp) if (m != null) Destroy(m);
        temp.Clear();
    }

    void Publish()
    {
        PocBench_Publish(JsonUtility.ToJson(report));
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = false };
            style.normal.textColor = Color.white;
        }
        style.fontSize = Mathf.Max(14, Screen.height / 62);
        var sb = new StringBuilder();
        sb.Append($"BENCH {report.gfx} {report.unityVersion} {report.screenW}x{report.screenH}\n");
        sb.Append($"load: 1stUpdate {report.firstUpdateMs:F0}ms / 1stFrame {report.firstFrameEndMs:F0}ms\n");
        sb.Append(report.done ? "DONE\n" : $"{status}\n");
        foreach (var s in report.stages)
            sb.Append($"{s.id}: show {s.showMs:F0} dt {s.dtP50:F1}/{s.dtP95:F1}/{s.dtMax:F0} cpu {s.cpuP50:F1}/{s.cpuP95:F1}\n");
        GUI.depth = -2000;
        float w = Screen.width * 0.94f, h = style.fontSize * (report.stages.Count + 5) * 1.35f;
        GUI.Box(new Rect((Screen.width - w) / 2, Screen.height * 0.01f, w, h), sb.ToString(), style);
    }
}
