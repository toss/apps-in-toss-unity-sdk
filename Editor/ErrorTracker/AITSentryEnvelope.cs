using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// Sentry Envelope 프로토콜 직렬화 모듈.
    /// StringBuilder만 사용하여 Error Event, Session, Transaction envelope을 생성합니다.
    /// </summary>
    internal static class AITSentryEnvelope
    {
        private const string SdkName = "apps-in-toss.unity";
        private const string Platform = "csharp";

        #region Public Types

        internal struct DsnComponents
        {
            public string PublicKey;
            public string Host;
            public string ProjectId;

            public string GetEnvelopeUrl()
            {
                return $"https://{Host}/api/{ProjectId}/envelope/";
            }
        }

        internal struct Breadcrumb
        {
            public DateTime Timestamp;
            public string Category;
            public string Message;
            public string Level;
        }

        internal struct SpanData
        {
            public string SpanId;
            public string ParentSpanId;
            public string Op;
            public string Description;
            public string Status;
            public DateTime StartTimestamp;
            public DateTime EndTimestamp;
        }

        #endregion

        #region DSN Parsing

        internal static DsnComponents ParseDsn(string dsn)
        {
            // Format: https://{PUBLIC_KEY}@{HOST}/{PROJECT_ID}
            var uri = new Uri(dsn);
            var publicKey = uri.UserInfo;
            var host = uri.Host;
            if (uri.Port != -1 && uri.Port != 443 && uri.Port != 80)
            {
                host = $"{uri.Host}:{uri.Port}";
            }
            var projectId = uri.AbsolutePath.TrimStart('/');

            return new DsnComponents
            {
                PublicKey = publicKey,
                Host = host,
                ProjectId = projectId,
            };
        }

        #endregion

        #region ID Generation

        internal static string GenerateEventId()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string GenerateTraceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string GenerateSpanId()
        {
            // 16-char hex from first 8 bytes of a GUID
            return Guid.NewGuid().ToString("N").Substring(0, 16);
        }

        #endregion

        #region Error Event Envelope

        internal static string BuildErrorEventEnvelope(
            string dsn,
            string exceptionType,
            string exceptionValue,
            string stackTrace,
            string level = "error",
            Dictionary<string, string> tags = null,
            Dictionary<string, string> extra = null,
            List<Breadcrumb> breadcrumbs = null,
            string[] fingerprint = null,
            string release = null,
            string environment = null)
        {
            return BuildErrorEventEnvelope(
                dsn, exceptionType, exceptionValue, stackTrace,
                out _,
                level, tags, extra, breadcrumbs, fingerprint, release, environment);
        }

        internal static string BuildErrorEventEnvelope(
            string dsn,
            string exceptionType,
            string exceptionValue,
            string stackTrace,
            out string eventId,
            string level = "error",
            Dictionary<string, string> tags = null,
            Dictionary<string, string> extra = null,
            List<Breadcrumb> breadcrumbs = null,
            string[] fingerprint = null,
            string release = null,
            string environment = null)
        {
            var dsnComponents = ParseDsn(dsn);
            eventId = GenerateEventId();
            var now = DateTime.UtcNow;
            var timestamp = FormatTimestamp(now);

            if (string.IsNullOrEmpty(release))
            {
                release = $"{SdkName}@{AITVersion.Version}";
            }

            var sb = new StringBuilder(4096);

            // Envelope header (에러 이벤트는 독립적으로 전송되므로 trace context 생략 —
            // Transaction과의 상관관계가 필요하면 추후 trace_id 파라미터 추가)
            BuildEnvelopeHeader(sb, eventId, dsnComponents, now);
            sb.Append('\n');

            // Item header
            sb.Append("{\"type\":\"event\"}");
            sb.Append('\n');

            // Event payload
            sb.Append('{');
            AppendJsonKeyValue(sb, "event_id", eventId, false);
            AppendJsonKeyValue(sb, "timestamp", timestamp);
            AppendJsonKeyValue(sb, "platform", Platform);
            AppendJsonKeyValue(sb, "level", level);

            if (!string.IsNullOrEmpty(release))
            {
                AppendJsonKeyValue(sb, "release", release);
            }
            if (!string.IsNullOrEmpty(environment))
            {
                AppendJsonKeyValue(sb, "environment", environment);
            }

            // Exception
            sb.Append(",\"exception\":{\"values\":[{");
            AppendJsonKeyValue(sb, "type", exceptionType, false);
            AppendJsonKeyValue(sb, "value", exceptionValue);

            // Stacktrace
            var frames = ParseStackTrace(stackTrace);
            if (frames.Count > 0)
            {
                sb.Append(",\"stacktrace\":{\"frames\":[");
                for (int i = 0; i < frames.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendStackFrame(sb, frames[i]);
                }
                sb.Append("]}");
            }

            sb.Append("}]}");

            // Tags
            if (tags != null && tags.Count > 0)
            {
                sb.Append(",\"tags\":{");
                AppendDictionary(sb, tags);
                sb.Append('}');
            }

            // Extra
            if (extra != null && extra.Count > 0)
            {
                sb.Append(",\"extra\":{");
                AppendDictionary(sb, extra);
                sb.Append('}');
            }

            // Breadcrumbs
            if (breadcrumbs != null && breadcrumbs.Count > 0)
            {
                sb.Append(",\"breadcrumbs\":{\"values\":[");
                for (int i = 0; i < breadcrumbs.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var bc = breadcrumbs[i];
                    sb.Append('{');
                    AppendJsonKeyValue(sb, "timestamp", FormatTimestamp(bc.Timestamp), false);
                    if (!string.IsNullOrEmpty(bc.Category))
                    {
                        AppendJsonKeyValue(sb, "category", bc.Category);
                    }
                    if (!string.IsNullOrEmpty(bc.Message))
                    {
                        AppendJsonKeyValue(sb, "message", bc.Message);
                    }
                    if (!string.IsNullOrEmpty(bc.Level))
                    {
                        AppendJsonKeyValue(sb, "level", bc.Level);
                    }
                    sb.Append('}');
                }
                sb.Append("]}");
            }

            // Fingerprint
            if (fingerprint != null && fingerprint.Length > 0)
            {
                sb.Append(",\"fingerprint\":[");
                for (int i = 0; i < fingerprint.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"');
                    sb.Append(EscapeJson(fingerprint[i]));
                    sb.Append('"');
                }
                sb.Append(']');
            }

            // Contexts
            AppendContexts(sb);

            // SDK info
            AppendSdkInfoObject(sb, true);

            sb.Append('}');

            return sb.ToString();
        }

        #endregion

        #region Message Event Envelope

        /// <summary>
        /// 메시지 기반 이벤트 envelope을 생성하고 event_id를 반환합니다.
        /// 예외 없이 단순 메시지로 이벤트를 만들고 싶을 때 사용합니다 (User Feedback 수동 제보 등).
        /// </summary>
        internal static string BuildMessageEventEnvelope(
            string dsn,
            string message,
            string level,
            Dictionary<string, string> tags,
            List<Breadcrumb> breadcrumbs,
            string release,
            string environment,
            out string eventId)
        {
            var dsnComponents = ParseDsn(dsn);
            eventId = GenerateEventId();
            var now = DateTime.UtcNow;
            var timestamp = FormatTimestamp(now);

            if (string.IsNullOrEmpty(release))
            {
                release = $"{SdkName}@{AITVersion.Version}";
            }

            var sb = new StringBuilder(2048);
            BuildEnvelopeHeader(sb, eventId, dsnComponents, now);
            sb.Append('\n');
            sb.Append("{\"type\":\"event\"}");
            sb.Append('\n');

            sb.Append('{');
            AppendJsonKeyValue(sb, "event_id", eventId, false);
            AppendJsonKeyValue(sb, "timestamp", timestamp);
            AppendJsonKeyValue(sb, "platform", Platform);
            AppendJsonKeyValue(sb, "level", string.IsNullOrEmpty(level) ? "info" : level);
            if (!string.IsNullOrEmpty(release))
                AppendJsonKeyValue(sb, "release", release);
            if (!string.IsNullOrEmpty(environment))
                AppendJsonKeyValue(sb, "environment", environment);

            // Sentry spec: message field is an object { "formatted": "..." }
            sb.Append(",\"message\":{");
            AppendJsonKeyValue(sb, "formatted", message ?? string.Empty, false);
            sb.Append('}');

            if (tags != null && tags.Count > 0)
            {
                sb.Append(",\"tags\":{");
                AppendDictionary(sb, tags);
                sb.Append('}');
            }

            if (breadcrumbs != null && breadcrumbs.Count > 0)
            {
                sb.Append(",\"breadcrumbs\":{\"values\":[");
                for (int i = 0; i < breadcrumbs.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var bc = breadcrumbs[i];
                    sb.Append('{');
                    AppendJsonKeyValue(sb, "timestamp", FormatTimestamp(bc.Timestamp), false);
                    if (!string.IsNullOrEmpty(bc.Category)) AppendJsonKeyValue(sb, "category", bc.Category);
                    if (!string.IsNullOrEmpty(bc.Message)) AppendJsonKeyValue(sb, "message", bc.Message);
                    if (!string.IsNullOrEmpty(bc.Level)) AppendJsonKeyValue(sb, "level", bc.Level);
                    sb.Append('}');
                }
                sb.Append("]}");
            }

            AppendContexts(sb);
            AppendSdkInfoObject(sb, true);
            sb.Append('}');

            return sb.ToString();
        }

        #endregion

        #region User Report Item

        /// <summary>
        /// 기존 에러 이벤트(eventId)에 user_report만 추가로 붙이기 위한 독립 envelope을 생성합니다.
        /// envelope header + user_report item 으로 구성됩니다 (event item 없음).
        /// Build 실패 다이얼로그 등에서 직전에 캡처된 에러 이벤트에 사용자 의견을 연결할 때 사용합니다.
        /// </summary>
        internal static string BuildStandaloneUserReportEnvelope(
            string dsn,
            string eventId,
            string email,
            string name,
            string comments)
        {
            var dsnComponents = ParseDsn(dsn);
            var now = DateTime.UtcNow;

            var sb = new StringBuilder(512);
            BuildEnvelopeHeader(sb, eventId, dsnComponents, now);
            sb.Append('\n');
            sb.Append(BuildUserReportItem(eventId, email, name, comments));
            return sb.ToString();
        }

        /// <summary>
        /// Sentry Envelope 의 user_report 아이템을 문자열로 생성합니다.
        /// header + payload 두 줄 구성. 반환값은 개행(\n)으로 구분되어 있습니다.
        /// </summary>
        internal static string BuildUserReportItem(
            string eventId,
            string email,
            string name,
            string comments)
        {
            var payload = new StringBuilder(256);
            payload.Append('{');
            AppendJsonKeyValue(payload, "event_id", eventId, false);
            if (!string.IsNullOrEmpty(email))
                AppendJsonKeyValue(payload, "email", email);
            if (!string.IsNullOrEmpty(name))
                AppendJsonKeyValue(payload, "name", name);
            if (!string.IsNullOrEmpty(comments))
                AppendJsonKeyValue(payload, "comments", comments);
            payload.Append('}');

            string payloadStr = payload.ToString();
            int length = Encoding.UTF8.GetByteCount(payloadStr);

            var sb = new StringBuilder(payloadStr.Length + 64);
            sb.Append("{\"type\":\"user_report\",\"length\":");
            sb.Append(length);
            sb.Append("}\n");
            sb.Append(payloadStr);
            return sb.ToString();
        }

        #endregion

        #region Attachment Item

        /// <summary>
        /// Sentry Envelope 의 attachment 아이템을 생성합니다.
        /// header 줄과 바이너리 payload를 개행으로 연결한 문자열을 반환합니다.
        /// 바이너리 안전성을 위해 payload는 ISO-8859-1 (Latin-1) 1:1 매핑으로 문자열화 합니다.
        /// 실제 UTF-8 기반 전송 파이프라인에서 byte 깨짐 방지를 위해서는 바이트 기반 envelope 경로가 필요하며,
        /// 이는 Task 7 (보류)에서 다룹니다. 이 helper는 계약만 제공합니다.
        /// </summary>
        internal static string BuildAttachmentItem(byte[] data, string filename, string contentType)
        {
            if (data == null) data = System.Array.Empty<byte>();

            var header = new StringBuilder(128);
            header.Append("{\"type\":\"attachment\",\"length\":");
            header.Append(data.Length);
            header.Append(",\"filename\":\"");
            header.Append(EscapeJson(filename ?? "attachment.bin"));
            header.Append("\",\"content_type\":\"");
            header.Append(EscapeJson(contentType ?? "application/octet-stream"));
            header.Append("\"}");

            string payload = Encoding.GetEncoding(28591).GetString(data);

            return header.ToString() + "\n" + payload;
        }

        #endregion

        #region Session Envelope

        internal static string BuildSessionEnvelope(
            string dsn,
            string sessionId,
            string distinctId,
            string status,
            bool isInit,
            DateTime started,
            int errorCount,
            double duration,
            string release = null,
            string environment = null)
        {
            var now = DateTime.UtcNow;

            if (string.IsNullOrEmpty(release))
            {
                release = $"{SdkName}@{AITVersion.Version}";
            }

            var sb = new StringBuilder(1024);

            // Envelope header (no event_id for sessions)
            sb.Append('{');
            AppendJsonKeyValue(sb, "dsn", dsn, false);
            AppendSdkInfoObject(sb, true);
            sb.Append(",\"sent_at\":\"");
            sb.Append(FormatTimestamp(now));
            sb.Append('"');
            sb.Append('}');
            sb.Append('\n');

            // Item header
            sb.Append("{\"type\":\"session\"}");
            sb.Append('\n');

            // Session payload
            sb.Append('{');
            AppendJsonKeyValue(sb, "sid", sessionId, false);
            AppendJsonKeyValue(sb, "did", distinctId);
            AppendJsonKeyValue(sb, "status", status);

            if (isInit)
            {
                sb.Append(",\"init\":true");
            }

            sb.Append(",\"started\":\"");
            sb.Append(FormatTimestamp(started));
            sb.Append('"');

            sb.Append(",\"errors\":");
            sb.Append(errorCount);

            sb.Append(",\"duration\":");
            sb.Append(duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));

            sb.Append(",\"attrs\":{");
            AppendJsonKeyValue(sb, "release", release, false);
            if (!string.IsNullOrEmpty(environment))
            {
                AppendJsonKeyValue(sb, "environment", environment);
            }
            sb.Append('}');

            sb.Append('}');

            return sb.ToString();
        }

        #endregion

        #region Transaction Envelope

        internal static string BuildTransactionEnvelope(
            string dsn,
            string transactionName,
            string op,
            string traceId,
            string spanId,
            DateTime startTimestamp,
            DateTime endTimestamp,
            string status,
            List<SpanData> spans = null,
            Dictionary<string, string> tags = null,
            Dictionary<string, double> measurements = null,
            string release = null,
            string environment = null)
        {
            var dsnComponents = ParseDsn(dsn);
            var eventId = GenerateEventId();
            var now = DateTime.UtcNow;

            if (string.IsNullOrEmpty(release))
            {
                release = $"{SdkName}@{AITVersion.Version}";
            }

            var sb = new StringBuilder(4096);

            // Envelope header (trace context 포함 — Sentry relay 라우팅에 필요)
            string traceField = $",\"trace\":{{\"trace_id\":\"{EscapeJson(traceId)}\",\"public_key\":\"{EscapeJson(dsnComponents.PublicKey)}\"}}";
            BuildEnvelopeHeader(sb, eventId, dsnComponents, now, traceField);
            sb.Append('\n');

            // Item header
            sb.Append("{\"type\":\"transaction\"}");
            sb.Append('\n');

            // Transaction payload
            sb.Append('{');
            AppendJsonKeyValue(sb, "event_id", eventId, false);
            AppendJsonKeyValue(sb, "type", "transaction");
            AppendJsonKeyValue(sb, "transaction", transactionName);

            sb.Append(",\"start_timestamp\":");
            sb.Append(FormatTimestampUnix(startTimestamp));
            sb.Append(",\"timestamp\":");
            sb.Append(FormatTimestampUnix(endTimestamp));

            AppendJsonKeyValue(sb, "platform", Platform);

            if (!string.IsNullOrEmpty(release))
            {
                AppendJsonKeyValue(sb, "release", release);
            }
            if (!string.IsNullOrEmpty(environment))
            {
                AppendJsonKeyValue(sb, "environment", environment);
            }

            // Contexts (trace + OS + runtime)
            sb.Append(",\"contexts\":{\"trace\":{");
            AppendJsonKeyValue(sb, "trace_id", traceId, false);
            AppendJsonKeyValue(sb, "span_id", spanId);
            AppendJsonKeyValue(sb, "op", op);
            if (!string.IsNullOrEmpty(status))
            {
                AppendJsonKeyValue(sb, "status", status);
            }
            sb.Append("},");
            // OS context
            sb.Append("\"os\":{");
            AppendJsonKeyValue(sb, "name", SystemInfo.operatingSystemFamily.ToString(), false);
            AppendJsonKeyValue(sb, "version", SystemInfo.operatingSystem);
            sb.Append("},");
            // Runtime context
            sb.Append("\"runtime\":{");
            AppendJsonKeyValue(sb, "name", "Unity", false);
            AppendJsonKeyValue(sb, "version", Application.unityVersion);
            sb.Append("}}");  // close runtime + contexts

            // Spans
            if (spans != null && spans.Count > 0)
            {
                sb.Append(",\"spans\":[");
                for (int i = 0; i < spans.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var span = spans[i];
                    sb.Append('{');
                    AppendJsonKeyValue(sb, "span_id", span.SpanId, false);
                    if (!string.IsNullOrEmpty(span.ParentSpanId))
                    {
                        AppendJsonKeyValue(sb, "parent_span_id", span.ParentSpanId);
                    }
                    AppendJsonKeyValue(sb, "op", span.Op);
                    if (!string.IsNullOrEmpty(span.Description))
                    {
                        AppendJsonKeyValue(sb, "description", span.Description);
                    }
                    sb.Append(",\"start_timestamp\":");
                    sb.Append(FormatTimestampUnix(span.StartTimestamp));
                    sb.Append(",\"timestamp\":");
                    sb.Append(FormatTimestampUnix(span.EndTimestamp));
                    if (!string.IsNullOrEmpty(span.Status))
                    {
                        AppendJsonKeyValue(sb, "status", span.Status);
                    }
                    sb.Append('}');
                }
                sb.Append(']');
            }

            // Tags
            if (tags != null && tags.Count > 0)
            {
                sb.Append(",\"tags\":{");
                AppendDictionary(sb, tags);
                sb.Append('}');
            }

            // Measurements
            if (measurements != null && measurements.Count > 0)
            {
                sb.Append(",\"measurements\":{");
                bool first = true;
                foreach (var kvp in measurements)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"');
                    sb.Append(EscapeJson(kvp.Key));
                    sb.Append("\":{\"value\":");
                    sb.Append(kvp.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append('}');
                }
                sb.Append('}');
            }

            // SDK info
            AppendSdkInfoObject(sb, true);

            sb.Append('}');

            return sb.ToString();
        }

        #endregion

        #region Stack Trace Parsing

        // Pattern: at Namespace.Class.Method(args) in /path/file.cs:line
        private static readonly Regex StackFrameRegex = new Regex(
            @"^\s*at\s+(.+?)\s*(?:\(([^)]*)\))?\s*(?:(?:\[0x[0-9a-fA-F]+\]\s+)?in\s+(.+?):(\d+))?\s*$",
            RegexOptions.Compiled
        );

        // Alternate pattern for frames without "in" clause
        private static readonly Regex StackFrameNoFileRegex = new Regex(
            @"^\s*at\s+(.+?)\s*(?:\(([^)]*)\))?\s*$",
            RegexOptions.Compiled
        );

        internal struct StackFrame
        {
            public string Function;
            public string Module;
            public string Filename;
            public int? LineNumber;
        }

        internal static StackFrame? ParseStackFrame(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            var match = StackFrameRegex.Match(line);
            if (match.Success)
            {
                var fullMethod = match.Groups[1].Value;
                var args = match.Groups[2].Success ? match.Groups[2].Value : "";
                var file = match.Groups[3].Success ? match.Groups[3].Value : null;
                int? lineNumber = match.Groups[4].Success ? (int?)int.Parse(match.Groups[4].Value) : null;

                SplitMethodName(fullMethod, out var module, out var function);

                return new StackFrame
                {
                    Function = !string.IsNullOrEmpty(args) ? $"{function}({args})" : function,
                    Module = module,
                    Filename = file != null ? SanitizeFilePath(file) : null,
                    LineNumber = lineNumber,
                };
            }

            var matchNoFile = StackFrameNoFileRegex.Match(line);
            if (matchNoFile.Success)
            {
                var fullMethod = matchNoFile.Groups[1].Value;
                var args = matchNoFile.Groups[2].Success ? matchNoFile.Groups[2].Value : "";

                SplitMethodName(fullMethod, out var module, out var function);

                return new StackFrame
                {
                    Function = !string.IsNullOrEmpty(args) ? $"{function}({args})" : function,
                    Module = module,
                    Filename = null,
                    LineNumber = null,
                };
            }

            return null;
        }

        internal static List<StackFrame> ParseStackTrace(string stackTrace)
        {
            var frames = new List<StackFrame>();
            if (string.IsNullOrEmpty(stackTrace))
                return frames;

            var lines = stackTrace.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var frame = ParseStackFrame(lines[i]);
                if (frame.HasValue)
                {
                    frames.Add(frame.Value);
                }
            }

            // Sentry convention: oldest frame first
            frames.Reverse();
            return frames;
        }

        private static void SplitMethodName(string fullMethod, out string module, out string function)
        {
            var lastDot = fullMethod.LastIndexOf('.');
            if (lastDot > 0)
            {
                module = fullMethod.Substring(0, lastDot);
                function = fullMethod.Substring(lastDot + 1);
            }
            else
            {
                module = null;
                function = fullMethod;
            }
        }

        internal static string SanitizeFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;

            // Normalize separators
            var normalized = filePath.Replace('\\', '/');

            // Keep paths starting with Assets/ or Packages/
            var assetsIndex = normalized.IndexOf("Assets/", StringComparison.Ordinal);
            if (assetsIndex >= 0)
            {
                return normalized.Substring(assetsIndex);
            }

            var packagesIndex = normalized.IndexOf("Packages/", StringComparison.Ordinal);
            if (packagesIndex >= 0)
            {
                return normalized.Substring(packagesIndex);
            }

            // Library/PackageCache/ 경로 보존 (유용한 디버깅 컨텍스트)
            var libraryIndex = normalized.IndexOf("Library/PackageCache/", StringComparison.Ordinal);
            if (libraryIndex >= 0)
            {
                return normalized.Substring(libraryIndex);
            }

            // Fallback: 파일명만 반환 (사용자 홈 디렉토리 등 PII 제거)
            int lastSlash = normalized.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < normalized.Length - 1)
            {
                return normalized.Substring(lastSlash + 1);
            }

            return normalized;
        }

        #endregion

        #region JSON Helpers

        // BMP 범위(U+0000~U+FFFF)만 처리 — 에러 메시지/스택 트레이스에서 surrogate pair는 실질적으로 발생하지 않음
        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Private Helpers

        private static string FormatTimestamp(DateTime utcTime)
        {
            return utcTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 호출자는 반드시 DateTime.UtcNow 또는 DateTimeKind.Utc 값을 전달해야 합니다
        private static string FormatTimestampUnix(DateTime utcTime)
        {
            var seconds = (utcTime - UnixEpoch).TotalSeconds;
            return seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Envelope 헤더를 StringBuilder에 추가합니다.
        /// extraFields가 제공되면 닫는 '}' 앞에 삽입됩니다 (콤마 포함 문자열, 예: ",\"trace\":{...}").
        /// </summary>
        private static void BuildEnvelopeHeader(StringBuilder sb, string eventId, DsnComponents dsn, DateTime sentAt, string extraFields = null)
        {
            sb.Append('{');
            AppendJsonKeyValue(sb, "event_id", eventId, false);
            AppendJsonKeyValue(sb, "dsn", $"https://{dsn.PublicKey}@{dsn.Host}/{dsn.ProjectId}");
            AppendSdkInfoObject(sb, true);
            sb.Append(",\"sent_at\":\"");
            sb.Append(FormatTimestamp(sentAt));
            sb.Append('"');
            if (!string.IsNullOrEmpty(extraFields))
                sb.Append(extraFields);
            sb.Append('}');
        }

        private static void AppendSdkInfoObject(StringBuilder sb, bool withComma)
        {
            if (withComma) sb.Append(',');
            sb.Append("\"sdk\":{");
            AppendJsonKeyValue(sb, "name", SdkName, false);
            AppendJsonKeyValue(sb, "version", AITVersion.Version);
            sb.Append('}');
        }

        private static void AppendContexts(StringBuilder sb)
        {
            sb.Append(",\"contexts\":{");

            // OS context
            sb.Append("\"os\":{");
            AppendJsonKeyValue(sb, "name", SystemInfo.operatingSystemFamily.ToString(), false);
            AppendJsonKeyValue(sb, "version", SystemInfo.operatingSystem);
            sb.Append("},");

            // Runtime context
            sb.Append("\"runtime\":{");
            AppendJsonKeyValue(sb, "name", "Unity", false);
            AppendJsonKeyValue(sb, "version", Application.unityVersion);
            sb.Append('}');

            sb.Append('}');
        }

        private static void AppendStackFrame(StringBuilder sb, StackFrame frame)
        {
            sb.Append('{');
            bool hasContent = false;

            if (!string.IsNullOrEmpty(frame.Function))
            {
                AppendJsonKeyValue(sb, "function", frame.Function, false);
                hasContent = true;
            }
            if (!string.IsNullOrEmpty(frame.Module))
            {
                AppendJsonKeyValue(sb, "module", frame.Module, hasContent);
                hasContent = true;
            }
            if (!string.IsNullOrEmpty(frame.Filename))
            {
                AppendJsonKeyValue(sb, "filename", frame.Filename, hasContent);
                hasContent = true;
            }
            if (frame.LineNumber.HasValue)
            {
                sb.Append(hasContent ? "," : "");
                sb.Append("\"lineno\":");
                sb.Append(frame.LineNumber.Value);
            }

            sb.Append('}');
        }

        private static void AppendJsonKeyValue(StringBuilder sb, string key, string value, bool prependComma = true)
        {
            if (prependComma) sb.Append(',');
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":\"");
            sb.Append(EscapeJson(value));
            sb.Append('"');
        }

        private static void AppendDictionary(StringBuilder sb, Dictionary<string, string> dict)
        {
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(EscapeJson(kvp.Key));
                sb.Append("\":\"");
                sb.Append(EscapeJson(kvp.Value));
                sb.Append('"');
            }
        }

        #endregion
    }
}
