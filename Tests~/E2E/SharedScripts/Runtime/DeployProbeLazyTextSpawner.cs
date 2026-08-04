#if AIT_E2E_DEPLOY_PROBE
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// 배포 프로브(deploy-probe) lazy 폰트 런타임 검증 스포너.
///
/// DeployProbeBuildRunner(Editor)가 빌드 시점에만 WebGL 스크립팅 디파인에
/// <c>AIT_E2E_DEPLOY_PROBE</c>를 추가하므로, 표준 E2E 빌드(디파인 없음)에서는 이 파일 전체가
/// #if 밖(빈 컴파일 유닛)이라 컴파일/실행 어느 쪽에도 영향이 없다.
///
/// 부팅(AfterSceneLoad) 후 8초 뒤 리플렉션만으로 Canvas + TextMeshProUGUI 를 생성해 일본어 문자열을
/// 표시한다. font 프로퍼티는 의도적으로 세팅하지 않는다 — TMP_Settings 의 기본 폰트(예:
/// LiberationSans SDF)에는 일본어 글리프가 없어 렌더 시 tofu(□)가 뜨는데, 이 tofu 조회가 TMP 의
/// 글로벌 fallback 체인 조회를 유발하고, 그 fallback 목록에 AITStreamingFont(런타임 재수화 컴포넌트,
/// Runtime/Helpers/AIT.StreamingFont.cs)가 lazy 확장 폰트를 온디맨드로 주입하는 것이 이 프로브가
/// 검증하려는 제품 경로다. TMP 미설치 환경(타입 미해석)에서는 경고 로그만 남기고 조용히 no-op 한다.
/// </summary>
public class DeployProbeLazyTextSpawner : MonoBehaviour
{
    private const float DelaySeconds = 8f;

    /// <summary>
    /// lazy 검증용 일본어 문구. LiberationSans SDF 등 라틴 전용 기본 폰트에는 이 글리프가 전혀
    /// 없으므로 tofu → fallback 조회 → lazy 주입 경로가 결정적으로 발화한다.
    /// </summary>
    private const string ProbeText = "遅延ロード検証: こんにちは世界";

    private const float ProbeFontSize = 36f;

    /// <summary>Playwright 동기화용 마커 로그(e2e-lazy-font.test.js 가 이 문자열을 기준점으로 대기한다).</summary>
    private const string SpawnedMarkerLog = "[DeployProbe] lazy 텍스트 표시";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    [Preserve]
    private static void Bootstrap()
    {
        var go = new GameObject("[DeployProbe] LazyTextSpawner");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<DeployProbeLazyTextSpawner>();
    }

    private void Start()
    {
        StartCoroutine(SpawnAfterDelay());
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(DelaySeconds);

        try
        {
            SpawnLazyProbeText();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DeployProbe] lazy 텍스트 스폰 예외(no-op): {e.Message}");
        }
    }

    private void SpawnLazyProbeText()
    {
        Type tmpUguiType = FindType("TMPro.TextMeshProUGUI");
        if (tmpUguiType == null)
        {
            Debug.LogWarning("[DeployProbe] TextMeshProUGUI 타입을 찾지 못해 lazy 텍스트 스폰을 건너뜁니다(TMP 미설치 — 정상 폴백 경로).");
            return;
        }

        // Canvas/RectTransform 은 UnityEngine 코어 모듈이라 UnityEngine.UI(uGUI 패키지) 참조 없이도
        // 사용 가능하다 — 이 asmdef 는 어떤 Unity 버전/uGUI 설치 여부와도 무관하게 컴파일돼야 한다.
        var canvasGo = new GameObject("[DeployProbe] LazyProbeCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var textGo = new GameObject("[DeployProbe] LazyProbeText", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600, 200);
        rt.anchoredPosition = Vector2.zero;

        var component = textGo.AddComponent(tmpUguiType);
        if (component == null)
        {
            Debug.LogWarning("[DeployProbe] TextMeshProUGUI 컴포넌트 부착 실패 — lazy 텍스트 스폰을 건너뜁니다.");
            return;
        }

        // font 프로퍼티는 의도적으로 미설정 — 클래스 주석 참조(tofu → fallback 조회 유발이 목적).
        var textProp = tmpUguiType.GetProperty("text");
        if (textProp == null || !textProp.CanWrite)
        {
            Debug.LogWarning("[DeployProbe] TMP_Text.text 프로퍼티를 찾지 못해 lazy 텍스트 스폰을 건너뜁니다.");
            return;
        }
        textProp.SetValue(component, ProbeText);

        var fontSizeProp = tmpUguiType.GetProperty("fontSize");
        if (fontSizeProp != null && fontSizeProp.CanWrite)
        {
            fontSizeProp.SetValue(component, ProbeFontSize);
        }

        Debug.Log(SpawnedMarkerLog);
    }

    private static Type FindType(string fullName)
    {
        var t = Type.GetType(fullName);
        if (t != null)
        {
            return t;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                t = asm.GetType(fullName);
                if (t != null)
                {
                    return t;
                }
            }
            catch
            {
                // 일부 어셈블리는 GetType 에서 예외 — 무시하고 계속.
            }
        }

        return null;
    }
}
#endif
