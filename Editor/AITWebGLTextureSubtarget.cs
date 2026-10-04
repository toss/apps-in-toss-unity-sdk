using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// WebGL 텍스처 서브타겟 자동 ASTC 처리기. <see cref="AITEditorScriptObject.webglTextureSubtargetAuto"/> 설정에 따라
    /// 빌드 중에만 서브타겟을 ASTC 로 바꾸고 빌드 후 원래 값으로 복원한다.
    /// iOS WebView 는 S3TC 를 지원하지 않아 DXT 텍스처가 RGBA8 로 풀리므로(메모리 4~8배, 로드 CPU 증가) 모바일 기본값을 ASTC 로 한다.
    /// 프로젝트가 ETC2/ASTC 를 직접 골랐다면 존중하고 건드리지 않는다.
    /// </summary>
    public static class AITWebGLTextureSubtarget
    {
        /// <summary>한 번의 적용 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class Handle
        {
            /// <summary>서브타겟을 실제로 바꿨는지.</summary>
            public bool Active;

            /// <summary>바꾸기 전 서브타겟 이름(복원용).</summary>
            public string OriginalName;
        }

        /// <summary>
        /// 순수 결정 함수. setting: -1 자동, 0 유지. currentName: 현재 서브타겟 이름(Generic/DXT/ETC2/ASTC).
        /// 자동이면서 현재가 Generic 또는 DXT(Unity 기본값)일 때만 true.
        /// </summary>
        public static bool ShouldSwitchToAstc(int setting, string currentName)
        {
            if (setting == 0)
            {
                return false;
            }

            return currentName == "Generic" || currentName == "DXT";
        }

        /// <summary>빌드 직전 호출. 항상 non-null 핸들을 반환한다.</summary>
        public static Handle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new Handle();
            if (config == null)
            {
                return handle;
            }

#if UNITY_2022_3_OR_NEWER
            try
            {
                var current = EditorUserBuildSettings.webGLBuildSubtarget;
                string currentName = current.ToString();

                if (config.webglTextureSubtargetAuto == 0)
                {
                    Debug.Log($"[AIT-TextureSubtarget] 자동 ASTC 비활성(설정 0) — 프로젝트 서브타겟 {currentName} 유지.");
                    return handle;
                }

                if (!ShouldSwitchToAstc(config.webglTextureSubtargetAuto, currentName))
                {
                    Debug.Log($"[AIT-TextureSubtarget] 프로젝트가 서브타겟 {currentName} 를 직접 지정해 그대로 둡니다.");
                    return handle;
                }

                handle.OriginalName = currentName;
                EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
                handle.Active = true;
                Debug.Log($"[AIT-TextureSubtarget] ✓ WebGL 텍스처 서브타겟 {currentName} → ASTC 로 변경(자동). " +
                    "iOS 는 DXT 를 지원하지 않아 RGBA8 로 풀려 메모리가 커집니다. 빌드 후 원래 값으로 복원합니다. " +
                    "끄려면 webglTextureSubtargetAuto = 0.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AIT-TextureSubtarget] 서브타겟 자동 변경 실패 — 프로젝트 설정으로 진행: {e.Message}");
            }
#else
            Debug.Log("[AIT-TextureSubtarget] Unity 2021.3 에서는 서브타겟 API 가 없어 자동 ASTC 를 건너뜁니다.");
#endif
            return handle;
        }

        /// <summary>빌드 후(성공/실패 무관) 호출. null/미활성 핸들은 no-op.</summary>
        public static void RestoreForBuild(Handle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

#if UNITY_2022_3_OR_NEWER
            try
            {
                if (System.Enum.TryParse(handle.OriginalName, out WebGLTextureSubtarget original))
                {
                    EditorUserBuildSettings.webGLBuildSubtarget = original;
                    Debug.Log($"[AIT-TextureSubtarget] 서브타겟을 {handle.OriginalName} 로 복원했습니다.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AIT-TextureSubtarget] 서브타겟 복원 실패 — Project Settings 에서 {handle.OriginalName} 로 되돌려 주세요: {e.Message}");
            }
#endif
            handle.Active = false;
        }
    }
}
