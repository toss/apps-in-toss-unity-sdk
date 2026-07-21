// Player loop freeze 진단 프로브 (techchat 4377 검증용) — round 2
//
// 결제 native 오버레이 구간에서 "무엇이" 멈추는지를 세 개의 독립 하트비트로 분리 계측한다.
//
//   (a) rAF    — requestAnimationFrame. Unity WebGL player loop를 실제로 구동하는 콜백.
//                "rAF가 멈추는가?"에 직접 답한다.
//   (b) timer  — setInterval(250ms). 타이머 큐가 도는지 = JS-side 타임아웃(레버 A)이 발화 가능한지.
//   (c) task   — MessageChannel 매크로태스크. 타이머만 clamp된 건지 이벤트 루프 자체가
//                멈춘 건지 구분한다 (브라우저는 background에서 타이머만 조이기도 한다).
//
// C# Update() 하트비트와 교차하면 시나리오가 확정된다:
//   1) rAF 멈춤 + timer 생존  → rAF만 정지. 템플릿 keep-alive 워크어라운드 가능,
//                               레버 A가 제때 발화한다.
//   2) rAF 멈춤 + timer 멈춤  → 웹뷰 전체 suspend. 네이티브 수정 필요,
//                               레버 A는 복귀 시점 지연 발화에 그친다.
//   3) 둘 다 생존             → player loop 정지 재현 안 됨. 다른 원인.
//
// 모든 틱은 메모리에만 쌓고 복귀 후 한 번에 리포트한다 (정지 중 로그 출력은 유실 위험).
mergeInto(LibraryManager.library, {
    PLP_StartJsProbe: function() {
        // 재무장 시 이전 프로브 정리 (중복 하트비트 방지)
        if (window.__plpTimer) { clearInterval(window.__plpTimer); window.__plpTimer = null; }
        if (window.__plpRafId) { cancelAnimationFrame(window.__plpRafId); window.__plpRafId = null; }
        if (window.__plpChannel) { window.__plpChannel.port1.onmessage = null; window.__plpChannel = null; }
        if (window.__plpVisHandler) {
            document.removeEventListener('visibilitychange', window.__plpVisHandler);
            window.__plpVisHandler = null;
        }

        var CAP = 20000;  // 틱 상한 — 방치돼도 메모리 무한 증가 방지 (rAF 60fps 기준 약 5.5분)
        var now = Date.now();
        window.__plpArmedAt = now;
        window.__plpRafTicks = [now];
        window.__plpTimerTicks = [now];
        window.__plpTaskTicks = [now];
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

        // (c) 매크로태스크 하트비트 — 타이머 clamp와 이벤트 루프 정지를 구분
        try {
            var ch = new MessageChannel();
            ch.port1.onmessage = function() {
                if (window.__plpTaskTicks.length < CAP) {
                    window.__plpTaskTicks.push(Date.now());
                    ch.port2.postMessage(0);
                }
            };
            window.__plpChannel = ch;
            ch.port2.postMessage(0);
        } catch (e) {
            // MessageChannel 미지원 환경 — (a)(b)만으로도 판별 가능하므로 무시
        }

        // 웹뷰가 실제로 hidden 처리되는지 (rAF 정지의 표준적 원인) 기록
        window.__plpVisHandler = function() {
            window.__plpVisibility.push(document.visibilityState + '@' + Date.now());
        };
        document.addEventListener('visibilitychange', window.__plpVisHandler);
    },

    PLP_GetJsReport: function() {
        // 하트비트 정지 — 리포트 시점 이후의 틱은 의미 없다
        if (window.__plpTimer) { clearInterval(window.__plpTimer); window.__plpTimer = null; }
        if (window.__plpRafId) { cancelAnimationFrame(window.__plpRafId); window.__plpRafId = null; }
        if (window.__plpChannel) { window.__plpChannel.port1.onmessage = null; window.__plpChannel = null; }
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
            task: summarize(window.__plpTaskTicks),
            visibility: window.__plpVisibility || [],
            armedAtEpochMs: window.__plpArmedAt || 0
        });

        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    }
});
