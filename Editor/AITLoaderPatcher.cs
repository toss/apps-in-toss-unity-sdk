// -----------------------------------------------------------------------
// AITLoaderPatcher.cs - 빌드 후 Unity loader(*.loader.js) 텍스트 패치
//
// 현재는 호출 지점만 잇는 no-op 스텁이다. 실제 패치(소비한 data 버퍼 해제용 훅 등)는 후속 배치가 채운다.
// 호출 지점·구현 계약은 AITFrameworkPatcher 와 동일하다(제자리 패치, .aitpN rename + renames 기록,
// 그룹별 앵커 개수 검증, node --check 통과 시에만 채택, fail-open).
// 호출 지점: WebGLBuildCopier.ApplyBuildPatches (AITFrameworkPatcher 다음).
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace AppsInToss.Editor
{
    internal static class AITLoaderPatcher
    {
        /// <summary>
        /// loader 파일에 패치를 적용한다.
        /// </summary>
        /// <param name="buildDir">Unity Build/ 폴더 경로.</param>
        /// <param name="config">현재 빌드 설정.</param>
        /// <param name="renames">rename 한 파일의 옛 이름 → 새 이름을 기록할 맵(null 허용).</param>
        /// <returns>패치를 적용한 파일 수. 스텁은 항상 0.</returns>
        internal static int Apply(string buildDir, AITEditorScriptObject config, IDictionary<string, string> renames = null)
        {
            return 0;
        }
    }
}
