using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// External 디버그 심볼로 Release 빌드할 때 Unity 로더 전처리가 실패하는 에디터에서 심볼을 끄고 다시 빌드한다.
    ///
    /// 배경: 6000.0.85f1·6000.3.27f1 의 Preprocess.js 는 해시 파일명(SYMBOLS_FILENAME 등)을 locals.argumentFiles 로
    /// 받도록 바뀌었는데, Release + External 빌드에서 이 인자가 빠져 UnityLoader.js 의 "#if SYMBOLS_FILENAME" 이
    /// ReferenceError 로 끝난다(심볼 파일을 만들지 않는 Development 빌드는 통과). 같은 계열 이전 패치(6000.0.62f1)는 통과.
    /// 어느 패치 범위가 영향을 받는지 미리 알 수 없어 버전 목록 대신 실패 서명으로 판정하고, 한 번 실패한 에디터
    /// 버전은 EditorPrefs 에 기억해 다음 빌드부터 처음부터 Off 로 빌드한다(링크 한 번 낭비를 반복하지 않게).
    ///
    /// Off 를 쓰는 이유: Embedded 는 심볼을 wasm 안에 넣어 다운로드 크기가 늘어난다. 외부 심볼 파일은 선택 산출물
    /// (메모리 예산 리포트의 코드 Top 20)이라 빠져도 빌드 결과는 같다. 바꾼 값은 PlayerSettingsSnapshot 이 원복한다.
    /// </summary>
    internal static class AITDebugSymbolFallback
    {
        internal const string Signature = "SYMBOLS_FILENAME is not defined";

        private const string PrefsKeyPrefix = "AIT.DebugSymbolsOffRequired.";

        /// <summary>빌드 실패 텍스트가 로더 전처리의 SYMBOLS_FILENAME 누락인지(순수 함수).</summary>
        internal static bool IsSignature(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains(Signature);
        }

        /// <summary>지금 설정으로 실패하면 심볼을 꺼서 재시도할 수 있는지(External 일 때만).</summary>
        internal static bool CanFallback()
        {
#if UNITY_2022_3_OR_NEWER
            return PlayerSettings.WebGL.debugSymbolMode == WebGLDebugSymbolMode.External;
#else
            return false;
#endif
        }

        /// <summary>이 에디터 버전에서 이미 실패를 확인했으면 External 을 Off 로 바꾼다. 빌드 프로필 적용 직후 호출.</summary>
        internal static void ApplyRemembered()
        {
#if UNITY_2022_3_OR_NEWER
            if (!CanFallback() || !EditorPrefs.GetBool(PrefsKeyPrefix + Application.unityVersion, false))
                return;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            Debug.Log($"[AIT] 디버그 심볼: External → Off (Unity {Application.unityVersion} 은 External Release 빌드의 " +
                      "로더 전처리가 실패한 기록이 있음 — 외부 심볼 파일 없이 빌드합니다)");
#endif
        }

        /// <summary>심볼을 끄고 이 에디터 버전에 기록한다. 재시도 직전에 호출.</summary>
        internal static void SwitchOffAndRemember()
        {
#if UNITY_2022_3_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            EditorPrefs.SetBool(PrefsKeyPrefix + Application.unityVersion, true);
#endif
        }
    }
}
