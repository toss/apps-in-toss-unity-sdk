// -----------------------------------------------------------------------
// AITFrameworkPatcher.cs - 빌드 후 Unity framework(*.framework.js[.br]) 텍스트 패치
//
// 현재는 호출 지점만 잇는 no-op 스텁이다. 실제 패치(오디오 강제 압축 재생 등)는 후속 배치가 채운다.
// 호출 지점: WebGLBuildCopier.ApplyBuildPatches — Unity 산출물이 buildSrc 에 놓인 직후,
//           brotli 재압축·페이지 캐시·warm manifest 산출보다 앞.
//
// === 구현 계약 (후속 배치가 지킬 것) ===
//  - buildDir 은 Unity 가 만든 Build/ 폴더(평면 구조)다. 패치는 이 폴더 안에서 제자리(in-place)로 한다.
//    Unity 원본 소스 텍스트를 저장소에 복사해 두지 않는다(공개 저장소). 앵커는 짧은 스니펫만 쓴다.
//  - 패치한 파일은 AITPatchedFileNaming.RenameInDirectory 로 ".aitpN" 접미사 이름으로 옮기고,
//    옛 이름 → 새 이름(파일명만, 디렉터리 없이)을 renames 에 기록한다. 호출부가 index.html·early-fetch·
//    page cache·warm manifest 가 쓰는 파일명 변수를 이 맵으로 갱신한다.
//  - 패치 그룹마다 앵커 일치 개수가 정확히 1 일 때만 적용하고, 어긋나면 그룹 단위로 건너뛰며 경고를 남긴다.
//  - 결과가 `node --check` 를 통과할 때만 채택한다. 어떤 실패도 예외를 밖으로 던지지 않는다(fail-open).
//  - .br 이면 풀어서 패치한 뒤 다시 압축한다. 호출부가 Decompression Fallback(.unityweb) 빌드는 미리 걸러 준다.
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace AppsInToss.Editor
{
    internal static class AITFrameworkPatcher
    {
        /// <summary>
        /// framework 파일에 패치를 적용한다.
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
