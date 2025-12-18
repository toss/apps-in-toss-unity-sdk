using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// OOM (Out of Memory) 테스터 컴포넌트
/// iOS WebView에서 메모리 압박으로 인한 크래시 재현용
/// 사용자가 직접 크기를 선택하여 메모리를 할당할 수 있음
/// </summary>
public class OOMTester : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // JavaScript 브릿지 (WebView 레벨 메모리 할당)
    // double 사용: int는 2GB 초과 시 오버플로우 발생
    [DllImport("__Internal")]
    private static extern double OOMTester_AllocateJSMemory(int megabytes);

    [DllImport("__Internal")]
    private static extern double OOMTester_AllocateVideoBuffer(int megabytes);

    [DllImport("__Internal")]
    private static extern double OOMTester_AllocateCanvasMemory(int megabytes);

    [DllImport("__Internal")]
    private static extern double OOMTester_GetTotalJSAllocated();

    [DllImport("__Internal")]
    private static extern double OOMTester_ClearJSMemory();
#else
    // Editor/Standalone 스텁
    private static double OOMTester_AllocateJSMemory(int megabytes) { Debug.Log($"[OOMTester-Stub] Would allocate {megabytes}MB JS memory"); return megabytes * 1024.0 * 1024.0; }
    private static double OOMTester_AllocateVideoBuffer(int megabytes) { Debug.Log($"[OOMTester-Stub] Would allocate {megabytes}MB video buffer"); return megabytes * 1024.0 * 1024.0; }
    private static double OOMTester_AllocateCanvasMemory(int megabytes) { Debug.Log($"[OOMTester-Stub] Would allocate {megabytes}MB canvas"); return megabytes * 1024.0 * 1024.0; }
    private static double OOMTester_GetTotalJSAllocated() { return 0; }
    private static double OOMTester_ClearJSMemory() { Debug.Log("[OOMTester-Stub] Would clear JS memory"); return 0; }
#endif

    // 메모리 할당 추적
    private List<byte[]> _allocations = new List<byte[]>();
    private string _status = "";
    private double _jsAllocatedBytes = 0;

    /// <summary>
    /// 현재 WASM 힙에 할당된 총 바이트 수
    /// </summary>
    public long WasmAllocatedBytes
    {
        get
        {
            long total = 0;
            foreach (var alloc in _allocations)
            {
                total += alloc.Length;
            }
            return total;
        }
    }

    /// <summary>
    /// 현재 WebView (JS)에 할당된 총 바이트 수
    /// </summary>
    public double JsAllocatedBytes => _jsAllocatedBytes;

    /// <summary>
    /// 마지막 작업 상태 메시지
    /// </summary>
    public string Status => _status;

    /// <summary>
    /// WASM 힙 할당 블록 수
    /// </summary>
    public int AllocationCount => _allocations.Count;

    /// <summary>
    /// OOM 테스터 UI를 렌더링합니다.
    /// </summary>
    /// <param name="boxStyle">외곽 박스 스타일</param>
    /// <param name="groupHeaderStyle">그룹 헤더 스타일</param>
    /// <param name="labelStyle">라벨 스타일</param>
    /// <param name="dangerButtonStyle">위험 버튼 스타일 (할당)</param>
    /// <param name="buttonStyle">일반 버튼 스타일 (해제)</param>
    public void DrawUI(GUIStyle boxStyle, GUIStyle groupHeaderStyle, GUIStyle labelStyle, GUIStyle dangerButtonStyle, GUIStyle buttonStyle)
    {
        GUILayout.BeginVertical(boxStyle);

        // 섹션 헤더
        GUILayout.Label("⚠️ OOM Tester", groupHeaderStyle);
        GUILayout.Label("iOS WebView 메모리 부족 상황을 재현합니다.", labelStyle);

        GUILayout.Space(10);

        // 현재 메모리 상태 표시
        long wasmAllocated = WasmAllocatedBytes;

        // JS 메모리 상태 업데이트
        _jsAllocatedBytes = OOMTester_GetTotalJSAllocated();

        string memoryInfo = $"WASM 힙: {wasmAllocated / (1024 * 1024)}MB ({_allocations.Count}개 블록)";
        GUILayout.Label(memoryInfo, labelStyle);

        string jsMemoryInfo = $"WebView (JS): {_jsAllocatedBytes / (1024 * 1024):F0}MB";
        GUILayout.Label(jsMemoryInfo, labelStyle);

        string totalInfo = $"총 할당: {(wasmAllocated + _jsAllocatedBytes) / (1024 * 1024):F0}MB";
        GUILayout.Label(totalInfo, labelStyle);

        if (!string.IsNullOrEmpty(_status))
        {
            GUILayout.Label(_status, labelStyle);
        }

        GUILayout.Space(10);

        // WASM 힙 할당 버튼들 (세로 배치)
        GUILayout.Label("WASM 힙 (C# byte[])", labelStyle);
        if (GUILayout.Button("+50MB WASM", dangerButtonStyle, GUILayout.Height(40)))
        {
            AllocateWasm(50);
        }
        if (GUILayout.Button("+100MB WASM", dangerButtonStyle, GUILayout.Height(40)))
        {
            AllocateWasm(100);
        }
        if (GUILayout.Button("+500MB WASM", dangerButtonStyle, GUILayout.Height(40)))
        {
            AllocateWasm(500);
        }

        GUILayout.Space(10);

        // WebView (JS) 할당 버튼들 (세로 배치)
        GUILayout.Label("WebView (JS ArrayBuffer)", labelStyle);
        if (GUILayout.Button("+50MB WebView", dangerButtonStyle, GUILayout.Height(40)))
        {
            AllocateWebView(50);
        }
        if (GUILayout.Button("+100MB WebView", dangerButtonStyle, GUILayout.Height(40)))
        {
            AllocateWebView(100);
        }
        if (GUILayout.Button("+500MB WebView", dangerButtonStyle, GUILayout.Height(40)))
        {
            AllocateWebView(500);
        }

        GUILayout.Space(10);

        // 메모리 해제 버튼 (세로 배치)
        bool hasWasmMemory = _allocations.Count > 0;
        bool hasJsMemory = _jsAllocatedBytes > 0;

        if (hasWasmMemory || hasJsMemory)
        {
            GUILayout.Label("메모리 해제", labelStyle);

            if (hasWasmMemory)
            {
                if (GUILayout.Button("WASM 해제", buttonStyle, GUILayout.Height(36)))
                {
                    ClearWasmAllocations();
                }
            }

            if (hasJsMemory)
            {
                if (GUILayout.Button("WebView 해제", buttonStyle, GUILayout.Height(36)))
                {
                    ClearJSAllocations();
                }
            }

            if (hasWasmMemory && hasJsMemory)
            {
                if (GUILayout.Button("전체 해제", buttonStyle, GUILayout.Height(36)))
                {
                    ClearAllAllocations();
                }
            }
        }

        GUILayout.EndVertical();
    }

    /// <summary>
    /// WASM 힙에 지정된 MB 크기의 메모리를 할당합니다.
    /// </summary>
    public void AllocateWasm(int megabytes)
    {
        try
        {
            int bytes = megabytes * 1024 * 1024;
            byte[] chunk = new byte[bytes];

            // 실제 데이터를 쓰면서 메모리가 실제로 할당되도록 합니다.
            // (Lazy allocation 방지)
            for (int i = 0; i < bytes; i += 4096)
            {
                chunk[i] = (byte)(i % 256);
            }

            _allocations.Add(chunk);
            _status = $"WASM +{megabytes}MB 할당됨";
            Debug.Log($"[OOMTester] WASM 힙 {megabytes}MB 청크 할당됨");
        }
        catch (OutOfMemoryException ex)
        {
            _status = $"WASM OOM 발생! {ex.Message}";
            Debug.LogError($"[OOMTester] WASM OOM: {ex.Message}");
        }
    }

    /// <summary>
    /// WebView (JavaScript) 레벨에서 지정된 MB 크기의 메모리를 할당합니다.
    /// </summary>
    public void AllocateWebView(int megabytes)
    {
        double allocated = OOMTester_AllocateJSMemory(megabytes);
        if (allocated > 0)
        {
            _status = $"WebView +{megabytes}MB 할당됨";
            Debug.Log($"[OOMTester] WebView {megabytes}MB 할당됨");
        }
        else
        {
            _status = $"WebView 할당 실패 ({megabytes}MB)";
            Debug.LogError($"[OOMTester] WebView {megabytes}MB 할당 실패");
        }
    }

    /// <summary>
    /// 할당된 WASM 힙 메모리를 해제합니다.
    /// </summary>
    public void ClearWasmAllocations()
    {
        int count = _allocations.Count;
        long totalSize = 0;
        foreach (var alloc in _allocations)
        {
            totalSize += alloc.Length;
        }

        _allocations.Clear();
        GC.Collect();

        _status = $"WASM: {count}개 블록 ({totalSize / (1024 * 1024)}MB) 해제됨";
        Debug.Log($"[OOMTester] WASM 힙 {count}개 블록 ({totalSize / (1024 * 1024)}MB) 해제됨");
    }

    /// <summary>
    /// 할당된 JavaScript/WebView 메모리를 해제합니다.
    /// </summary>
    public void ClearJSAllocations()
    {
        double freedBytes = OOMTester_ClearJSMemory();
        _jsAllocatedBytes = 0;

        _status = $"WebView: {freedBytes / (1024 * 1024):F0}MB 해제됨";
        Debug.Log($"[OOMTester] WebView {freedBytes / (1024 * 1024):F0}MB 해제됨");
    }

    /// <summary>
    /// 할당된 모든 메모리 (WASM + WebView)를 해제합니다.
    /// </summary>
    public void ClearAllAllocations()
    {
        // WASM 해제
        int wasmCount = _allocations.Count;
        long wasmSize = 0;
        foreach (var alloc in _allocations)
        {
            wasmSize += alloc.Length;
        }
        _allocations.Clear();
        GC.Collect();

        // JS 해제
        double jsFreed = OOMTester_ClearJSMemory();
        _jsAllocatedBytes = 0;

        _status = $"전체 해제: WASM {wasmSize / (1024 * 1024)}MB + WebView {jsFreed / (1024 * 1024):F0}MB";
        Debug.Log($"[OOMTester] 전체 해제 완료: WASM {wasmSize / (1024 * 1024)}MB + WebView {jsFreed / (1024 * 1024):F0}MB");
    }
}
