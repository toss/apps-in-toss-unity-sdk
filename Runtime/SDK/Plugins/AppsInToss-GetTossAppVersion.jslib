/**
 * AppsInToss-GetTossAppVersion.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-26T13:03:05.260Z
 * Category: GetTossAppVersion
 */

mergeInto(LibraryManager.library, {
    __getTossAppVersion_Internal: function() {
        // @apps-in-toss/web-framework 직접 호출 (bridge-core 패턴)
        // bridge-core와 동일한 에러 메시지 사용
        // Constant Bridge: bridge-core와 동일한 에러 처리
        var constantHandlerMap = window.__CONSTANT_HANDLER_MAP;
        if (constantHandlerMap && 'getTossAppVersion' in constantHandlerMap) {
            return constantHandlerMap['getTossAppVersion'];
        }
        throw new Error('getTossAppVersion is not a constant handler');
    },

});
