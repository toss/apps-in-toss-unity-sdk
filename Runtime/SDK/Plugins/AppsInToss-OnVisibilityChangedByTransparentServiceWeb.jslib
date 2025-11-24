/**
 * AppsInToss-OnVisibilityChangedByTransparentServiceWeb.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-24T10:39:15.951Z
 * Category: OnVisibilityChangedByTransparentServiceWeb
 */

mergeInto(LibraryManager.library, {
    onVisibilityChangedByTransparentServiceWeb: function(eventParams) {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.onVisibilityChangedByTransparentServiceWeb) {
            return window.AppsInToss.onVisibilityChangedByTransparentServiceWeb(eventParams);
        }
        console.warn('window.AppsInToss.onVisibilityChangedByTransparentServiceWeb not available');
        return null;
    },

});
