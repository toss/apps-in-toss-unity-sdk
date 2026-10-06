using System.Collections;
using UnityEngine;

/// <summary>
/// perf heavy 'tex-stream' 변형 전용: Resources 의 HeavyTexStream 프리팹(외부화된 2048² 스프라이트 4장)을 부팅 직후 인스턴스화해
/// AITStreamingTexture 의 복원 경로(스텁 → 실 픽셀 LoadImage 제자리 복원)를 실제로 태운다. 일반 E2E/heavy 빌드에는 프리팹이 없어 아무것도 하지 않는다.
/// 복원 여부는 노이즈 줄 규약(y % 16 == 0 줄은 노이즈, 나머지는 단색)으로 확인한다 — 스텁이면 (0,0) 과 (0,1) 이 같은 색이다.
/// </summary>
public class HeavyTexStreamProbe : MonoBehaviour
{
    // HeavyLowMemVariants.TexStreamResourceName 과 같아야 한다(Editor 어셈블리라 상수를 공유할 수 없다).
    private const string PrefabResourceName = "HeavyTexStream";

    private SpriteRenderer[] renderers;
    private bool drawCheckDone;

    // draw-check: HeavyLowMemVariants.BuildTexStreamPixels 규약과 같아야 한다(Editor 어셈블리라 공유 불가).
    // 줄 번호 r 은 Texture2D.SetPixels32 기준(r=0 이 맨 아래 줄 = uv.v 0). 노이즈 줄은 r % 16 == 0, 나머지는 단색.
    private const int DrawTolerance = 6;
    private const int DrawStripWidth = 64;
    private const int NoiseMinDiff = 16; // 노이즈 줄 64픽셀 중 베이스와 다른 픽셀이 이 수 이상이면 노이즈로 본다(블록 압축 번짐 허용).
    private const int RowNoiseBottom = 0;
    private const int RowNoiseMid = 1024;
    private const int RowBaseLow = 8;      // 4x4/8x8 블록 어디에도 노이즈 줄이 섞이지 않는 단색 줄(8..15 구간).
    private const int RowBaseTop = 2047;   // 마지막 블록(2040..2047)도 단색. 뒤집히면 이 위치에 줄 0 의 노이즈가 온다.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourceName);
        if (prefab == null)
        {
            return;
        }

        var instance = Instantiate(prefab);
        DontDestroyOnLoad(instance);
        var probe = instance.AddComponent<HeavyTexStreamProbe>();
        probe.renderers = instance.GetComponentsInChildren<SpriteRenderer>();
        Debug.Log($"[HeavyTexStreamProbe] 프리팹 인스턴스화: 스프라이트 {probe.renderers.Length}장");
    }

    private IEnumerator Start()
    {
        // 첫 방문 콘솔 수집 창(~30초) 안에서 복원 진행을 몇 번 남긴다.
        float[] marks = { 3f, 10f, 20f, 28f };
        float elapsed = 0f;
        foreach (float m in marks)
        {
            yield return new WaitForSecondsRealtime(m - elapsed);
            elapsed = m;

            int restored = 0;
            int total = 0;
            foreach (var r in renderers)
            {
                if (r == null || r.sprite == null || r.sprite.texture == null)
                {
                    continue;
                }

                total++;
                try
                {
                    var tex = r.sprite.texture;
                    // 스텁(readable 단색)이면 두 줄이 같다. 복원되면 노이즈 줄(y=0)과 단색 줄(y=1)이 다르다.
                    if (!Color32Equals(tex.GetPixel(0, 0), tex.GetPixel(0, 1)))
                    {
                        restored++;
                    }
                }
                catch (UnityException)
                {
                    // non-readable 로 복원된 경우(후속 배치의 markNonReadable): 픽셀을 읽을 수 없으니 판정 불가 — 로드된 것으로만 센다.
                    restored++;
                }
            }

            // non-readable 로 복원된 텍스처는 위 GetPixel 로 복원 여부를 가릴 수 없으므로(UnityException 을 복원으로 센다)
            // AITStreamingTexture 의 실제 복원 카운터(internal)도 리플렉션으로 함께 남긴다. browser = 브라우저 디코드 경로로 복원된 수.
            int streamRestored = ReadStreamCounter("RestoredCount");
            int browser = ReadStreamCounter("BrowserRestoredCount");
            Debug.Log($"[HeavyTexStreamProbe] t={m:0}s restored={restored}/{total} streamRestored={streamRestored} browser={browser}");

            // 전부 복원된 첫 시점(스트림 카운터를 읽을 수 있으면 그것도 total 이상일 때)에 한 번, 못 채우면 마지막 마크에서 현 상태로 한 번 남긴다.
            bool allRestored = total > 0 && restored == total && (streamRestored < 0 || streamRestored >= total);
            bool last = m == marks[marks.Length - 1];
            if (!drawCheckDone && (allRestored || last))
            {
                drawCheckDone = true;
                // Blit/ReadPixels 는 프레임 끝에 한다.
                yield return new WaitForEndOfFrame();
                RunDrawCheck(restored, total, streamRestored, browser);
            }
        }
    }

    /// <summary>
    /// Unity 가 스왑된 GL 텍스처를 실제로 샘플링하는지 확인한다(GL 직접 readback 이 아니라 Unity 의 Graphics.Blit → RenderTexture → ReadPixels).
    /// 텍스처마다 64x1 RT 로 한 줄(64 텍셀)을 정확히 텍셀 중심에서 샘플링한다(RT 픽셀 i 의 중심 u = (i+0.5)/w).
    /// 색 공간: RenderTextureReadWrite.Default 라 선형 프로젝트면 sRGB RT, 감마면 선형 RT 가 된다. 어느 쪽이든 샘플링 변환과 기록 변환이
    /// 상쇄돼 ReadPixels 값이 원본 바이트와 같다(GL.sRGBWrite 를 따로 만지지 않는다).
    /// 방향: 줄 0·1024(노이즈)와 줄 8·2047(단색)을 비교한다. 위아래가 뒤집혔으면 줄 0 이 단색이 되고 줄 2047 이 노이즈가 된다.
    /// Unity 자체 경로(LoadImage, legacy 변형)가 낸 결과가 기준이다: legacy 에서 orient=ok 면 브라우저 스왑도 ok 여야 한다.
    /// </summary>
    private void RunDrawCheck(int restored, int total, int streamRestored, int browser)
    {
        var sb = new System.Text.StringBuilder();
        int pass = 0;
        int checkedCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r.sprite == null || r.sprite.texture == null)
            {
                continue;
            }

            checkedCount++;
            var tex = r.sprite.texture;
            string detail;
            bool ok;
            try
            {
                ok = CheckTexture(tex, i, out detail);
            }
            catch (System.Exception e)
            {
                ok = false;
                detail = "exception=" + e.GetType().Name + ":" + e.Message;
            }

            if (ok)
            {
                pass++;
            }

            sb.Append(" [").Append(tex.name).Append(' ').Append(ok ? "ok" : "FAIL").Append(' ').Append(detail).Append(']');
        }

        Debug.Log($"[AIT-TexStreamProbe] draw-check pass={pass}/{checkedCount} restored={restored}/{total} streamRestored={streamRestored} browser={browser} colorSpace={QualitySettings.activeColorSpace} tol={DrawTolerance}{sb}");
    }

    private static bool CheckTexture(Texture2D tex, int fallbackIndex, out string detail)
    {
        int index = ParseIndex(tex.name, fallbackIndex);
        Color32 want = ExpectedBase(index);
        int w = tex.width;
        int h = tex.height;
        int n = Mathf.Min(DrawStripWidth, w);
        if (h <= RowBaseTop)
        {
            detail = $"size={w}x{h} 예상과 다름";
            return false;
        }

        Color32[] low = SampleRow(tex, RowBaseLow, n);
        Color32[] top = SampleRow(tex, RowBaseTop, n);
        Color32[] nzBottom = SampleRow(tex, RowNoiseBottom, n);
        Color32[] nzMid = SampleRow(tex, RowNoiseMid, n);
        if (low == null || top == null || nzBottom == null || nzMid == null)
        {
            detail = "blit 실패";
            return false;
        }

        Color32 gotLow = low[n / 2];
        Color32 gotTop = top[n / 2];
        bool colorOk = Near(gotLow, want) && Near(gotTop, want);
        int diffBottom = CountDiff(nzBottom, want);
        int diffMid = CountDiff(nzMid, want);
        int diffTop = CountDiff(top, want);

        // 정방향: 줄 0 과 줄 1024 가 노이즈. 뒤집힘: 줄 0 이 단색이고 줄 2047 쪽에 노이즈가 온다(저장 줄 0 이 위로 간 경우).
        string orient;
        if (diffBottom >= NoiseMinDiff && diffMid >= NoiseMinDiff && diffTop < NoiseMinDiff)
        {
            orient = "ok";
        }
        else if (diffTop >= NoiseMinDiff && diffBottom < NoiseMinDiff)
        {
            orient = "flipped";
        }
        else
        {
            orient = "unknown";
        }

        bool ok = colorOk && orient == "ok";
        detail = $"want=({want.r},{want.g},{want.b}) gotRow{RowBaseLow}=({gotLow.r},{gotLow.g},{gotLow.b}) gotRow{RowBaseTop}=({gotTop.r},{gotTop.g},{gotTop.b}) orient={orient} noiseDiff(row0/row1024/row2047)={diffBottom}/{diffMid}/{diffTop} of {n}";
        return ok;
    }

    /// <summary>텍스처의 줄 row(0 = 맨 아래)에서 가로 n 텍셀을 텍셀 중심으로 읽는다. 실패하면 null.</summary>
    private static Color32[] SampleRow(Texture2D tex, int row, int n)
    {
        int w = tex.width;
        int h = tex.height;
        var rt = RenderTexture.GetTemporary(n, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        var prev = RenderTexture.active;
        Texture2D readTex = null;
        try
        {
            rt.filterMode = FilterMode.Point;
            // 출력 픽셀 i 의 uv = offset + (i+0.5)/n * scale = (i+0.5)/w, v = (row+0.5)/h.
            Graphics.Blit(tex, rt, new Vector2((float)n / w, 1f / h), new Vector2(0f, (float)row / h));
            RenderTexture.active = rt;
            readTex = new Texture2D(n, 1, TextureFormat.RGBA32, false);
            readTex.ReadPixels(new Rect(0, 0, n, 1), 0, 0, false);
            readTex.Apply(false);
            return readTex.GetPixels32();
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (readTex != null)
            {
                Destroy(readTex);
            }
        }
    }

    private static int ParseIndex(string name, int fallback)
    {
        // "texstream_02" 형태. 이름을 못 읽으면 렌더러 순서(프리팹 자식 순서 = 생성 순서)를 쓴다.
        int u = name != null ? name.LastIndexOf('_') : -1;
        if (u >= 0 && int.TryParse(name.Substring(u + 1), out int idx) && idx >= 0)
        {
            return idx;
        }

        return fallback;
    }

    // HeavyLowMemVariants.TexStreamBaseColor 와 같은 식.
    private static Color32 ExpectedBase(int index)
    {
        byte r = (byte)(32 + (index * 67) % 192);
        byte g = (byte)(48 + (index * 109) % 160);
        byte b = (byte)(64 + (index * 151) % 128);
        return new Color32(r, g, b, 255);
    }

    private static bool Near(Color32 a, Color32 b)
    {
        return Mathf.Abs(a.r - b.r) <= DrawTolerance && Mathf.Abs(a.g - b.g) <= DrawTolerance && Mathf.Abs(a.b - b.b) <= DrawTolerance;
    }

    private static int CountDiff(Color32[] px, Color32 baseColor)
    {
        int c = 0;
        foreach (var p in px)
        {
            if (!Near(p, baseColor))
            {
                c++;
            }
        }

        return c;
    }

    private static int ReadStreamCounter(string field)
    {
        try
        {
            var type = System.Type.GetType("AppsInToss.AITStreamingTexture, AppsInToss.Helpers");
            var f = type?.GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return f != null ? (int)f.GetValue(null) : -1;
        }
        catch (System.Exception)
        {
            return -1;
        }
    }

    private static bool Color32Equals(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
    }
}
