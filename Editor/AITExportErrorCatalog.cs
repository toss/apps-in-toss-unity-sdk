// 스위치 본문을 AITConvertCore에서 그대로 옮기기 위한 별칭 (AITExportError.XXX 표기 유지)
using AITExportError = AppsInToss.AITConvertCore.AITExportError;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 에러 코드 → 사용자 메시지 카탈로그 (AITConvertCore에서 분리).
    /// AITConvertCore의 공개 에러 메시지 API(GetErrorMessage/GetErrorShortReason/GetErrorCause)가
    /// 이 클래스로 위임하며 동작은 기존과 동일하다.
    /// </summary>
    internal static class AITExportErrorCatalog
    {
        /// <summary>
        /// 에러 코드를 사용자 친화적 메시지로 변환
        /// </summary>
        internal static string GetErrorMessage(AITConvertCore.AITExportError error)
        {
            switch (error)
            {
                case AITExportError.SUCCEED:
                    return "성공";

                case AITExportError.NODE_NOT_FOUND:
                    return "Node.js를 찾을 수 없습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. https://nodejs.org 에서 Node.js 설치\n" +
                           "2. Unity Editor 재시작\n" +
                           "3. 터미널에서 'node --version' 확인";

                case AITExportError.BUILD_WEBGL_FAILED:
                    return "WebGL 빌드에 실패했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Unity Console 창에서 에러 메시지 확인\n" +
                           "2. WebGL Build Support가 설치되어 있는지 확인\n" +
                           "3. 프로젝트에 빌드 오류가 없는지 확인\n" +
                           "4. File > Build Settings > WebGL에서 직접 빌드 시도";

                case AITExportError.INVALID_APP_CONFIG:
                    return "앱 설정이 올바르지 않습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Apps in Toss > Build & Deploy Window 열기\n" +
                           "2. 설정 섹션에서 아이콘 URL 입력 (필수)\n" +
                           "3. 앱 ID, 버전 등 기본 정보 확인";

                case AITExportError.NETWORK_ERROR:
                    return "네트워크 오류가 발생했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 인터넷 연결 확인\n" +
                           "2. npm 레지스트리 접속 가능 여부 확인\n" +
                           "3. 방화벽 또는 프록시 설정 확인";

                case AITExportError.CANCELLED:
                    return "사용자에 의해 빌드가 취소되었습니다.";

                case AITExportError.FAIL_NPM_BUILD:
                    return "pnpm 빌드에 실패했습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. Unity Console 창에서 에러 메시지 확인\n" +
                           "2. ait-build 폴더에서 직접 pnpm install 시도\n" +
                           "3. package.json 파일이 올바른지 확인\n" +
                           "4. ~/.ait-unity-sdk/nodejs 폴더를 삭제 후 다시 빌드 시도";

                case AITExportError.BUILD_FOLDER_MISSING:
                    return "WebGL 빌드의 Build 폴더를 찾을 수 없습니다.\n\n" +
                           "WebGL 빌드가 실행되지 않았거나 빌드 결과물이 삭제되었습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 'Build & Package' 메뉴로 전체 빌드를 실행하세요.\n" +
                           "2. webgl/ 폴더가 존재하는지 확인하세요.";

                case AITExportError.REQUIRED_FILE_MISSING:
                    return "WebGL 빌드 필수 파일이 누락되었습니다.\n\n" +
                           "loader.js, data, framework.js, wasm 파일 중 일부가 없습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n" +
                           "2. Unity Console에서 빌드 에러를 확인하세요.";

                case AITExportError.INDEX_HTML_MISSING:
                    return "WebGL 빌드의 index.html을 찾을 수 없습니다.\n\n" +
                           "WebGL 템플릿이 올바르게 설정되지 않았을 수 있습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. AIT > Clean 메뉴로 빌드 폴더 삭제 후 재빌드\n" +
                           "2. 'Clean Build' 옵션 활성화 후 재빌드";

                case AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED:
                    return "빌드 산출물(index.html 또는 apps-in-toss.config.ts/granite.config.ts)의 필수 플레이스홀더가 치환되지 않았습니다.\n\n" +
                           "이 상태로 배포하면 'createUnityInstance is not defined' 에러가 발생하거나 앱 설정(bundle.json)이 깨집니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n" +
                           "2. AIT > Clean 메뉴로 빌드 폴더 삭제 후 재빌드하세요.";

                case AITExportError.DIST_FOLDER_MISSING:
                    return "granite build가 완료되었으나 dist 폴더가 생성되지 않았습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. ait-build/ 폴더에서 수동으로 'npm run build' 실행하여 오류 확인\n" +
                           "2. granite.config.ts의 플레이스홀더가 올바르게 치환되었는지 확인\n" +
                           "3. AIT > Clean 메뉴 실행 후 Clean Build 재시도";

                case AITExportError.AIT_FILE_MISSING:
                    return "dist/ 폴더에 .ait 파일이 생성되지 않았습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. ait-build/dist/ 폴더의 내용을 확인하세요\n" +
                           "2. granite.config.ts 설정을 확인하세요\n" +
                           "3. AIT > Clean 메뉴 실행 후 Clean Build 재시도";

                default:
                    return $"알 수 없는 오류가 발생했습니다. (코드: {error})";
            }
        }

        /// <summary>
        /// 다이얼로그 제목에 붙일 짧은 사유 라벨을 반환합니다.
        /// 예: "빌드 실패 ({shortReason})" 형태로 조합됩니다.
        /// </summary>
        internal static string GetErrorShortReason(AITConvertCore.AITExportError error)
        {
            switch (error)
            {
                case AITExportError.SUCCEED: return "성공";
                case AITExportError.NODE_NOT_FOUND: return "Node.js 없음";
                case AITExportError.BUILD_WEBGL_FAILED: return "WebGL 빌드 오류";
                case AITExportError.INVALID_APP_CONFIG: return "앱 설정 오류";
                case AITExportError.NETWORK_ERROR: return "네트워크 오류";
                case AITExportError.CANCELLED: return "사용자 취소";
                case AITExportError.FAIL_NPM_BUILD: return "pnpm 빌드 오류";
                case AITExportError.BUILD_FOLDER_MISSING: return "Build 폴더 없음";
                case AITExportError.REQUIRED_FILE_MISSING: return "필수 파일 누락";
                case AITExportError.INDEX_HTML_MISSING: return "index.html 없음";
                case AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED: return "플레이스홀더 미치환";
                case AITExportError.DIST_FOLDER_MISSING: return "dist 폴더 없음";
                case AITExportError.AIT_FILE_MISSING: return ".ait 파일 없음";
                default: return error.ToString();
            }
        }

        /// <summary>
        /// 다이얼로그 본문에 사용할 원인 한 단락을 반환합니다. 해결 방법은 포함하지 않습니다.
        /// </summary>
        internal static string GetErrorCause(AITConvertCore.AITExportError error)
        {
            switch (error)
            {
                case AITExportError.SUCCEED:
                    return "성공";
                case AITExportError.NODE_NOT_FOUND:
                    return "SDK 가 사용할 Node.js 실행 파일을 찾을 수 없습니다.\n" +
                           "내장 Node 바이너리(~/.ait-unity-sdk/nodejs) 또는 시스템 PATH에서 node를 찾지 못했습니다.";
                case AITExportError.BUILD_WEBGL_FAILED:
                    return "Unity WebGL 빌드 중 오류가 발생했습니다.\n" +
                           "Console 창에서 컴파일 오류나 스택 트레이스를 확인해주세요.";
                case AITExportError.INVALID_APP_CONFIG:
                    return "앱 설정이 올바르지 않거나 필수 필드(App ID, 아이콘 URL 등)가 누락되었습니다.\n" +
                           "AIT > Configuration 창에서 설정을 확인해주세요.";
                case AITExportError.NETWORK_ERROR:
                    return "빌드 과정에서 네트워크 요청에 실패했습니다.\n" +
                           "인터넷 연결 또는 프록시/방화벽 설정을 확인해주세요.";
                case AITExportError.CANCELLED:
                    return "사용자에 의해 빌드가 취소되었습니다.";
                case AITExportError.FAIL_NPM_BUILD:
                    return "ait-build 디렉터리의 pnpm/granite 빌드 단계에서 오류가 발생했습니다.\n" +
                           "Console 창에서 빌드 로그를 확인해주세요.";
                case AITExportError.BUILD_FOLDER_MISSING:
                    return "WebGL 빌드 결과물의 Build 폴더가 존재하지 않습니다.\n" +
                           "WebGL 빌드가 완료되지 않았거나 결과물이 삭제되었을 수 있습니다.";
                case AITExportError.REQUIRED_FILE_MISSING:
                    return "WebGL 빌드 결과물(loader.js, data, framework.js, wasm) 중 일부가 누락되었습니다.\n" +
                           "Clean Build 옵션을 켜고 다시 빌드해보세요.";
                case AITExportError.INDEX_HTML_MISSING:
                    return "WebGL 빌드의 index.html 이 생성되지 않았습니다.\n" +
                           "WebGL 템플릿(AITTemplate) 설정이 올바른지 확인해주세요.";
                case AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED:
                    return "빌드 산출물(index.html 또는 apps-in-toss.config.ts/granite.config.ts)의 필수 플레이스홀더가 치환되지 않은 채 저장되었습니다.\n" +
                           "이 상태로 배포하면 런타임에 'createUnityInstance is not defined' 오류가 발생하거나 앱 설정(bundle.json)이 깨집니다.\n" +
                           "Clean Build 옵션으로 재빌드해보세요.";
                case AITExportError.DIST_FOLDER_MISSING:
                    return "granite 빌드가 완료되었지만 dist 폴더가 생성되지 않았습니다.\n" +
                           "granite.config.ts 의 플레이스홀더가 올바르게 치환되었는지 확인해주세요.";
                case AITExportError.AIT_FILE_MISSING:
                    return "dist/ 폴더에 .ait 파일이 생성되지 않았습니다.\n" +
                           "granite 빌드 설정을 확인한 뒤 Clean Build 로 재시도해주세요.";
                default:
                    return $"알 수 없는 오류 (코드: {error}).";
            }
        }
    }
}
