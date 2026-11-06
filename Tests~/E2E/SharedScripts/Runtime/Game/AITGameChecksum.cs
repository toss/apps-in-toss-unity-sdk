using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class AITGameJsonInner
{
    public int a;
    public string b;
}

[Serializable]
public class AITGameJsonProbe
{
    public int id;
    public string name;
    public float[] values;
    public List<int> nums;
    public AITGameJsonInner inner;
}

/// <summary>
/// 결정론 체크섬. 같은 Unity 버전의 두 빌드는 최적화 설정과 무관하게 항상 같은 값을 내야 한다(프로그램 의미가 안 바뀌었다는 증거).
/// 각 성분은 순수 로직만 쓰고 시간·GetHashCode·플랫폼 의존 값은 쓰지 않는다.
/// </summary>
public static class AITGameChecksum
{
    public static string Fnv(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            return h.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    public static string Rng()
    {
        var r = new System.Random(12345);
        var sb = new StringBuilder();
        for (int i = 0; i < 256; i++)
        {
            sb.Append(r.Next(1000000)).Append(',');
        }
        sb.Append(r.NextDouble().ToString("F6", CultureInfo.InvariantCulture));
        return Fnv(sb.ToString());
    }

    public static string MathSum()
    {
        double acc = 0.0;
        for (int i = 0; i < 2000; i++)
        {
            float f = i * 0.01f;
            acc += Mathf.Sin(f) * Mathf.Cos(f * 0.5f) + Mathf.Sqrt(f + 1f) + Mathf.Pow(f, 1.5f) * 0.001f;
        }
        // 1e-3 로 둥글려 비교(최적화에 따른 마지막 비트 차이를 흡수).
        return Fnv(acc.ToString("F3", CultureInfo.InvariantCulture));
    }

    public static string Hashing()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1500; i++)
        {
            sb.Append("ait-game-").Append(i).Append('-');
        }
        sb.Append(string.Format(CultureInfo.InvariantCulture, "{0:F2}|{1:D5}|{2:X}", 3.14159, 42, 255));
        return Fnv(sb.ToString());
    }

    public static string Collections()
    {
        var r = new System.Random(777);
        var list = new List<int>();
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < 500; i++)
        {
            int v = r.Next(10000);
            list.Add(v);
            string key = "k" + (v % 17);
            int c;
            counts.TryGetValue(key, out c);
            counts[key] = c + 1;
        }
        int sumDiv3 = list.Where(x => x % 3 == 0).Select(x => x * 2).OrderBy(x => x).Take(20).Sum();
        var groups = list.GroupBy(x => x % 5).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count());
        var sb = new StringBuilder();
        sb.Append(sumDiv3).Append('|').Append(string.Join(",", groups.ToArray())).Append('|');
        foreach (var k in counts.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            sb.Append(k).Append('=').Append(counts[k]).Append(';');
        }
        sb.Append(list.Max()).Append('|').Append(list.Min());
        return Fnv(sb.ToString());
    }

    public static string JsonRoundTrip()
    {
        var p = new AITGameJsonProbe();
        p.id = 7;
        p.name = "round-trip";
        p.values = new float[] { 0.5f, 1.25f, -3f, 100.125f };
        p.nums = new List<int> { 1, 1, 2, 3, 5, 8, 13 };
        p.inner = new AITGameJsonInner { a = 42, b = "inner" };
        string j1 = JsonUtility.ToJson(p);
        var back = JsonUtility.FromJson<AITGameJsonProbe>(j1);
        string j2 = JsonUtility.ToJson(back);
        return Fnv(j1 + "|" + j2 + "|" + (j1 == j2 ? "same" : "diff"));
    }

    /// <summary>숨겨진 Physics3D 씬에서 300 스텝 시뮬레이션하고 최종 위치를 1e-3 로 둥글려 해시한다.</summary>
    public static IEnumerator PhysicsProbe(Action<string> done)
    {
        Scene scene = SceneManager.CreateScene("AITGamePhysProbe", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        PhysicsScene ps = scene.GetPhysicsScene();
        var bodies = new List<Rigidbody>();
        var created = new List<GameObject>();

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0f, -1f, 0f);
        floor.transform.localScale = new Vector3(30f, 2f, 30f);
        SceneManager.MoveGameObjectToScene(floor, scene);
        created.Add(floor);

        for (int i = 0; i < 6; i++)
        {
            var go = GameObject.CreatePrimitive((i % 2 == 0) ? PrimitiveType.Sphere : PrimitiveType.Cube);
            go.transform.position = new Vector3(i * 0.6f - 1.5f, 2f + i * 1.3f, (i % 3) * 0.35f);
            go.transform.rotation = Quaternion.Euler(i * 7f, i * 13f, i * 3f);
            var rb = go.AddComponent<Rigidbody>();
            SceneManager.MoveGameObjectToScene(go, scene);
            bodies.Add(rb);
            created.Add(go);
        }

        for (int step = 0; step < 300; step++)
        {
            ps.Simulate(0.02f);
        }

        var sb = new StringBuilder();
        for (int i = 0; i < bodies.Count; i++)
        {
            Vector3 p = bodies[i].position;
            sb.Append(Mathf.RoundToInt(p.x * 1000f)).Append(',')
              .Append(Mathf.RoundToInt(p.y * 1000f)).Append(',')
              .Append(Mathf.RoundToInt(p.z * 1000f)).Append(';');
        }
        string result = Fnv(sb.ToString());

        for (int i = 0; i < created.Count; i++)
        {
            UnityEngine.Object.Destroy(created[i]);
        }
        yield return null;
        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
        if (unload != null)
        {
            yield return unload;
        }
        done(result);
    }
}
