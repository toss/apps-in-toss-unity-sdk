/**
 * AppsInToss-Media.jslib
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

mergeInto(LibraryManager.library, {
    __FileSaveBase64_Internal: function(params, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] FileSaveBase64 called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] FileSaveBase64 raw param params:', UTF8ToString(params));

        try {
            var promiseResult = window.AppsInToss.File.saveBase64(JSON.parse(UTF8ToString(params)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64 returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64 did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64 resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64 rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64 sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __FileSaveBase64IsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.File.saveBase64.isSupported();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __FileOpenPDFViewer_Internal: function(params, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] FileOpenPDFViewer called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] FileOpenPDFViewer raw param params:', UTF8ToString(params));

        try {
            var promiseResult = window.AppsInToss.File.openPDFViewer(JSON.parse(UTF8ToString(params)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __FileOpenPDFViewerIsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.File.openPDFViewer.isSupported();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __fetchAlbumItems_Internal: function(options, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems raw param options:', UTF8ToString(options));

        try {
            var promiseResult = window.AppsInToss.fetchAlbumItems(JSON.parse(UTF8ToString(options)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumItems sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __fetchAlbumItemsIsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.fetchAlbumItems.isSupported();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __fetchAlbumPhotos_Internal: function(options, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos raw param options:', UTF8ToString(options));

        try {
            var promiseResult = window.AppsInToss.fetchAlbumPhotos(JSON.parse(UTF8ToString(options)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotos sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __fetchAlbumPhotosGetPermission_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotosGetPermission called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.fetchAlbumPhotos.getPermission();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __fetchAlbumPhotosOpenPermissionDialog_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] fetchAlbumPhotosOpenPermissionDialog called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.fetchAlbumPhotos.openPermissionDialog();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __openCamera_Internal: function(options, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera raw param options:', UTF8ToString(options));

        try {
            var promiseResult = window.AppsInToss.openCamera(JSON.parse(UTF8ToString(options)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCamera sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __openCameraGetPermission_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCameraGetPermission called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.openCamera.getPermission();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] getPermission sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __openCameraOpenPermissionDialog_Internal: function(callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openCameraOpenPermissionDialog called, callbackId:', callback);

        try {
            var promiseResult = window.AppsInToss.openCamera.openPermissionDialog();
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPermissionDialog sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __openPDFViewer_Internal: function(params, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer raw param params:', UTF8ToString(params));

        try {
            var promiseResult = window.AppsInToss.openPDFViewer(JSON.parse(UTF8ToString(params)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] openPDFViewer sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __openPDFViewerIsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.openPDFViewer.isSupported();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __saveBase64Data_Internal: function(params, callbackId, typeName) {
        // 비동기 함수 (Promise 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data called, callbackId:', callback);
        if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data raw param params:', UTF8ToString(params));

        try {
            var promiseResult = window.AppsInToss.saveBase64Data(JSON.parse(UTF8ToString(params)));
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data returned:', promiseResult, 'isPromise:', promiseResult && typeof promiseResult.then === 'function');

            if (!promiseResult || typeof promiseResult.then !== 'function') {
                // Promise가 아닌 경우 (undefined, null 등) - 즉시 응답
                if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data did not return a Promise, sending immediate response');
                var payload = JSON.stringify({
                    CallbackId: callback,
                    TypeName: typeNameStr,
                    Result: JSON.stringify({ success: true, data: JSON.stringify(promiseResult), error: '' })
                });
                SendMessage('AITCore', 'OnAITCallback', payload);
                return;
            }

            promiseResult
                .then(function(result) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data resolved:', result);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
                    });
                    SendMessage('AITCore', 'OnAITCallback', payload);
                })
                .catch(function(error) {
                    if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data rejected:', error);
                    var payload = JSON.stringify({
                        CallbackId: callback,
                        TypeName: typeNameStr,
                        Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
                    });
                    setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
                });
        } catch (error) {
            if (window.__AIT_VERBOSE) console.log('[AIT jslib] saveBase64Data sync error:', error);
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

    __saveBase64DataIsSupported_Internal: function(callbackId, typeName) {
        // 동기 함수 (즉시 값 반환)
        var callback = UTF8ToString(callbackId);
        var typeNameStr = UTF8ToString(typeName);

        try {
            var result = window.AppsInToss.saveBase64Data.isSupported();
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: true, data: JSON.stringify(result), error: '' })
            });
            SendMessage('AITCore', 'OnAITCallback', payload);
        } catch (error) {
            var payload = JSON.stringify({
                CallbackId: callback,
                TypeName: typeNameStr,
                Result: JSON.stringify({ success: false, data: '', error: error.message || String(error) })
            });
            setTimeout(function() { SendMessage('AITCore', 'OnAITCallback', payload); }, 0);
        }
    },

});
