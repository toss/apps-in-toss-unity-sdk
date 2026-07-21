// Player loop freeze 진단 프로브 (techchat 4377 검증용) — round 2
//
// 결제 native 오버레이 구간에서 "무엇이" 멈추는지를 세 개의 독립 하트비트로 분리 계측한다.
//
//   (a) rAF    — requestAnimationFrame. Unity WebGL player loop를 실제로 구동하는 콜백.
//                "rAF가 멈추는가?"에 직접 답한다.
//   (b) timer  — setInterval(250ms). 타이머 큐가 도는지 = JS-side 타임아웃(레버 A)이 발화 가능한지.
//   (c) visibility — visibilitychange 기록. rAF 정지의 원인이 표준 hidden 처리인지 확인.
//
// round 2 실측 결과 (2026-07-21, iOS Toss 5.269.0):
//   rAF 갭 27.52s ≡ C# 프레임 갭 27.44s ≡ hidden→visible 27.51s (세 값 일치)
//   timer는 28s 동안 22회 발화(최대 갭 11.6s) — throttle될 뿐 죽지 않는다.
//   → 결론: 웹뷰 suspend가 아니라 표준 hidden 처리. rAF만 스펙대로 멈춘다.
//     레버 A(JS 타임아웃)는 작동하되 최대 ~12s 지터를 감수해야 한다.
//
// MessageChannel 매크로태스크 프로브는 제거했다: (1) ping-pong이 상한을 895ms 만에
// 소진해 정작 프리즈 구간 데이터가 없었고, (2) timer가 이미 이벤트 루프 생존을
// 증명하므로 답할 질문이 남지 않았으며, (3) 상시 spin이 측정 자체를 왜곡한다.
//
// 모든 틱은 메모리에만 쌓고 복귀 후 한 번에 리포트한다 (정지 중 로그 출력은 유실 위험).
mergeInto(LibraryManager.library, {
    PLP_StartJsProbe: function() {
        // 재무장 시 이전 프로브 정리 (중복 하트비트 방지)
        if (window.__plpTimer) { clearInterval(window.__plpTimer); window.__plpTimer = null; }
        if (window.__plpRafId) { cancelAnimationFrame(window.__plpRafId); window.__plpRafId = null; }
        if (window.__plpVisHandler) {
            document.removeEventListener('visibilitychange', window.__plpVisHandler);
            window.__plpVisHandler = null;
        }

        var CAP = 20000;  // 틱 상한 — 방치돼도 메모리 무한 증가 방지 (rAF 60fps 기준 약 5.5분)
        var now = Date.now();
        window.__plpArmedAt = now;
        window.__plpRafTicks = [now];
        window.__plpTimerTicks = [now];
        window.__plpVisibility = [];

        // (a) rAF 하트비트 — Unity player loop와 동일한 구동원
        var rafTick = function() {
            if (window.__plpRafTicks.length < CAP) {
                window.__plpRafTicks.push(Date.now());
            }
            window.__plpRafId = requestAnimationFrame(rafTick);
        };
        window.__plpRafId = requestAnimationFrame(rafTick);

        // (b) 타이머 큐 하트비트
        window.__plpTimer = setInterval(function() {
            if (window.__plpTimerTicks.length < CAP) {
                window.__plpTimerTicks.push(Date.now());
            }
        }, 250);

        // (c) 웹뷰가 실제로 hidden 처리되는지 — round 2에서 rAF 정지의 원인으로 확인된 신호
        window.__plpVisHandler = function() {
            window.__plpVisibility.push(document.visibilityState + '@' + Date.now());
        };
        document.addEventListener('visibilitychange', window.__plpVisHandler);
    },

    PLP_GetJsReport: function() {
        // 하트비트 정지 — 리포트 시점 이후의 틱은 의미 없다
        if (window.__plpTimer) { clearInterval(window.__plpTimer); window.__plpTimer = null; }
        if (window.__plpRafId) { cancelAnimationFrame(window.__plpRafId); window.__plpRafId = null; }
        if (window.__plpVisHandler) {
            document.removeEventListener('visibilitychange', window.__plpVisHandler);
            window.__plpVisHandler = null;
        }

        var summarize = function(ticks) {
            ticks = ticks || [];
            var maxGap = 0;
            var maxGapAt = 0;
            for (var i = 1; i < ticks.length; i++) {
                var gap = ticks[i] - ticks[i - 1];
                if (gap > maxGap) { maxGap = gap; maxGapAt = ticks[i - 1]; }
            }
            return {
                count: ticks.length,
                maxGapMs: maxGap,
                maxGapAtEpochMs: maxGapAt,
                spanMs: ticks.length > 1 ? ticks[ticks.length - 1] - ticks[0] : 0
            };
        };

        var report = JSON.stringify({
            raf: summarize(window.__plpRafTicks),
            timer: summarize(window.__plpTimerTicks),
            visibility: window.__plpVisibility || [],
            armedAtEpochMs: window.__plpArmedAt || 0
        });

        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    }
});
