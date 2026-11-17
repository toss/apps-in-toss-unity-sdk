using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

// WebGPU PoC 벤치마크. 씬 로드 후 자동 실행되며 결과는 window.__pocBench / localStorage 'pocBenchRuns'에 남는다.
// 기본 실행 순서와 단계 그룹 ID:
//   S  빌드마다 소금(salt)이 달라지는 콜드 셰이더 변형 40개 첫 표시
//   C  Standard 셰이더 변형 80개 첫 표시
//   A  드로우콜 N개(머티리얼 인스턴스가 모두 다른 큐브) 250~4000
//   B  화면 전체 반투명 쿼드 K겹 4~256
//   V  용량 램프(GPU 인스턴싱): 조명+노이즈 셰이더 오브젝트 500~64000, 3초씩
//   VN 용량 램프(인스턴싱 없음, 개별 GameObject/머티리얼) 500~16000
//   G2 2D 스프라이트(아틀라스 1장, 알파 블렌드) 1000~32000
//   PT 파티클 한 개, 알파 가산, max particles 2000~128000
//   PP 전체 화면 후처리(노이즈+블러) k=1,2,4,8,16패스
//   TX 매 프레임 Texture2D 업로드 1024², 2048²
//   RB AsyncGPUReadback 512² 30회 순차 지연
// 모든 단계는 벤치 카메라를 RenderTexture에 그리고 OnGUI에서 화면에 표시한다. 그래서 같은 RT를
// 축소·읽어서 "실제로 픽셀이 나왔는지"(pixMs), 검은/멈춘 화면(health), 썸네일을 얻는다.
// 주소에 nobench 가 있으면 실행하지 않는다. 런타임 설정은 window.__pocCfg(PocBench_GetCfg).
[DefaultExecutionOrder(-32000)]
public class PocBench : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern double PocBench_Now();
    [DllImport("__Internal")] static extern void PocBench_Publish(string json);
    [DllImport("__Internal")] static extern string PocBench_GetCfg();
    [DllImport("__Internal")] static extern double PocBench_WasmHeapMB();
    [DllImport("__Internal")] static extern void PocBench_Thumb(string id, string b64);
#else
    static double PocBench_Now() => Time.realtimeSinceStartup * 1000.0;
    static void PocBench_Publish(string json) { }
    static string PocBench_GetCfg() => "null";
    static double PocBench_WasmHeapMB() => 0;
    static void PocBench_Thumb(string id, string b64) { }
#endif

    [Serializable]
    public class Stage
    {
        public string id, group, note;
        public int param, frames;
        public float fps, showMs;
        // 렌더 결과에 새 오브젝트 픽셀이 실제로 나타날 때까지(스테이지 활성화 시점부터 읽기 완료까지).
        // -1: 5초 안에 확인 못 함, -2: 읽기 미지원/대상 아님.
        public float pixMs = -2f, pixReqMs = -2f;
        public int pixFrame = -1;
        public float dtAvg, dtP50, dtP95, dtMax;
        public float cpuAvg, cpuP50, cpuP95, cpuMax;
        public float memAlloc, memReserved, memGfx, wasmHeap;
        public int samples, blackFrames, bgFrames, frozenFrames;
        public float rbP50, rbP95, rbMax;
        public int rbCount, rbErr;
        public float[] dts, cpus;
    }

    [Serializable]
    public class Cfg
    {
        public string[] stages;
        public float soakSec, warmupSec, measureSec;
    }

    [Serializable]
    public class Env
    {
        public string unityVersion, graphicsDeviceType, graphicsDeviceName, graphicsDeviceVendor, graphicsDeviceVersion, quality;
        public string readbackMode, readbackProbe;
        public int graphicsMemorySize, maxTextureSize, systemMemorySize, processorCount, screenW, screenH, rtW, rtH;
        public bool supportsComputeShaders, supportsInstancing, supportsAsyncGPUReadback, uvStartsAtTop, readbackFlipY;
    }

    [Serializable]
    public class Cap { public int V, VN, G2, PT; }

    [Serializable]
    public class SoakSample
    {
        public float t, fps, dtAvg, dtP95, dtMax, cpuAvg, memAlloc, memReserved, memGfx, wasmHeap;
        public int n, frames, samples, blackFrames, bgFrames, frozenFrames;
    }

    [Serializable]
    public class Report
    {
        public string runId, gfx, gfxVersion, gfxName, unityVersion, current, cfgRaw;
        public int screenW, screenH, targetFrameRate;
        public double firstUpdateMs, firstFrameEndMs, benchStartMs;
        public bool done;
        public Cfg cfg = new Cfg();
        public Env env = new Env();
        public Cap cap = new Cap();
        public List<Stage> stages = new List<Stage>();
        public List<SoakSample> soak = new List<SoakSample>();
    }

    // 단계 그룹별 기본 워밍업/측정(ms). cfg의 warmupSec/measureSec이 0보다 크면 모든 단계에 덮어쓴다.
    // A/B는 1s/3s로 줄였다(원래 2s/5s), 램프 계열은 1s/2s.
    static float DefWarm(string g) => g == "S" || g == "C" ? 2000f : g == "A" || g == "B" ? 1000f : 1000f;
    static float DefMeas(string g) => g == "S" || g == "C" ? 5000f : g == "A" || g == "B" ? 3000f : 2000f;

    static readonly int[] DrawCalls = { 250, 500, 1000, 2000, 4000 };
    static readonly int[] Overdraw = { 4, 8, 16, 32, 64, 128, 256 };
    static readonly int[] VSteps = { 500, 1000, 2000, 4000, 8000, 16000, 32000, 64000 };
    static readonly int[] VNSteps = { 500, 1000, 2000, 4000, 8000, 16000 };
    static readonly int[] G2Steps = { 1000, 2000, 4000, 8000, 16000, 32000 };
    static readonly int[] PTSteps = { 2000, 4000, 8000, 16000, 32000, 64000, 128000 };
    static readonly int[] PPSteps = { 1, 2, 4, 8, 16 };
    static readonly int[] TXSteps = { 1024, 2048 };
    const float StopDt = 22f, CapDt = 17.5f;
    const int MaxRaw = 400;

    readonly Report report = new Report();
    Cfg cfg = new Cfg();
    long frameStartTicks;
    float lastDtMs;
    bool firstUpdateSeen;
    string status = "starting";
    GUIStyle style, bigStyle, bigShadow;

    Camera cam, screenCam;
    Light benchLight;
    GameObject root;
    Transform marker;
    Material markerMat;
    readonly List<UnityEngine.Object> temp = new List<UnityEngine.Object>();
    Action<float> tick;
    Action frameHook;
    bool rendering;

    RenderTexture rtMain, rtP1, rtP2, present, px32, h8, thumbRT, rb512;
    int thumbW = 108, thumbH = 234;
    Material postMat;
    int ppPasses;
    bool asyncOk, readbackUsable, flipY;
    int inflight, pendingThumbs;
    readonly Dictionary<int, Texture2D> syncTex = new Dictionary<int, Texture2D>();

    // 화면 오버레이용
    string curId = "", lastStageId = "";
    int curParam;
    float liveFps, liveCpu, liveAcc, liveCpuAcc;
    int liveFrames;
    Stage lastStage;
    float orbitT, vR;
    Vector3 rootPos = new Vector3(0, 1000, 0);

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
        var e = report.env;
        e.unityVersion = Application.unityVersion;
        e.graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString();
        e.graphicsDeviceName = SystemInfo.graphicsDeviceName;
        e.graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor;
        e.graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion;
        e.graphicsMemorySize = SystemInfo.graphicsMemorySize;
        e.maxTextureSize = SystemInfo.maxTextureSize;
        e.supportsComputeShaders = SystemInfo.supportsComputeShaders;
        e.supportsInstancing = SystemInfo.supportsInstancing;
        e.supportsAsyncGPUReadback = SystemInfo.supportsAsyncGPUReadback;
        e.uvStartsAtTop = SystemInfo.graphicsUVStartsAtTop;
        e.systemMemorySize = SystemInfo.systemMemorySize;
        e.processorCount = SystemInfo.processorCount;
        e.screenW = Screen.width;
        e.screenH = Screen.height;
        try { e.quality = QualitySettings.names[QualitySettings.GetQualityLevel()]; } catch (Exception) { e.quality = "?"; }
        asyncOk = SystemInfo.supportsAsyncGPUReadback;
        readbackUsable = true;
        e.readbackMode = asyncOk ? "async" : "sync";
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

    // 카메라는 직접 렌더한다(cam.enabled=false). 후처리 체인과 RT 표시를 결정적으로 다루기 위해서다.
    void LateUpdate()
    {
        if (!rendering || cam == null || rtMain == null) return;
        AnimateMarker();
        if (frameHook != null) frameHook();
        cam.Render();
        present = rtMain;
        if (ppPasses > 0 && postMat != null)
        {
            var src = rtMain;
            var dst = rtP1;
            for (int i = 0; i < ppPasses; i++)
            {
                Graphics.Blit(src, dst, postMat);
                src = dst;
                dst = dst == rtP1 ? rtP2 : rtP1;
            }
            present = src;
        }
    }

    float CpuMs() => (System.Diagnostics.Stopwatch.GetTimestamp() - frameStartTicks) * 1000f / System.Diagnostics.Stopwatch.Frequency;
    static float Elapsed(long t0) => (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000f / System.Diagnostics.Stopwatch.Frequency;

    bool Wants(string g)
    {
        if (cfg.stages == null || cfg.stages.Length == 0) return true;
        return Array.IndexOf(cfg.stages, g) >= 0;
    }

    IEnumerator Run()
    {
        yield return new WaitForEndOfFrame();
        report.firstFrameEndMs = PocBench_Now();

        // cfg 폴링: 최소 1초, 있으면 즉시 반영, 없으면 최대 3초 기다린다.
        float tw = Time.realtimeSinceStartup;
        bool got = false;
        while (true)
        {
            if (!got)
            {
                string s = null;
                try { s = PocBench_GetCfg(); } catch (Exception) { }
                if (!string.IsNullOrEmpty(s) && s != "null")
                {
                    try { cfg = JsonUtility.FromJson<Cfg>(s) ?? new Cfg(); report.cfgRaw = s; got = true; }
                    catch (Exception) { cfg = new Cfg(); }
                }
            }
            float el = Time.realtimeSinceStartup - tw;
            if ((got && el >= 1f) || el >= 3f) break;
            yield return null;
        }
        report.cfg = cfg;
        report.benchStartMs = PocBench_Now();

        foreach (var other in Camera.allCameras) other.enabled = false;
        // 화면용 더미 카메라(아무것도 안 그리고 검정으로 지운다). 실제 출력은 OnGUI가 RT를 그린다.
        var scGo = new GameObject("PocBenchScreenCam");
        scGo.transform.SetParent(transform, false);
        screenCam = scGo.AddComponent<Camera>();
        screenCam.clearFlags = CameraClearFlags.SolidColor;
        screenCam.backgroundColor = Color.black;
        screenCam.cullingMask = 0;
        screenCam.depth = 90;

        var camGo = new GameObject("PocBenchCamera");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.depth = 100;
        var lightGo = new GameObject("PocBenchLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.rotation = Quaternion.Euler(40, 30, 0);
        benchLight = lightGo.AddComponent<Light>();
        benchLight.type = LightType.Directional;
        benchLight.shadows = LightShadows.None;

        int sw = Mathf.Max(16, Screen.width), sh = Mathf.Max(16, Screen.height);
        rtMain = NewRT(sw, sh, 24);
        cam.targetTexture = rtMain;
        thumbH = Mathf.Max(16, Mathf.RoundToInt(thumbW * (float)sh / sw));
        px32 = NewRT(32, 32, 0);
        h8 = NewRT(8, 8, 0);
        thumbRT = NewRT(thumbW, thumbH, 0);
        rb512 = NewRT(512, 512, 0);
        report.env.rtW = sw; report.env.rtH = sh;
        var postShader = Resources.Load<Shader>("PocBench/PocPost");
        if (postShader != null) postMat = new Material(postShader);

        var mk = new GameObject("PocBenchMarker");
        mk.transform.SetParent(cam.transform, false);
        mk.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Quad);
        markerMat = new Material(Resources.Load<Shader>("PocBench/PocUnlit"));
        mk.AddComponent<MeshRenderer>().sharedMaterial = markerMat;
        marker = mk.transform;
        PreStage("A", 0);
        rendering = true;

        yield return ProbeOrientation();
        Publish();

        int coldCount = Resources.LoadAll<Material>("PocBench/Cold").Length;
        if (Wants("S") && coldCount > 0) yield return RunStage("S" + coldCount, "S", coldCount);
        if (Wants("C")) yield return RunStage("C80", "C", PocBenchVariantCount());
        if (Wants("A")) foreach (var n in DrawCalls) yield return RunStage("A" + n, "A", n);
        if (Wants("B")) foreach (var k in Overdraw) yield return RunStage("B" + k, "B", k);
        if (Wants("V")) yield return RunRamp("V", "V", VSteps);
        if (Wants("VN")) yield return RunRamp("VN", "VN", VNSteps);
        if (Wants("G2")) yield return RunRamp("G2", "G2_", G2Steps);
        if (Wants("PT")) yield return RunRamp("PT", "PT_", PTSteps);
        if (Wants("PP")) foreach (var k in PPSteps) yield return RunStage("PP" + k, "PP", k);
        if (Wants("TX")) foreach (var n in TXSteps) yield return RunStage("TX" + n, "TX", n);
        if (Wants("RB")) yield return RunStage("RB", "RB", 512);
        if (cfg.soakSec > 0) yield return RunSoak(cfg.soakSec);

        // 마지막 썸네일이 도착할 시간을 준다.
        float tt = Time.realtimeSinceStartup;
        while (pendingThumbs > 0 && Time.realtimeSinceStartup - tt < 3f) yield return null;

        report.current = "";
        report.done = true;
        status = "done";
        Publish();
        Debug.Log("[AIT-POC] BENCH DONE " + JsonUtility.ToJson(report));
    }

    static RenderTexture NewRT(int w, int h, int depth)
    {
        var rt = new RenderTexture(w, h, depth, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    static int PocBenchVariantCount()
    {
        var all = Resources.LoadAll<Material>("PocBench/Variants");
        return all.Length;
    }

    // ---------------------------------------------------------------- 읽기(readback)

    // src를 dest 크기로 줄여(2배씩 반으로 줄이는 체인으로 박스 필터에 가깝게) 픽셀을 받아 온다.
    // 콜백에는 위에서 아래 순서의 RGBA32 바이트가 들어온다(raw=true면 메모리 순서 그대로). 실패 시 null.
    void Grab(RenderTexture src, RenderTexture dest, Action<byte[]> cb, bool raw = false)
    {
        if (src == null || dest == null || !readbackUsable) { cb(null); return; }
        try
        {
            Downscale(src, dest);
            int w = dest.width, h = dest.height;
            bool flip = !raw && (asyncOk ? flipY : true);
            if (asyncOk)
            {
                inflight++;
                AsyncGPUReadback.Request(dest, 0, TextureFormat.RGBA32, req =>
                {
                    inflight--;
                    if (req.hasError) { cb(null); return; }
                    var d = req.GetData<byte>().ToArray();
                    if (flip) FlipRows(d, w, h);
                    cb(d);
                });
            }
            else
            {
                if (!syncTex.TryGetValue(w * 10000 + h, out var t) || t == null)
                {
                    t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    syncTex[w * 10000 + h] = t;
                }
                var prev = RenderTexture.active;
                RenderTexture.active = dest;
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                RenderTexture.active = prev;
                var d = t.GetRawTextureData();
                var copy = new byte[d.Length];
                Buffer.BlockCopy(d, 0, copy, 0, d.Length);
                if (flip) FlipRows(copy, w, h);
                cb(copy);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PocBench] readback failed: " + ex.Message);
            report.env.readbackMode = "error:" + ex.GetType().Name;
            readbackUsable = false;
            cb(null);
        }
    }

    static void Downscale(RenderTexture src, RenderTexture dest)
    {
        var cur = src;
        int w = dest.width, h = dest.height;
        while (cur.width >= w * 2 || cur.height >= h * 2)
        {
            int nw = Mathf.Max(w, cur.width / 2), nh = Mathf.Max(h, cur.height / 2);
            var tmp = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            tmp.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, tmp);
            if (cur != src) RenderTexture.ReleaseTemporary(cur);
            cur = tmp;
        }
        Graphics.Blit(cur, dest);
        if (cur != src) RenderTexture.ReleaseTemporary(cur);
    }

    static void FlipRows(byte[] d, int w, int h)
    {
        int row = w * 4;
        var tmp = new byte[row];
        for (int y = 0; y < h / 2; y++)
        {
            int a = y * row, b = (h - 1 - y) * row;
            Buffer.BlockCopy(d, a, tmp, 0, row);
            Buffer.BlockCopy(d, b, d, a, row);
            Buffer.BlockCopy(tmp, 0, d, b, row);
        }
    }

    // 마커(오른쪽 위 코너)가 메모리의 앞쪽 행인지 뒤쪽 행인지로 readback 방향을 한 번 판별한다.
    IEnumerator ProbeOrientation()
    {
        report.env.readbackProbe = "skipped";
        if (!asyncOk) yield break;
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        byte[] got = null; bool done = false;
        Grab(present, px32, d => { got = d; done = true; }, true);
        float t0 = Time.realtimeSinceStartup;
        while (!done && Time.realtimeSinceStartup - t0 < 2f) yield return null;
        if (got == null) { report.env.readbackProbe = "failed"; yield break; }
        long top = 0, bot = 0;
        for (int y = 0; y < 4; y++) for (int x = 24; x < 32; x++)
        {
            int a = (y * 32 + x) * 4, b = ((31 - y) * 32 + x) * 4;
            top += got[a] + got[a + 1] + got[a + 2];
            bot += got[b] + got[b + 1] + got[b + 2];
        }
        if (top > bot + 200) { flipY = false; report.env.readbackProbe = "memory-top-first"; }
        else if (bot > top + 200) { flipY = true; report.env.readbackProbe = "memory-bottom-first"; }
        else report.env.readbackProbe = "inconclusive";
        report.env.readbackFlipY = flipY;
    }

    // ---------------------------------------------------------------- 단계 실행

    class SS
    {
        public Stage st;
        public long t0;
        public byte[] base32, base8;
        public bool pixCheck, pixDone, health;
        public int frame;
        public bool hasPrev;
        public uint prev;
    }

    static bool PixGroup(string g) => g != "PP" && g != "TX" && g != "RB";

    IEnumerator RunRamp(string group, string prefix, int[] steps)
    {
        foreach (var n in steps)
        {
            yield return RunStage(prefix + n, group, n);
            var st = lastStage;
            if (st == null) yield break;
            if (st.dtAvg <= CapDt)
            {
                if (group == "V") report.cap.V = n;
                else if (group == "VN") report.cap.VN = n;
                else if (group == "G2") report.cap.G2 = n;
                else if (group == "PT") report.cap.PT = n;
            }
            if (st.dtAvg > StopDt) { st.note = "ramp stopped (dtAvg>" + StopDt + ")"; Publish(); break; }
        }
    }

    // 빈 장면을 그린 결과를 기준 픽셀로 저장한다(32x32, 8x8).
    IEnumerator Baseline(SS ss)
    {
        yield return new WaitForEndOfFrame();
        bool d32 = false, d8 = false;
        Grab(present, px32, d => { ss.base32 = d; d32 = true; });
        Grab(present, h8, d => { ss.base8 = d; d8 = true; });
        float t0 = Time.realtimeSinceStartup;
        while ((!d32 || !d8) && Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
    }

    IEnumerator RunStage(string id, string group, int param)
    {
        report.current = id;
        curId = id; curParam = param;
        status = $"{id} setup";
        Publish();
        yield return null;

        float warmMs = cfg.warmupSec > 0 ? cfg.warmupSec * 1000f : DefWarm(group);
        float measMs = cfg.measureSec > 0 ? cfg.measureSec * 1000f : DefMeas(group);

        PreStage(group, param);
        var st = new Stage { id = id, group = group, param = param };
        var ss = new SS { st = st, health = true, pixCheck = PixGroup(group) && readbackUsable };
        if (readbackUsable) yield return Baseline(ss);
        if (ss.base32 == null) ss.pixCheck = false;
        if (!ss.pixCheck) st.pixMs = -2f;
        else { st.pixMs = -1f; st.pixReqMs = -1f; }

        ss.t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Setup(group, param);
        yield return new WaitForEndOfFrame();
        st.showMs = Elapsed(ss.t0);
        ServiceFrame(ss, false);

        float acc = 0;
        status = $"{id} warmup";
        while (acc < warmMs) { yield return new WaitForEndOfFrame(); acc += lastDtMs; ServiceFrame(ss, false); }

        status = $"{id} measure";
        var dts = new List<float>(2048);
        var cpus = new List<float>(2048);
        acc = 0;
        if (group == "RB")
        {
            var lat = new List<float>();
            for (int i = 0; i < 30; i++)
            {
                bool done = false; bool err = false; long rt0 = System.Diagnostics.Stopwatch.GetTimestamp(); float ms = 0;
                RequestRb(() => { ms = Elapsed(rt0); done = true; }, () => { err = true; done = true; ms = Elapsed(rt0); });
                while (!done && Elapsed(rt0) < 5000f)
                {
                    yield return new WaitForEndOfFrame();
                    dts.Add(lastDtMs); cpus.Add(CpuMs()); acc += lastDtMs; ServiceFrame(ss, true);
                }
                if (!done) { st.rbErr++; break; }
                if (err) st.rbErr++; else lat.Add(ms);
                yield return new WaitForEndOfFrame();
                dts.Add(lastDtMs); cpus.Add(CpuMs()); acc += lastDtMs; ServiceFrame(ss, true);
            }
            st.rbCount = lat.Count;
            if (lat.Count > 0) Fill(lat, out _, out st.rbP50, out st.rbP95, out st.rbMax);
            else { st.rbP50 = st.rbP95 = st.rbMax = -1f; st.note = "readback failed"; }
        }
        else
        {
            while (acc < measMs)
            {
                yield return new WaitForEndOfFrame();
                dts.Add(lastDtMs); cpus.Add(CpuMs()); acc += lastDtMs;
                ServiceFrame(ss, true);
            }
        }
        if (dts.Count == 0) { dts.Add(lastDtMs); cpus.Add(CpuMs()); acc = Mathf.Max(acc, 1f); }
        st.frames = dts.Count;
        st.fps = st.frames * 1000f / Mathf.Max(acc, 1f);
        st.dts = Round1(dts); st.cpus = Round1(cpus);
        Fill(dts, out st.dtAvg, out st.dtP50, out st.dtP95, out st.dtMax);
        Fill(cpus, out st.cpuAvg, out st.cpuP50, out st.cpuP95, out st.cpuMax);
        FillMem(st);
        RequestThumb(id);
        report.stages.Add(st);
        lastStage = st;
        lastStageId = id;

        Teardown();
        Publish();
        yield return null;
        yield return null;
    }

    static float[] Round1(List<float> v)
    {
        int n = Mathf.Min(v.Count, MaxRaw);
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = Mathf.Round(v[i] * 10f) / 10f;
        return a;
    }

    static void FillMem(Stage st)
    {
        st.memAlloc = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        st.memReserved = Profiler.GetTotalReservedMemoryLong() / 1048576f;
        st.memGfx = Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f;
        try { st.wasmHeap = (float)PocBench_WasmHeapMB(); } catch (Exception) { st.wasmHeap = -1f; }
    }

    // RB: 512x512 RT 읽기 한 번. 완료/오류 콜백.
    void RequestRb(Action ok, Action fail)
    {
        if (!readbackUsable || present == null) { fail(); return; }
        try
        {
            Graphics.Blit(present, rb512);
            if (asyncOk)
            {
                inflight++;
                AsyncGPUReadback.Request(rb512, 0, TextureFormat.RGBA32, req =>
                {
                    inflight--;
                    if (req.hasError) fail(); else ok();
                });
            }
            else
            {
                if (!syncTex.TryGetValue(512 * 10000 + 512, out var t) || t == null)
                {
                    t = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    syncTex[512 * 10000 + 512] = t;
                }
                var prev = RenderTexture.active;
                RenderTexture.active = rb512;
                t.ReadPixels(new Rect(0, 0, 512, 512), 0, 0, false);
                RenderTexture.active = prev;
                ok();
            }
        }
        catch (Exception) { fail(); }
    }

    void RequestThumb(string id)
    {
        if (!readbackUsable) return;
        pendingThumbs++;
        Grab(present, thumbRT, d =>
        {
            pendingThumbs--;
            if (d == null) return;
            try
            {
                var jpg = ImageConversion.EncodeArrayToJPG(d, GraphicsFormat.R8G8B8A8_UNorm, (uint)thumbW, (uint)thumbH, 0, 60);
                PocBench_Thumb(id, Convert.ToBase64String(jpg));
            }
            catch (Exception ex) { Debug.LogWarning("[PocBench] thumb failed: " + ex.Message); }
        });
    }

    // 프레임 끝마다 호출: 라이브 fps/cpu, pixMs 확인용 읽기, 10프레임마다 8x8 건강 점검.
    void ServiceFrame(SS ss, bool measuring)
    {
        liveFrames++; liveAcc += lastDtMs; liveCpuAcc += CpuMs();
        if (liveAcc >= 500f) { liveFps = liveFrames * 1000f / liveAcc; liveCpu = liveCpuAcc / liveFrames; liveFrames = 0; liveAcc = 0; liveCpuAcc = 0; }

        var st = ss.st;
        if (ss.pixCheck && !ss.pixDone)
        {
            if (Elapsed(ss.t0) > 5000f) ss.pixDone = true;
            else if (inflight < 4)
            {
                int reqFrame = ss.frame;
                float reqMs = Elapsed(ss.t0);
                Grab(present, px32, d =>
                {
                    if (ss.pixDone || d == null) return;
                    if (CountDiff(d, ss.base32) >= 2)
                    {
                        ss.pixDone = true;
                        st.pixMs = Elapsed(ss.t0);
                        st.pixReqMs = reqMs;
                        st.pixFrame = reqFrame;
                    }
                });
            }
        }
        if (measuring && ss.health && ss.frame % 10 == 0 && inflight < 6)
        {
            Grab(present, h8, d =>
            {
                if (d == null) return;
                st.samples++;
                bool black = true, bg = ss.base8 != null;
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        if (y == 0 && x >= 6) continue; // 마커 칸
                        int i = (y * 8 + x) * 4;
                        if (Mathf.Max(d[i], Mathf.Max(d[i + 1], d[i + 2])) > 6) black = false;
                        if (bg && (Mathf.Abs(d[i] - ss.base8[i]) > 8 || Mathf.Abs(d[i + 1] - ss.base8[i + 1]) > 8 || Mathf.Abs(d[i + 2] - ss.base8[i + 2]) > 8)) bg = false;
                    }
                if (black) st.blackFrames++;
                else if (bg) st.bgFrames++;
                uint h = 2166136261;
                for (int i = 0; i < d.Length; i++) { h ^= d[i]; h *= 16777619; }
                if (ss.hasPrev && h == ss.prev) st.frozenFrames++;
                ss.prev = h; ss.hasPrev = true;
            });
        }
        ss.frame++;
    }

    // 32x32 읽기에서 기준과 다른 픽셀 수(오른쪽 위 마커 영역 제외).
    static int CountDiff(byte[] d, byte[] b)
    {
        int n = 0;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                if (y < 5 && x >= 24) continue;
                int i = (y * 32 + x) * 4;
                if (Mathf.Abs(d[i] - b[i]) > 14 || Mathf.Abs(d[i + 1] - b[i + 1]) > 14 || Mathf.Abs(d[i + 2] - b[i + 2]) > 14) n++;
            }
        return n;
    }

    static void Fill(List<float> v, out float avg, out float p50, out float p95, out float max)
    {
        var s = new List<float>(v);
        s.Sort();
        int n = s.Count;
        float sum = 0; foreach (var x in s) sum += x;
        avg = sum / n;
        p50 = s[(n - 1) / 2];
        p95 = s[Mathf.Clamp(Mathf.CeilToInt(0.95f * n) - 1, 0, n - 1)];
        max = s[n - 1];
    }

    // ---------------------------------------------------------------- 소크

    IEnumerator RunSoak(float sec)
    {
        int n = report.cap.V > 0 ? report.cap.V : 4000;
        string id = "SOAK" + n;
        report.current = id; curId = id; curParam = n;
        status = id + " soak";
        yield return null;
        PreStage("V", n);
        var st = new Stage { id = id, group = "V", param = n };
        var ss = new SS { st = st, health = true, pixCheck = false };
        if (readbackUsable) yield return Baseline(ss);
        ss.t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Setup("V", n);
        yield return new WaitForEndOfFrame();
        float acc = 0;
        while (acc < 2000f) { yield return new WaitForEndOfFrame(); acc += lastDtMs; ServiceFrame(ss, false); }

        long soakT0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var dts = new List<float>(1024);
        var cpus = new List<float>(1024);
        float win = 0; int sampleNo = 0;
        while (Elapsed(soakT0) < sec * 1000f)
        {
            yield return new WaitForEndOfFrame();
            dts.Add(lastDtMs); cpus.Add(CpuMs()); win += lastDtMs;
            ServiceFrame(ss, true);
            if (win >= 10000f)
            {
                sampleNo++;
                var s = new SoakSample { t = Elapsed(soakT0) / 1000f, n = n, frames = dts.Count, fps = dts.Count * 1000f / win };
                Fill(dts, out s.dtAvg, out _, out s.dtP95, out s.dtMax);
                Fill(cpus, out s.cpuAvg, out _, out _, out _);
                s.samples = st.samples; s.blackFrames = st.blackFrames; s.bgFrames = st.bgFrames; s.frozenFrames = st.frozenFrames;
                var m = new Stage(); FillMem(m);
                s.memAlloc = m.memAlloc; s.memReserved = m.memReserved; s.memGfx = m.memGfx; s.wasmHeap = m.wasmHeap;
                report.soak.Add(s);
                RequestThumb("soak" + (sampleNo * 10));
                st.samples = st.blackFrames = st.bgFrames = st.frozenFrames = 0;
                dts.Clear(); cpus.Clear(); win = 0;
                Publish();
            }
        }
        Teardown();
        Publish();
        yield return null;
        yield return null;
    }

    // ---------------------------------------------------------------- 장면

    void PreStage(string group, int param)
    {
        bool persp = group == "V" || group == "VN";
        cam.transform.SetParent(transform, false);
        cam.nearClipPlane = persp ? 0.3f : 0.1f;
        if (persp)
        {
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            vR = 1.3f * Mathf.Sqrt(param / Mathf.PI);
            cam.farClipPlane = vR * 3f + 200f;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
            cam.transform.position = rootPos + new Vector3(vR * 0.75f, vR * 0.35f, 0);
            cam.transform.LookAt(rootPos);
        }
        else
        {
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.farClipPlane = 100f;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cam.transform.position = new Vector3(0, 1000, -10);
            cam.transform.rotation = Quaternion.identity;
        }
        ppPasses = 0;
        PlaceMarker();
    }

    // 모든 단계에 있는 작은 회전 마커(오른쪽 위). 화면이 멈췄는지 알아내는 용도다.
    void PlaceMarker()
    {
        const float d = 3f;
        var a = cam.ViewportToWorldPoint(new Vector3(0.86f, 0.90f, d));
        var b = cam.ViewportToWorldPoint(new Vector3(0.94f, 0.98f, d));
        var c = cam.transform.InverseTransformPoint((a + b) * 0.5f);
        var la = cam.transform.InverseTransformPoint(a);
        var lb = cam.transform.InverseTransformPoint(b);
        float sz = Mathf.Min(Mathf.Abs(la.x - lb.x), Mathf.Abs(la.y - lb.y));
        marker.localPosition = c;
        marker.localScale = new Vector3(sz, sz, 1f);
    }

    void AnimateMarker()
    {
        float t = Time.unscaledTime;
        marker.localRotation = Quaternion.Euler(0, 0, t * 180f);
        markerMat.SetColor("_Color", Color.HSVToRGB(Mathf.Repeat(t * 0.37f, 1f), 1f, 1f));
    }

    void Setup(string group, int param)
    {
        root = new GameObject("PocBenchStage");
        root.transform.position = rootPos;
        float aspect = (float)rtMain.width / rtMain.height;
        float viewH = cam.orthographicSize * 2f, viewW = viewH * aspect;

        switch (group)
        {
            case "A": SetupA(param, viewW, viewH, aspect); break;
            case "PP": SetupA(800, viewW, viewH, aspect); ppPasses = param; EnsurePostRTs(); break;
            case "RB": SetupA(250, viewW, viewH, aspect); break;
            case "B": SetupB(param, viewW, viewH); break;
            case "V": SetupV(param, true); break;
            case "VN": SetupV(param, false); break;
            case "G2": SetupG2(param, viewW, viewH); break;
            case "PT": SetupPT(param, viewW, viewH); break;
            case "TX": SetupTX(param, viewW, viewH); break;
            default: SetupSC(group, viewW, viewH, aspect); break;
        }
    }

    void EnsurePostRTs()
    {
        if (rtP1 == null) rtP1 = NewRT(rtMain.width, rtMain.height, 0);
        if (rtP2 == null) rtP2 = NewRT(rtMain.width, rtMain.height, 0);
    }

    void SetupA(int n, float viewW, float viewH, float aspect)
    {
        var shader = Resources.Load<Shader>("PocBench/PocUnlit");
        var mesh = PrimitiveMesh(PrimitiveType.Cube);
        int cols = Mathf.CeilToInt(Mathf.Sqrt(n * aspect));
        int rows = Mathf.CeilToInt(n / (float)cols);
        float cell = Mathf.Min(viewW / cols, viewH / rows);
        var spinners = new Transform[n];
        var axes = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("c");
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

    void SetupB(int k, float viewW, float viewH)
    {
        var shader = Resources.Load<Shader>("PocBench/PocOverdraw");
        var mesh = PrimitiveMesh(PrimitiveType.Quad);
        for (int i = 0; i < k; i++)
        {
            var go = new GameObject("q");
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
    }

    // S/C: 변형 머티리얼마다 구 하나. 첫 표시 때 셰이더 변형이 처음 쓰인다.
    void SetupSC(string group, float viewW, float viewH, float aspect)
    {
        var mats = Resources.LoadAll<Material>(group == "S" ? "PocBench/Cold" : "PocBench/Variants");
        var mesh = PrimitiveMesh(PrimitiveType.Sphere);
        int cols = Mathf.CeilToInt(Mathf.Sqrt(mats.Length * aspect));
        int rows = Mathf.CeilToInt(mats.Length / (float)cols);
        float cell = Mathf.Min(viewW / cols, viewH / rows);
        var spheres = new Transform[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            var go = new GameObject("v");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3((i % cols + 0.5f - cols / 2f) * cell, (i / cols + 0.5f - rows / 2f) * cell, 0);
            go.transform.localScale = Vector3.one * cell * 0.8f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mats[i];
            spheres[i] = go.transform;
        }
        // 구를 천천히 돌려 화면이 움직이는지 보이게 한다(구 모양이라 셰이더 비용은 거의 안 변한다).
        tick = dt => { for (int i = 0; i < spheres.Length; i++) spheres[i].Rotate(0, 40f * dt, 0, Space.Self); };
    }

    // V/VN: 피보나치 나선 은하 모양으로 오브젝트를 깔고 카메라가 돈다. 회전은 셰이더가 하므로
    // CPU는 드로우 제출 비용만 든다.
    void SetupV(int n, bool instanced)
    {
        var shader = Resources.Load<Shader>("PocBench/PocLitNoise");
        var mesh = PrimitiveMesh(PrimitiveType.Cube);
        var mats = new Matrix4x4[n];
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float r = Mathf.Sqrt(t) * vR;
            float a = i * 2.39996323f;
            float h = Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f);
            float h2 = Mathf.Repeat(Mathf.Sin(i * 78.233f) * 12345.6789f, 1f);
            float y = Mathf.Sin(r * 0.2f) * vR * 0.03f + (h - 0.5f) * 2.6f;
            var pos = rootPos + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            mats[i] = Matrix4x4.TRS(pos, Quaternion.Euler(h * 360f, h2 * 360f, 0), Vector3.one * (0.6f + 0.5f * h2));
        }
        if (instanced)
        {
            var mat = new Material(shader) { enableInstancing = true };
            temp.Add(mat);
            int nb = (n + 1022) / 1023;
            var batches = new Matrix4x4[nb][];
            var counts = new int[nb];
            for (int b = 0; b < nb; b++)
            {
                counts[b] = Mathf.Min(1023, n - b * 1023);
                batches[b] = new Matrix4x4[counts[b]];
                Array.Copy(mats, b * 1023, batches[b], 0, counts[b]);
            }
            var camRef = cam;
            frameHook = () =>
            {
                for (int b = 0; b < batches.Length; b++)
                    Graphics.DrawMeshInstanced(mesh, 0, mat, batches[b], counts[b], null, ShadowCastingMode.Off, false, 0, camRef);
            };
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("o");
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(mats[i].GetColumn(3), mats[i].rotation);
                go.transform.localScale = mats[i].lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var m = new Material(shader) { enableInstancing = false };
                temp.Add(m);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
        }
        orbitT = 0;
        tick = dt =>
        {
            orbitT += dt * 0.25f;
            float rr = vR * 0.75f;
            cam.transform.position = rootPos + new Vector3(Mathf.Cos(orbitT) * rr, vR * 0.35f + Mathf.Sin(orbitT * 0.7f) * vR * 0.08f, Mathf.Sin(orbitT) * rr);
            cam.transform.LookAt(rootPos);
        };
        tick(0f);
    }

    static Sprite[] spriteSet;
    static Texture2D atlasTex;

    static void EnsureAtlas()
    {
        if (spriteSet != null && atlasTex != null) return;
        const int S = 512, C = 128;
        atlasTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        atlasTex.filterMode = FilterMode.Bilinear;
        var px = new Color32[S * S];
        for (int cy = 0; cy < 4; cy++)
            for (int cx = 0; cx < 4; cx++)
            {
                int kind = (cx + cy * 4) % 4;
                for (int y = 0; y < C; y++)
                    for (int x = 0; x < C; x++)
                    {
                        float u = (x + 0.5f) / C * 2f - 1f, v = (y + 0.5f) / C * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v), a = 0f;
                        switch (kind)
                        {
                            case 0: a = Mathf.Clamp01((1f - r) * 6f); break;
                            case 1: a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) * 8f); break;
                            case 2: a = Mathf.Clamp01((1f - (Mathf.Abs(u) + Mathf.Abs(v))) * 6f); break;
                            default: a = Mathf.Clamp01((0.9f + 0.25f * Mathf.Cos(Mathf.Atan2(v, u) * 5f) - r) * 6f); break;
                        }
                        byte b = (byte)(200 + 55 * (1f - r));
                        px[(cy * C + y) * S + cx * C + x] = new Color32(b, b, b, (byte)(a * 255f));
                    }
            }
        atlasTex.SetPixels32(px);
        atlasTex.Apply(false);
        spriteSet = new Sprite[16];
        for (int i = 0; i < 16; i++)
            spriteSet[i] = Sprite.Create(atlasTex, new Rect((i % 4) * C, (i / 4) * C, C, C), new Vector2(0.5f, 0.5f), 256f);
    }

    void SetupG2(int n, float viewW, float viewH)
    {
        EnsureAtlas();
        var tr = new Transform[n];
        var pos = new Vector2[n];
        var vel = new Vector2[n];
        var ang = new float[n];
        var spin = new float[n];
        var rnd = new System.Random(12345);
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("s");
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spriteSet[i % 16];
            var c = Color.HSVToRGB((i * 0.0713f) % 1f, 0.65f, 1f); c.a = 0.6f;
            sr.color = c;
            tr[i] = go.transform;
            pos[i] = new Vector2((float)(rnd.NextDouble() - 0.5) * viewW, (float)(rnd.NextDouble() - 0.5) * viewH);
            vel[i] = new Vector2((float)(rnd.NextDouble() - 0.5) * 3f, (float)(rnd.NextDouble() - 0.5) * 3f);
            ang[i] = (float)rnd.NextDouble() * 6.28f;
            spin[i] = ((float)rnd.NextDouble() - 0.5f) * 4f;
        }
        float hw = viewW * 0.5f, hh = viewH * 0.5f;
        tick = dt =>
        {
            for (int i = 0; i < n; i++)
            {
                var p = pos[i] + vel[i] * dt;
                if (p.x > hw || p.x < -hw) { vel[i].x = -vel[i].x; p.x = Mathf.Clamp(p.x, -hw, hw); }
                if (p.y > hh || p.y < -hh) { vel[i].y = -vel[i].y; p.y = Mathf.Clamp(p.y, -hh, hh); }
                pos[i] = p;
                float a = (ang[i] += spin[i] * dt) * 0.5f;
                tr[i].SetLocalPositionAndRotation(new Vector3(p.x, p.y, 0), new Quaternion(0, 0, Mathf.Sin(a), Mathf.Cos(a)));
            }
        };
        tick(0f);
    }

    void SetupPT(int n, float viewW, float viewH)
    {
        var go = new GameObject("ps");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Resources.Load<Shader>("PocBench/PocParticle"));
        temp.Add(mat);
        rend.sharedMaterial = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = n;
        main.startLifetime = 1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.1f, 0.25f), new Color(0.1f, 0.6f, 1f, 0.25f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = n;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(viewW, viewH, 0.1f);
        ps.Play();
    }

    void SetupTX(int sz, float viewW, float viewH)
    {
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        temp.Add(tex);
        var buf = new byte[sz * sz * 4];
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                int i = (y * sz + x) * 4;
                buf[i] = (byte)(x * 255 / sz); buf[i + 1] = (byte)(y * 255 / sz); buf[i + 2] = 128; buf[i + 3] = 255;
            }
        tex.SetPixelData(buf, 0);
        tex.Apply(false);
        var mat = new Material(Resources.Load<Shader>("PocBench/PocTex"));
        mat.mainTexture = tex;
        temp.Add(mat);
        var go = new GameObject("tx");
        go.transform.SetParent(root.transform, false);
        go.transform.localScale = new Vector3(viewW, viewH, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Quad);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        int band = 0;
        frameHook = () =>
        {
            int rows = 32, y0 = (band * rows) % sz;
            byte c = (byte)(band * 9);
            band++;
            for (int y = y0; y < y0 + rows && y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    int i = (y * sz + x) * 4;
                    buf[i] = c; buf[i + 1] = (byte)(255 - c); buf[i + 2] = (byte)(x * 255 / sz);
                }
            tex.SetPixelData(buf, 0);
            tex.Apply(false);
        };
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
        frameHook = null;
        ppPasses = 0;
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
        if (present != null && Event.current.type == EventType.Repaint)
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), present, ScaleMode.StretchToFill, false);
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = false };
            style.normal.textColor = Color.white;
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            bigStyle.normal.textColor = Color.white;
            bigShadow = new GUIStyle(bigStyle);
            bigShadow.normal.textColor = Color.black;
        }
        style.fontSize = Mathf.Max(14, Screen.height / 70);
        var sb = new StringBuilder();
        sb.Append($"BENCH {report.gfx} {report.unityVersion} {report.screenW}x{report.screenH}\n");
        sb.Append($"load: 1stUpdate {report.firstUpdateMs:F0}ms / 1stFrame {report.firstFrameEndMs:F0}ms\n");
        sb.Append(report.done ? "DONE\n" : $"{status}\n");
        int from = Mathf.Max(0, report.stages.Count - 10);
        for (int i = from; i < report.stages.Count; i++)
        {
            var s = report.stages[i];
            sb.Append($"{s.id}: show {s.showMs:F0} pix {s.pixMs:F0} dt {s.dtP50:F1}/{s.dtP95:F1}/{s.dtMax:F0} cpu {s.cpuP50:F1}\n");
        }
        float w = Screen.width * 0.94f, h = style.fontSize * (Mathf.Min(10, report.stages.Count) + 5) * 1.35f;
        GUI.depth = -2000;
        GUI.Box(new Rect((Screen.width - w) / 2, Screen.height * 0.01f, w, h), sb.ToString(), style);

        if (!report.done && curId.Length > 0)
        {
            int fs = Mathf.Max(24, Screen.height / 20);
            bigStyle.fontSize = fs; bigShadow.fontSize = fs;
            string t = $"{curId}   N={curParam}\n{liveFps:F0} fps   cpu {liveCpu:F1} ms";
            var r = new Rect(0, Screen.height * 0.72f, Screen.width, fs * 3.2f);
            var rs = r; rs.x += fs * 0.06f; rs.y += fs * 0.06f;
            GUI.Label(rs, t, bigShadow);
            GUI.Label(r, t, bigStyle);
        }
    }
}
