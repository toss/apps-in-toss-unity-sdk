using UnityEngine;

/// <summary>
/// E2EBootstrapper 실행을 보장하기 위한 Helper MonoBehaviour
/// Scene에 배치되어 Start에서 E2EBootstrapper를 호출
/// RuntimeInitializeOnLoadMethod의 대안으로 사용
/// </summary>
public class E2EBootstrapperHelper : MonoBehaviour
{
    private static bool hasInitialized = false;

    void Start()
    {
        if (hasInitialized)
        {
            Debug.Log("[E2EBootstrapperHelper] Already initialized, skipping");
            return;
        }

        Debug.Log("[E2EBootstrapperHelper] Start called - invoking E2EBootstrapper.Initialize()");

        try
        {
            // E2EBootstrapper.Initialize() 메서드를 직접 호출
            E2EBootstrapper.Initialize();
            hasInitialized = true;
            Debug.Log("[E2EBootstrapperHelper] Successfully invoked E2EBootstrapper");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[E2EBootstrapperHelper] Failed to invoke E2EBootstrapper: {ex}");
        }
    }
}
