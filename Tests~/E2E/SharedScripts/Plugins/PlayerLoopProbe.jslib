// Player loop freeze 진단 프로브 (techchat 4377 검증용)
//
// 결제 native 오버레이 구간에서 웹뷰 JS 이벤트 루프(setInterval)가 계속 도는지,
// 아니면 웹뷰 전체가 suspend되는지를 판별하기 위한 JS 측 하트비트.
// C# Update() 하트비트(=player loop)와 비교하면 세 시나리오를 구분할 수 있다:
//   1) C# 프레임 gap 大 + JS gap 小  → rAF(player loop)만 정지, JS는 생존
//      (템플릿 레벨 keep-alive 워크어라운드 가능)
//   2) C# 프레임 gap 大 + JS gap 大  → 웹뷰 전체 suspend (네이티브 수정 필요)
//   3) 둘 다 小                      → player loop 정지 주장 재현 안 됨
mergeInto(LibraryManager.library, {
    PLP_StartJsProbe: function() {
        if (window.__plpTimer) {
            clearInterval(window.__plpTimer);
        }
        window.__plpTicks = [Date.now()];
        window.__plpTimer = setInterval(function() {
            // 최대 4800틱(약 20분) 캡 — 방치돼도 메모리 무한 증가 방지
            if (window.__plpTicks.length < 4800) {
                window.__plpTicks.push(Date.now());
            }
        }, 250);
    },

    PLP_GetJsReport: function() {
        var ticks = window.__plpTicks || [];
        if (window.__plpTimer) {
            clearInterval(window.__plpTimer);
            window.__plpTimer = null;
        }
        var maxGap = 0;
        var maxGapAt = 0;
        for (var i = 1; i < ticks.length; i++) {
            var gap = ticks[i] - ticks[i - 1];
            if (gap > maxGap) {
                maxGap = gap;
                maxGapAt = ticks[i - 1];
            }
        }
        var report = JSON.stringify({
            count: ticks.length,
            maxGapMs: maxGap,
            maxGapAtEpochMs: maxGapAt,
            spanMs: ticks.length > 1 ? ticks[ticks.length - 1] - ticks[0] : 0
        });
        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    }
});
