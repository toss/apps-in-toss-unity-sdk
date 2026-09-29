/**
 * AppsInToss-Core.jslib
 *
 * AITCore 인프라 수동 API (verbose 로깅 스위치 등 - web-framework 외부)
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    // AITCore.VerboseLogging 프로퍼티 setter에서 호출되어, C# 스위치를 JS 브릿지에 전파합니다.
    // 생성된 jslib의 정보성 console.log는 모두 window.__AIT_VERBOSE를 따릅니다
    // (console.error/console.warn 등 경고·에러 로그는 이 스위치와 무관하게 항상 출력됩니다).
    __AITSetVerboseLogging: function(verbose) {
        window.__AIT_VERBOSE = verbose !== 0;
    },
});
