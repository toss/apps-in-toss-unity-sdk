using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 렌더링 성능 벤치마크
/// 대량의 오브젝트를 렌더링하여 GPU 성능을 측정합니다.
/// </summary>
public class RenderingBenchmark : MonoBehaviour
{
    [Header("Benchmark Settings")]
    public int gridSize = 10;
    public float spacing = 2f;
    public bool useInstancing = true;
    public Material instancedMaterial;

    [Header("Animation")]
    public bool animateObjects = true;
    public float rotationSpeed = 30f;
    public float bobSpeed = 1f;
    public float bobHeight = 0.5f;

    private List<GameObject> benchmarkObjects = new List<GameObject>();
    private List<Vector3> initialPositions = new List<Vector3>();

    void Start()
    {
        CreateBenchmarkScene();
    }

    void Update()
    {
        if (animateObjects)
        {
            AnimateObjects();
        }

        // 'B' 키로 벤치마크 재생성
        if (Input.GetKeyDown(KeyCode.B))
        {
            RegenerateBenchmark();
        }

        // '+/-' 키로 그리드 크기 조절
        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
        {
            gridSize = Mathf.Min(gridSize + 2, 50);
            RegenerateBenchmark();
        }
        else if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
        {
            gridSize = Mathf.Max(gridSize - 2, 2);
            RegenerateBenchmark();
        }

        // 'I' 키로 인스턴싱 토글
        if (Input.GetKeyDown(KeyCode.I))
        {
            useInstancing = !useInstancing;
            RegenerateBenchmark();
        }
    }

    void CreateBenchmarkScene()
    {
        ClearBenchmark();

        // 그리드로 오브젝트 배치
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 position = new Vector3(
                    (x - gridSize / 2f) * spacing,
                    0,
                    (z - gridSize / 2f) * spacing
                );

                GameObject obj = CreateBenchmarkObject(position);
                benchmarkObjects.Add(obj);
                initialPositions.Add(position);
            }
        }

        Debug.Log($"Created {benchmarkObjects.Count} objects for rendering benchmark");
    }

    GameObject CreateBenchmarkObject(Vector3 position)
    {
        // 다양한 프리미티브 타입 사용
        PrimitiveType[] types = new PrimitiveType[]
        {
            PrimitiveType.Cube,
            PrimitiveType.Sphere,
            PrimitiveType.Cylinder,
            PrimitiveType.Capsule
        };

        PrimitiveType type = types[Random.Range(0, types.Length)];
        GameObject obj = GameObject.CreatePrimitive(type);

        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);

        // 머티리얼 설정
        Renderer renderer = obj.GetComponent<Renderer>();
        if (useInstancing && instancedMaterial != null)
        {
            renderer.material = instancedMaterial;
            renderer.material.enableInstancing = true;
        }
        else
        {
            renderer.material.color = new Color(
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f)
            );
        }

        // 콜라이더 제거 (렌더링 벤치마크이므로)
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        return obj;
    }

    void AnimateObjects()
    {
        float time = Time.time;

        for (int i = 0; i < benchmarkObjects.Count; i++)
        {
            if (benchmarkObjects[i] != null)
            {
                // 회전
                benchmarkObjects[i].transform.Rotate(
                    Vector3.up * rotationSpeed * Time.deltaTime +
                    Vector3.right * (rotationSpeed * 0.5f) * Time.deltaTime
                );

                // 상하 운동
                Vector3 pos = initialPositions[i];
                float offset = Mathf.Sin(time * bobSpeed + i * 0.1f) * bobHeight;
                pos.y = offset;
                benchmarkObjects[i].transform.position = pos;
            }
        }
    }

    void RegenerateBenchmark()
    {
        CreateBenchmarkScene();
        Debug.Log($"Regenerated benchmark with grid size: {gridSize}x{gridSize}");
    }

    void ClearBenchmark()
    {
        foreach (GameObject obj in benchmarkObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        benchmarkObjects.Clear();
        initialPositions.Clear();
    }

    void OnDestroy()
    {
        ClearBenchmark();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 260, Screen.height - 150, 250, 140));
        GUILayout.Box($"Rendering Benchmark\n\n" +
                      $"Grid: {gridSize}x{gridSize}\n" +
                      $"Objects: {benchmarkObjects.Count}\n" +
                      $"Instancing: {(useInstancing ? "ON" : "OFF")}\n\n" +
                      $"B: Regenerate\n" +
                      $"+/-: Grid Size\n" +
                      $"I: Toggle Instancing");
        GUILayout.EndArea();
    }
}
