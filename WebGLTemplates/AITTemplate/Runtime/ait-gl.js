/**
 * AIT WebGL 컨텍스트 레이어 (스텁)
 *
 * 현재는 아무것도 하지 않는 no-op 스텁이다. 실제 구현(getContext 훅, requested/actual 속성 기록,
 * 조건부 antialias 해제, probe 컨텍스트 해제, context 손실 복구, DPR tier)은 후속 배치가 이 파일을 채운다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: glHook(기본 true), glDropAntialias(기본 false), glContextRecovery(기본 true).
 *      객체가 없거나 키가 없으면 위 기본값으로 동작한다(fail-open).
 *  - window.__AIT_GL.tierCap(): number
 *      부팅 시점 DPR 상한. 0 = 상한 없음, 그 외 2 / 1.5 / 1 중 하나. index.html 의 getOptimalDevicePixelRatio 가
 *      자동 DPR 과 min 을 취한다(명시적 DPR 설정이 있으면 호출하지 않음). 연속 크래시/컨텍스트 손실 기기에서
 *      단계를 낮춘다. 근거로 window.AITMemory.crashCount(ait-mem.js)를 읽을 수 있다(없으면 0 으로 취급).
 *  - window.__AIT_GL.bindContextLoss(canvas): boolean
 *      index.html 이 createUnityInstance 직전에 호출한다. true 를 돌려주면 이 레이어가 webglcontextlost 를
 *      전담하고 index.html 의 기본 핸들러는 설치되지 않는다. false 면 index.html 이 기존 핸들러를 쓴다.
 *  - window.AITPacing.setHint(name, value): 압력 신호를 ait-pacing.js 에 전달할 때 쓴다(이 파일은 선택적으로 호출).
 *
 * ⚠️ Unity 로더(컨텍스트 생성)보다 먼저 로드되어야 한다 — index.html 에서 "Unity Loader Script" 앞에 위치한다.
 */
(function () {
    'use strict';

    window.__AIT_GL = {
        // 스텁 식별용. 실제 구현이 들어오면 이 필드를 지운다.
        __stub: true,
        tierCap: function () { return 0; },
        bindContextLoss: function (canvas) { return false; }
    };
})();
