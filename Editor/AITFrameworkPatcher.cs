// -----------------------------------------------------------------------
// AITFrameworkPatcher.cs - 빌드 후 Unity framework(*.framework.js[.br|.gz]) 텍스트 패치 (오디오 + 스택 트레이스 지연)
//
// 호출 지점: WebGLBuildCopier.ApplyBuildPatches — Unity 산출물이 buildSrc 에 놓인 직후,
//           brotli 재압축·페이지 캐시·warm manifest 산출보다 앞.
//
// === 무엇을 패치하나 (WEBAudio 영역만) ===
//  mime-map                    FMOD 사운드 타입 → mime 매핑에 ogg/flac/aiff 추가(압축 경로가 mp4 로 잘못 표기하던 것)
//  compressed-clip-meta        압축 클립의 길이·채널·주파수를 컨테이너 헤더 probe 로 채운다.
//                              (stock 은 media element 메타데이터 로드 전 duration=NaN → clip.length=0, AudioSource.time NaN)
//                              probe 가 정확한 길이를 주면 메타데이터용 Audio 요소를 아예 만들지 않는다.
//  media-source-pitch-position 재생용 media element 의 preservesPitch 해제(Unity pitch=playbackRate)와 재생 위치 추정을 currentTime 으로.
//  media-source-release        채널 해제(disconnect) 때 media element 의 src 도 놓아 디코더·버퍼를 즉시 반환
//  sound-load-prologue         _JS_Sound_Load 앞단: HEAPU8 를 복사하지 않고(subarray) 압축 클립을 만든다.
//                              audioForceCompressedPlayback 이 켜져 있으면 Unity 가 DecompressOnLoad(PCM 상주)로 둔 긴 클립도
//                              압축 상태 media element 로 돌린다. 강제 여부·최소 길이는 패치 시점에 literal 로 박는다.
//
// === 오디오와 독립인 그룹 ===
//  stacktrace-lazy             Unity prejs/Error.js 가 framework 함수 최상위에서 jsStackTrace() 를 불러 Module.stackTraceRegExp 를
//                              만드는 문장을, 첫 접근 때 계산하는 getter 로 바꾼다. 스택을 문자열로 만들 때 V8 이 바깥 framework
//                              함수 전체를 다시 파싱(소스 위치 수집)하는 비용이 부팅에서 빠진다. 소비자는 로더 errorHandler 뿐이다.
//                              마커 /*ait-stacklazy1*/ . 오디오 패치가 꺼져 있어도 단독으로 적용된다(frameworkLazyStackTraceMode).
//
// === 안전 계약 ===
//  - 그룹 단위로 원자적이다. 그룹의 모든 앵커가 현재 텍스트에서 정확히 1회 일치할 때만 적용하고,
//    아니면 그 그룹만 건너뛰며 경고를 남긴다(Unity 패치 릴리스로 문자열이 바뀌어도 stock 그대로 동작).
//  - prologue 는 가장 마지막에, 선행 그룹(compressed-clip-meta, media-source-pitch-position)이 모두 적용됐을 때만 적용한다.
//  - 멱등: 이미 패치 마커가 있으면 아무것도 하지 않는다.
//  - 결과가 `node --check` 를 통과하고 재압축 라운드트립 검증이 끝났을 때만 채택한다. 어떤 실패도 예외를 밖으로 던지지 않는다(fail-open).
//  - Decompression Fallback(.unityweb)은 호출부가 거르고, 여기서도 .unityweb 가 있으면 건너뛴다(감지 마커 보호).
//  - 패치한 파일은 AITPatchedFileNaming 의 ".aitpN" 이름으로 옮기고 옛 이름 → 새 이름을 renames 에 기록한다.
//  - Unity 원본 소스 텍스트를 저장소에 두지 않는다(공개 저장소). 앵커는 매칭에 필요한 짧은 스니펫만 쓴다.
//  - 런타임 JS(컨테이너 probe 등)는 Editor/FrameworkPatch~/ait-audio-runtime.js 에 있다('~' 폴더라 Unity 미임포트).
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AppsInToss.Editor.Package;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    internal static class AITFrameworkPatcher
    {
        /// <summary>패치 마커. 이 문자열이 이미 있으면 재패치하지 않는다(버전 숫자는 patch-set 버전).</summary>
        internal const string MarkerPrefix = "/*ait-audio-patch";

        /// <summary>런타임 JS payload 파일(Editor/FrameworkPatch~ 안).</summary>
        internal const string RuntimePayloadDirName = "FrameworkPatch~";

        internal const string RuntimePayloadFileName = "ait-audio-runtime.js";

        // 그룹 이름(로그·테스트에서 쓴다).
        internal const string GroupMimeMap = "mime-map";
        internal const string GroupClipMeta = "compressed-clip-meta";
        internal const string GroupPitchPosition = "media-source-pitch-position";
        internal const string GroupSourceRelease = "media-source-release";
        internal const string GroupPrologue = "sound-load-prologue";
        internal const string GroupStackTraceLazy = "stacktrace-lazy";

        /// <summary>stacktrace-lazy 그룹의 패치 마커. 오디오 마커와 별개라 한쪽만 적용된 파일도 멱등이다.</summary>
        internal const string StackLazyMarker = "/*ait-stacklazy1*/";

        /// <summary>stacktrace-lazy 환경 변수 오버라이드(1/true = 강제 켬, 0/false = 끔).</summary>
        internal const string LazyStackTraceEnvVar = "AIT_FW_LAZY_STACKTRACE";

        private const int NodeTimeoutMs = 120000;

        // ─────────────────────────── 앵커·치환 (Unity 3종 버전에서 각각 정확히 1회 일치 확인) ───────────────────────────

        private const string MimeMapFrom =
            @"case 13:return""audio/mpeg"";case 20:return""audio/wav"";default:return""audio/mp4""";

        // FMOD 사운드 타입: 2 = AIFF, 7 = FLAC, 14 = OGGVORBIS. 컨테이너 probe 가 mime 을 주면 그쪽이 우선한다.
        private const string MimeMapTo =
            @"case 13:return""audio/mpeg"";case 20:return""audio/wav"";case 14:return""audio/ogg"";case 7:return""audio/flac"";case 2:return""audio/aiff"";default:return""audio/mp4""";

        private const string ClipMetaAFrom =
            @"function jsAudioCreateCompressedSoundClip(audioData,fmodSoundType){var mimeType=jsAudioGetMimeTypeFromType(fmodSoundType);var blob=new Blob([audioData],{type:mimeType});var soundClip={url:URL.createObjectURL(blob),error:false,mediaElement:new Audio};soundClip.mediaElement.preload=""metadata"";soundClip.mediaElement.src=soundClip.url;soundClip.release=function(){if(!this.mediaElement){return}this.mediaElement.src="""";URL.revokeObjectURL(this.url);delete this.mediaElement;delete this.url};soundClip.getLength=function(){return this.mediaElement.duration*44100};";

        private const string ClipMetaATo =
            @"function jsAudioCreateCompressedSoundClip(audioData,fmodSoundType,aitInfo){var mimeType=(aitInfo&&aitInfo.mime)||jsAudioGetMimeTypeFromType(fmodSoundType);var blob=new Blob([audioData],{type:mimeType});var soundClip={url:URL.createObjectURL(blob),error:false,aitInfo:aitInfo||null,mediaElement:null};if(!(aitInfo&&aitInfo.dur>0&&!aitInfo.approx)){soundClip.mediaElement=new Audio;soundClip.mediaElement.preload=""metadata"";soundClip.mediaElement.src=soundClip.url}soundClip.release=function(){if(this.mediaElement){this.mediaElement.src="""";delete this.mediaElement}if(this.url){URL.revokeObjectURL(this.url);delete this.url}};soundClip.getLength=function(){var d=this.mediaElement?this.mediaElement.duration:NaN;if(!(d>0&&isFinite(d))&&this.aitInfo&&this.aitInfo.dur>0)d=this.aitInfo.dur;return d*44100};";

        private const string ClipMetaBFrom =
            @"soundClip.getNumberOfChannels=function(){console.warn(""getNumberOfChannels() is not supported for compressed sound."");return 0};soundClip.getFrequency=function(){console.warn(""getFrequency() is not supported for compressed sound."");return 0};";

        private const string ClipMetaBTo =
            @"soundClip.getNumberOfChannels=function(){if(this.aitInfo&&this.aitInfo.ch)return this.aitInfo.ch;console.warn(""getNumberOfChannels() is not supported for compressed sound."");return 0};soundClip.getFrequency=function(){if(this.aitInfo&&this.aitInfo.rate)return this.aitInfo.rate;console.warn(""getFrequency() is not supported for compressed sound."");return 0};";

        private const string PitchAFrom =
            @"mediaElement.preload=""metadata"";mediaElement.src=this.url;var source=WEBAudio.audioContext.createMediaElementSource(mediaElement);";

        private const string PitchATo =
            @"mediaElement.preload=""metadata"";mediaElement.preservesPitch=false;mediaElement.webkitPreservesPitch=false;mediaElement.src=this.url;var source=WEBAudio.audioContext.createMediaElementSource(mediaElement);";

        private const string PitchBFrom =
            @"jsAudioMixinSetPitch(source);return source};return soundClip}function _JS_Sound_Load(";

        private const string PitchBTo =
            @"jsAudioMixinSetPitch(source);source.estimatePlaybackPosition=function(){return source.mediaElement.currentTime};return source};return soundClip}function _JS_Sound_Load(";

        private const string ReleaseAFrom =
            @"if(this.source.mediaElement){this.source._pauseMediaElement()}this.source.onended=null;this.source.disconnect();delete this.source};";

        private const string ReleaseATo =
            @"var aitM=this.source.mediaElement,aitSrc=this.source;if(aitM){this.source._pauseMediaElement()}this.source.onended=null;this.source.disconnect();delete this.source;if(aitM&&!aitSrc.playPromise&&!aitSrc.playTimeout){try{aitM.removeAttribute(""src"");aitM.load()}catch(e){}}};";

        private const string PrologueFrom =
            @"function _JS_Sound_Load(ptr,length,decompress,fmodSoundType){";

        // helper(payload + cfg) 뒤에 이어 붙는 본문. 131072 바이트 미만은 stock 이 항상 decompress=1 로 돌리는 영역이라 건드리지 않는다.
        private const string PrologueBody =
            @"function _JS_Sound_Load(ptr,length,decompress,fmodSoundType){if(WEBAudio.audioWebEnabled!=0&&length>=131072){var aitD=WEBAudio.aitDecide(ptr,length,decompress);if(aitD&&aitD.compressed){var aitS=jsAudioCreateCompressedSoundClip(HEAPU8.subarray(ptr,ptr+length),fmodSoundType,aitD.info);WEBAudio.aitLog(aitD,length,decompress);WEBAudio.audioInstances[++WEBAudio.audioInstanceIdCounter]=aitS;return WEBAudio.audioInstanceIdCounter}}";

        // ─────────────────────────── stacktrace-lazy (정규식 앵커 + 괄호 균형 스캔) ───────────────────────────

        // 앵커는 `var stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));if(stackTraceReferenceMatch)
        // Module.stackTraceRegExp=` 까지이고, 대입 우변(`new RegExp(...)`)은 정규식이 아니라 괄호 균형 스캔으로 잘라낸다.
        private static readonly Regex StackLazyAnchor = new Regex(
            @"var stackTraceReferenceMatch\s*=\s*jsStackTrace\(\)\.match\(new RegExp\(stackTraceReference\)\);?\s*"
            + @"if\s*\(stackTraceReferenceMatch\)\s*Module\.stackTraceRegExp\s*=\s*(?=new RegExp\()",
            RegexOptions.CultureInvariant);

        private const string StackLazyHead =
            "var stackTraceReferenceMatch;(function(){var c,d=false;Object.defineProperty(Module,\"stackTraceRegExp\","
            + "{configurable:true,enumerable:true,get:function(){if(!d){d=true;try{"
            + "stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));if(stackTraceReferenceMatch)c=";

        private const string StackLazyTail =
            "}catch(e){}}return c},set:function(v){d=true;c=v}})})();" + StackLazyMarker;

        /// <summary>
        /// stacktrace-lazy 적용. 앵커가 정확히 1회가 아니거나 우변 끝을 확신할 수 없으면 입력을 그대로 두고 false 와 사유를 돌려준다.
        /// </summary>
        internal static bool TryPatchStackTraceLazy(string text, out string patched, out string skipReason)
        {
            patched = text;
            skipReason = null;
            MatchCollection matches = string.IsNullOrEmpty(text) ? null : StackLazyAnchor.Matches(text);
            int n = matches == null ? 0 : matches.Count;
            if (n != 1)
            {
                skipReason = "앵커 1/1 일치 " + n + "회(기대 1회)";
                return false;
            }

            Match m = matches[0];
            int rhsStart = m.Index + m.Length;
            if (!TryScanRegExpCall(text, rhsStart, out int callEnd))
            {
                skipReason = "우변 new RegExp(...) 의 괄호 균형을 찾지 못함";
                return false;
            }

            // 문장 끝: 공백/탭 뒤에 ';'(함께 소비), '}'·개행·파일 끝(소비하지 않음)만 허용한다. 그 밖이면 우변이 더 이어지는 식이다.
            int j = callEnd;
            while (j < text.Length && (text[j] == ' ' || text[j] == '\t'))
            {
                j++;
            }

            int stmtEnd;
            if (j >= text.Length || text[j] == '}' || text[j] == '\n' || text[j] == '\r')
            {
                stmtEnd = callEnd;
            }
            else if (text[j] == ';')
            {
                stmtEnd = j + 1;
            }
            else
            {
                skipReason = "우변 뒤에 예상 밖 토큰 '" + text[j] + "'";
                return false;
            }

            string rhs = text.Substring(rhsStart, callEnd - rhsStart);
            patched = text.Substring(0, m.Index) + StackLazyHead + rhs + StackLazyTail + text.Substring(stmtEnd);
            return true;
        }

        // start 에서 `new RegExp(` 로 시작하는 호출의 닫는 괄호 다음 위치를 찾는다. 문자열·정규식 리터럴·주석 안의 괄호는 세지 않는다.
        // 템플릿 리터럴(`)은 안쪽 ${} 를 해석하지 않으므로 만나면 실패로 본다(건너뛰기).
        private static bool TryScanRegExpCall(string t, int start, out int callEnd)
        {
            callEnd = -1;
            const string head = "new RegExp(";
            if (start < 0 || start + head.Length > t.Length || string.CompareOrdinal(t, start, head, 0, head.Length) != 0)
            {
                return false;
            }

            int depth = 1;
            int i = start + head.Length;
            char prev = '(';
            while (i < t.Length)
            {
                char c = t[i];
                if (c == '"' || c == '\'')
                {
                    i = SkipStringLiteral(t, i);
                    if (i < 0) return false;
                    prev = 'a';
                    continue;
                }

                if (c == '`')
                {
                    return false;
                }

                if (c == '/')
                {
                    char next = i + 1 < t.Length ? t[i + 1] : '\0';
                    if (next == '/')
                    {
                        int nl = t.IndexOf('\n', i);
                        if (nl < 0) return false;
                        i = nl + 1;
                        continue;
                    }

                    if (next == '*')
                    {
                        int close = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                        if (close < 0) return false;
                        i = close + 2;
                        continue;
                    }

                    if ("(,=:[!&|?{};+-*%<>~^".IndexOf(prev) >= 0)
                    {
                        i = SkipRegexLiteral(t, i);
                        if (i < 0) return false;
                        prev = 'a';
                        continue;
                    }

                    prev = c;
                    i++;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        callEnd = i + 1;
                        return true;
                    }
                }

                if (!char.IsWhiteSpace(c))
                {
                    prev = c;
                }

                i++;
            }

            return false;
        }

        // t[i] 가 여는 따옴표. 닫는 따옴표 다음 위치를 돌려준다(개행이 먼저 나오거나 닫히지 않으면 -1).
        private static int SkipStringLiteral(string t, int i)
        {
            char quote = t[i];
            i++;
            while (i < t.Length)
            {
                char c = t[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '\n' || c == '\r') return -1;
                if (c == quote) return i + 1;
                i++;
            }

            return -1;
        }

        // t[i] 가 정규식 리터럴의 여는 '/'. 닫는 '/' 다음 위치(플래그 앞)를 돌려준다. [...] 클래스 안의 '/' 는 닫는 것이 아니다.
        private static int SkipRegexLiteral(string t, int i)
        {
            i++;
            bool inClass = false;
            while (i < t.Length)
            {
                char c = t[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '\n' || c == '\r') return -1;
                if (c == '[') inClass = true;
                else if (c == ']') inClass = false;
                else if (c == '/' && !inClass) return i + 1;
                i++;
            }

            return -1;
        }

        // ─────────────────────────── 패치 엔진(순수 로직, Node 불필요) ───────────────────────────

        /// <summary>앵커 → 치환 한 쌍.</summary>
        internal sealed class PatchEdit
        {
            internal readonly string Anchor;
            internal readonly string Replacement;

            internal PatchEdit(string anchor, string replacement)
            {
                Anchor = anchor;
                Replacement = replacement;
            }
        }

        /// <summary>한 번에 적용하거나 통째로 건너뛰는 편집 묶음.</summary>
        internal sealed class PatchGroup
        {
            internal readonly string Name;
            internal readonly PatchEdit[] Edits;

            /// <summary>이 그룹보다 먼저 적용돼 있어야 하는 그룹 이름(없으면 빈 배열).</summary>
            internal readonly string[] Requires;

            internal PatchGroup(string name, PatchEdit[] edits, string[] requires)
            {
                Name = name;
                Edits = edits;
                Requires = requires ?? new string[0];
            }
        }

        /// <summary>패치 결과. Source 는 하나도 적용하지 못했으면 입력 그대로다.</summary>
        internal sealed class PatchOutcome
        {
            internal string Source;
            internal readonly List<string> Applied = new List<string>();

            /// <summary>"그룹: 사유" 형식.</summary>
            internal readonly List<string> Skipped = new List<string>();

            /// <summary>이미 패치된 텍스트라 아무것도 하지 않았는지.</summary>
            internal bool AlreadyPatched;
        }

        /// <summary>
        /// 그룹 목록을 만든다. 순서가 곧 적용 순서이며 prologue 는 마지막이다.
        /// <paramref name="runtimePayload"/> 는 <see cref="PreparePayload"/> 를 거친 JS 텍스트다.
        /// </summary>
        internal static List<PatchGroup> BuildGroups(bool forceCompressed, float minSeconds, string runtimePayload)
        {
            string helper = BuildHelperBlock(forceCompressed, minSeconds, runtimePayload);
            return new List<PatchGroup>
            {
                new PatchGroup(GroupMimeMap, new[] { new PatchEdit(MimeMapFrom, MimeMapTo) }, null),
                new PatchGroup(GroupClipMeta, new[]
                {
                    new PatchEdit(ClipMetaAFrom, ClipMetaATo),
                    new PatchEdit(ClipMetaBFrom, ClipMetaBTo),
                }, null),
                new PatchGroup(GroupPitchPosition, new[]
                {
                    new PatchEdit(PitchAFrom, PitchATo),
                    new PatchEdit(PitchBFrom, PitchBTo),
                }, null),
                new PatchGroup(GroupSourceRelease, new[] { new PatchEdit(ReleaseAFrom, ReleaseATo) }, null),
                new PatchGroup(GroupPrologue, new[] { new PatchEdit(PrologueFrom, helper + PrologueBody) },
                    new[] { GroupClipMeta, GroupPitchPosition }),
            };
        }

        /// <summary>
        /// helper 블록: 마커 + payload + 설정 literal. 줄 단위로 끝나게 해 압축된 framework 한 줄 안에서
        /// 주석이 뒤 코드를 삼키지 않게 한다.
        /// </summary>
        internal static string BuildHelperBlock(bool forceCompressed, float minSeconds, string runtimePayload)
        {
            return "\n" + MarkerPrefix + " v" + AITPatchedFileNaming.PatchSetVersion.ToString(CultureInfo.InvariantCulture) + "*/\n"
                + (runtimePayload ?? string.Empty).TrimEnd() + "\n"
                + "WEBAudio.aitCfg={force:" + (forceCompressed ? "1" : "0") + ",minSec:" + FormatSeconds(minSeconds) + "};\n";
        }

        /// <summary>JS 숫자 literal 로 쓸 수 있는 초 값(0.001 미만/NaN/무한대는 기본 10).</summary>
        internal static string FormatSeconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0.001f)
            {
                seconds = AITDefaultSettings.DefaultAudioForceCompressedMinSeconds;
            }

            return seconds.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// payload 파일 텍스트를 주입용으로 정리한다: 전체 줄 // 주석 제거(압축 framework 에 섞여도 안전하도록),
        /// 선택적 module.exports 줄 제거, 줄바꿈을 LF 로 통일.
        /// </summary>
        internal static string PreparePayload(string fileText)
        {
            if (string.IsNullOrEmpty(fileText))
            {
                return string.Empty;
            }

            string t = fileText.Replace("\r\n", "\n").Replace('\r', '\n');
            t = Regex.Replace(t, @"^[ \t]*//[^\n]*\n?", string.Empty, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            t = Regex.Replace(t, @"^[ \t]*if\(typeof module[^\n]*\n?", string.Empty, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return t.Trim('\n') + "\n";
        }

        /// <summary>겹치지 않는 ordinal 일치 횟수.</summary>
        internal static int CountOccurrences(string text, string anchor)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(anchor))
            {
                return 0;
            }

            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(anchor, i, StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += anchor.Length;
            }

            return count;
        }

        /// <summary>
        /// framework 텍스트에 그룹을 적용한다. 앵커가 정확히 1회가 아닌 그룹은 건너뛰고 이유를 Skipped 에 남긴다.
        /// 이미 마커(오디오 또는 stacktrace-lazy)가 있으면 아무것도 하지 않는다(멱등).
        /// <paramref name="patchAudio"/> 가 false 면 오디오 그룹을 평가하지 않는다(stacktrace-lazy 와 독립).
        /// <paramref name="lazyStackTrace"/> 가 true 일 때만 stacktrace-lazy 를 평가한다(기본 false — 호출부가 정한다).
        /// </summary>
        internal static PatchOutcome PatchText(
            string source, bool forceCompressed, float minSeconds, string runtimePayload,
            bool patchAudio = true, bool lazyStackTrace = false)
        {
            var outcome = new PatchOutcome { Source = source };
            if (string.IsNullOrEmpty(source))
            {
                return outcome;
            }

            if (HasAudioMarker(source) || source.IndexOf(StackLazyMarker, StringComparison.Ordinal) >= 0)
            {
                outcome.AlreadyPatched = true;
                return outcome;
            }

            string text = source;
            if (patchAudio)
            {
                foreach (PatchGroup group in BuildGroups(forceCompressed, minSeconds, runtimePayload))
                {
                    string missingDependency = null;
                    foreach (string required in group.Requires)
                    {
                        if (!outcome.Applied.Contains(required))
                        {
                            missingDependency = required;
                            break;
                        }
                    }

                    if (missingDependency != null)
                    {
                        outcome.Skipped.Add(group.Name + ": 선행 그룹 '" + missingDependency + "' 미적용");
                        continue;
                    }

                    string mismatch = null;
                    for (int i = 0; i < group.Edits.Length; i++)
                    {
                        int n = CountOccurrences(text, group.Edits[i].Anchor);
                        if (n != 1)
                        {
                            mismatch = "앵커 " + (i + 1) + "/" + group.Edits.Length + " 일치 " + n + "회(기대 1회)";
                            break;
                        }
                    }

                    if (mismatch != null)
                    {
                        outcome.Skipped.Add(group.Name + ": " + mismatch);
                        continue;
                    }

                    foreach (PatchEdit edit in group.Edits)
                    {
                        text = ReplaceOnce(text, edit.Anchor, edit.Replacement);
                    }

                    outcome.Applied.Add(group.Name);
                }
            }

            if (lazyStackTrace)
            {
                if (TryPatchStackTraceLazy(text, out string lazyPatched, out string lazyReason))
                {
                    text = lazyPatched;
                    outcome.Applied.Add(GroupStackTraceLazy);
                }
                else
                {
                    outcome.Skipped.Add(GroupStackTraceLazy + ": " + lazyReason);
                }
            }

            outcome.Source = outcome.Applied.Count > 0 ? text : source;
            return outcome;
        }

        /// <summary>오디오 패치 마커(/*ait-audio-patch v1*/ 형태)가 있는지.</summary>
        internal static bool HasAudioMarker(string source)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(MarkerPrefix, StringComparison.Ordinal) >= 0;
        }

        // 일치가 정확히 1회임을 확인한 뒤에만 부른다. '$' 패턴 해석이 없도록 문자열 이어붙이기로 치환한다.
        private static string ReplaceOnce(string text, string anchor, string replacement)
        {
            int idx = text.IndexOf(anchor, StringComparison.Ordinal);
            return text.Substring(0, idx) + replacement + text.Substring(idx + anchor.Length);
        }

        // ─────────────────────────── 설정 해석 ───────────────────────────

        /// <summary>
        /// 설정에서 패치 동작을 정한다. audioForceCompressedPlayback: -1(자동) = 정확성 수정만(강제 압축 재생은 자동 꺼짐),
        /// 1 = 정확성 수정 + 긴 클립 강제 압축 재생, 0 = framework 를 건드리지 않는다(stock 대조군).
        /// </summary>
        internal static void ResolveSettings(AITEditorScriptObject config, out bool enabled, out bool force, out float minSeconds)
        {
            enabled = config == null || config.audioForceCompressedPlayback != 0;
            force = AITPerfFlags.EffectiveAudioForceCompressed(config);
            minSeconds = AITPerfFlags.EffectiveAudioForceCompressedMinSeconds(config);
        }

        /// <summary>
        /// stacktrace-lazy 실효 활성 여부. 우선순위: AIT_FW_LAZY_STACKTRACE 환경 변수(1/true, 0/false) &gt;
        /// config.frameworkLazyStackTraceMode(0 끔 / 1 강제 켬) &gt; 자동(-1 → ON). 환경 변수 값이 이상하면 경고 후 설정값 사용.
        /// config==null 이면 자동과 같이 ON(패치는 앵커가 정확히 1회 맞을 때만 적용되므로 fail-open).
        /// </summary>
        internal static bool EffectiveLazyStackTrace(AITEditorScriptObject config)
        {
            string env = System.Environment.GetEnvironmentVariable(LazyStackTraceEnvVar);
            if (!string.IsNullOrEmpty(env))
            {
                string v = env.Trim().ToLowerInvariant();
                if (v == "1" || v == "true") return true;
                if (v == "0" || v == "false") return false;
                Debug.LogWarning($"[AIT] {LazyStackTraceEnvVar} 환경 변수 값이 올바르지 않습니다: '{env}' (1/0/true/false 필요) — 설정값 사용");
            }

            if (config == null) return true;
            if (config.frameworkLazyStackTraceMode >= 0) return config.frameworkLazyStackTraceMode == 1;
            return true;
        }

        // ─────────────────────────── 파일 처리 ───────────────────────────

        /// <summary>
        /// framework 파일에 패치를 적용한다.
        /// </summary>
        /// <param name="buildDir">Unity Build/ 폴더 경로.</param>
        /// <param name="config">현재 빌드 설정.</param>
        /// <param name="renames">rename 한 파일의 옛 이름 → 새 이름을 기록할 맵(null 허용).</param>
        /// <returns>패치를 적용한 파일 수.</returns>
        internal static int Apply(string buildDir, AITEditorScriptObject config, IDictionary<string, string> renames = null)
        {
            LastApplyClipMetaApplied = false;
            try
            {
                return ApplyCore(buildDir, config, renames);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-Audio] framework 패치 예외 — 패치 없이 계속합니다: {e.GetType().Name}: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 가장 최근 <see cref="Apply"/> 에서 압축 클립 메타(compressed-clip-meta) 그룹이 하나라도 적용됐는지.
        /// 이 패치가 없는 빌드에서 압축 재생 경로를 타면 clip.length 가 0 이 되므로, 빌드 후 단계가
        /// __AIT_PERF.audioPatched 로 런타임에 알린다.
        /// </summary>
        internal static bool LastApplyClipMetaApplied { get; private set; }

        private static int ApplyCore(string buildDir, AITEditorScriptObject config, IDictionary<string, string> renames)
        {
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                return 0;
            }

            ResolveSettings(config, out bool audioEnabled, out bool force, out float minSeconds);
            bool lazyStack = EffectiveLazyStackTrace(config);
            Debug.Log($"[AIT-Audio] framework 패치 설정: 오디오={(audioEnabled ? "ON" : "OFF")}, stacktrace-lazy={(lazyStack ? "ON" : "OFF")}");
            if (!audioEnabled && !lazyStack)
            {
                Debug.Log("[AIT-Audio] audioForceCompressedPlayback=0, stacktrace-lazy 끔 — framework 패치를 건너뜁니다(stock).");
                return 0;
            }

            var targets = new List<string>();
            foreach (string path in Directory.GetFiles(buildDir))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
                {
                    // 감지 마커 때문에 재포장하면 안 된다. 호출부가 이미 걸러야 하지만 여기서도 확실히 막는다.
                    Debug.Log("[AIT-Audio] .unityweb 산출물이 있어 framework 패치를 건너뜁니다.");
                    return 0;
                }

                if (GetFrameworkKind(name) != null && !AITPatchedFileNaming.IsPatched(name))
                {
                    targets.Add(path);
                }
            }

            if (targets.Count == 0)
            {
                return 0;
            }

            // 오디오 전용 선행 조건(payload). 어긋나면 오디오 그룹만 끄고 stacktrace-lazy 는 계속한다.
            string payload = string.Empty;
            if (audioEnabled)
            {
                string payloadPath = ResolveRuntimePayloadPath();
                if (string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
                {
                    Debug.LogWarning($"[AIT-Audio] 런타임 payload 를 찾지 못해 오디오 패치를 건너뜁니다: '{payloadPath}'");
                    audioEnabled = false;
                }
                else
                {
                    payload = PreparePayload(File.ReadAllText(payloadPath));
                    if (payload.IndexOf("function aitAudioProbe", StringComparison.Ordinal) < 0)
                    {
                        Debug.LogWarning("[AIT-Audio] 런타임 payload 형식이 예상과 달라 오디오 패치를 건너뜁니다.");
                        audioEnabled = false;
                        payload = string.Empty;
                    }
                }
            }

            if (!audioEnabled && !lazyStack)
            {
                return 0;
            }

            if (!AITBrotliCompressor.TryResolveNode(out string node))
            {
                Debug.LogWarning("[AIT-Audio] 내장 Node 미가용 — framework 패치를 건너뜁니다(stock).");
                return 0;
            }

            string work = Path.Combine(Path.GetTempPath(), "ait-fwpatch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(work);
            int patched = 0;
            try
            {
                string runner = Path.Combine(work, "runner.js");
                File.WriteAllText(runner, RunnerJs);

                foreach (string path in targets)
                {
                    try
                    {
                        if (PatchFile(buildDir, path, node, runner, work, payload, force, minSeconds, audioEnabled, lazyStack, renames))
                        {
                            patched++;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AIT-Audio] {Path.GetFileName(path)} 패치 실패(원본 유지): {e.GetType().Name}: {e.Message}");
                    }
                }
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { /* 임시 폴더 정리 실패 무시 */ }
            }

            return patched;
        }

        private static bool PatchFile(
            string buildDir, string path, string node, string runner, string work,
            string payload, bool force, float minSeconds, bool patchAudio, bool lazyStack, IDictionary<string, string> renames)
        {
            string name = Path.GetFileName(path);
            string kind = GetFrameworkKind(name);

            string decoded = Path.Combine(work, "decoded.js");
            if (!RunRunner(node, runner, "decode", path, decoded, kind, out string err))
            {
                Debug.LogWarning($"[AIT-Audio] {name} 해제 실패(원본 유지): {err}");
                return false;
            }

            string source = File.ReadAllText(decoded, new UTF8Encoding(false));
            PatchOutcome outcome = PatchText(source, force, minSeconds, payload, patchAudio, lazyStack);

            if (outcome.AlreadyPatched)
            {
                if (HasAudioMarker(source))
                {
                    LastApplyClipMetaApplied = true; // 이전 오디오 패치 마커가 이미 있다.
                }

                Debug.Log($"[AIT-Audio] {name}: 이미 패치됨 — 건너뜁니다.");
                return false;
            }

            if (outcome.Applied.Contains(GroupClipMeta))
            {
                LastApplyClipMetaApplied = true;
            }

            if (outcome.Applied.Count == 0)
            {
                // 오디오 코드가 없거나(모듈 제외) Unity 가 앵커 문자열을 바꾼 경우 — stock 그대로 둔다.
                Debug.LogWarning($"[AIT-Audio] {name}: 적용할 그룹이 없어 stock 그대로 둡니다 ({string.Join("; ", outcome.Skipped)})");
                return false;
            }

            foreach (string skipped in outcome.Skipped)
            {
                Debug.LogWarning($"[AIT-Audio] framework 패치 그룹 건너뜀 — {skipped}");
            }

            string patchedJs = Path.Combine(work, "patched.js");
            File.WriteAllText(patchedJs, outcome.Source, new UTF8Encoding(false));

            if (!RunProcess(node, new[] { "--check", patchedJs }, null, out _, out string checkErr))
            {
                Debug.LogWarning($"[AIT-Audio] {name}: 패치 결과가 node --check 를 통과하지 못해 원본을 유지합니다: {Truncate(checkErr)}");
                return false;
            }

            string encoded = Path.Combine(work, "encoded.bin");
            if (!RunRunner(node, runner, "encode", patchedJs, encoded, kind, out err))
            {
                Debug.LogWarning($"[AIT-Audio] {name} 재압축 실패(원본 유지): {err}");
                return false;
            }

            // 새 바이트를 patch-set 이름으로 먼저 쓰고 나서 원본을 지운다 — 중간에 실패해도 원본은 남는다.
            // 설정에 따라 패치 바이트가 달라지므로(강제 여부·최소 길이·payload·적용 그룹) 그 입력의 해시를 이름에 넣는다.
            string configHash = AITPatchedFileNaming.ComputeConfigHash(
                force ? "1" : "0", FormatSeconds(minSeconds), payload, string.Join(",", outcome.Applied));
            string newName = AITPatchedFileNaming.GetPatchedName(name, AITPatchedFileNaming.PatchSetVersion, configHash);
            string dest = Path.Combine(buildDir, newName);
            File.Copy(encoded, dest, true);
            if (!string.Equals(newName, name, StringComparison.Ordinal))
            {
                File.Delete(path);
            }

            var local = new Dictionary<string, string>(StringComparer.Ordinal) { { name, newName } };
            if (renames != null)
            {
                renames[name] = newName;
            }

            RewriteTextReferences(buildDir, local);

            long delta = outcome.Source.Length - source.Length;
            Debug.Log($"[AIT-Audio] framework 패치: {name} → {newName} (적용 {string.Join(",", outcome.Applied)}"
                + $"{(outcome.Skipped.Count > 0 ? ", 건너뜀 " + outcome.Skipped.Count + "개" : string.Empty)}"
                + $", +{delta}B, 오디오={(patchAudio ? "ON" : "OFF")}, 강제 압축 재생={(force ? "ON" : "OFF")}, 최소 {FormatSeconds(minSeconds)}s"
                + $", stacktrace-lazy={(lazyStack ? "ON" : "OFF")})");
            return true;
        }

        // framework 파일 이름을 가리키는 작은 텍스트(빌드 폴더의 html/json)가 있으면 새 이름으로 고친다.
        private static void RewriteTextReferences(string buildDir, IDictionary<string, string> renames)
        {
            foreach (string path in Directory.GetFiles(buildDir))
            {
                string name = Path.GetFileName(path);
                bool text = name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    || (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        && name.IndexOf(".symbols.", StringComparison.OrdinalIgnoreCase) < 0);
                if (!text)
                {
                    continue;
                }

                try
                {
                    if (new FileInfo(path).Length <= 2 * 1024 * 1024)
                    {
                        AITPatchedFileNaming.RewriteReferencesInFile(path, renames);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-Audio] 참조 갱신 실패(무시): {name}: {e.Message}");
                }
            }
        }

        /// <summary>파일명이 framework 산출물이면 압축 종류("br"/"gz"/"none"), 아니면 null.</summary>
        internal static string GetFrameworkKind(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            if (fileName.EndsWith(".framework.js.br", StringComparison.OrdinalIgnoreCase)) return "br";
            if (fileName.EndsWith(".framework.js.gz", StringComparison.OrdinalIgnoreCase)) return "gz";
            if (fileName.EndsWith(".framework.js", StringComparison.OrdinalIgnoreCase)) return "none";
            return null;
        }

        private static string ResolveRuntimePayloadPath()
        {
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AITFrameworkPatcher).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    string p = Path.Combine(pkg.resolvedPath, "Editor", RuntimePayloadDirName, RuntimePayloadFileName);
                    if (File.Exists(p))
                    {
                        return p;
                    }
                }
            }
            catch
            {
                // PackageInfo 미해석(예: Assets 내 임베드 개발) → 소스 파일 위치 폴백
            }

            string here = CallerDir();
            return string.IsNullOrEmpty(here) ? null : Path.Combine(here, RuntimePayloadDirName, RuntimePayloadFileName);
        }

        /// <summary>테스트용 노출: payload 파일의 해석된 경로(없으면 null 또는 존재하지 않는 경로).</summary>
        internal static string GetRuntimePayloadPathForTests() => ResolveRuntimePayloadPath();

        private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

        // ─────────────────────────── Node 러너 ───────────────────────────

        [Serializable]
        private class RunnerResult
        {
            public bool ok;
            public long bytes;
            public string error;
        }

        // stdin {op, kind, src, dst} → stdout {ok, bytes, error}.
        //  decode: .br/.gz 를 풀어 dst 에 쓴다(없는 압축이면 그대로 복사).
        //  encode: src 를 br(q11, LGWIN 24 — 표준 윈도우 상한이라 브라우저 디코더 호환)/gz(9) 로 눌러 dst 에 쓰고,
        //          다시 풀어 원본과 바이트가 같은지 확인한다. 불일치면 실패.
        private const string RunnerJs =
            "'use strict';\n" +
            "const zlib=require('zlib'),fs=require('fs');\n" +
            "let raw='';\n" +
            "process.stdin.on('data',(d)=>{raw+=d;});\n" +
            "process.stdin.on('end',()=>{\n" +
            "  let req;\n" +
            "  try{req=JSON.parse(raw);}catch(e){process.stdout.write('{\"ok\":false,\"error\":\"bad request\"}');process.exitCode=2;return;}\n" +
            "  try{\n" +
            "    const input=fs.readFileSync(req.src);\n" +
            "    let out;\n" +
            "    if(req.op==='decode'){\n" +
            "      out=req.kind==='br'?zlib.brotliDecompressSync(input):req.kind==='gz'?zlib.gunzipSync(input):input;\n" +
            "    }else if(req.op==='encode'){\n" +
            "      let back;\n" +
            "      if(req.kind==='br'){\n" +
            "        out=zlib.brotliCompressSync(input,{params:{[zlib.constants.BROTLI_PARAM_QUALITY]:11,[zlib.constants.BROTLI_PARAM_LGWIN]:24,[zlib.constants.BROTLI_PARAM_SIZE_HINT]:input.length}});\n" +
            "        back=zlib.brotliDecompressSync(out);\n" +
            "      }else if(req.kind==='gz'){\n" +
            "        out=zlib.gzipSync(input,{level:9});back=zlib.gunzipSync(out);\n" +
            "      }else{out=input;back=input;}\n" +
            "      if(back.length!==input.length||Buffer.compare(back,input)!==0)throw new Error('roundtrip mismatch');\n" +
            "    }else{throw new Error('unknown op');}\n" +
            "    fs.writeFileSync(req.dst,out);\n" +
            "    process.stdout.write(JSON.stringify({ok:true,bytes:out.length}));\n" +
            "  }catch(e){\n" +
            "    process.stdout.write(JSON.stringify({ok:false,error:String((e&&e.message)||e)}));\n" +
            "    process.exitCode=1;\n" +
            "  }\n" +
            "});\n";

        private static bool RunRunner(string node, string runner, string op, string src, string dst, string kind, out string error)
        {
            error = null;
            string request = "{\"op\":" + JsonStr(op) + ",\"kind\":" + JsonStr(kind)
                + ",\"src\":" + JsonStr(src) + ",\"dst\":" + JsonStr(dst) + "}";
            bool exited = RunProcess(node, new[] { runner }, request, out string stdout, out string stderr);

            RunnerResult result = null;
            try
            {
                result = UnityEngine.JsonUtility.FromJson<RunnerResult>(stdout);
            }
            catch
            {
                // 아래에서 실패로 처리
            }

            if (exited && result != null && result.ok && File.Exists(dst))
            {
                return true;
            }

            error = result != null && !string.IsNullOrEmpty(result.error) ? result.error : Truncate(stderr ?? stdout);
            return false;
        }

        private static bool RunProcess(string exe, string[] args, string stdinText, out string stdout, out string stderr)
        {
            stdout = null;
            stderr = null;
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardInput = stdinText != null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            foreach (string a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();

                // stderr 는 비동기로 모아 stdout ReadToEnd 중 버퍼가 차서 교착하는 일을 막는다.
                var errSb = new StringBuilder();
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) { errSb.AppendLine(ev.Data); } };
                p.BeginErrorReadLine();

                if (stdinText != null)
                {
                    p.StandardInput.Write(stdinText);
                    p.StandardInput.Close();
                }

                stdout = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(NodeTimeoutMs))
                {
                    try { p.Kill(); } catch { /* 이미 종료됨 */ }
                    stderr = errSb.ToString();
                    return false;
                }

                p.WaitForExit();
                stderr = errSb.ToString();
                return p.ExitCode == 0;
            }
        }

        private static string JsonStr(string s)
        {
            if (s == null)
            {
                return "\"\"";
            }

            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "(출력 없음)";
            }

            s = s.Trim();
            return s.Length <= 300 ? s : s.Substring(0, 300) + "…";
        }
    }
}
