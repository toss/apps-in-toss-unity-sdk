/**
 * AIT data 응답 버퍼 레이어 (스텁)
 *
 * 현재는 아무것도 하지 않는 no-op 스텁이다. 실제 구현(data URL 응답을 정확한 Content-Length 로 재포장해
 * 로더가 버퍼를 한 번만 할당하게 하기, 소비한 data 구간 해제)은 후속 배치가 이 파일을 채운다.
 *
 * === 크로스 파일 계약 ===
 *  - window.__AIT_PERF (index.html head 맨 위 인라인 스크립트가 정의, 이 파일보다 먼저 실행됨)
 *      읽는 키: exactDataBody(기본 true), dataRawSize(압축 해제 크기 바이트, 없거나 <=0 이면 재포장 안 함),
 *      releaseConsumedData(기본 false), unityweb(true 면 아무것도 하지 않음).
 *      객체가 없거나 키가 없으면 위 기본값으로 동작한다(fail-open).
 *  - window.__AIT_DATABUF.configure(config)
 *      index.html 이 __AIT_PP.configure(config) 직후, createUnityInstance 이전에 호출한다. 던져도 부팅은 계속된다.
 *  - fetch 는 가장 바깥 래퍼로 얹는다(페이지 캐시/조기 fetch 인라인 스크립트가 먼저 설치한 래퍼 위).
 *      조기 fetch 가 붙인 markNonNet 마커는 그대로 옮겨 붙인다. dataCaching 경로에는 적용되지 않는다(무해).
 *
 * ⚠️ Unity 로더보다 먼저 로드되어야 한다 — index.html 의 body 초입(ait-playerprefs.js 다음)에서 로드된다.
 */
(function () {
    'use strict';

    window.__AIT_DATABUF = {
        // 스텁 식별용. 실제 구현이 들어오면 이 필드를 지운다.
        __stub: true,
        configure: function (config) { }
    };
})();
