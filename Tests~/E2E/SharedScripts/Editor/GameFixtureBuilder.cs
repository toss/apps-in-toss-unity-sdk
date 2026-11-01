using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// perf posture "game" 용 실제 게임형 픽스처 생성기. 빌드 시 텍스처·오디오·JSON·머티리얼·씬 3개(GameTitle/GamePlay/GameResult)를
/// 절차적으로 만들어 정상 임포트 파이프라인에 태운다(SDK 의 빌드 타임 최적화가 실제로 이 에셋을 건드리게 하려는 것).
/// 전부 gitignore 대상(Assets/GameGen, Assets/StreamingAssets)이고 매 빌드 전에 지우고 다시 만든다(결정론).
/// 런타임 쪽은 Runtime/Game/AITGame*.cs.
/// </summary>
public static class GameFixtureBuilder
{
    internal const string Root = "Assets/GameGen";
    internal const string StreamingDir = "Assets/StreamingAssets/ait-game";
    internal const string TitleScenePath = Root + "/Scenes/GameTitle.unity";
    internal const string PlayScenePath = Root + "/Scenes/GamePlay.unity";
    internal const string ResultScenePath = Root + "/Scenes/GameResult.unity";
    internal const string LargeTexturePath = Root + "/Resources/AITGame/bg_large.png";
    internal const int LargeTextureSize = 2048;

    /// <summary>E2EBuildRunner 가 읽는 환경 변수. ';' 로 이은 씬 경로 목록이 빌드 씬 전체를 대체한다(index 0 = 부트 씬).</summary>
    internal const string ScenesEnvVar = "AIT_BUILD_SCENES";

    /// <summary>다른 posture 빌드에 이 픽스처가 끌려 들어가지 않게 생성물을 지운다(Resources 폴더는 씬 참조와 무관하게 전량 포함된다).</summary>
    public static void Cleanup()
    {
        AssetDatabase.DeleteAsset(Root);
        AssetDatabase.DeleteAsset(StreamingDir);
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
        if (File.Exists(Root + ".meta")) File.Delete(Root + ".meta");
    }

    /// <summary>에셋과 씬을 생성하고 빌드 씬 경로(부트 씬 먼저)를 돌려준다.</summary>
    public static string[] Generate()
    {
        Cleanup();
        EnsureFolder(Root);
        EnsureFolder(Root + "/Art");
        EnsureFolder(Root + "/Audio");
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Scenes");
        EnsureFolder(Root + "/Resources");
        EnsureFolder(Root + "/Resources/AITGame");
        EnsureFolder("Assets/StreamingAssets");
        EnsureFolder(StreamingDir);

        // ---- 텍스처 ----
        WritePng(LargeTexturePath, LargeTextureSize, LargeTextureSize, (x, y) => Background(x, y, LargeTextureSize, 1));
        WritePng(Root + "/Art/bg_title.png", 1024, 1024, (x, y) => Background(x, y, 1024, 2));
        string[] brickPaths = new string[3];
        Color32[] brickColors =
        {
            new Color32(235, 110, 70, 255), new Color32(90, 190, 110, 255), new Color32(90, 140, 235, 255)
        };
        for (int i = 0; i < 3; i++)
        {
            brickPaths[i] = Root + "/Art/brick_" + i + ".png";
            Color32 baseColor = brickColors[i];
            int seed = 10 + i;
            WritePng(brickPaths[i], 256, 256, (x, y) => Tile(x, y, baseColor, seed));
        }
        WritePng(Root + "/Art/paddle.png", 256, 256, (x, y) => Tile(x, y, new Color32(230, 230, 240, 255), 20));
        WritePng(Root + "/Art/ball.png", 256, 256, (x, y) => Orb(x, y));

        // ---- 오디오 ----
        File.WriteAllBytes(Root + "/Audio/bgm.wav", BuildBgm());
        File.WriteAllBytes(Root + "/Audio/sfx_hit.wav", BuildBlip(0.12f, 520f, 330f, 0.5f));
        File.WriteAllBytes(Root + "/Audio/sfx_break.wav", BuildBreak());
        File.WriteAllBytes(Root + "/Audio/sfx_click.wav", BuildBlip(0.07f, 880f, 880f, 0.4f));

        // ---- StreamingAssets JSON ----
        File.WriteAllText(StreamingDir + "/level.json",
            "{\"levelName\":\"alpha-grid\",\"brickRows\":5,\"brickCols\":8,\"ballSpeed\":7.5,\"secret\":\"streaming-ok-7391\"}");

        // ---- link.xml ----
        // 씬이 사용자 스크립트를 참조하지 않으므로(AITGameScene 은 런타임 부착) High 스트리핑이 테스트 어셈블리를 통째로 지운다.
        // RuntimeInitializeOnLoadMethod 만으로는 어셈블리가 남지 않아 게임 클래스를 명시적으로 보존한다.
        File.WriteAllText(Root + "/link.xml",
            "<linker>\n  <assembly fullname=\"AppsInTossTestScripts\">\n    <type fullname=\"AITGame*\" preserve=\"all\" />\n  </assembly>\n</linker>\n");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // ---- 머티리얼(셰이더가 빌드에 포함되도록 에셋으로 저장) ----
        Shader unlit = Shader.Find("Unlit/Texture");
        if (unlit == null) throw new Exception("[game] Unlit/Texture 셰이더를 찾지 못했다");
        var matBg = MakeMaterial(unlit, Root + "/Materials/bg.mat", Root + "/Art/bg_title.png");
        var matPaddle = MakeMaterial(unlit, Root + "/Materials/paddle.mat", Root + "/Art/paddle.png");
        var matBall = MakeMaterial(unlit, Root + "/Materials/ball.mat", Root + "/Art/ball.png");
        var matBricks = new Material[3];
        for (int i = 0; i < 3; i++) matBricks[i] = MakeMaterial(unlit, Root + "/Materials/brick_" + i + ".mat", brickPaths[i]);

        var bgm = LoadAsset<AudioClip>(Root + "/Audio/bgm.wav");
        var hit = LoadAsset<AudioClip>(Root + "/Audio/sfx_hit.wav");
        var brk = LoadAsset<AudioClip>(Root + "/Audio/sfx_break.wav");
        var click = LoadAsset<AudioClip>(Root + "/Audio/sfx_click.wav");

#if UNITY_2022_2_OR_NEWER
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        if (font == null) throw new Exception("[game] 내장 폰트를 찾지 못했다");

        string[] modes = { "title", "play", "result" };
        string[] paths = { TitleScenePath, PlayScenePath, ResultScenePath };
        for (int i = 0; i < 3; i++)
        {
            // 사용자 스크립트(AITGameScene)는 씬에 직렬화하지 않는다. Unity 6 배치모드에서 같은 세션에 AddComponent 로
            // 붙인 스크립트는 콜드 컴파일 빌드에서 필드 없이 구워져 WebGL 플레이어가 씬을 "corrupted" 로 거부한다.
            // 참조는 엔진 내장 컴포넌트(AudioSource·MeshRenderer)에 담고, AITGameScene 이 씬 로드 때 붙어 자식에서 읽는다.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(AITGameScene.RootName);
            new GameObject(AITGameScene.ModePrefix + modes[i]).transform.SetParent(go.transform, false);
            AddClipRef(go, "bgm", bgm);
            AddClipRef(go, "sfxHit", hit);
            AddClipRef(go, "sfxBreak", brk);
            AddClipRef(go, "sfxClick", click);
            AddMaterialRef(go, "matBackground", matBg);
            AddMaterialRef(go, "matPaddle", matPaddle);
            AddMaterialRef(go, "matBall", matBall);
            AddMaterialRef(go, "matBricks", matBricks);
            if (!EditorSceneManager.SaveScene(scene, paths[i]))
                throw new Exception("[game] 씬 저장 실패: " + paths[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (var p in paths)
        {
            if (!File.Exists(p)) throw new Exception("[game] 씬 파일이 없다: " + p);
        }
        Debug.Log("[game] fixture generated: scenes=" + string.Join(", ", paths));
        return paths;
    }

    // ---- 에셋 헬퍼 ----

    private static void AddClipRef(GameObject parent, string name, AudioClip clip)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.clip = clip;
    }

    private static void AddMaterialRef(GameObject parent, string name, params Material[] mats)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        var mr = child.AddComponent<MeshRenderer>();
        mr.enabled = false;
        mr.sharedMaterials = mats;
    }

    private static T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) throw new Exception("[game] 에셋 로드 실패: " + path);
        return a;
    }

    private static Material MakeMaterial(Shader shader, string matPath, string texPath)
    {
        var m = new Material(shader);
        m.mainTexture = LoadAsset<Texture2D>(texPath);
        AssetDatabase.CreateAsset(m, matPath);
        return m;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void WritePng(string path, int w, int h, Func<int, int, Color32> fn)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn(x, y);
        tex.SetPixels32(px);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, png);
    }

    private static int Noise(int x, int y, int seed, int amp)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (int)(h % (uint)(amp * 2 + 1)) - amp;
        }
    }

    private static byte Clamp(int v)
    {
        return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
    }

    /// <summary>그라디언트 + 사인 무늬 + 노이즈(PNG 로 쉽게 압축되지 않게).</summary>
    private static Color32 Background(int x, int y, int size, int seed)
    {
        float fx = (float)x / (size - 1);
        float fy = (float)y / (size - 1);
        int r = (int)(30 + 90 * fx) + Noise(x, y, seed, 14);
        int g = (int)(40 + 100 * fy) + Noise(x, y, seed + 1, 14);
        int b = (int)(90 + 60 * Mathf.Sin((fx + fy) * 12f)) + Noise(x, y, seed + 2, 14);
        return new Color32(Clamp(r), Clamp(g), Clamp(b), 255);
    }

    private static Color32 Tile(int x, int y, Color32 c, int seed)
    {
        bool edge = x < 12 || y < 12 || x > 243 || y > 243;
        int shade = edge ? -60 : (int)(30 * (1f - y / 255f));
        int n = Noise(x, y, seed, 10);
        return new Color32(Clamp(c.r + shade + n), Clamp(c.g + shade + n), Clamp(c.b + shade + n), 255);
    }

    private static Color32 Orb(int x, int y)
    {
        float dx = (x - 127.5f) / 127.5f;
        float dy = (y - 127.5f) / 127.5f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d > 1f) return new Color32(255, 255, 255, 255);
        int v = (int)(255 - 120 * d);
        return new Color32(Clamp(v), Clamp(v), Clamp(v - 20), 255);
    }

    // ---- 오디오 합성 ----

    private static byte[] ToWav16(float[] samples, int channels, int sampleRate)
    {
        int dataBytes = samples.Length * 2;
        using (var ms = new MemoryStream(44 + dataBytes))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataBytes);
            bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2);
            bw.Write((short)(channels * 2));
            bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                float s = Mathf.Clamp(samples[i], -0.98f, 0.98f);
                bw.Write((short)Mathf.RoundToInt(s * 32767f));
            }
            bw.Flush();
            return ms.ToArray();
        }
    }

    /// <summary>64초 스테레오 BGM: C - Am - F - G 진행(1마디 2초, 120bpm)에 8분음표 아르페지오 + 낮은 옥타브 베이스. 다장조, 낮은 볼륨.</summary>
    private static byte[] BuildBgm()
    {
        const int rate = 44100;
        const int seconds = 64;
        int frames = rate * seconds;
        var buf = new float[frames * 2];
        // 코드: 루트 반음 오프셋(C4 기준) + 장/단 3화음
        int[][] chords =
        {
            new[] { 0, 4, 7 },     // C
            new[] { -3, 0, 4 },    // Am (A3 C4 E4)
            new[] { -7, -3, 0 },   // F (F3 A3 C4)
            new[] { -5, -1, 2 },   // G (G3 B3 D4)
        };
        int[] pattern = { 0, 1, 2, 1, 0, 1, 2, 1 };   // 한 마디 8분음표 8개
        float eighth = 0.25f;
        int notes = (int)(seconds / eighth);
        for (int n = 0; n < notes; n++)
        {
            int bar = n / 8;
            var chord = chords[bar % 4];
            int step = n % 8;
            int semis = chord[pattern[step]] + 12;   // 한 옥타브 위 멜로디
            float t0 = n * eighth;
            AddNote(buf, rate, t0, 0.55f, 261.63f * Mathf.Pow(2f, semis / 12f), 0.16f, (n % 2 == 0) ? -0.25f : 0.25f);
            if (step == 0 || step == 4)
            {
                int bass = chord[0] - 12;
                AddNote(buf, rate, t0, 0.9f, 261.63f * Mathf.Pow(2f, bass / 12f), 0.14f, 0f);
            }
        }
        return ToWav16(buf, 2, rate);
    }

    private static void AddNote(float[] stereo, int rate, float start, float len, float freq, float amp, float pan)
    {
        int s0 = (int)(start * rate);
        int count = (int)(len * rate);
        float gl = amp * (1f - pan) * 0.5f + amp * 0.5f * (pan < 0 ? 1f : 0.6f);
        float gr = amp * (1f + pan) * 0.5f + amp * 0.5f * (pan > 0 ? 1f : 0.6f);
        for (int i = 0; i < count; i++)
        {
            int idx = (s0 + i) * 2;
            if (idx + 1 >= stereo.Length) break;
            float t = (float)i / rate;
            float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 5.5f);
            float ph = 2f * Mathf.PI * freq * t;
            // 사인 + 2배음 + 약한 톱니 성분
            float saw = 2f * ((freq * t) - Mathf.Floor(freq * t + 0.5f));
            float v = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph) + 0.08f * saw) * env;
            stereo[idx] += v * gl;
            stereo[idx + 1] += v * gr;
        }
    }

    private static byte[] BuildBlip(float seconds, float f0, float f1, float amp)
    {
        const int rate = 44100;
        int n = (int)(seconds * rate);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / rate;
            float env = Mathf.Min(1f, i / 200f) * (1f - t);
            buf[i] = Mathf.Sin(phase) * env * amp;
        }
        return ToWav16(buf, 1, rate);
    }

    private static byte[] BuildBreak()
    {
        const int rate = 44100;
        int n = (int)(0.25f * rate);
        var buf = new float[n];
        uint state = 12345u;
        for (int i = 0; i < n; i++)
        {
            unchecked { state = state * 1664525u + 1013904223u; }
            float noise = ((state >> 8) & 0xFFFF) / 32767.5f - 1f;
            float t = (float)i / n;
            float env = Mathf.Exp(-t * 7f);
            buf[i] = (noise * 0.35f + Mathf.Sin(2f * Mathf.PI * 220f * i / rate) * 0.3f) * env * 0.7f;
        }
        return ToWav16(buf, 1, rate);
    }
}
