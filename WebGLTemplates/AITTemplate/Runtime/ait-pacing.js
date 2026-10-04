/**
 * AIT 라이프사이클·프레임 페이싱 레이어 (스텁)
 *
 * 현재는 아무것도 하지 않는 no-op 스텁이다. 실제 구현(hidden/pagehide/freeze 시 메인 루프 정지,
 * AudioContext·미디어 요소 일시정지/재개, 프레임레이트 상한, 적응형 30fps)은 후속 배치가 이 파일을 채운다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: mobileLifecycle(기본 true), frameRateCap(기본 60, 0 이면 상한 없음), adaptiveFrameRate(기본 false).
 *      객체가 없거나 키가 없으면 위 기본값으로 동작한다(fail-open).
 *  - window.__AIT_PACING.configure(config)
 *      index.html 이 __AIT_PP.configure(config) 직후, createUnityInstance 이전에 호출한다.
 *      Unity 로더 config(예: preRun, 모듈 훅)에 붙는 용도다. 던져도 부팅은 계속된다.
 *  - window.AITPacing (= window.__AIT_PACING 의 별칭, 같은 객체)
 *      setHint(name, value): 다른 모듈(ait-mem.js 의 메모리 압력, 배터리/발열 신호 등)이 압력 힌트를 넘기는 진입점.
 *      name 예: 'memory' | 'battery' | 'thermal'. value 의 형식은 name 별로 구현이 정한다. 알 수 없는 name 은 무시한다.
 *  - window.AITMemory.crashCount(ait-mem.js)나 window.__AIT_GL.tierCap()(ait-gl.js)을 읽을 필요가 있어도
 *      존재 여부를 먼저 확인한다(스크립트 로드 순서·부재에 의존하지 않는다).
 *
 * ⚠️ Unity 로더보다 먼저 로드되어야 한다 — index.html 의 body 초입(ait-playerprefs.js 다음)에서 로드된다.
 */
(function () {
    'use strict';

    var api = {
        // 스텁 식별용. 실제 구현이 들어오면 이 필드를 지운다.
        __stub: true,
        configure: function (config) { },
        setHint: function (name, value) { }
    };

    window.__AIT_PACING = api;
    window.AITPacing = api;
})();
