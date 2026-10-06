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
            Debug.Log($"[HeavyTexStreamProbe] t={m:0}s restored={restored}/{total} streamRestored={ReadStreamCounter("RestoredCount")} browser={ReadStreamCounter("BrowserRestoredCount")}");
        }
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
