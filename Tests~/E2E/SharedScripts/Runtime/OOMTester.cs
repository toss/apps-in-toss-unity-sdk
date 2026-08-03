using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

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

    // uGUI 참조
    private Text _wasmInfoText;
    private Text _jsInfoText;
    private Text _totalInfoText;
    private Text _statusTextUI;
    private GameObject _releaseSection;
    private Button _wasmReleaseBtn;
    private Button _jsReleaseBtn;
    private Button _allReleaseBtn;

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
    /// uGUI 기반 UI를 생성합니다.
    /// </summary>
    public void SetupUI(Transform parent)
    {
        var section = UIBuilder.CreatePanel(parent, UIBuilder.Theme.SectionBg);
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingSmall;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        section.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(section, "OOM Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "iOS WebView 메모리 부족 상황을 재현합니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 메모리 정보
        _wasmInfoText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _jsInfoText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _totalInfoText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _statusTextUI = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // WASM 힙 할당 버튼
        UIBuilder.CreateText(section, "WASM 힙 (C# byte[])",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "+50MB WASM", onClick: () => { AllocateWasm(50); UpdateUI(); }, style: UIBuilder.ButtonStyle.Danger);
        UIBuilder.CreateButton(section, "+100MB WASM", onClick: () => { AllocateWasm(100); UpdateUI(); }, style: UIBuilder.ButtonStyle.Danger);
        UIBuilder.CreateButton(section, "+500MB WASM", onClick: () => { AllocateWasm(500); UpdateUI(); }, style: UIBuilder.ButtonStyle.Danger);

        // WebView 할당 버튼
        UIBuilder.CreateText(section, "WebView (JS ArrayBuffer)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "+50MB WebView", onClick: () => { AllocateWebView(50); UpdateUI(); }, style: UIBuilder.ButtonStyle.Danger);
        UIBuilder.CreateButton(section, "+100MB WebView", onClick: () => { AllocateWebView(100); UpdateUI(); }, style: UIBuilder.ButtonStyle.Danger);
        UIBuilder.CreateButton(section, "+500MB WebView", onClick: () => { AllocateWebView(500); UpdateUI(); }, style: UIBuilder.ButtonStyle.Danger);

        // 해제 버튼들
        _releaseSection = new GameObject("ReleaseSection");
        _releaseSection.AddComponent<RectTransform>().SetParent(section, false);
        var rvlg = _releaseSection.AddComponent<VerticalLayoutGroup>();
        rvlg.spacing = UIBuilder.Theme.SpacingSmall;
        rvlg.childForceExpandWidth = true;
        rvlg.childForceExpandHeight = false;
        rvlg.childControlWidth = true;
        rvlg.childControlHeight = true;
        _releaseSection.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(_releaseSection.transform, "메모리 해제",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        _wasmReleaseBtn = UIBuilder.CreateButton(_releaseSection.transform, "WASM 해제", onClick: () => { ClearWasmAllocations(); UpdateUI(); });
        _jsReleaseBtn = UIBuilder.CreateButton(_releaseSection.transform, "WebView 해제", onClick: () => { ClearJSAllocations(); UpdateUI(); });
        _allReleaseBtn = UIBuilder.CreateButton(_releaseSection.transform, "전체 해제", onClick: () => { ClearAllAllocations(); UpdateUI(); });

        UpdateUI();
    }

    private void UpdateUI()
    {
        long wasmAllocated = WasmAllocatedBytes;
        _jsAllocatedBytes = OOMTester_GetTotalJSAllocated();

        if (_wasmInfoText != null)
            _wasmInfoText.text = $"WASM 힙: {wasmAllocated / (1024 * 1024)}MB ({_allocations.Count}개 블록)";
        if (_jsInfoText != null)
            _jsInfoText.text = $"WebView (JS): {_jsAllocatedBytes / (1024 * 1024):F0}MB";
        if (_totalInfoText != null)
            _totalInfoText.text = $"총 할당: {(wasmAllocated + _jsAllocatedBytes) / (1024 * 1024):F0}MB";
        if (_statusTextUI != null)
        {
            _statusTextUI.text = _status;
            _statusTextUI.gameObject.SetActive(!string.IsNullOrEmpty(_status));
        }

        bool hasWasm = _allocations.Count > 0;
        bool hasJs = _jsAllocatedBytes > 0;
        if (_releaseSection != null)
            _releaseSection.SetActive(hasWasm || hasJs);
        if (_wasmReleaseBtn != null)
            _wasmReleaseBtn.gameObject.SetActive(hasWasm);
        if (_jsReleaseBtn != null)
            _jsReleaseBtn.gameObject.SetActive(hasJs);
        if (_allReleaseBtn != null)
            _allReleaseBtn.gameObject.SetActive(hasWasm && hasJs);
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
        int wasmCount = _allocations.Count;
        long wasmSize = 0;
        foreach (var alloc in _allocations)
        {
            wasmSize += alloc.Length;
        }
        _allocations.Clear();
        GC.Collect();

        double jsFreed = OOMTester_ClearJSMemory();
        _jsAllocatedBytes = 0;

        _status = $"전체 해제: WASM {wasmSize / (1024 * 1024)}MB + WebView {jsFreed / (1024 * 1024):F0}MB";
        Debug.Log($"[OOMTester] 전체 해제 완료: WASM {wasmSize / (1024 * 1024)}MB + WebView {jsFreed / (1024 * 1024):F0}MB");
    }
}
