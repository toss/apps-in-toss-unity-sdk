using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 다양한 3D 오브젝트를 생성하는 스포너
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public PrimitiveType primitiveType = PrimitiveType.Cube;
    public Material objectMaterial;
    public Vector3 spawnPosition = Vector3.zero;
    public float spawnHeight = 10f;
    public bool addPhysics = true;

    [Header("Object Pool")]
    public int poolSize = 50;
    private List<GameObject> objectPool = new List<GameObject>();

    void Start()
    {
        InitializePool();
    }

    void Update()
    {
        // Space 키로 오브젝트 생성
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnObject();
        }

        // 숫자 키로 다양한 프리미티브 타입 생성
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            primitiveType = PrimitiveType.Cube;
            SpawnObject();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            primitiveType = PrimitiveType.Sphere;
            SpawnObject();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            primitiveType = PrimitiveType.Cylinder;
            SpawnObject();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            primitiveType = PrimitiveType.Capsule;
            SpawnObject();
        }
    }

    void InitializePool()
    {
        // 오브젝트 풀 미리 생성 (성능 최적화)
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = CreateObject();
            obj.SetActive(false);
            objectPool.Add(obj);
        }
    }

    GameObject CreateObject()
    {
        GameObject obj = GameObject.CreatePrimitive(primitiveType);

        if (objectMaterial != null)
        {
            obj.GetComponent<Renderer>().material = objectMaterial;
        }
        else
        {
            // 랜덤 색상
            obj.GetComponent<Renderer>().material.color = Random.ColorHSV();
        }

        if (addPhysics)
        {
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = Random.Range(0.5f, 2f);
        }

        return obj;
    }

    public void SpawnObject()
    {
        GameObject obj = GetPooledObject();

        if (obj != null)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-2f, 2f),
                spawnHeight,
                Random.Range(-2f, 2f)
            );

            obj.transform.position = spawnPosition + randomOffset;
            obj.transform.rotation = Random.rotation;
            obj.SetActive(true);

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    GameObject GetPooledObject()
    {
        foreach (GameObject obj in objectPool)
        {
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }

        // 풀이 가득 차면 새로 생성
        GameObject newObj = CreateObject();
        objectPool.Add(newObj);
        return newObj;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 210, 10, 200, 150));
        GUILayout.Box("Object Spawner\n\nSpace: Spawn\n1: Cube\n2: Sphere\n3: Cylinder\n4: Capsule");
        GUILayout.EndArea();
    }
}
