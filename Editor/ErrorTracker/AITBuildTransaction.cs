using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// 빌드 파이프라인 단계를 Sentry Transaction/Span으로 추적하는 모듈.
    /// </summary>
    internal class AITBuildTransaction
    {
        private readonly string _name;
        private readonly string _traceId;
        private readonly string _rootSpanId;
        private readonly DateTime _startTimestamp;
        private readonly List<AITSentryEnvelope.SpanData> _spans = new List<AITSentryEnvelope.SpanData>();
        private readonly Dictionary<string, string> _tags = new Dictionary<string, string>();
        private readonly Dictionary<string, double> _measurements = new Dictionary<string, double>();

        internal AITBuildTransaction(string name)
        {
            _name = name;
            _traceId = AITSentryEnvelope.GenerateTraceId();
            _rootSpanId = AITSentryEnvelope.GenerateSpanId();
            _startTimestamp = DateTime.UtcNow;

            _tags["sdk_version"] = AITVersion.Version;
            _tags["unity_version"] = Application.unityVersion;
            _tags["os"] = SystemInfo.operatingSystem;
            _tags["editor_platform"] = Application.platform.ToString();
        }

        internal void SetTag(string key, string value)
        {
            _tags[key] = value;
        }

        internal void SetMeasurement(string name, double valueMs)
        {
            _measurements[name] = valueMs;
        }

        internal Span StartSpan(string op, string description)
        {
            return new Span(this, op, description);
        }

        internal void Finish(string status)
        {
            if (!AITErrorTrackerConsent.IsEnabled())
                return;

            string dsn = AITEditorErrorTracker.GetDsn();
            if (string.IsNullOrEmpty(dsn))
                return;

            DateTime endTimestamp = DateTime.UtcNow;
            string release = AITEditorErrorTracker.GetRelease();

            string envelope = AITSentryEnvelope.BuildTransactionEnvelope(
                dsn: dsn,
                transactionName: _name,
                op: "build",
                traceId: _traceId,
                spanId: _rootSpanId,
                startTimestamp: _startTimestamp,
                endTimestamp: endTimestamp,
                status: status,
                spans: _spans,
                tags: _tags,
                measurements: _measurements,
                release: release,
                environment: "editor"
            );

            AITSentryTransport.SendEnvelope(envelope);
        }

        internal class Span
        {
            private readonly AITBuildTransaction _parent;
            private readonly string _spanId;
            private readonly string _op;
            private readonly string _description;
            private readonly DateTime _startTimestamp;

            internal Span(AITBuildTransaction parent, string op, string description)
            {
                _parent = parent;
                _spanId = AITSentryEnvelope.GenerateSpanId();
                _op = op;
                _description = description;
                _startTimestamp = DateTime.UtcNow;
            }

            internal void Finish(string status = "ok")
            {
                DateTime endTimestamp = DateTime.UtcNow;
                _parent._spans.Add(new AITSentryEnvelope.SpanData
                {
                    SpanId = _spanId,
                    ParentSpanId = _parent._rootSpanId,
                    Op = _op,
                    Description = _description,
                    Status = status,
                    StartTimestamp = _startTimestamp,
                    EndTimestamp = endTimestamp
                });
            }
        }
    }
}
