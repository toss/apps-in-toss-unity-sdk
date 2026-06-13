/**
 * Apps in Toss Unity SDK - Banner Ad JavaScript Bridge
 * 배너 광고 DOM 컨테이너 생성·관리 + web-bridge attachBanner 호출 + 생명주기 이벤트 릴레이
 *
 * 상태:
 * - window.__aitBanners: instanceId → 슬롯 맵 (multi-slot)
 * - window.__aitBannerInit: TossAds.initialize 직렬화 상태 머신
 *   (동시 Show 호출 시 initialize가 중복 실행되지 않도록 queue로 직렬화)
 *
 * 이벤트는 SendMessage('AITBannerAdBridge', 'OnBannerAdEvent', json)으로 C#에 전달.
 * web-bridge attachBanner는 실패를 throw하지 않고 callbacks로만 알리므로,
 * 모든 callbacks를 구독해 정규화된 페이로드로 릴레이한다.
 */
mergeInto(LibraryManager.library, {
    __AITBannerAd_Show: function(instanceId, adGroupIdPtr, mode, themePtr, tonePtr, variantPtr) {
        var adGroupId = UTF8ToString(adGroupIdPtr);
        var theme = UTF8ToString(themePtr);
        var tone = UTF8ToString(tonePtr);
        var variant = UTF8ToString(variantPtr);

        if (!window.__aitBanners) window.__aitBanners = {};
        var banners = window.__aitBanners;

        function sendEvent(kind, payload) {
            payload = payload || {};
            var err = payload.error || {};
            var meta = payload.adMetadata || {};
            SendMessage('AITBannerAdBridge', 'OnBannerAdEvent', JSON.stringify({
                instanceId: instanceId,
                kind: kind,
                adGroupId: payload.adGroupId || adGroupId,
                slotId: payload.slotId || '',
                creativeId: meta.creativeId || '',
                requestId: meta.requestId || '',
                errorCode: typeof err.code === 'number' ? err.code : 0,
                errorMessage: err.message || ''
            }));
        }

        // 같은 instanceId 재호출 = 교체 (다른 슬롯은 건드리지 않음)
        if (banners[instanceId]) {
            banners[instanceId].destroy();
        }

        // 컨테이너 생성 — web-bridge가 컨테이너를 WeakMap 키로 캐싱하므로 매번 새 div를 만든다
        var container = document.createElement('div');
        container.id = 'ait-banner-container-' + instanceId;
        container.style.position = 'fixed';
        container.style.zIndex = '9000'; // toast(9999)/modal(10000) 아래
        container.style.pointerEvents = 'none';

        if (mode === 0) { // Top
            container.style.top = 'env(safe-area-inset-top, 0px)';
            container.style.left = 'env(safe-area-inset-left, 0px)';
            container.style.right = 'env(safe-area-inset-right, 0px)';
        } else if (mode === 1) { // Bottom
            container.style.bottom = 'env(safe-area-inset-bottom, 0px)';
            container.style.left = 'env(safe-area-inset-left, 0px)';
            container.style.right = 'env(safe-area-inset-right, 0px)';
        } else { // Rect — 위치는 SetRect가 캔버스 rect에 매핑해 설정
            container.style.top = '0';
            container.style.left = '0';
            container.style.width = '0';
        }
        document.body.appendChild(container);

        var slot = {
            container: container,
            banner: null,
            lastRect: null,
            resizeHandler: null,
            resizeObserver: null,
            destroyed: false,
            applyRect: function() {
                if (slot.destroyed || !slot.lastRect) return;
                var r = slot.lastRect;
                // 정규화(0~1) 좌표를 캔버스 화면 영역에 매핑 (캔버스가 없으면 뷰포트 기준)
                var canvas = document.querySelector('#unity-canvas');
                var base = canvas ? canvas.getBoundingClientRect()
                                  : { left: 0, top: 0, width: window.innerWidth, height: window.innerHeight };
                container.style.left = (base.left + r.x * base.width) + 'px';
                container.style.top = (base.top + r.y * base.height) + 'px';
                container.style.width = (r.w * base.width) + 'px';
                // 높이는 광고 소재가 결정 — 컨테이너는 가로폭·위치만 제공
            },
            destroy: function() {
                if (slot.destroyed) return;
                slot.destroyed = true;
                if (slot.resizeObserver) {
                    try { slot.resizeObserver.disconnect(); } catch (e) { /* 무시 */ }
                    slot.resizeObserver = null;
                }
                if (slot.resizeHandler) {
                    window.removeEventListener('resize', slot.resizeHandler);
                    slot.resizeHandler = null;
                }
                if (slot.banner && typeof slot.banner.destroy === 'function') {
                    try { slot.banner.destroy(); } catch (e) { /* 이미 제거된 경우 무시 */ }
                }
                slot.banner = null;
                if (container.parentNode) container.parentNode.removeChild(container);
                delete banners[instanceId];
            }
        };
        banners[instanceId] = slot;

        if (mode === 2) {
            slot.resizeHandler = function() { slot.applyRect(); };
            window.addEventListener('resize', slot.resizeHandler);
        }

        // 렌더된 배너 높이를 C#에 통지한다.
        // 컨테이너는 높이를 명시하지 않아(광고 소재가 결정) container 높이 = 실제 배너 높이.
        // ResizeObserver로 그 높이를 관찰해 '캔버스 대비 비율(heightFraction)'로 보고하면
        // C#이 RectTransform 영역을 배너 실제 크기에 맞출 수 있다 (문구 강조 ~90px vs 이미지 강조 가변).
        var lastSentHeight = -1;
        function sendResize() {
            if (slot.destroyed) return;
            var rect = container.getBoundingClientRect();
            var h = rect.height;
            var w = rect.width;
            if (h <= 0) return;
            if (Math.abs(h - lastSentHeight) < 0.5) return; // 미세 변동 무시
            lastSentHeight = h;
            var canvas = document.querySelector('#unity-canvas');
            var base = canvas ? canvas.getBoundingClientRect()
                              : { width: window.innerWidth, height: window.innerHeight };
            SendMessage('AITBannerAdBridge', 'OnBannerAdEvent', JSON.stringify({
                instanceId: instanceId,
                kind: 'resized',
                adGroupId: adGroupId,
                slotId: '',
                creativeId: '',
                requestId: '',
                errorCode: 0,
                errorMessage: '',
                width: w,
                height: h,
                widthFraction: base.width > 0 ? (w / base.width) : 0,
                heightFraction: base.height > 0 ? (h / base.height) : 0
            }));
        }

        if (typeof ResizeObserver !== 'undefined') {
            slot.resizeObserver = new ResizeObserver(function() { sendResize(); });
            slot.resizeObserver.observe(container);
        }

        // 광고 영역만 클릭을 받고 주변은 캔버스로 통과시킨다
        function markWrapperInteractive() {
            var wrapper = container.querySelector('[data-toss-ads-attach-banner-wrapper="true"]');
            if (wrapper) wrapper.style.pointerEvents = 'auto';
            container.style.pointerEvents = 'none';
        }

        function attach() {
            if (slot.destroyed) return;
            try {
                slot.banner = window.AppsInToss.TossAds.attachBanner(adGroupId, container, {
                    theme: theme,
                    tone: tone,
                    variant: variant,
                    callbacks: {
                        onAdRendered: function(p) {
                            markWrapperInteractive();
                            sendEvent('rendered', p);
                        },
                        onAdViewable: function(p) { sendEvent('viewable', p); },
                        onAdClicked: function(p) { sendEvent('clicked', p); },
                        onAdImpression: function(p) { sendEvent('impression', p); },
                        onAdFailedToRender: function(p) { sendEvent('failedToRender', p); },
                        onNoFill: function(p) { sendEvent('noFill', p); }
                    }
                });
                markWrapperInteractive();
            } catch (e) {
                sendEvent('failedToRender', { error: { message: e && e.message ? e.message : String(e) } });
            }
        }

        // setTimeout으로 한 틱 미룬다:
        // - C#이 Show 직후 호출하는 SetRect가 attach 전에 적용되도록 순서 보장
        // - SendMessage 재진입(WebGL 호출 스택 안에서의 콜백) 회피
        setTimeout(function() {
            if (slot.destroyed) return;

            if (!window.AppsInToss || !window.AppsInToss.TossAds) {
                sendEvent('initializationFailed', { error: { message: 'AppsInToss.TossAds bridge not available' } });
                return;
            }

            // 이미 초기화 완료(다른 경로 포함)면 즉시 attach
            if (window.TossAdsSpaceKit && typeof window.TossAdsSpaceKit.isInitialized === 'function'
                && window.TossAdsSpaceKit.isInitialized()) {
                sendEvent('initialized', {});
                attach();
                return;
            }

            // initialize 직렬화 — 동시 Show 호출 시 첫 호출만 initialize하고 나머지는 queue 대기
            if (!window.__aitBannerInit) window.__aitBannerInit = { state: 'idle', queue: [] };
            var init = window.__aitBannerInit;

            if (init.state === 'ready') {
                sendEvent('initialized', {});
                attach();
                return;
            }

            init.queue.push({
                onReady: function() {
                    if (slot.destroyed) return;
                    sendEvent('initialized', {});
                    attach();
                },
                onFailed: function(error) {
                    if (slot.destroyed) return;
                    sendEvent('initializationFailed', { error: { message: error && error.message ? error.message : String(error) } });
                }
            });

            if (init.state === 'pending') return;

            init.state = 'pending';
            function flush(ok, error) {
                init.state = ok ? 'ready' : 'idle'; // 실패 시 idle로 되돌려 다음 Show에서 재시도
                var waiters = init.queue.splice(0, init.queue.length);
                for (var i = 0; i < waiters.length; i++) {
                    if (ok) waiters[i].onReady();
                    else waiters[i].onFailed(error);
                }
            }
            try {
                window.AppsInToss.TossAds.initialize({
                    callbacks: {
                        onInitialized: function() { flush(true); },
                        onInitializationFailed: function(error) { flush(false, error); }
                    }
                });
            } catch (e) {
                flush(false, e);
            }
        }, 0);
    },

    __AITBannerAd_SetRect: function(instanceId, x, y, width, height) {
        var banners = window.__aitBanners;
        var slot = banners && banners[instanceId];
        if (!slot || slot.destroyed) return;
        slot.lastRect = { x: x, y: y, w: width, h: height };
        slot.applyRect();
    },

    __AITBannerAd_Hide: function(instanceId) {
        var banners = window.__aitBanners;
        var slot = banners && banners[instanceId];
        if (!slot) return;
        slot.destroy();
    }
});
