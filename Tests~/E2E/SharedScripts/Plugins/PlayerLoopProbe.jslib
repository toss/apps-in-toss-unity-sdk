// Player loop freeze 진단 프로브 (techchat 4377 검증용) — round 4
//
// round 1~3 실측 확정 사항 (미머지 브랜치 test/plp-round2/round3 실측, 결론만 포팅):
//   rAF 갭 27.52s ≡ hidden→visible 27.51s ≡ C# 프레임 갭 27.44s (세 값 일치).
//   원인은 웹뷰 suspend가 아니라 표준 visibilityState=hidden 처리이며,
//   rAF가 스펙대로 멈추는 것뿐이다. setTimeout은 hidden 중에도 살아있으나
//   스로틀된다(28s 동안 22회 발화, 최대 갭 11.6s). await Task.Delay는 WebGL에
//   스레드가 없어 영영 완료되지 않는다(확증 완료 — 이후 라운드에서는 관찰 대상에서 뺀다).
//
//   (a) rAF    — requestAnimationFrame. Unity WebGL player loop를 실제로 구동하는 콜백.
//   (b) timer  — setInterval(250ms). 타이머 큐 생존 여부(레버 A 발화 가능성).
//   (c) visibility — visibilitychange 기록. rAF 정지 원인이 표준 hidden 처리인지 확인.
//
// round 4가 새로 답하려는 두 질문 (SDK 착수 게이트):
//   (a) 오버레이 정지 중 발사한 JS fetch가 실제로 완료되고, .then이 스로틀 없이
//       즉시 발화하며, SendMessage로 C#까지 전달되는가?             → PLP_StartFetchProbe
//   (b) 플랫폼이 processProductGrant의 Promise를 수 초간 pending으로 두는 것을
//       허용하는가(지연 resolve 후에도 정상 지급되는가)?             → PLP_EnableGrantDelay
//
// 모든 기록은 메모리에만 쌓고 복귀 후 한 번에 리포트한다 (정지 중 로그 출력은 유실 위험).
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
    },

    // round 4 (a) — 오버레이 정지 중 fetch 왕복이 완료·전달되는가.
    //
    // ProcessProductGrant 콜백 진입 시점(= 오버레이가 아직 열려 있는 순간)에 호출된다.
    // same-origin 정적 파일(index.html)에 cache-buster를 붙여 요청한다 — 외부 서비스
    // 가용성·CORS에 의존하지 않고 "fetch가 완료되고 .then/SendMessage가 도달하는가"라는
    // 메커니즘만 격리해 잰다. fetchStart/then 발화/res.text() 완료를 모두 performance.now()
    // 기준으로 기록하고, 마지막 단계에서 SendMessage로 C#에 전달한다 — 그 SendMessage가
    // 실제로 C# 수신 메서드에 도달한 시각은 C#쪽 LogIap 타임스탬프로 남는다(loop가 멈춰
    // 있으면 이 전달 자체가 지연될 수 있다는 것이 관찰 대상이다).
    PLP_StartFetchProbe: function() {
        var fetchStartMs = performance.now();
        var fetchStartEpochMs = Date.now();
        var url = 'index.html?plpFetch=' + fetchStartEpochMs + '_' + Math.random().toString(36).slice(2);

        fetch(url, { cache: 'no-store' }).then(function(res) {
            var thenAtMs = performance.now();
            return res.text().then(function(text) {
                var textAtMs = performance.now();
                var payload = JSON.stringify({
                    ok: res.ok,
                    status: res.status,
                    fetchStartMs: fetchStartMs,
                    thenAtMs: thenAtMs,
                    textAtMs: textAtMs,
                    thenDeltaMs: thenAtMs - fetchStartMs,
                    textDeltaMs: textAtMs - thenAtMs,
                    totalDeltaMs: textAtMs - fetchStartMs,
                    textLength: text.length
                });
                if (window.__AIT_VERBOSE) console.log('[PLP] fetch probe complete', payload);
                SendMessage('BenchmarkManager', 'OnPlpFetchProbeComplete', payload);
            });
        }).catch(function(err) {
            var payload = JSON.stringify({
                ok: false,
                fetchStartMs: fetchStartMs,
                errorAtMs: performance.now(),
                error: String((err && err.message) || err)
            });
            if (window.__AIT_VERBOSE) console.log('[PLP] fetch probe failed', payload);
            SendMessage('BenchmarkManager', 'OnPlpFetchProbeComplete', payload);
        });
    },

    // round 4 (b) — 플랫폼이 processProductGrant의 Promise를 수 초간 pending으로
    // 두는 것을 허용하는가.
    //
    // window.AppsInToss.IAP.createOneTimePurchaseOrder를 래핑해(원본은 보관 후 교체)
    // 전달되는 options.processProductGrant가 반환하는 Promise를 가로챈다. 내부 콜백이
    // resolve된 뒤 delayMs만큼 setTimeout으로 외부 resolve를 늦춘다 — 실제 지연도
    // 함께 기록해 타이머 스로틀 영향을 관측한다(요청한 delayMs와 실제 delay가 다를 수 있음).
    //
    // AppsInToss-IAP.jslib(Runtime/SDK, 자동 생성)는 __IAPCreateOneTimePurchaseOrder_Internal
    // 호출 시점마다 window.AppsInToss.IAP.createOneTimePurchaseOrder를 다시 조회하므로,
    // 결제 시작(버튼 클릭) 전에만 이 함수를 호출해 두면 다음 구매 시도부터 맞물린다.
    //
    // delayMs=0도 래퍼는 그대로 설치한다 — "래핑 자체가 무해한가"까지 같은 경로로 관측하기 위함.
    PLP_EnableGrantDelay: function(delayMs) {
        window.__plpGrantDelayMs = delayMs;
        window.__plpGrantDelayRecords = window.__plpGrantDelayRecords || [];

        if (window.__plpGrantDelayWrapped) {
            if (window.__AIT_VERBOSE) console.log('[PLP] grant delay updated to', delayMs, 'ms');
            return;
        }

        if (!window.AppsInToss || !window.AppsInToss.IAP || typeof window.AppsInToss.IAP.createOneTimePurchaseOrder !== 'function') {
            console.error('[PLP] window.AppsInToss.IAP.createOneTimePurchaseOrder 를 찾을 수 없어 grant delay 래퍼를 설치하지 못했습니다.');
            return;
        }

        var originalCreate = window.AppsInToss.IAP.createOneTimePurchaseOrder;
        window.__plpOriginalCreateOneTimePurchaseOrder = originalCreate;

        window.AppsInToss.IAP.createOneTimePurchaseOrder = function(args) {
            var originalOptions = (args && args.options) || {};
            var originalGrant = originalOptions.processProductGrant;

            if (typeof originalGrant !== 'function') {
                return originalCreate(args);
            }

            var wrappedOptions = Object.assign({}, originalOptions, {
                processProductGrant: function(data) {
                    return new Promise(function(outerResolve, outerReject) {
                        Promise.resolve(originalGrant(data)).then(function(innerResult) {
                            var innerResolvedAtMs = performance.now();
                            var delayMs = window.__plpGrantDelayMs || 0;
                            setTimeout(function() {
                                var outerResolvedAtMs = performance.now();
                                window.__plpGrantDelayRecords.push({
                                    requestedDelayMs: delayMs,
                                    innerResolvedAtMs: innerResolvedAtMs,
                                    outerResolvedAtMs: outerResolvedAtMs,
                                    actualDelayMs: outerResolvedAtMs - innerResolvedAtMs
                                });
                                if (window.__AIT_VERBOSE) console.log('[PLP] grant delay resolved', delayMs, 'ms requested');
                                outerResolve(innerResult);
                            }, delayMs);
                        }, outerReject);
                    });
                }
            });

            return originalCreate(Object.assign({}, args, { options: wrappedOptions }));
        };

        window.__plpGrantDelayWrapped = true;
        if (window.__AIT_VERBOSE) console.log('[PLP] grant delay wrapper installed, delayMs=', delayMs);
    },

    PLP_GetGrantDelayReport: function() {
        var report = JSON.stringify(window.__plpGrantDelayRecords || []);
        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    }
});
