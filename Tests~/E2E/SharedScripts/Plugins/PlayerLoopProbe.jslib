// Player loop freeze 진단 프로브 (techchat 4377 검증용) — round 5 v2
//
// round 1~3 실측 확정 사항 (미머지 브랜치 test/plp-round2/round3 실측, 결론만 포팅):
//   rAF 갭 27.52s ≡ hidden→visible 27.51s ≡ C# 프레임 갭 27.44s (세 값 일치).
//   원인은 웹뷰 suspend가 아니라 표준 visibilityState=hidden 처리이며,
//   rAF가 스펙대로 멈추는 것뿐이다. setTimeout은 hidden 중에도 살아있으나
//   스로틀된다(28s 동안 22회 발화, 최대 갭 11.6s).
//
//   (a) rAF    — requestAnimationFrame. Unity WebGL player loop를 실제로 구동하는 콜백.
//   (b) timer  — setInterval(250ms). 타이머 큐 생존 여부(레버 A 발화 가능성).
//   (c) visibility — visibilitychange 기록. rAF 정지 원인이 표준 hidden 처리인지 확인.
//
// round 4가 답한 두 질문 (SDK 착수 게이트):
//   (a) 오버레이 정지 중 발사한 JS fetch가 실제로 완료되고, .then이 스로틀 없이
//       즉시 발화하며, SendMessage로 C#까지 전달되는가?             → PLP_StartFetchProbe
//   (b) 플랫폼이 processProductGrant의 Promise를 수 초간 pending으로 두는 것을
//       허용하는가(지연 resolve 후에도 정상 지급되는가)?             → PLP_EnableGrantDelay
//
// round 5 v1 실기기 실측(iOS, Unity 6000.2) — 가설 기각:
//   Application.targetFrameRate=30을 설정해도 Browser.mainLoop.method는 'rAF'로 유지됐다
//   (현대 Unity는 targetFrameRate로 Emscripten 루프 타이밍을 전환하지 않는 것으로 보인다 —
//   rAF 고정 + 내부 프레임 스킵). 오버레이 중 루프도 여전히 정지(maxFrameGap 13.15s ≈
//   hidden 13.08s). 또한 await Task.Delay(3000)은 오버레이 밖 평시 상태에서조차 예외 없이
//   영영 재개되지 않아(WebGL에 스레드 기반 타이머가 없는 것으로 보임) 루프 생존 측정
//   도구로 부적합함이 확인됐다.
//
// round 5 v2가 새로 답하려는 질문: Unity를 거치지 않고 Emscripten 함수
// (emscripten_set_main_loop_timing)를 jslib에서 직접 호출해 루프 타이밍을 강제 전환하면
// 결제 오버레이(visibilityState=hidden, rAF 정지) 중에도 루프가 살아나는가, 그리고 그때
// C# await(Task.Yield 루프, UnityWebRequest)가 재개되는가?
//   → PLP_ForceLoopTiming / PLP_GetLoopTimingInfo, IAPv2Tester의
//     RunPlp5AwaitProbeAsync/TogglePlpLoopTiming (대조군: 강제 전환 없는 기본 rAF 구동).
//     Task.Delay 자체의 생사는 별도 버튼(RunPlp5TaskDelayOnlyProbeAsync)으로 계속 고정 관측한다.
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
    },

    // round 5 v2 — Application.targetFrameRate 토글이 Emscripten 루프 타이밍을 바꾸지
    // 못함이 실기기에서 확인됐다(위 헤더 주석 참조). 이 헬퍼는 Unity를 우회해 Emscripten
    // 함수를 직접 호출해 루프 타이밍 자체를 강제 전환한다.
    // mode: 0=setTimeout(valueMs 간격), 1=rAF(valueMs는 무시됨) — Emscripten
    // emscripten_set_main_loop_timing 규약과 동일.
    // 반환값(rc)을 그대로 리턴한다: 0=성공, 그 외는 Emscripten 쪽 실패 코드.
    // 심볼 부재 시 -999, 호출 중 예외 시 -998을 리턴한다 — 둘 다 실제 rc(보통 0/음수)와
    // 겹치지 않는 sentinel 값으로 골랐다. Emscripten 버전에 따라 심볼이 다를 수 있으나
    // (구버전 Browser.mainLoop 기반 / 신버전 MainLoop 객체) 여기서 대안 탐색은 하지 않는다
    // — 그 판별은 아래 PLP_GetLoopTimingInfo가 어떤 전역이 존재하는지 노출하는 것으로 한다.
    PLP_ForceLoopTiming: function(mode, valueMs) {
        try {
            if (typeof _emscripten_set_main_loop_timing === 'function') {
                var rc = _emscripten_set_main_loop_timing(mode, valueMs);
                if (window.__AIT_VERBOSE) console.log('[PLP5v2] force loop timing mode=', mode, 'valueMs=', valueMs, '-> rc=', rc);
                return rc;
            }
            if (window.__AIT_VERBOSE) console.log('[PLP5v2] _emscripten_set_main_loop_timing 없음');
            return -999;
        } catch (err) {
            if (window.__AIT_VERBOSE) console.log('[PLP5v2] force loop timing 예외', err);
            return -998;
        }
    },

    // round 5 v2 — 위 강제 전환이 어떤 상태에서 성공/실패하는지 진단하기 위해 Emscripten
    // 내부 상태를 그대로 노출한다. 모든 필드는 개별 try/catch로 방어하며, 실패한 필드는
    // 'err'로 채운다(그 필드만 못 읽는 것이지 나머지 리포트는 계속 유효하다).
    PLP_GetLoopTimingInfo: function() {
        var info = {};

        try {
            info.hasSetTiming = (typeof _emscripten_set_main_loop_timing === 'function');
        } catch (err) {
            info.hasSetTiming = 'err';
        }

        try {
            info.browserMethod = (typeof Browser !== 'undefined' && Browser.mainLoop && Browser.mainLoop.method) ? Browser.mainLoop.method : 'n/a';
        } catch (err) {
            info.browserMethod = 'err';
        }

        try {
            info.timingMode = (typeof Browser !== 'undefined' && Browser.mainLoop && typeof Browser.mainLoop.timingMode === 'number') ? Browser.mainLoop.timingMode : -1;
        } catch (err) {
            info.timingMode = 'err';
        }

        try {
            info.timingValue = (typeof Browser !== 'undefined' && Browser.mainLoop && typeof Browser.mainLoop.timingValue === 'number') ? Browser.mainLoop.timingValue : -1;
        } catch (err) {
            info.timingValue = 'err';
        }

        try {
            info.hasMainLoopObj = (typeof MainLoop !== 'undefined');
        } catch (err) {
            info.hasMainLoopObj = 'err';
        }

        var report = JSON.stringify(info);
        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    },

    // round 5 v4 — v3의 수동 3단 순환은 가설 검증(1000ms 간격이 hidden 타이머 예산 안에서
    // 생존하는가)에는 맞았지만, 화면이 보이는 동안에도 계속 1000ms(=1fps)로 묶여 있어
    // 사용자가 결제 UI를 조작할 수 없다는 실사용 문제가 실기기에서 확인됐다. 최종 SDK
    // 기능과 동일한 모양으로 "visible이면 rAF(정상 프레임레이트), hidden 진입 순간에만
    // setTimeout(1000ms), visible 복귀 시 rAF 복원"을 visibilitychange에 맞춰 자동
    // 전환한다. 기존 PLP_ForceLoopTiming(수동 단발 전환)과 독립적으로 존재하며, 자동
    // 전환이 켜진 상태에서 수동 전환 버튼을 눌러도 다음 hidden/visible 전이 때 자동
    // 전환이 값을 다시 덮어쓴다(둘을 동시에 쓰는 것은 권장하지 않음, 진단 도구이므로
    // 상호 배제는 강제하지 않는다).
    //
    // enable=1: document에 visibilitychange 리스너를 등록한다(중복 등록 방지 — 기존
    // 핸들러가 있으면 먼저 제거 후 재등록). hidden 진입 시 setTimeout(1000ms)로,
    // visible 복귀 시 rAF로 전환하며 매 전환마다 {to, rc, at}를 window.__plp5SwapLog에
    // 누적 기록한다(PLP_GetAutoSwapLog로 조회, 비우지 않음).
    // enable=0: 리스너를 제거하고 즉시 rAF로 복원한다(전환 이력 중인 채로 꺼졌을 때
    // 루프가 setTimeout(1000ms)에 갇혀 있지 않도록).
    //
    // 반환값: 0=성공, _emscripten_set_main_loop_timing 부재 시 -999, 예외 시 -998.
    // (enable=0 경로에서 리스너가 애초에 없었어도 rAF 복원 자체는 시도하고 그 결과를 반환한다.)
    // round 5 v5 — v4 실기기 결과: hidden 진입 시 _emscripten_set_main_loop_timing(0,1000)은
    // rc=0으로 성공하고 Browser.mainLoop.timingMode/timingValue도 실제로 바뀌지만, 루프는
    // hidden 12초 내내 정지했다(maxFrameGap 12.08s). 원인은 _emscripten_set_main_loop_timing이
    // timingMode와 scheduler 함수만 교체할 뿐 scheduler를 직접 호출하지는 않는다는 데 있다 —
    // 새 타이밍은 다음 runner 실행 시 재스케줄되는 시점에야 발효되는데, hidden 진입 시점에
    // 걸려 있던 pending rAF는 hidden 중 발화하지 않으므로 runner가 다시 돌 기회 자체가 없어
    // setTimeout 체인이 시작조차 못 한다.
    //
    // framework.js 실코드 확인: Browser.mainLoop.pause()는 scheduler를 null로 비우고
    // currentlyRunningMainloop를 증가시켜 기존 pending rAF를 무효화하고(stale 콜백은 카운터
    // 불일치로 no-op), resume()은 setMainLoop을 재등록한 뒤 _emscripten_set_main_loop_timing을
    // 다시 호출하고 Browser.mainLoop.scheduler()를 그 자리에서 즉시 실행한다. 즉 "타이밍
    // 설정 → pauseMainLoop() → resumeMainLoop()" 순서면 hidden 중에도 새 체인이 즉시
    // 무장된다. 이 킥은 hidden 전환·visible 전환 양쪽에 모두 적용한다 — visible 복귀 시에도
    // 재스케줄을 기다리지 않고 즉시 rAF 체인으로 복귀시켜야 최대 1초 지연을 없앨 수 있다.
    PLP_EnableAutoTimingSwap: function(enable) {
        var applyTiming = function(mode, valueMs, toLabel) {
            try {
                if (typeof _emscripten_set_main_loop_timing !== 'function') {
                    window.__plp5SwapLog.push({ to: toLabel, rc: -999, kick: -999, at: Date.now() });
                    return -999;
                }
                var rc = _emscripten_set_main_loop_timing(mode, valueMs);
                var kick = kickMainLoop();
                window.__plp5SwapLog.push({ to: toLabel, rc: rc, kick: kick, at: Date.now() });
                if (window.__AIT_VERBOSE) console.log('[PLP5v5] auto swap ->', toLabel, 'rc=', rc, 'kick=', kick);
                return rc;
            } catch (err) {
                window.__plp5SwapLog.push({ to: toLabel, rc: -998, kick: -998, at: Date.now() });
                if (window.__AIT_VERBOSE) console.log('[PLP5v5] auto swap 예외', err);
                return -998;
            }
        };

        // 타이밍 전환 직후 pending rAF가 사라져 새 스케줄러가 무장되지 않는 문제(위 헤더 주석
        // 참조)를 우회한다. Module.pauseMainLoop()로 기존 pending rAF를 무효화하고
        // Module.resumeMainLoop()로 방금 설정한 타이밍 기준으로 즉시 재스케줄한다.
        var kickMainLoop = function() {
            try {
                if (typeof Module.pauseMainLoop === 'function' && typeof Module.resumeMainLoop === 'function') {
                    Module.pauseMainLoop();
                    Module.resumeMainLoop();
                    return 0;
                }
                return -997;
            } catch (err) {
                if (window.__AIT_VERBOSE) console.log('[PLP5v5] kick 예외', err);
                return -996;
            }
        };

        window.__plp5SwapLog = window.__plp5SwapLog || [];

        if (window.__plp5AutoSwapHandler) {
            document.removeEventListener('visibilitychange', window.__plp5AutoSwapHandler);
            window.__plp5AutoSwapHandler = null;
        }

        if (!enable) {
            return applyTiming(1, 1, 'rAF');
        }

        window.__plp5AutoSwapHandler = function() {
            if (document.visibilityState === 'hidden') {
                applyTiming(0, 1000, 'timeout1000');
            } else {
                applyTiming(1, 1, 'rAF');
            }
        };
        document.addEventListener('visibilitychange', window.__plp5AutoSwapHandler);
        if (window.__AIT_VERBOSE) console.log('[PLP5v5] auto swap 리스너 등록됨');
        return 0;
    },

    // round 5 v4 — PLP_EnableAutoTimingSwap이 실제로 hidden/visible 전이 순간에 발화했는지,
    // 그때 rc가 무엇이었는지 사후 확인하기 위한 누적 로그 조회. 배열은 비우지 않는다(계속
    // 누적) — onEvent/onError 덤프에서 반복 조회해도 이전 항목이 사라지지 않아야 여러
    // 결제 시도에 걸친 전이 이력을 한 번에 볼 수 있다.
    PLP_GetAutoSwapLog: function() {
        var report = JSON.stringify(window.__plp5SwapLog || []);
        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    },

    // =====================================================
    // 지연 지급 결정 PoC — round 4 (a)(b)가 확인한 두 사실(fetch 왕복 완주, processProductGrant
    // Promise의 장시간 pending 허용)을 실서버 HTTP 검증으로 이어붙인 end-to-end 실증이다.
    //
    // PLP_EnableGrantDelay(위)는 SDK 자체 wrapper(originalGrant)를 그대로 거쳐가며 outer resolve만
    // setTimeout으로 늦추지만, 여기서는 options.processProductGrant 자체를 완전히 새 함수로
    // 교체한다 — 플랫폼 지급 질의를 C#으로 넘겨 실제 HTTP 응답을 받은 뒤 그 결정을 resolve한다.
    // 이 경로에서는 originalGrant(=SDK jslib이 주입한 __AIT_NESTED_CALLBACKS 브릿지)를 아예
    // 호출하지 않으므로, PoC ON 중에는 C# IAPv2Tester.ExecuteIAPCreateOrder의 동기
    // ProcessProductGrant 델리게이트가 호출되지 않는다(의도된 우회 — C# 쪽 주석 참조).
    // =====================================================
    PLP_EnablePocGrant: function(goNamePtr) {
        try {
            var goName = UTF8ToString(goNamePtr);
            window.__plpPocPending = window.__plpPocPending || {};
            window.__plpPocLog = window.__plpPocLog || [];
            window.__plpPocGoName = goName;

            var pocLog = function(ev, id, extra) {
                window.__plpPocLog.push({
                    ev: ev,
                    id: id || '',
                    at: Date.now(),
                    vis: (typeof document !== 'undefined') ? document.visibilityState : 'n/a',
                    extra: (extra === undefined || extra === null) ? '' : String(extra)
                });
                if (window.__AIT_VERBOSE) console.log('[PLPPoc]', ev, id, extra);
            };

            if (window.__plpPocWrapped) {
                // 이미 설치돼 있으면 target GameObject 이름만 갱신하고 재설치하지 않는다(중복 설치 방지).
                pocLog('reEnable', '', 'goName=' + goName);
                return 0;
            }

            if (!window.AppsInToss || !window.AppsInToss.IAP || typeof window.AppsInToss.IAP.createOneTimePurchaseOrder !== 'function') {
                console.error('[PLPPoc] window.AppsInToss.IAP.createOneTimePurchaseOrder 를 찾을 수 없어 PoC 그랜트 인터셉터를 설치하지 못했습니다.');
                return -999;
            }

            var originalCreate = window.AppsInToss.IAP.createOneTimePurchaseOrder;
            window.__plpPocOrig = originalCreate;

            window.AppsInToss.IAP.createOneTimePurchaseOrder = function(args) {
                var originalOptions = (args && args.options) || {};

                if (typeof originalOptions.processProductGrant !== 'function') {
                    return originalCreate(args);
                }

                var wrappedOptions = Object.assign({}, originalOptions, {
                    processProductGrant: function(param) {
                        return new Promise(function(resolve) {
                            var id = 'poc_' + Date.now() + '_' + Math.random().toString(36).slice(2);

                            var timeoutId = setTimeout(function() {
                                var pending = window.__plpPocPending[id];
                                if (!pending) return;
                                delete window.__plpPocPending[id];
                                pocLog('safetyTimeout', id);
                                resolve(true);
                            }, 12000); // 12초 근거: 플랫폼 30초 환불 페이지 대비 역산, JS 1s 타이머는 hidden 중 생존 실측.

                            window.__plpPocPending[id] = { resolve: resolve, timeoutId: timeoutId };
                            pocLog('grantAsked', id);

                            var payload;
                            try {
                                payload = JSON.stringify({ id: id, param: param });
                            } catch (e) {
                                payload = JSON.stringify({ id: id, param: null });
                            }
                            SendMessage(window.__plpPocGoName, 'OnPocGrantRequest', payload);
                        });
                    }
                });

                return originalCreate(Object.assign({}, args, { options: wrappedOptions }));
            };

            window.__plpPocWrapped = true;
            pocLog('installed', '', 'goName=' + goName);
            return 0;
        } catch (err) {
            console.error('[PLPPoc] enable 예외', err);
            return -998;
        }
    },

    PLP_PocRespond: function(idPtr, result) {
        try {
            var id = UTF8ToString(idPtr);
            window.__plpPocPending = window.__plpPocPending || {};
            window.__plpPocLog = window.__plpPocLog || [];

            var pending = window.__plpPocPending[id];
            if (!pending) {
                window.__plpPocLog.push({ ev: 'respondMiss', id: id, at: Date.now(), vis: document.visibilityState, extra: String(result) });
                if (window.__AIT_VERBOSE) console.log('[PLPPoc] respond: pending 엔트리 없음(이미 처리됨/안전망 발화됨)', id);
                return;
            }

            clearTimeout(pending.timeoutId);
            delete window.__plpPocPending[id];
            window.__plpPocLog.push({ ev: 'respond', id: id, at: Date.now(), vis: document.visibilityState, extra: String(result) });
            if (window.__AIT_VERBOSE) console.log('[PLPPoc] respond', id, result);
            pending.resolve(!!result);
        } catch (err) {
            console.error('[PLPPoc] respond 예외', err);
        }
    },

    PLP_DisablePocGrant: function() {
        try {
            window.__plpPocLog = window.__plpPocLog || [];

            if (window.__plpPocWrapped && window.__plpPocOrig && window.AppsInToss && window.AppsInToss.IAP) {
                window.AppsInToss.IAP.createOneTimePurchaseOrder = window.__plpPocOrig;
            }
            window.__plpPocWrapped = false;
            window.__plpPocOrig = null;

            window.__plpPocPending = window.__plpPocPending || {};
            var ids = Object.keys(window.__plpPocPending);
            for (var i = 0; i < ids.length; i++) {
                var id = ids[i];
                var pending = window.__plpPocPending[id];
                clearTimeout(pending.timeoutId);
                window.__plpPocLog.push({ ev: 'disableSafeResolve', id: id, at: Date.now(), vis: document.visibilityState, extra: '' });
                pending.resolve(true);
            }
            window.__plpPocPending = {};

            window.__plpPocLog.push({ ev: 'disabled', id: '', at: Date.now(), vis: document.visibilityState, extra: '' });
            if (window.__AIT_VERBOSE) console.log('[PLPPoc] disabled, 원본 복원 완료');
            return 0;
        } catch (err) {
            console.error('[PLPPoc] disable 예외', err);
            return -998;
        }
    },

    PLP_GetPocReport: function() {
        var report = JSON.stringify(window.__plpPocLog || []);
        var bufferSize = lengthBytesUTF8(report) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(report, buffer, bufferSize);
        return buffer;
    }
});
