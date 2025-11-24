using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 물리 엔진 스트레스 테스트
/// 다수의 리지드바디 오브젝트를 생성하여 물리 시뮬레이션 성능을 측정합니다.
/// </summary>
public class PhysicsStressTest : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject prefabToSpawn;
    public int objectsPerWave = 10;
    public float spawnInterval = 1.0f;
    public int maxObjects = 100;
    public Vector3 spawnArea = new Vector3(10f, 5f, 10f);
    public bool autoStart = true;

    [Header("Physics Settings")]
    public PhysicMaterial physicsMaterial;
    public float objectMass = 1.0f;
    public float initialForce = 5.0f;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private bool isSpawning = false;
    private int totalSpawned = 0;

    void Start()
    {
        if (autoStart)
        {
            StartSpawning();
        }
    }

    void Update()
    {
        // 'S' 키로 스포닝 시작/중지
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (isSpawning)
                StopSpawning();
            else
                StartSpawning();
        }

        // 'C' 키로 모든 오브젝트 제거
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllObjects();
        }

        // 'R' 키로 리셋
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetTest();
        }
    }

    public void StartSpawning()
    {
        if (!isSpawning)
        {
            isSpawning = true;
            StartCoroutine(SpawnRoutine());
            Debug.Log("Physics stress test started");
        }
    }

    public void StopSpawning()
    {
        isSpawning = false;
        Debug.Log("Physics stress test stopped");
    }

    private IEnumerator SpawnRoutine()
    {
        while (isSpawning && totalSpawned < maxObjects)
        {
            SpawnWave();
            yield return new WaitForSeconds(spawnInterval);
        }

        if (totalSpawned >= maxObjects)
        {
            Debug.Log($"Max objects ({maxObjects}) reached");
            isSpawning = false;
        }
    }

    void SpawnWave()
    {
        for (int i = 0; i < objectsPerWave && totalSpawned < maxObjects; i++)
        {
            SpawnObject();
            totalSpawned++;
        }
    }

    void SpawnObject()
    {
        GameObject obj;

        // Prefab이 없으면 기본 큐브 생성
        if (prefabToSpawn == null)
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.localScale = Vector3.one * Random.Range(0.5f, 1.5f);

            // 랜덤 색상
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(
                    Random.Range(0f, 1f),
                    Random.Range(0f, 1f),
                    Random.Range(0f, 1f)
                );
            }
        }
        else
        {
            obj = Instantiate(prefabToSpawn);
        }

        // 랜덤 위치 설정
        Vector3 randomPos = transform.position + new Vector3(
            Random.Range(-spawnArea.x / 2, spawnArea.x / 2),
            spawnArea.y + Random.Range(0, 5f),
            Random.Range(-spawnArea.z / 2, spawnArea.z / 2)
        );
        obj.transform.position = randomPos;

        // 랜덤 회전
        obj.transform.rotation = Random.rotation;

        // Rigidbody 추가 또는 설정
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
        }

        rb.mass = objectMass;
        rb.useGravity = true;

        // 초기 힘 추가
        Vector3 randomForce = new Vector3(
            Random.Range(-initialForce, initialForce),
            Random.Range(0, initialForce),
            Random.Range(-initialForce, initialForce)
        );
        rb.AddForce(randomForce, ForceMode.VelocityChange);

        // 회전 토크 추가
        rb.AddTorque(Random.insideUnitSphere * initialForce, ForceMode.VelocityChange);

        // Collider 확인
        if (obj.GetComponent<Collider>() == null)
        {
            obj.AddComponent<BoxCollider>();
        }

        // Physics Material 적용
        if (physicsMaterial != null)
        {
            Collider collider = obj.GetComponent<Collider>();
            collider.material = physicsMaterial;
        }

        // 일정 시간 후 자동 제거 (메모리 관리)
        Destroy(obj, 30f);

        spawnedObjects.Add(obj);
    }

    public void ClearAllObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        spawnedObjects.Clear();
        totalSpawned = 0;
        Debug.Log("All objects cleared");
    }

    public void ResetTest()
    {
        StopSpawning();
        ClearAllObjects();
        Debug.Log("Test reset");
    }

    void OnDrawGizmos()
    {
        // 스폰 영역 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * spawnArea.y / 2, spawnArea);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, Screen.height - 120, 300, 110));
        GUILayout.Box($"Physics Stress Test\nObjects: {totalSpawned}/{maxObjects}\nActive: {spawnedObjects.Count}");

        if (GUILayout.Button(isSpawning ? "Stop Spawning (S)" : "Start Spawning (S)"))
        {
            if (isSpawning)
                StopSpawning();
            else
                StartSpawning();
        }

        if (GUILayout.Button("Clear All (C)"))
        {
            ClearAllObjects();
        }

        if (GUILayout.Button("Reset (R)"))
        {
            ResetTest();
        }

        GUILayout.EndArea();
    }
}
