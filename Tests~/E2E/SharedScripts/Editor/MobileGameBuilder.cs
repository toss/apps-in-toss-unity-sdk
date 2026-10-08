using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// perf posture "mobilegame" 용 픽스처 생성기: 세로 화면 탭 점프 러너(하이퍼캐주얼 미니게임 형태).
/// 빌드 때 스프라이트 PNG·BGM/효과음 WAV·link.xml·씬 1개를 절차적으로 만들어 일반 임포트 파이프라인에 태운다.
/// 임포트 설정은 스프라이트로 바꾸는 것 외에 Unity 기본값 그대로 둔다(사용자가 그냥 넣은 에셋과 같은 조건).
/// 이 파일과 런타임(Runtime/MobileGame/AITRun*.cs)은 SDK 설정 필드를 쓰지 않아 어느 SDK 버전 위에서도 그대로 빌드된다.
/// 생성물은 전부 Assets/RunGen 아래이고 매 빌드 전에 지우고 다시 만든다(결정론).
/// </summary>
public static class MobileGameBuilder
{
    internal const string Root = "Assets/RunGen";
    internal const string ScenePath = Root + "/Scenes/RunGame.unity";

    /// <summary>E2EBuildRunner 가 읽는 환경 변수. ';' 로 이은 씬 경로 목록이 빌드 씬 전체를 대체한다(index 0 = 부트 씬).</summary>
    internal const string ScenesEnvVar = "AIT_BUILD_SCENES";

    public static void Cleanup()
    {
        AssetDatabase.DeleteAsset(Root);
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
        if (File.Exists(Root + ".meta")) File.Delete(Root + ".meta");
    }

    /// <summary>에셋과 씬을 생성하고 빌드 씬 경로를 돌려준다.</summary>
    public static string[] Generate()
    {
        Cleanup();
        EnsureFolder(Root + "/Art");
        EnsureFolder(Root + "/Audio");
        EnsureFolder(Root + "/Scenes");

        // ---- 스프라이트(px, 유닛당 픽셀) ----
        var sprites = new[]
        {
            new SpriteSpec("player0", 128, 128, 128f, (x, y) => Runner(x, y, 0)),
            new SpriteSpec("player1", 128, 128, 128f, (x, y) => Runner(x, y, 1)),
            new SpriteSpec("playerJump", 128, 128, 128f, (x, y) => Runner(x, y, 2)),
            new SpriteSpec("obstacle", 108, 128, 128f, (x, y) => Crate(x, y, 108, 128)),
            new SpriteSpec("obstacleTall", 108, 230, 128f, (x, y) => Crate(x, y, 108, 230)),
            new SpriteSpec("coin", 96, 96, 128f, Coin),
            new SpriteSpec("ground", 256, 256, 128f, Ground),
            new SpriteSpec("bgFar", 2048, 2048, 204.8f, SkyFar),
            new SpriteSpec("bgNear", 2048, 1024, 204.8f, Hills),
            new SpriteSpec("cloud", 256, 128, 128f, CloudPx),
            new SpriteSpec("spark", 32, 32, 128f, SparkPx),
            new SpriteSpec("button", 512, 192, 100f, ButtonPx),
        };
        foreach (var s in sprites) WritePng(ArtPath(s.Name), s.W, s.H, s.Fn);

        // ---- 오디오 ----
        File.WriteAllBytes(Root + "/Audio/bgm.wav", BuildBgm());
        File.WriteAllBytes(Root + "/Audio/sfx_jump.wav", BuildSweep(0.16f, 420f, 900f, 0.45f));
        File.WriteAllBytes(Root + "/Audio/sfx_coin.wav", BuildCoin());
        File.WriteAllBytes(Root + "/Audio/sfx_hit.wav", BuildHit());
        File.WriteAllBytes(Root + "/Audio/sfx_click.wav", BuildSweep(0.06f, 1200f, 1200f, 0.35f));

        // ---- link.xml ----
        // 씬이 사용자 스크립트를 참조하지 않으므로(AITRunScene 은 런타임 부착) High 스트리핑이 테스트 어셈블리를 통째로 지운다.
        File.WriteAllText(Root + "/link.xml",
            "<linker>\n  <assembly fullname=\"AppsInTossTestScripts\">\n    <type fullname=\"AITRun*\" preserve=\"all\" />\n  </assembly>\n</linker>\n");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var s in sprites)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(ArtPath(s.Name));
            if (imp == null) throw new Exception("[mobilegame] TextureImporter 없음: " + ArtPath(s.Name));
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = s.Ppu;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        // 사용자 스크립트는 씬에 직렬화하지 않는다(AITRunScene 문서 참조). 참조는 내장 컴포넌트 자식에 담는다.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject(AITRunScene.RootName);
        AddClipRef(root, "bgm", Root + "/Audio/bgm.wav");
        AddClipRef(root, "sfxJump", Root + "/Audio/sfx_jump.wav");
        AddClipRef(root, "sfxCoin", Root + "/Audio/sfx_coin.wav");
        AddClipRef(root, "sfxHit", Root + "/Audio/sfx_hit.wav");
        AddClipRef(root, "sfxClick", Root + "/Audio/sfx_click.wav");
        foreach (var s in sprites) AddSpriteRef(root, s.Name, ArtPath(s.Name));
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("[mobilegame] 씬 저장 실패: " + ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!File.Exists(ScenePath)) throw new Exception("[mobilegame] 씬 파일이 없다: " + ScenePath);
        Debug.Log("[mobilegame] fixture generated: " + ScenePath);
        return new[] { ScenePath };
    }

    private struct SpriteSpec
    {
        public string Name;
        public int W, H;
        public float Ppu;
        public Func<int, int, Color32> Fn;

        public SpriteSpec(string name, int w, int h, float ppu, Func<int, int, Color32> fn)
        {
            Name = name;
            W = w;
            H = h;
            Ppu = ppu;
            Fn = fn;
        }
    }

    private static string ArtPath(string name)
    {
        return Root + "/Art/" + name + ".png";
    }

    private static void AddClipRef(GameObject parent, string name, string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new Exception("[mobilegame] 오디오 로드 실패: " + path);
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.clip = clip;
    }

    private static void AddSpriteRef(GameObject parent, string name, string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new Exception("[mobilegame] 스프라이트 로드 실패: " + path);
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        var sr = child.AddComponent<SpriteRenderer>();
        sr.enabled = false;
        sr.sprite = sprite;
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

    // ---- 픽셀 함수 ----

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

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

    private static byte B(int v)
    {
        return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
    }

    private static Color32 Shade(Color32 c, int d)
    {
        return new Color32(B(c.r + d), B(c.g + d), B(c.b + d), c.a);
    }

    private static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    /// <summary>둥근 몸통 캐릭터. pose 0/1 = 달리기 두 프레임(다리 위치), 2 = 점프(다리 모음).</summary>
    private static Color32 Runner(int x, int y, int pose)
    {
        var body = new Color32(255, 140, 50, 255);
        var dark = new Color32(120, 50, 20, 255);
        // 다리
        int[] legX = pose == 0 ? new[] { 46, 78 } : pose == 1 ? new[] { 56, 70 } : new[] { 52, 74 };
        int legTop = pose == 2 ? 30 : 34;
        int legBottom = pose == 2 ? 18 : 6;
        foreach (int lx in legX)
            if (x >= lx - 7 && x <= lx + 7 && y >= legBottom && y <= legTop) return dark;
        // 몸통
        if (InEllipse(x, y, 64, 70, 44, 40))
        {
            if (!InEllipse(x, y, 64, 70, 40, 36)) return dark;
            if (InEllipse(x, y, 82, 82, 11, 13)) return InEllipse(x, y, 85, 82, 5, 6) ? new Color32(20, 20, 30, 255) : new Color32(255, 255, 255, 255);
            return Shade(body, (y - 70) / 2 + Noise(x, y, 3, 4));
        }
        return Clear;
    }

    private static Color32 Crate(int x, int y, int w, int h)
    {
        var wood = new Color32(150, 95, 50, 255);
        bool border = x < 8 || y < 8 || x >= w - 8 || y >= h - 8;
        bool brace = Math.Abs((x * h) / w - y) < 7 || Math.Abs(((w - 1 - x) * h) / w - y) < 7;
        int plank = (y / 24) % 2 == 0 ? 0 : -14;
        if (border) return Shade(wood, -55);
        if (brace) return Shade(wood, -30 + Noise(x, y, 7, 6));
        return Shade(wood, plank + Noise(x, y, 5, 10));
    }

    private static Color32 Coin(int x, int y)
    {
        if (!InEllipse(x, y, 47.5f, 47.5f, 46, 46)) return Clear;
        if (!InEllipse(x, y, 47.5f, 47.5f, 38, 38)) return new Color32(220, 150, 20, 255);
        bool mark = x > 40 && x < 56 && y > 22 && y < 74;
        return mark ? new Color32(255, 240, 150, 255) : new Color32(255, 200, 40, 255);
    }

    private static Color32 Ground(int x, int y)
    {
        if (y > 216 + (int)(6 * Mathf.Sin(x * 0.1f)))
            return new Color32(B(80 + Noise(x, y, 1, 12)), B(180 + Noise(x, y, 2, 14)), B(70 + Noise(x, y, 3, 10)), 255);
        bool stone = ((x / 32 + y / 32) % 3 == 0) && Noise(x / 32, y / 32, 9, 1) == 0;
        var dirt = new Color32(130, 90, 55, 255);
        return Shade(dirt, (stone ? -20 : 0) + Noise(x, y, 4, 12) - (216 - y) / 12);
    }

    private static Color32 SkyFar(int x, int y)
    {
        float fy = y / 2047f;
        float fx = x / 2047f;
        // 하늘 그라디언트 + 먼 산 실루엣 + 미세 노이즈(텍스처 압축이 실제로 일하게)
        float ridge = 0.32f + 0.08f * Mathf.Sin(fx * 9f) + 0.04f * Mathf.Sin(fx * 23f + 1.3f);
        if (fy < ridge)
            return new Color32(B(90 + Noise(x, y, 11, 6)), B(120 + (int)(fy * 60) + Noise(x, y, 12, 6)), B(170 + Noise(x, y, 13, 6)), 255);
        int r = (int)(120 + 100 * fy);
        int g = (int)(180 + 50 * fy);
        int b = (int)(235 + 15 * fy);
        return new Color32(B(r + Noise(x, y, 14, 4)), B(g + Noise(x, y, 15, 4)), B(b + Noise(x, y, 16, 3)), 255);
    }

    private static Color32 Hills(int x, int y)
    {
        float fx = x / 2047f;
        float top = 0.55f + 0.2f * Mathf.Sin(fx * 6.283f * 2f) + 0.08f * Mathf.Sin(fx * 6.283f * 5f + 0.7f);
        float fy = y / 1023f;
        if (fy > top) return Clear;
        int d = (int)((top - fy) * 60);
        bool tree = (x % 180) < 40 && fy > top - 0.12f && Noise(x / 180, 0, 21, 1) >= 0;
        if (tree) return new Color32(B(40 + Noise(x, y, 22, 8)), B(110 + Noise(x, y, 23, 10)), B(50), 255);
        return new Color32(B(70 - d / 3 + Noise(x, y, 24, 6)), B(160 - d + Noise(x, y, 25, 8)), B(80 - d / 2 + Noise(x, y, 26, 6)), 255);
    }

    private static Color32 CloudPx(int x, int y)
    {
        bool inside = InEllipse(x, y, 80, 54, 60, 34) || InEllipse(x, y, 150, 64, 70, 46) || InEllipse(x, y, 200, 50, 44, 28);
        if (!inside) return Clear;
        return new Color32(B(245 - (64 - y) / 8), B(248 - (64 - y) / 8), 255, 235);
    }

    private static Color32 SparkPx(int x, int y)
    {
        float dx = Math.Abs(x - 15.5f);
        float dy = Math.Abs(y - 15.5f);
        float star = Math.Min(dx, dy) * 3f + Math.Max(dx, dy) * 0.5f;
        if (star > 16f) return Clear;
        return new Color32(255, 255, 255, B((int)(255 * (1f - star / 16f))));
    }

    private static Color32 ButtonPx(int x, int y)
    {
        const int w = 512, h = 192, r = 48;
        int cx = Mathf.Clamp(x, r, w - 1 - r);
        int cy = Mathf.Clamp(y, r, h - 1 - r);
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        if (d > r) return Clear;
        if (d > r - 10) return new Color32(30, 110, 50, 255);
        return new Color32(B(70 + y / 6), B(190 + y / 8), B(90 + y / 8), 255);
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

    /// <summary>48초 스테레오 BGM: G장조 경쾌한 진행(G - Em - C - D, 1마디 1.6초, 150bpm) 위에 16분음표 아르페지오와 베이스.</summary>
    private static byte[] BuildBgm()
    {
        const int rate = 44100;
        const int seconds = 48;
        var buf = new float[rate * seconds * 2];
        const float g4 = 392f;
        int[][] chords =
        {
            new[] { 0, 4, 7, 12 },    // G
            new[] { -3, 0, 4, 9 },    // Em
            new[] { -7, -3, 0, 5 },   // C
            new[] { -5, -1, 2, 7 },   // D
        };
        int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 3 };
        float eighth = 0.2f;
        int notes = (int)(seconds / eighth);
        for (int n = 0; n < notes; n++)
        {
            int bar = n / 8;
            var chord = chords[bar % 4];
            int stepInBar = n % 8;
            float t0 = n * eighth;
            float freq = g4 * Mathf.Pow(2f, chord[pattern[stepInBar]] / 12f);
            AddNote(buf, rate, t0, 0.3f, freq, 0.13f, (n % 2 == 0) ? -0.3f : 0.3f);
            if (stepInBar == 0 || stepInBar == 4)
                AddNote(buf, rate, t0, 0.7f, g4 * 0.25f * Mathf.Pow(2f, chord[0] / 12f), 0.16f, 0f);
        }
        return ToWav16(buf, 2, rate);
    }

    private static void AddNote(float[] stereo, int rate, float start, float len, float freq, float amp, float pan)
    {
        int s0 = (int)(start * rate);
        int count = (int)(len * rate);
        float gl = amp * (1f - pan * 0.5f);
        float gr = amp * (1f + pan * 0.5f);
        for (int i = 0; i < count; i++)
        {
            int idx = (s0 + i) * 2;
            if (idx + 1 >= stereo.Length) break;
            float t = (float)i / rate;
            float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 7f);
            float ph = 2f * Mathf.PI * freq * t;
            float v = (Mathf.Sin(ph) + 0.25f * Mathf.Sin(2f * ph) + 0.1f * Mathf.Sin(3f * ph)) * env;
            stereo[idx] += v * gl;
            stereo[idx + 1] += v * gr;
        }
    }

    private static byte[] BuildSweep(float seconds, float f0, float f1, float amp)
    {
        const int rate = 44100;
        int n = (int)(seconds * rate);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / rate;
            buf[i] = Mathf.Sin(phase) * Mathf.Min(1f, i / 150f) * (1f - t) * amp;
        }
        return ToWav16(buf, 1, rate);
    }

    private static byte[] BuildCoin()
    {
        const int rate = 44100;
        int n = (int)(0.22f * rate);
        var buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = t < 0.06f ? 988f : 1319f;   // B5 → E6
            buf[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 14f) * 0.45f;
        }
        return ToWav16(buf, 1, rate);
    }

    private static byte[] BuildHit()
    {
        const int rate = 44100;
        int n = (int)(0.35f * rate);
        var buf = new float[n];
        uint state = 777u;
        for (int i = 0; i < n; i++)
        {
            unchecked { state = state * 1664525u + 1013904223u; }
            float noise = ((state >> 8) & 0xFFFF) / 32767.5f - 1f;
            float t = (float)i / n;
            buf[i] = (noise * 0.4f + Mathf.Sin(2f * Mathf.PI * 140f * i / rate) * 0.4f) * Mathf.Exp(-t * 6f) * 0.7f;
        }
        return ToWav16(buf, 1, rate);
    }
}
