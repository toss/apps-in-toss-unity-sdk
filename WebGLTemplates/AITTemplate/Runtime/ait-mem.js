/**
 * AIT 메모리 텔레메트리 레이어 (스텁)
 *
 * 현재는 최소 계약 객체만 노출하는 스텁이다. 실제 구현(WebAssembly.Memory.prototype.grow 래퍼로 grow 횟수·ms·크기·OOM 기록,
 * 이전 세션 비정상 종료 감지, ait:memory 이벤트)은 후속 배치가 이 파일을 채운다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: memoryTelemetry(기본 true). 객체가 없거나 키가 없으면 기본값으로 동작한다(fail-open).
 *  - window.AITMemory.crashCount: number
 *      이전 세션(들)이 정상 종료 신호 없이 끝난 횟수. 0 이상의 정수. ait-gl.js 의 tierCap() 이 2 이상이면
 *      DPR 상한을 낮추는 근거로 읽는다. 읽는 쪽은 객체/필드가 없으면 0 으로 취급해야 한다.
 *      memoryTelemetry 가 꺼져 있으면 항상 0.
 *  - 래퍼 안(wasm 호출 스택 위)에서는 SendMessage·무거운 작업을 하지 않는다. setTimeout 으로 미룬다.
 *
 * ⚠️ wasm 인스턴스화보다 먼저 로드되어야 한다 — index.html 의 head, 페이지 캐시/조기 fetch 스크립트 뒤에서 동기 로드된다.
 */
(function () {
    'use strict';

    var flags = (typeof window.__AIT_PERF === 'object' && window.__AIT_PERF) || {};

    window.AITMemory = {
        // 스텁 식별용. 실제 구현이 들어오면 이 필드를 지운다.
        __stub: true,
        enabled: flags.memoryTelemetry !== false,
        crashCount: 0
    };
})();
