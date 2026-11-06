using UnityEngine;

/// <summary>
/// 배포 프로브(deploy-probe) 씬 전용 런타임 컴포넌트.
/// <see cref="Start"/> 시점에 텍스트 컴포넌트의 "text" 프로퍼티에 한글 리터럴을 주입한다.
///
/// TMP(TextMeshProUGUI)/레거시(UnityEngine.UI.Text) 어느 쪽이 붙어있어도 동일하게 동작하도록
/// 컴파일 타임에 TMPro 네임스페이스를 참조하지 않고, "text" 프로퍼티를 리플렉션으로 설정한다.
/// (TMP_Text.text 와 UnityEngine.UI.Text.text 는 이름이 동일해 리플렉션 한 벌로 양쪽을 커버한다.)
/// 이렇게 하면 SharedScripts asmdef 가 Unity.TextMeshPro 를 참조하지 않아도(TMP 미설치 프로젝트에서도)
/// 이 스크립트는 항상 컴파일된다 — DeployProbeBuildRunner(Editor)가 TMP 설치 여부에 따라
/// TextMeshProUGUI 또는 레거시 Text 중 무엇을 부착할지 결정하고, 이 컴포넌트는 그 결과만 받는다.
///
/// 한글 리터럴이 이 .cs 소스에 있어야 AITFontUsedCharScanner(C# 소스 문자 스캔, Assets/ 트리 대상)가
/// 검출할 수 있다는 것이 원안 의도지만, 이 스크립트는 SharedScripts 패키지(Packages/ 하위)에 위치해
/// Application.dataPath(Assets/) 스캔 범위 밖이라 실제로는 스캔되지 않는다. 다만 한글 음절/자모
/// (U+AC00-D7A3, U+1100-11FF)는 AITFontUsedCharScanner.BaselineRanges 에 스캔 결과와 무관하게 항상
/// 보존되므로, 폰트 subset 정확성에는 영향이 없다(레거시 폴백 fallback text 도 동일하게 보호됨).
/// </summary>
public class DeployProbeTextSetter : MonoBehaviour
{
    [Tooltip("\"text\" 프로퍼티를 가진 텍스트 컴포넌트(TextMeshProUGUI 또는 UnityEngine.UI.Text)")]
    public Component textComponent;

    /// <summary>
    /// 배포 프로브용 한글 텍스트. 텍스처·오디오·폰트 스트리밍 레버 검증 목적의 합성 문구이며
    /// 특정 게임/서비스와 무관하다.
    /// </summary>
    private const string ProbeKoreanText = "배포 프로브: 텍스처·오디오·폰트 스트리밍 검증용 한글 텍스트입니다.";

    private void Start()
    {
        if (textComponent == null)
        {
            Debug.LogWarning("[DeployProbe] textComponent 미설정 — 한글 텍스트 주입을 건너뜁니다.");
            return;
        }

        var textProperty = textComponent.GetType().GetProperty("text");
        if (textProperty == null || !textProperty.CanWrite)
        {
            Debug.LogWarning($"[DeployProbe] {textComponent.GetType().Name} 에 쓰기 가능한 text 프로퍼티가 없어 건너뜁니다.");
            return;
        }

        textProperty.SetValue(textComponent, ProbeKoreanText);
    }
}
