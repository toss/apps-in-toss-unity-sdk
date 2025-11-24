/**
 * AppsInToss-위치 정보.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-24T10:39:15.951Z
 * Category: 위치 정보
 */

mergeInto(LibraryManager.library, {
    startUpdateLocation: function(eventParams) {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.startUpdateLocation) {
            return window.AppsInToss.startUpdateLocation(JSON.parse(UTF8ToString(eventParams)));
        }
        console.warn('window.AppsInToss.startUpdateLocation not available');
        return null;
    },

});
