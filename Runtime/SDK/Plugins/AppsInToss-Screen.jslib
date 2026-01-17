/**
 * AppsInToss-Screen.jslib
 *
 * 화면 관련 수동 API (브라우저 API)
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

var __ait_last_dpr = null;

mergeInto(LibraryManager.library, {
    __GetDevicePixelRatio_Internal: function() {
        // Unity config에서 설정된 devicePixelRatio 사용
        // index.html에서 config.devicePixelRatio = getOptimalDevicePixelRatio()로 설정됨
        // unityInstance.Module.devicePixelRatio에 저장됨
        var dpr = 1;

        // 1. Unity Instance에서 설정된 값 확인
        if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.Module) {
            dpr = unityInstance.Module.devicePixelRatio || window.devicePixelRatio || 1;
        }
        // 2. 글로벌 unityConfig에서 확인 (초기화 전)
        else if (typeof unityConfig !== 'undefined' && unityConfig) {
            dpr = unityConfig.devicePixelRatio || window.devicePixelRatio || 1;
        }
        // 3. 브라우저 기본값 사용
        else {
            dpr = window.devicePixelRatio || 1;
        }

        // dpr이 변경되었을 때만 로깅
        if (__ait_last_dpr !== dpr) {
            console.log('[AIT jslib] GetDevicePixelRatio changed:', __ait_last_dpr, '->', dpr);
            __ait_last_dpr = dpr;
        }

        return dpr;
    },
});
