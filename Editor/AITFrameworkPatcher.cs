// -----------------------------------------------------------------------
// AITFrameworkPatcher.cs - 빌드 후 Unity framework(*.framework.js[.br|.gz]) 텍스트 패치 (오디오)
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
        /// 이미 마커가 있으면 아무것도 하지 않는다(멱등).
        /// </summary>
        internal static PatchOutcome PatchText(string source, bool forceCompressed, float minSeconds, string runtimePayload)
        {
            var outcome = new PatchOutcome { Source = source };
            if (string.IsNullOrEmpty(source))
            {
                return outcome;
            }

            if (source.IndexOf(MarkerPrefix, StringComparison.Ordinal) >= 0)
            {
                outcome.AlreadyPatched = true;
                return outcome;
            }

            string text = source;
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

            outcome.Source = outcome.Applied.Count > 0 ? text : source;
            return outcome;
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

        private static int ApplyCore(string buildDir, AITEditorScriptObject config, IDictionary<string, string> renames)
        {
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                return 0;
            }

            ResolveSettings(config, out bool enabled, out bool force, out float minSeconds);
            if (!enabled)
            {
                Debug.Log("[AIT-Audio] audioForceCompressedPlayback=0 — framework 오디오 패치를 건너뜁니다(stock).");
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

            string payloadPath = ResolveRuntimePayloadPath();
            if (string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
            {
                Debug.LogWarning($"[AIT-Audio] 런타임 payload 를 찾지 못해 framework 패치를 건너뜁니다: '{payloadPath}'");
                return 0;
            }

            string payload = PreparePayload(File.ReadAllText(payloadPath));
            if (payload.IndexOf("function aitAudioProbe", StringComparison.Ordinal) < 0)
            {
                Debug.LogWarning("[AIT-Audio] 런타임 payload 형식이 예상과 달라 framework 패치를 건너뜁니다.");
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
                        if (PatchFile(buildDir, path, node, runner, work, payload, force, minSeconds, renames))
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
            string payload, bool force, float minSeconds, IDictionary<string, string> renames)
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
            PatchOutcome outcome = PatchText(source, force, minSeconds, payload);

            if (outcome.AlreadyPatched)
            {
                Debug.Log($"[AIT-Audio] {name}: 이미 패치됨 — 건너뜁니다.");
                return false;
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
            string newName = AITPatchedFileNaming.GetPatchedName(name);
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
                + $", +{delta}B, 강제 압축 재생={(force ? "ON" : "OFF")}, 최소 {FormatSeconds(minSeconds)}s)");
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
