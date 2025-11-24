/**
 * AppsInToss-환경 확인.jslib
 *
 * 이 파일은 자동 생성되었습니다.
 * tools/generate-unity-sdk로 생성됨
 * 수정하지 마세요. 변경사항은 재생성 시 손실됩니다.
 *
 * web-framework tag: next
 * 생성 시각: 2025-11-24T10:39:15.951Z
 * Category: 환경 확인
 */

mergeInto(LibraryManager.library, {
    getDeviceId: function() {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getDeviceId) {
            return window.AppsInToss.getDeviceId();
        }
        console.warn('window.AppsInToss.getDeviceId not available');
        return null;
    },

    getOperationalEnvironment: function() {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getOperationalEnvironment) {
            return window.AppsInToss.getOperationalEnvironment();
        }
        console.warn('window.AppsInToss.getOperationalEnvironment not available');
        return null;
    },

    getPlatformOS: function() {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getPlatformOS) {
            return window.AppsInToss.getPlatformOS();
        }
        console.warn('window.AppsInToss.getPlatformOS not available');
        return null;
    },

    getSchemeUri: function() {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getSchemeUri) {
            return window.AppsInToss.getSchemeUri();
        }
        console.warn('window.AppsInToss.getSchemeUri not available');
        return null;
    },

    getTossAppVersion: function() {
        // 동기 반환 함수
        if (typeof window.AppsInToss !== 'undefined' && window.AppsInToss.getTossAppVersion) {
            return window.AppsInToss.getTossAppVersion();
        }
        console.warn('window.AppsInToss.getTossAppVersion not available');
        return null;
    },

});
