using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// Sentry Envelope HTTP 전송 모듈.
    /// UnityWebRequest를 사용하여 큐잉 및 Rate Limiting을 지원합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class AITSentryTransport
    {
        private const int MaxQueueSize = 50;
        private const int MaxConcurrentRequests = 3;
        private const double FlushIntervalSeconds = 5.0;
        private const double DefaultRateLimitSeconds = 60.0;

        private static AITSentryEnvelope.DsnComponents _dsnComponents;
        private static bool _dsnSet;

        private static readonly Queue<string> _queue = new Queue<string>();
        private static readonly List<UnityWebRequestAsyncOperation> _activeRequests =
            new List<UnityWebRequestAsyncOperation>();

        private static double _lastFlushTime;
        private static double _rateLimitedUntil;

        private static readonly Dictionary<string, Action<SubmitResult>> _pendingCallbacks
            = new Dictionary<string, Action<SubmitResult>>();
        private static readonly Dictionary<UnityWebRequest, string> _requestEnvelopes
            = new Dictionary<UnityWebRequest, string>();

        // 참고: AITEditorErrorTracker의 static constructor가 SetDsn()을 호출합니다.
        // _queue, _activeRequests 등은 인라인 초기화로 선언되어 있어
        // static constructor 실행 순서에 관계없이 안전합니다.
        static AITSentryTransport()
        {
            _lastFlushTime = EditorApplication.timeSinceStartup;
            // 도메인 리로드 시 핸들러 중복 등록 방지
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            // FlushSync는 AITEditorErrorTracker.EndSession()에서 호출됨
            // 이중 등록하면 실행 순서에 따라 세션 종료 envelope이 플러시되지 않을 수 있음
        }

        #region Result Type

        internal struct SubmitResult
        {
            public bool Success;
            public string ErrorMessage;
            public int? HttpStatusCode;

            public static SubmitResult Ok() => new SubmitResult { Success = true };
            public static SubmitResult Fail(string msg, int? code = null)
                => new SubmitResult { Success = false, ErrorMessage = msg, HttpStatusCode = code };
        }

        #endregion

        #region Public API

        /// <summary>
        /// DSN을 설정합니다. 파싱된 컴포넌트를 캐시합니다.
        /// </summary>
        internal static void SetDsn(string dsn)
        {
            if (string.IsNullOrEmpty(dsn))
            {
                _dsnSet = false;
                return;
            }

            _dsnComponents = AITSentryEnvelope.ParseDsn(dsn);
            _dsnSet = true;
        }

        /// <summary>
        /// Envelope을 전송 큐에 추가합니다.
        /// Rate Limit 중이면 버립니다.
        /// </summary>
        internal static void SendEnvelope(string envelope)
        {
            if (string.IsNullOrEmpty(envelope) || !_dsnSet)
                return;

            if (IsRateLimited())
                return;

            if (_queue.Count >= MaxQueueSize)
            {
                // 가장 오래된 항목 제거
                _queue.Dequeue();
            }

            _queue.Enqueue(envelope);
        }

        /// <summary>
        /// Envelope을 전송 큐에 추가하고, 전송 완료 시 콜백을 호출합니다.
        /// 즉시 실패(DSN 미설정, 빈 envelope, Rate Limit)는 동기적으로 콜백을 호출합니다.
        /// </summary>
        internal static void SendEnvelope(string envelope, Action<SubmitResult> onComplete)
        {
            if (onComplete == null)
            {
                SendEnvelope(envelope);
                return;
            }

            if (string.IsNullOrEmpty(envelope))
            {
                onComplete(SubmitResult.Fail("빈 envelope"));
                return;
            }
            if (!_dsnSet)
            {
                onComplete(SubmitResult.Fail("DSN이 설정되지 않았습니다"));
                return;
            }
            if (IsRateLimited())
            {
                onComplete(SubmitResult.Fail("요청이 Rate Limit으로 거부되었습니다", 429));
                return;
            }
            if (_queue.Count >= MaxQueueSize)
            {
                // 가장 오래된 envelope이 드롭됨 — 해당 콜백에 실패 통보
                var dropped = _queue.Dequeue();
                if (_pendingCallbacks.TryGetValue(dropped, out var droppedCb))
                {
                    _pendingCallbacks.Remove(dropped);
                    droppedCb(SubmitResult.Fail("전송 큐가 가득 차 이전 요청이 드롭되었습니다"));
                }
            }

            _pendingCallbacks[envelope] = onComplete;
            _queue.Enqueue(envelope);
        }

        /// <summary>
        /// 큐에 있는 모든 Envelope을 동기적으로 전송합니다.
        /// 에디터 종료 시 호출됩니다.
        /// </summary>
        internal static void FlushSync()
        {
            if (!_dsnSet)
                return;

            // 완료된 활성 요청 정리
            CleanupCompletedRequests();

            // 종료 시 최대 3초, 최대 5개만 전송 (에디터 행 방지)
            const int maxFlushCount = 5;
            int flushed = 0;
            var flushDeadline = DateTime.UtcNow.AddSeconds(3);

            while (_queue.Count > 0 && flushed < maxFlushCount && DateTime.UtcNow < flushDeadline)
            {
                if (IsRateLimited())
                {
                    // Rate Limit 상태에서 남은 큐 항목의 콜백에 실패 통보
                    DrainQueueCallbacks(SubmitResult.Fail("요청이 Rate Limit으로 거부되었습니다", 429));
                    _queue.Clear();
                    return;
                }

                var envelope = _queue.Dequeue();
                SendSync(envelope, flushDeadline);
                flushed++;
            }

            // 타임아웃 또는 maxFlushCount 초과로 남은 큐 항목의 콜백에 실패 통보
            if (_queue.Count > 0)
            {
                DrainQueueCallbacks(SubmitResult.Fail("에디터 종료 중 전송 실패"));
            }
            _queue.Clear();
        }

        #endregion

        #region Update Loop

        private static void Update()
        {
            CleanupCompletedRequests();

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastFlushTime < FlushIntervalSeconds)
                return;

            _lastFlushTime = now;
            FlushQueue();
        }

        private static void FlushQueue()
        {
            while (_queue.Count > 0 && _activeRequests.Count < MaxConcurrentRequests)
            {
                if (IsRateLimited())
                {
                    _queue.Clear();
                    return;
                }

                var envelope = _queue.Dequeue();
                SendAsync(envelope);
            }
        }

        #endregion

        #region HTTP

        private static void SendAsync(string envelope)
        {
            var request = CreateRequest(envelope);
            if (request == null)
            {
                InvokeCallback(envelope, SubmitResult.Fail("요청 생성 실패"));
                return;
            }

            _requestEnvelopes[request] = envelope;
            var operation = request.SendWebRequest();
            operation.completed += op =>
            {
                _activeRequests.Remove((UnityWebRequestAsyncOperation)op);
                HandleResponse(request);
            };
            _activeRequests.Add(operation);
        }

        private static void SendSync(string envelope, DateTime deadline)
        {
            var request = CreateRequest(envelope);
            if (request == null)
                return;

            try
            {
                var operation = request.SendWebRequest();
                // 동기 대기 - 에디터 종료 시에만 사용 (deadline 초과 시 중단)
                while (!operation.isDone)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        request.Abort();
                        request.Dispose();
                        return;
                    }
                    System.Threading.Thread.Sleep(10);
                }
                HandleResponse(request);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AITSentryTransport] 동기 전송 실패: {e}");
                request.Dispose();
            }
        }

        private static UnityWebRequest CreateRequest(string envelope)
        {
            if (!_dsnSet)
                return null;

            var url = _dsnComponents.GetEnvelopeUrl();
            var bodyBytes = Encoding.UTF8.GetBytes(envelope);

            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.timeout = 5;
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/x-sentry-envelope");
            request.SetRequestHeader(
                "X-Sentry-Auth",
                $"Sentry sentry_key={_dsnComponents.PublicKey}, sentry_version=7, sentry_client=apps-in-toss.unity/{AITVersion.Version}"
            );

            return request;
        }

        private static void HandleResponse(UnityWebRequest request)
        {
            // result를 try 블록 밖에서 초기화하여 finally에서 확실하게 할당되도록 함
            var result = SubmitResult.Fail("알 수 없는 오류");
            try
            {
                // 연결 에러를 먼저 확인 (responseCode가 0일 수 있음)
#if UNITY_2020_1_OR_NEWER
                if (request.result == UnityWebRequest.Result.ConnectionError)
#else
                if (request.isNetworkError)
#endif
                {
                    Debug.LogWarning(
                        $"[AITSentryTransport] 네트워크 오류: {request.error}"
                    );
                    result = SubmitResult.Fail(request.error ?? "네트워크 오류");
                    return;
                }

                var statusCode = request.responseCode;

                if (statusCode == 429)
                {
                    ApplyRateLimitFromHeaders(request);
                    result = SubmitResult.Fail("Rate Limit", 429);
                    return;
                }

                if (statusCode >= 400)
                {
                    Debug.LogWarning(
                        $"[AITSentryTransport] Sentry 전송 실패 (HTTP {statusCode})"
                    );
                    result = SubmitResult.Fail($"HTTP {statusCode}", (int)statusCode);
                    return;
                }

                result = SubmitResult.Ok();
            }
            finally
            {
                if (_requestEnvelopes.TryGetValue(request, out var env))
                {
                    _requestEnvelopes.Remove(request);
                    InvokeCallback(env, result);
                }
                request.Dispose();
            }
        }

        #endregion

        #region Rate Limiting

        private static bool IsRateLimited()
        {
            return EditorApplication.timeSinceStartup < _rateLimitedUntil;
        }

        private static void ApplyRateLimitFromHeaders(UnityWebRequest request)
        {
            var now = EditorApplication.timeSinceStartup;

            // X-Sentry-Rate-Limits 헤더 우선 파싱
            // 형식: "60:error:key, 2700:default:organization"
            var rateLimitsHeader = request.GetResponseHeader("X-Sentry-Rate-Limits");
            if (!string.IsNullOrEmpty(rateLimitsHeader))
            {
                double maxRetryAfter = 0;
                var entries = rateLimitsHeader.Split(',');
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i].Trim();
                    var parts = entry.Split(':');
                    if (parts.Length > 0)
                    {
                        double seconds;
                        if (double.TryParse(
                                parts[0].Trim(),
                                NumberStyles.Number,
                                CultureInfo.InvariantCulture,
                                out seconds))
                        {
                            if (seconds > maxRetryAfter)
                                maxRetryAfter = seconds;
                        }
                    }
                }

                if (maxRetryAfter > 0)
                {
                    _rateLimitedUntil = now + maxRetryAfter;
                    return;
                }
            }

            // Retry-After 헤더 폴백
            var retryAfterHeader = request.GetResponseHeader("Retry-After");
            if (!string.IsNullOrEmpty(retryAfterHeader))
            {
                double retryAfterSeconds;
                if (double.TryParse(
                        retryAfterHeader.Trim(),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out retryAfterSeconds))
                {
                    _rateLimitedUntil = now + retryAfterSeconds;
                    return;
                }
            }

            // 기본값: 60초
            _rateLimitedUntil = now + DefaultRateLimitSeconds;
        }

        #endregion

        #region Callback Helpers

        private static void InvokeCallback(string envelope, SubmitResult result)
        {
            if (_pendingCallbacks.TryGetValue(envelope, out var cb))
            {
                _pendingCallbacks.Remove(envelope);
                EditorApplication.delayCall += () => cb(result);
            }
        }

        private static void DrainQueueCallbacks(SubmitResult result)
        {
            foreach (var envelope in _queue)
            {
                if (_pendingCallbacks.TryGetValue(envelope, out var cb))
                {
                    _pendingCallbacks.Remove(envelope);
                    var capturedResult = result;
                    EditorApplication.delayCall += () => cb(capturedResult);
                }
            }
        }

        #endregion

        #region Cleanup

        private static void CleanupCompletedRequests()
        {
            for (int i = _activeRequests.Count - 1; i >= 0; i--)
            {
                if (_activeRequests[i].isDone)
                {
                    _activeRequests.RemoveAt(i);
                }
            }
        }

        #endregion
    }
}
