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

    /// <summary>에셋과 씬을 생성하고 빌드 씬 경로를 돌려준다. heavy=true 면 mobileheavy posture 콘텐츠(<see cref="AITRunHeavy"/>)를 더한다.</summary>
    public static string[] Generate(bool heavy = false)
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

        if (heavy) WriteHeavyAssets();

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        if (heavy) ImportHeavyAssets();
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
        if (heavy) BuildHeavyScene(root);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("[mobilegame] 씬 저장 실패: " + ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!File.Exists(ScenePath)) throw new Exception("[mobilegame] 씬 파일이 없다: " + ScenePath);
        Debug.Log("[mobilegame] fixture generated: " + ScenePath + (heavy ? " (heavy)" : ""));
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

    // ---- mobileheavy 콘텐츠 ----

    private const string HeavyRes = Root + "/Resources/RunHeavy";
    private const string MatDir = Root + "/Mat";
    private const string Props0Path = Root + "/Art/props0.png";
    private const int HeavyStages = 4;   // AITRunHeavy.StageCount 와 같다. 스테이지 0 의 배경은 기본 스프라이트를 쓴다.

    private static string StageDir(int k)
    {
        return HeavyRes + "/s" + k;
    }

    /// <summary>스테이지별 색 변환(1=가을, 2=눈, 3=밤). 같은 그림이라도 스테이지마다 다른 텍스처가 되도록 노이즈 시드도 바꾼다.</summary>
    private static Color32 StageTint(Color32 c, int stage, int x, int y)
    {
        if (c.a == 0) return c;
        int n = Noise(x, y, 100 + stage, 5);
        switch (stage)
        {
            case 1: return new Color32(B(c.r * 5 / 4 + 30 + n), B(c.g * 4 / 5 + n), B(c.b / 2 + n), c.a);
            case 2: return new Color32(B((c.r + 255) / 2 + n), B((c.g + 255) / 2 + n), B((c.b + 255) / 2 + 10 + n), c.a);
            case 3: return new Color32(B(c.r / 3 + n), B(c.g / 3 + 5 + n), B(c.b / 2 + 30 + n), c.a);
            default: return new Color32(B(c.r + n), B(c.g + n), B(c.b + n), c.a);
        }
    }

    /// <summary>건물 외벽: 창문 격자 + 노이즈. 3D 소품 머티리얼(Props)의 알베도로 스테이지마다 교체된다.</summary>
    private static Color32 Facade(int x, int y, int stage)
    {
        bool window = (x % 64) > 14 && (x % 64) < 50 && (y % 80) > 18 && (y % 80) < 62;
        bool lit = Noise(x / 64, y / 80, 300 + stage, 1) >= 0;
        Color32 wall = new Color32(B(150 + Noise(x, y, 31, 14)), B(140 + Noise(x, y, 32, 14)), B(135 + Noise(x, y, 33, 14)), 255);
        if (!window) return StageTint(wall, stage, x, y);
        return lit ? new Color32(255, B(225 + Noise(x, y, 34, 20)), 140, 255) : StageTint(new Color32(60, 80, 110, 255), stage, x, y);
    }

    private static void WriteHeavyAssets()
    {
        EnsureFolder(MatDir);
        for (int k = 0; k < HeavyStages; k++) EnsureFolder(StageDir(k));

        int stage0 = 0;
        WritePng(Props0Path, 1024, 1024, (x, y) => Facade(x, y, stage0));
        for (int k = 1; k < HeavyStages; k++)
        {
            int st = k;
            WritePng(StageDir(k) + "/far.png", 2048, 2048, (x, y) => StageTint(SkyFar(x, y), st, x, y));
            WritePng(StageDir(k) + "/near.png", 2048, 1024, (x, y) => StageTint(Hills(x, y), st, x, y));
            WritePng(StageDir(k) + "/ground.png", 256, 256, (x, y) => StageTint(Ground(x, y), st, x, y));
            WritePng(StageDir(k) + "/props.png", 1024, 1024, (x, y) => Facade(x, y, st));
        }
        for (int k = 0; k < HeavyStages; k++)
            File.WriteAllBytes(StageDir(k) + "/amb.wav", BuildAmbience(k));
    }

    private static void ImportHeavyAssets()
    {
        for (int k = 1; k < HeavyStages; k++)
        {
            SetSpriteImport(StageDir(k) + "/far.png", 204.8f);
            SetSpriteImport(StageDir(k) + "/near.png", 204.8f);
            SetSpriteImport(StageDir(k) + "/ground.png", 128f);
        }
        // props*.png 와 amb.wav 는 임포트 기본값(텍스처: Default·밉맵, 오디오: 기본 로드 타입·압축) 그대로 둔다.
    }

    private static void SetSpriteImport(string path, float ppu)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) throw new Exception("[mobilegame] TextureImporter 없음: " + path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
    }

    private static Material MakeMaterial(string name, Color color, Texture tex)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) throw new Exception("[mobilegame] Standard 셰이더 없음");
        var mat = new Material(shader) { name = name, color = color };
        if (tex != null) mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", 0.25f);
        string path = MatDir + "/" + name + ".mat";
        AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static float Hash01(int i, int seed)
    {
        return (Noise(i, 0, seed, 5000) + 5000) / 10000f;
    }

    /// <summary>AITRunHeavy 가 이름으로 찾는 구성을 루트 아래에 만든다. 사용자 스크립트는 붙이지 않는다(내장 컴포넌트만).</summary>
    private static void BuildHeavyScene(GameObject root)
    {
        new GameObject(AITRunHeavy.MarkerName).transform.SetParent(root.transform, false);
        var g3 = Group(AITRunHeavy.Group3D, root.transform);

        var props0 = AssetDatabase.LoadAssetAtPath<Texture2D>(Props0Path);
        if (props0 == null) throw new Exception("[mobilegame] props0 텍스처 없음");
        var matProps = MakeMaterial("Props", Color.white, props0);
        var matPole = MakeMaterial("Pole", new Color(0.85f, 0.85f, 0.8f), null);
        var matBlade = MakeMaterial("Blade", new Color(0.95f, 0.95f, 0.98f), null);
        var matBall = MakeMaterial("Ball", new Color(1f, 0.45f, 0.35f), null);
        var matWall = MakeMaterial("Wall", new Color(0.35f, 0.4f, 0.5f), null);
        var matDebris = MakeMaterial("Debris", new Color(0.6f, 0.38f, 0.2f), null);

        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(g3, false);
        sunGo.transform.localRotation = Quaternion.Euler(35f, -40f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;

        const float baseY = -2.6f;   // AITRunGame.GroundTop(-3) 보다 조금 위(근경 언덕 뒤로 솟는다)
        var city = Group("City", g3);
        for (int i = 0; i < 160; i++)
        {
            float x = -6f + i * (12f / 160f) + (Hash01(i, 41) - 0.5f) * 0.05f;
            float w = 0.3f + Hash01(i, 42) * 0.4f;
            float h = 0.8f + Hash01(i, 43) * 2.6f;
            float z = 4.2f + Hash01(i, 44) * 0.7f;
            Prim(PrimitiveType.Cube, "b" + i, city, new Vector3(x, baseY + h * 0.5f, z), new Vector3(w, h, w), matProps, false);
        }

        var mills = Group("Windmills", g3);
        for (int i = 0; i < 24; i++)
        {
            var wm = Group("w" + i, mills);
            wm.localPosition = new Vector3(-6f + i * 0.5f + 0.25f, baseY, 4.1f);
            Prim(PrimitiveType.Cylinder, "pole", wm, new Vector3(0f, 1.6f, 0f), new Vector3(0.08f, 1.6f, 0.08f), matPole, false);
            var rotor = Group("rotor", wm);
            rotor.localPosition = new Vector3(0f, 3.2f, -0.06f);
            for (int b = 0; b < 4; b++)
            {
                var pivot = Group("arm" + b, rotor);
                pivot.localRotation = Quaternion.Euler(0f, 0f, b * 90f);
                Prim(PrimitiveType.Cube, "blade", pivot, new Vector3(0f, 0.38f, 0f), new Vector3(0.09f, 0.7f, 0.02f), matBlade, false);
            }
        }

        // 물리 통: 벽·바닥(정적 콜라이더), 회전 패들(키네매틱), 공 60개. 앞벽은 보이지 않는 콜라이더만 둔다.
        var hopper = Group("Hopper", g3);
        hopper.localPosition = new Vector3(1.7f, 2.6f, 3.5f);
        Prim(PrimitiveType.Cube, "wallL", hopper, new Vector3(-0.62f, 0f, 0f), new Vector3(0.08f, 1.6f, 0.6f), matWall, true);
        Prim(PrimitiveType.Cube, "wallR", hopper, new Vector3(0.62f, 0f, 0f), new Vector3(0.08f, 1.6f, 0.6f), matWall, true);
        Prim(PrimitiveType.Cube, "floor", hopper, new Vector3(0f, -0.8f, 0f), new Vector3(1.32f, 0.08f, 0.6f), matWall, true);
        Prim(PrimitiveType.Cube, "back", hopper, new Vector3(0f, 0f, 0.3f), new Vector3(1.32f, 1.6f, 0.04f), matWall, true);
        var front = new GameObject("front");
        front.transform.SetParent(hopper, false);
        front.transform.localPosition = new Vector3(0f, 0f, -0.3f);
        front.transform.localScale = new Vector3(1.32f, 1.6f, 0.04f);
        front.AddComponent<BoxCollider>();
        var paddle = Prim(PrimitiveType.Cube, "paddle", hopper, new Vector3(0f, -0.35f, 0f), new Vector3(0.8f, 0.06f, 0.5f), matWall, true);
        var prb = paddle.AddComponent<Rigidbody>();
        prb.isKinematic = true;
        prb.interpolation = RigidbodyInterpolation.Interpolate;
        var ballRoot = Group("balls", hopper);
        for (int i = 0; i < 60; i++)
        {
            // 6(x) × 2(z) × 5(y) 격자. 맨 윗줄도 벽 높이(0.8) 아래에서 시작한다.
            var pos = new Vector3(-0.45f + (i % 6) * 0.18f, -0.15f + (i / 12) * 0.17f, -0.1f + ((i / 6) % 2) * 0.2f);
            var ball = Prim(PrimitiveType.Sphere, "ball" + i, ballRoot, pos, Vector3.one * 0.16f, matBall, true);
            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
        }

        var debris = Group("Debris", g3);
        for (int i = 0; i < 24; i++)
        {
            var d = Prim(PrimitiveType.Cube, "d" + i, debris, Vector3.zero, Vector3.one * 0.14f, matDebris, true);
            d.AddComponent<Rigidbody>().mass = 0.2f;
            d.SetActive(false);
        }
    }

    /// <summary>스테이지 앰비언스 40초 스테레오 루프: 바람 소리(저역 통과 노이즈) + 스테이지마다 다른 느린 패드 화음.</summary>
    private static byte[] BuildAmbience(int stage)
    {
        const int rate = 44100;
        const int seconds = 40;
        var buf = new float[rate * seconds * 2];
        uint st = 991u + (uint)stage * 7919u;
        float lpL = 0f, lpR = 0f;
        float[] roots = { 196f, 174.61f, 220f, 146.83f };   // G3, F3, A3, D3
        float root = roots[stage % roots.Length];
        for (int i = 0; i < rate * seconds; i++)
        {
            unchecked { st = st * 1664525u + 1013904223u; }
            float nL = ((st >> 8) & 0xFFFF) / 32767.5f - 1f;
            unchecked { st = st * 1664525u + 1013904223u; }
            float nR = ((st >> 8) & 0xFFFF) / 32767.5f - 1f;
            lpL += (nL - lpL) * 0.02f;
            lpR += (nR - lpR) * 0.02f;
            float t = (float)i / rate;
            float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 10f);
            float pad = (Mathf.Sin(2f * Mathf.PI * root * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * root * 1.5f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * root * 2.5198f * t)) * 0.06f * swell;
            buf[i * 2] = lpL * 0.9f + pad;
            buf[i * 2 + 1] = lpR * 0.9f + pad;
        }
        return ToWav16(buf, 2, rate);
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
