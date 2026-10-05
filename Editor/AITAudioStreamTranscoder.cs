// -----------------------------------------------------------------------
// <copyright file="AITAudioStreamTranscoder.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming audio transcoder (build-time)
// </copyright>
// -----------------------------------------------------------------------
//
// 오디오 스트리밍(AITAudioStreamingProcessor)이 StreamingAssets 로 외부화한 "사본"을
// 저비트레이트 MP3 로 재인코딩해 .ait 번들 크기를 줄인다. 외부화 스트림은 원본 소스
// 바이트 그대로라(고비트레이트 MP3 등) 번들 최대 단일 요소가 되기 쉽다.
//
// 설계 근거(2026-07 멀티에이전트 리서치 + 적대 검증):
//   - 컨테이너/코덱을 MP3 로 유지 → 런타임 복원 경로(UnityWebRequestMultimedia →
//     브라우저 미디어 엘리먼트) 불변, iOS WKWebView 호환성이 소스와 동일(재생 리스크 ≈ 0).
//     (Vorbis/Opus 는 iOS 18.4 미만 미지원/불안정, WebGL AssetBundle 은 AAC 강제
//     재인코딩 + 전체 메모리 로드라 기각.)
//   - 트랜스코딩 대상이 "외부화 사본"뿐이므로 프로젝트 원본은 구조적으로 비파괴.
//   - 인코딩은 순수 WASM(mpg123-decoder + wasm-media-encoders/LAME)을 SDK 내장
//     Node.js 로 실행 — AITBrotliCompressor(내장 Node)와 AITFontSubsetProcessor
//     (on-demand npm 설치) 의 기존 패턴 재사용. 도구 미가용 시 원본 사본 유지(기능
//     저하 없음 — 번들 크기만 종전과 동일).
//
// ⚠ 기본값 정책: auto(-1)는 ON(AITDefaultSettings.GetDefaultAudioStreamTranscode() == true),
//   audioStreamTranscode=0 으로 끈다. cascaded lossy(320→160kbps 등)는 세대손실이 누적되고
//   루핑 BGM 은 LAME delay/padding 갭 리스크가 있어, auto 에서는 빌드 씬·프리팹의 AudioSource 가
//   loop=true 로 참조하는 클립과 20초 초과 클립(러너 출력에 Xing/Info delay·padding 태그 없음)을 건너뛴다(CollectLoopingClipGuids). 명시 활성(==1)은 게이트 없음.
//
// P0-6(저메모리 대응): 같은 audioStreamTranscode 스위치가 PCM WAV → AAC-LC(.m4a) 변환도 맡는다(TranscodeWavToAac).
//   대상은 압축 재생 경로(매니페스트 compressed=true)로 가는 PCM WAV 뿐이다. 이 경로의 WAV 는 바이트 그대로 wasm 힙과
//   Blob 에 두 번 상주하기 때문이다. 인코더는 시스템 ffmpeg/afconvert(AITAudioAacEncoder)이고 없으면 경고 후 WAV 유지.
//   루프 클립은 AAC 이음새(인코더 프라이밍) 위험 때문에 audioStreamLoopTranscode=1 옵트인(auto=0)일 때만 변환한다.
//   MP3 경로의 20초 길이 게이트는 쓰지 않는다 — 쓰면 BGM 이 전부 빠져 이득이 사라진다(스크립트가 런타임에 켜는 loop 는 탐지 불가).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AppsInToss.Editor.Package;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 외부화된 스트리밍 오디오 사본의 빌드타임 MP3 재인코더.
    /// <see cref="AITAudioStreamingProcessor"/> 가 외부화 직후 호출한다.
    /// </summary>
    internal static class AITAudioStreamTranscoder
    {
        /// <summary>도구 소스 디렉토리명(SDK 패키지 동봉, '~' 접미사라 Unity 미임포트).</summary>
        private const string ToolDirName = "AudioTranscode~";

        /// <summary>러너 파일명.</summary>
        private const string RunnerName = "audio-transcode-runner.mjs";

        /// <summary>설치된 node_modules가 어떤 lockfile 해시로 설치됐는지 기록하는 스탬프 파일명.</summary>
        private const string LockfileHashStampName = ".lockfile-hash";

        /// <summary>일괄 트랜스코딩 타임아웃(ms). 실측 파일당 ~0.7초(디코드+인코드)라 여유 값.</summary>
        private const int TimeoutMs = 600000;

        /// <summary>임시 산출물 접미사(채택 판정 전 dst).</summary>
        private const string TmpSuffix = ".aittranscodetmp";

        /// <summary>
        /// 채택 임계(%): 재인코딩 산출물이 원본 사본보다 이 비율 이상 작을 때만 교체한다.
        /// 320→160kbps 는 실측 ~50% 절감이므로 정상 동작 시 여유 통과 — 미달은 이상 신호로
        /// 보고 원본을 유지한다(brotli 채택 게이트와 동일 사상, 임계는 lossy 라 더 높게).
        /// </summary>
        internal const int MinGainPercent = 25;

        /// <summary>트랜스코딩 대상 후보(외부화 사본 1건).</summary>
        internal struct Candidate
        {
            /// <summary>StreamingAssets 사본의 절대 경로.</summary>
            public string AbsPath;

            /// <summary>사본 바이트 수.</summary>
            public long Bytes;

            /// <summary>클립 실 길이(초). AudioClip.length 캡처값.</summary>
            public float Seconds;

            /// <summary>원본 AudioClip 에셋 GUID(루프 클립 게이트용). 비어 있으면 게이트 미적용.</summary>
            public string Guid;

            /// <summary>매니페스트 compressed=true(런타임이 압축 재생 경로로 받음)인지. WAV → AAC 변환은 이 경로 대상만 한다.</summary>
            public bool Compressed;
        }

        /// <summary>파일 1건의 러너 결과. error 가 비어 있으면 성공.</summary>
        [Serializable]
        internal class Result
        {
            public int idx;
            public long raw;
            public long @out;
            public int srcKbps;
            public float durationSec;
            public string error;

            public bool Ok => string.IsNullOrEmpty(error) && raw > 0 && @out > 0;
        }

        [Serializable]
        private class Batch
        {
            public Result[] results;
        }

        // ─────────────────────────── 판정 (순수 함수, Level 0 테스트 대상) ───────────────────────────

        /// <summary>tri-state 해석. auto(-1)는 ON(루프 클립은 TranscodeInPlace 가 별도로 제외), 0 이면 끔.</summary>
        internal static bool IsEnabled(AITEditorScriptObject config)
        {
            if (config == null)
            {
                return false;
            }

            return config.audioStreamTranscode >= 0
                ? config.audioStreamTranscode == 1
                : AITDefaultSettings.GetDefaultAudioStreamTranscode();
        }

        /// <summary>파일 크기/재생 길이에서 평균 비트레이트(kbps)를 추정한다. 길이가 무의미하면 0.</summary>
        internal static int EstimateKbps(long bytes, float seconds)
        {
            if (bytes <= 0 || seconds < 0.5f)
            {
                return 0;
            }

            return (int)Math.Round(bytes * 8.0 / seconds / 1000.0);
        }

        /// <summary>
        /// 재인코딩 대상 판정: 소스 평균 비트레이트가 minSourceKbps 이상이고 target 보다
        /// 실질적으로 높을 때만. (target 근처 소스를 재인코딩하면 세대손실만 남는다.)
        /// </summary>
        internal static bool ShouldTranscode(long bytes, float seconds, int minSourceKbps, int targetKbps)
        {
            int kbps = EstimateKbps(bytes, seconds);
            if (kbps <= 0)
            {
                return false;
            }

            // 하한 방어: minSourceKbps 가 target 이하로 잘못 설정돼도 target+32 미만 소스는 제외.
            int floor = Math.Max(minSourceKbps, targetKbps + 32);
            return kbps >= floor;
        }

        /// <summary>
        /// 자동 모드에서 재인코딩하지 않을 '긴 클립' 기준(초). 러너 출력은 LAME Xing/Info 태그(인코더 delay/padding)가
        /// 없는 순수 프레임 스트림이라 gapless 재생이 불가하고 세대마다 ~1105샘플 선행 지연이 붙는다.
        /// 런타임 loop 설정은 탐지할 수 없으므로 BGM 일 가능성이 큰 긴 클립은 자동에서 제외한다(명시 ==1 은 무관).
        /// </summary>
        internal const float AutoMaxClipSeconds = 20f;

        /// <summary>자동 모드 긴 클립 제외 판정. 길이 미상(0 이하)은 보수적으로 제외하지 않고 비트레이트 게이트에 맡긴다.</summary>
        internal static bool IsLikelyBgmByLength(float seconds)
        {
            return seconds > AutoMaxClipSeconds;
        }

        /// <summary>채택 판정: 산출물이 원본 대비 MinGainPercent 이상 작아야 교체.</summary>
        internal static bool ShouldAdopt(long rawBytes, long outBytes)
        {
            return AITBrotliCompressor.ShouldKeep(rawBytes, outBytes, MinGainPercent);
        }

        /// <summary>목표 비트레이트 해석(96~320 클램프, 비정상 값은 기본 160).</summary>
        internal static int ResolveTargetKbps(AITEditorScriptObject config)
        {
            int v = config != null ? config.audioStreamTranscodeBitrateKbps : 0;
            if (v <= 0)
            {
                return 160;
            }

            return Math.Max(96, Math.Min(320, v));
        }

        /// <summary>소스 최소 비트레이트 게이트 해석(비정상 값은 기본 256).</summary>
        internal static int ResolveMinSourceKbps(AITEditorScriptObject config)
        {
            int v = config != null ? config.audioStreamTranscodeMinSourceKbps : 0;
            return v > 0 ? v : 256;
        }

        // ─────────────────────────── 루프 클립 탐지 ───────────────────────────

        /// <summary>
        /// 빌드 씬(활성) + 프로젝트 프리팹(Resources 포함)의 AudioSource 중 loop=true 가 참조하는 AudioClip GUID 집합.
        /// 텍스트 YAML 만 읽는 값싼 스캔이며(에셋 로드 없음), 바이너리 직렬화 파일이 하나라도 있으면
        /// incomplete=true 로 알려 호출부가 보수적으로 건너뛰게 한다. 스크립트가 런타임에 켜는 loop 는 탐지 불가.
        /// </summary>
        internal static HashSet<string> CollectLoopingClipGuids(out bool incomplete)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            incomplete = false;
            try
            {
                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
                {
                    if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                    {
                        paths.Add(scene.path);
                    }
                }

                foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                {
                    string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    if (!string.IsNullOrEmpty(p))
                    {
                        paths.Add(p);
                    }
                }

                foreach (var p in paths)
                {
                    string full = Path.Combine(projectRoot, p);
                    if (!File.Exists(full))
                    {
                        continue;
                    }

                    string text = File.ReadAllText(full);
                    if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                    {
                        incomplete = true; // 바이너리 직렬화 — 내용을 읽을 수 없음.
                        continue;
                    }

                    ScanYamlForLoopingClips(text, guids);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] 루프 클립 탐지 예외 → 보수적으로 건너뜀: {e.Message}");
                incomplete = true;
            }

            return guids;
        }

        /// <summary>
        /// 텍스트 YAML(.unity/.prefab) 한 개에서 Loop: 1 인 AudioSource(!u!82) 블록의 m_audioClip GUID 를 수집한다.
        /// 프리팹 인스턴스 오버라이드(propertyPath: Loop, value: 1)가 있으면 같은 파일의
        /// m_audioClip 오버라이드 참조도 보수적으로 루프 클립으로 취급한다. 순수 함수(테스트 대상).
        /// </summary>
        internal static void ScanYamlForLoopingClips(string yaml, HashSet<string> sink)
        {
            if (string.IsNullOrEmpty(yaml) || sink == null || yaml.IndexOf("m_audioClip", StringComparison.Ordinal) < 0)
            {
                return;
            }

            const string ClipKey = "m_audioClip:";
            const string GuidKey = "guid:";
            var lines = yaml.Split('\n');
            bool inAudioSource = false;
            bool blockLoop = false;
            string blockGuid = null;
            bool overrideLoop = false;
            var overrideGuids = new List<string>();
            bool pendingClipOverride = false;
            bool pendingLoopOverride = false;

            void FlushBlock()
            {
                if (inAudioSource && blockLoop && !string.IsNullOrEmpty(blockGuid))
                {
                    sink.Add(blockGuid);
                }
                inAudioSource = false;
                blockLoop = false;
                blockGuid = null;
            }

            foreach (var raw in lines)
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    FlushBlock();
                    inAudioSource = line.StartsWith("--- !u!82 ", StringComparison.Ordinal);
                    continue;
                }

                string t = line.Trim();
                if (inAudioSource)
                {
                    if (t.StartsWith(ClipKey, StringComparison.Ordinal))
                    {
                        int gi = t.IndexOf(GuidKey, StringComparison.Ordinal);
                        if (gi >= 0)
                        {
                            int start = gi + GuidKey.Length;
                            int end = t.IndexOfAny(new[] { ',', '}' }, start);
                            blockGuid = (end > start ? t.Substring(start, end - start) : t.Substring(start)).Trim();
                        }
                    }
                    else if (t == "Loop: 1")
                    {
                        blockLoop = true;
                    }
                    continue;
                }

                // 프리팹 인스턴스 오버라이드(m_Modifications): propertyPath 다음 줄들에 value/objectReference.
                if (t.StartsWith("propertyPath:", StringComparison.Ordinal))
                {
                    pendingLoopOverride = t == "propertyPath: Loop";
                    pendingClipOverride = t == "propertyPath: m_audioClip";
                }
                else if (pendingLoopOverride && t == "value: 1")
                {
                    overrideLoop = true;
                    pendingLoopOverride = false;
                }
                else if (pendingClipOverride && t.StartsWith("objectReference:", StringComparison.Ordinal))
                {
                    int gi = t.IndexOf(GuidKey, StringComparison.Ordinal);
                    if (gi >= 0)
                    {
                        int start = gi + GuidKey.Length;
                        int end = t.IndexOfAny(new[] { ',', '}' }, start);
                        overrideGuids.Add((end > start ? t.Substring(start, end - start) : t.Substring(start)).Trim());
                    }
                    pendingClipOverride = false;
                }
            }

            FlushBlock();
            if (overrideLoop)
            {
                foreach (var g in overrideGuids)
                {
                    sink.Add(g);
                }
            }
        }

        // ─────────────────────────── 실행 ───────────────────────────

        /// <summary>
        /// 후보 사본들을 판정해 대상만 일괄 재인코딩하고, 채택 게이트를 통과한 산출물로
        /// 사본을 제자리 교체한다(파일명/매니페스트 불변). 실패는 파일 단위로 격리되며
        /// 어떤 실패에서도 원본 사본이 유지된다. 반환: 교체된 파일 수.
        /// </summary>
        internal static int TranscodeInPlace(AITEditorScriptObject config, IReadOnlyList<Candidate> candidates)
        {
            if (!IsEnabled(config) || candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            int targetKbps = ResolveTargetKbps(config);
            int minSourceKbps = ResolveMinSourceKbps(config);

            var targets = new List<Candidate>();
            foreach (var c in candidates)
            {
                if (!string.Equals(Path.GetExtension(c.AbsPath), ".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // v1 은 MP3 전용(런타임 경로 불변 보장). WAV/OGG 는 후속.
                }

                if (ShouldTranscode(c.Bytes, c.Seconds, minSourceKbps, targetKbps))
                {
                    targets.Add(c);
                }
            }

            // 자동 모드 안전 게이트 ①: 긴 클립(>20초)은 BGM 가능성 — 인코더가 delay/padding 태그를 쓰지 않아 루프 갭을 막을 수 없다.
            if (targets.Count > 0 && config.audioStreamTranscode < 0)
            {
                targets.RemoveAll(c =>
                {
                    bool longClip = IsLikelyBgmByLength(c.Seconds);
                    if (longClip)
                    {
                        Debug.Log($"[AIT-AudioTranscode]   자동 제외({c.Seconds:0.#}초 > {AutoMaxClipSeconds:0}초, gapless 태그 없음): {Path.GetFileName(c.AbsPath)}");
                    }
                    return longClip;
                });
            }

            // 자동 모드 안전 게이트 ②: loop=true AudioSource 가 쓰는 클립은 제외(LAME delay/padding 이음새 갭 방지).
            // 명시 활성(==1)은 사용자 의도를 그대로 따른다.
            if (targets.Count > 0 && config.audioStreamTranscode < 0)
            {
                var loopGuids = CollectLoopingClipGuids(out bool scanIncomplete);
                if (scanIncomplete)
                {
                    Debug.LogWarning("[AIT-AudioTranscode] 바이너리 직렬화 씬/프리팹이 있어 루프 클립을 확정할 수 없습니다 → 안전을 위해 재인코딩을 건너뜁니다 (audioStreamTranscode=1 로 강제 가능).");
                    return 0;
                }

                targets.RemoveAll(c =>
                {
                    bool looping = !string.IsNullOrEmpty(c.Guid) && loopGuids.Contains(c.Guid);
                    if (looping)
                    {
                        Debug.Log($"[AIT-AudioTranscode]   루프 재생 클립이라 재인코딩 제외: {Path.GetFileName(c.AbsPath)}");
                    }
                    return looping;
                });
            }

            if (targets.Count == 0)
            {
                Debug.Log($"[AIT-AudioTranscode] 재인코딩 대상 없음(≥{minSourceKbps}kbps MP3 스트림 없음) → no-op.");
                return 0;
            }

            if (!EnsureTool(out string node, out string runner))
            {
                Debug.LogWarning("[AIT-AudioTranscode] 도구 준비 실패 — 스트림 사본을 원본 그대로 유지합니다.");
                return 0;
            }

            var results = RunBatch(node, runner, targets, targetKbps);
            int adopted = 0;
            long savedBytes = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                string src = targets[i].AbsPath;
                string tmp = src + TmpSuffix;
                try
                {
                    Result r = results != null && results.TryGetValue(i, out var rr) ? rr : null;
                    if (r == null || !r.Ok || !File.Exists(tmp))
                    {
                        string why = r == null ? "결과 없음" : (string.IsNullOrEmpty(r.error) ? "산출물 없음" : r.error);
                        Debug.LogWarning($"[AIT-AudioTranscode]   실패(원본 유지) {Path.GetFileName(src)}: {why}");
                        continue;
                    }

                    if (!ShouldAdopt(r.raw, r.@out))
                    {
                        Debug.LogWarning($"[AIT-AudioTranscode]   절감 미달(원본 유지) {Path.GetFileName(src)}: {r.raw / 1048576f:0.00}→{r.@out / 1048576f:0.00}MB (<{MinGainPercent}%)");
                        continue;
                    }

                    File.Delete(src);
                    File.Move(tmp, src);
                    adopted++;
                    savedBytes += r.raw - r.@out;
                    Debug.Log($"[AIT-AudioTranscode]   재인코딩 {Path.GetFileName(src)}: {r.srcKbps}→{targetKbps}kbps, {r.raw / 1048576f:0.00}→{r.@out / 1048576f:0.00}MB");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch
                    {
                        // 임시 산출물 정리 실패는 무시(다음 빌드에서 streamroot 째로 제거됨).
                    }
                }
            }

            Debug.Log($"[AIT-AudioTranscode] ✓ 스트림 오디오 {adopted}/{targets.Count}개 재인코딩({targetKbps}kbps CBR), {savedBytes / 1048576f:0.0}MB 절감");
            return adopted;
        }

        // ─────────────────────────── PCM WAV → AAC-LC (P0-6) ───────────────────────────

        /// <summary>AAC 변환 산출물 확장자(매니페스트 file 이 이 확장자로 바뀐다).</summary>
        internal const string AacExtension = ".m4a";

        /// <summary>AAC 변환 산출물의 매니페스트 mime(런타임 AudioType 판정과 진단용).</summary>
        internal const string AacMime = "audio/mp4";

        /// <summary>
        /// WAV 사본 1건의 AAC 변환 가능 여부. 변환하면 안 되는 이유를 짧은 코드로 돌려주고, 변환 대상이면 null 이다(순수 함수).
        /// 코드: not-wav / not-compressed-path / not-pcm / channels / loop / loop-unknown.
        /// </summary>
        internal static string DecideAacSkipReason(string ext, bool compressedPath, bool pcm, int channels,
            bool looping, bool loopScanIncomplete, bool loopOptIn)
        {
            if (!string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                return "not-wav";
            }

            if (!compressedPath)
            {
                return "not-compressed-path"; // 짧은 효과음은 지연 없는 PCM 재생을 유지한다.
            }

            if (!pcm)
            {
                return "not-pcm"; // ADPCM 등 이미 압축된 WAV 는 세대손실만 남는다.
            }

            if (channels < 1 || channels > 2)
            {
                return "channels"; // 다채널은 브라우저별 AAC 다채널 재생이 갈려 건드리지 않는다.
            }

            if (!loopOptIn && looping)
            {
                return "loop";
            }

            if (!loopOptIn && loopScanIncomplete)
            {
                return "loop-unknown"; // 바이너리 씬/프리팹이 있어 루프 여부를 확정할 수 없다.
            }

            return null;
        }

        /// <summary>
        /// 압축 재생 경로로 가는 PCM WAV 사본을 AAC-LC(.m4a)로 바꾼다. 성공한 것만 WAV 사본을 지우고 .m4a 를 남기며,
        /// 반환값은 에셋 GUID → 새 사본의 절대 경로다(호출부가 매니페스트 file/mime 을 고친다).
        /// 인코더가 없거나 실패·절감 미달이면 그 항목은 WAV 그대로 둔다(프로젝트 원본은 어떤 경우에도 비접촉).
        /// </summary>
        internal static Dictionary<string, string> TranscodeWavToAac(AITEditorScriptObject config, IReadOnlyList<Candidate> candidates)
        {
            var renamed = new Dictionary<string, string>();
            if (!IsEnabled(config) || candidates == null || candidates.Count == 0)
            {
                return renamed;
            }

            var wavs = new List<Candidate>();
            foreach (var c in candidates)
            {
                if (c.Compressed && string.Equals(Path.GetExtension(c.AbsPath), ".wav", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(c.Guid))
                {
                    wavs.Add(c);
                }
            }

            if (wavs.Count == 0)
            {
                return renamed;
            }

            bool loopOptIn = AITPerfFlags.EffectiveAudioStreamLoopTranscode(config);
            HashSet<string> loopGuids = null;
            bool scanIncomplete = false;
            if (!loopOptIn)
            {
                loopGuids = CollectLoopingClipGuids(out scanIncomplete);
            }

            var targets = new List<Candidate>();
            var infos = new List<AITAudioAacEncoder.WavInfo>();
            foreach (var c in wavs)
            {
                bool pcm = AITAudioAacEncoder.TryReadWavInfo(c.AbsPath, out var info) && info.IsPcm;
                string why = DecideAacSkipReason(".wav", c.Compressed, pcm, info.Channels,
                    loopGuids != null && loopGuids.Contains(c.Guid), scanIncomplete, loopOptIn);
                if (why != null)
                {
                    Debug.Log($"[AIT-AudioTranscode]   WAV→AAC 제외({why}): {Path.GetFileName(c.AbsPath)}"
                              + (why == "loop" || why == "loop-unknown" ? " — 루프 클립은 audioStreamLoopTranscode=1 로 켠다" : string.Empty));
                    continue;
                }

                targets.Add(c);
                infos.Add(info);
            }

            if (targets.Count == 0)
            {
                return renamed;
            }

            if (!AITAudioAacEncoder.TryFindTool(out var tool, out string toolPath))
            {
                Debug.LogWarning($"[AIT-AudioTranscode] AAC 인코더(ffmpeg/afconvert)를 찾지 못해 PCM WAV {targets.Count}개를 그대로 둡니다 "
                                 + $"— 압축 재생 경로의 WAV 는 힙과 Blob 에 원본 크기로 두 번 상주합니다. ffmpeg 를 설치하거나 {AITAudioAacEncoder.FfmpegPathEnvVar} 로 경로를 지정하세요.");
                return renamed;
            }

            int targetKbps = ResolveTargetKbps(config);
            long savedBytes = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                string src = targets[i].AbsPath;
                string dst = Path.ChangeExtension(src, AacExtension);
                string tmp = dst + TmpSuffix;
                int kbps = AITAudioAacEncoder.AacBitrateKbps(targetKbps, infos[i].Channels);
                try
                {
                    if (!AITAudioAacEncoder.TryEncode(tool, toolPath, src, tmp, kbps, out string error))
                    {
                        Debug.LogWarning($"[AIT-AudioTranscode]   AAC 인코딩 실패(WAV 유지) {Path.GetFileName(src)}: {error}");
                        continue;
                    }

                    long raw = new FileInfo(src).Length;
                    long outBytes = new FileInfo(tmp).Length;
                    if (!ShouldAdopt(raw, outBytes))
                    {
                        Debug.LogWarning($"[AIT-AudioTranscode]   AAC 절감 미달(WAV 유지) {Path.GetFileName(src)}: {raw / 1048576f:0.00}→{outBytes / 1048576f:0.00}MB (<{MinGainPercent}%)");
                        continue;
                    }

                    if (File.Exists(dst))
                    {
                        File.Delete(dst);
                    }

                    File.Move(tmp, dst);
                    File.Delete(src);
                    renamed[targets[i].Guid] = dst;
                    savedBytes += raw - outBytes;
                    Debug.Log($"[AIT-AudioTranscode]   WAV→AAC {Path.GetFileName(src)}: {raw / 1048576f:0.00}→{outBytes / 1048576f:0.00}MB ({kbps}kbps, {tool})");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-AudioTranscode]   WAV→AAC 예외(WAV 유지) {Path.GetFileName(src)}: {e.Message}");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch
                    {
                        // 임시 산출물 정리 실패는 무시.
                    }
                }
            }

            Debug.Log($"[AIT-AudioTranscode] ✓ 압축 재생용 WAV {renamed.Count}/{targets.Count}개를 AAC-LC({targetKbps}kbps)로 변환, {savedBytes / 1048576f:0.0}MB 절감");
            return renamed;
        }

        // ─────────────────────────── 도구 준비 (FontSubset 패턴) ───────────────────────────

        /// <summary>내장 Node.js + 러너/의존성을 준비한다. 미설치 시 on-demand npm install.</summary>
        private static bool EnsureTool(out string nodeExe, out string runnerPath)
        {
            nodeExe = null;
            runnerPath = null;
            try
            {
                if (!AITBrotliCompressor.TryResolveNode(out string node))
                {
                    return false;
                }

                string srcDir = ResolveToolSourceDir();
                if (string.IsNullOrEmpty(srcDir) || !File.Exists(Path.Combine(srcDir, RunnerName)))
                {
                    Debug.LogWarning($"[AIT-AudioTranscode] 러너 소스 디렉토리를 찾지 못했습니다: '{srcDir}'");
                    return false;
                }

                string homeTool = GetHomeToolDir();
                Directory.CreateDirectory(homeTool);

                // 러너/매니페스트는 항상 최신본으로 동기화(SDK 업데이트 반영).
                // package-lock.json도 함께 동기화해 npm ci가 전이 의존성까지 고정된 버전으로 설치하게 한다.
                File.Copy(Path.Combine(srcDir, RunnerName), Path.Combine(homeTool, RunnerName), true);
                File.Copy(Path.Combine(srcDir, "package.json"), Path.Combine(homeTool, "package.json"), true);
                string srcLockfile = Path.Combine(srcDir, "package-lock.json");
                string lockfileHash = null;
                if (File.Exists(srcLockfile))
                {
                    File.Copy(srcLockfile, Path.Combine(homeTool, "package-lock.json"), true);
                    lockfileHash = ComputeFileHash(srcLockfile);
                }

                // 의존성 미설치 또는 lockfile 해시 불일치(기존 설치자 마이그레이션 포함) 시 재설치.
                // lockfile이 없는 예외 상황(구 SDK 배포본 등)에서는 산출물 존재 판정으로 축퇴한다.
                string installedDecoder = Path.Combine(homeTool, "node_modules", "mpg123-decoder", "package.json");
                string installedEncoder = Path.Combine(homeTool, "node_modules", "wasm-media-encoders", "package.json");
                string stampPath = Path.Combine(homeTool, LockfileHashStampName);
                bool stampMatches = lockfileHash == null || ReadStampHash(stampPath) == lockfileHash;
                if (!File.Exists(installedDecoder) || !File.Exists(installedEncoder) || !stampMatches)
                {
                    // 기존 설치를 삭제 대신 .bak으로 대피시켜 npm ci 실패 시 원복할 수 있게 한다.
                    string nodeModulesDir = Path.Combine(homeTool, "node_modules");
                    string nodeModulesBakDir = nodeModulesDir + ".bak";
                    if (Directory.Exists(nodeModulesBakDir))
                    {
                        Directory.Delete(nodeModulesBakDir, true);
                    }
                    if (Directory.Exists(nodeModulesDir))
                    {
                        Directory.Move(nodeModulesDir, nodeModulesBakDir);
                    }

                    Debug.Log("[AIT-AudioTranscode] 트랜스코더 설치 중(내장 npm)...");
                    string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
                    if (string.IsNullOrEmpty(npm) || !RunNpmInstall(npm, Path.GetDirectoryName(npm), homeTool)
                        || !File.Exists(installedDecoder) || !File.Exists(installedEncoder))
                    {
                        Debug.LogWarning("[AIT-AudioTranscode] 트랜스코더 설치 실패. 기존 설치를 유지합니다.");
                        if (Directory.Exists(nodeModulesDir))
                        {
                            Directory.Delete(nodeModulesDir, true);
                        }
                        if (Directory.Exists(nodeModulesBakDir))
                        {
                            Directory.Move(nodeModulesBakDir, nodeModulesDir);
                        }
                        return false;
                    }

                    if (Directory.Exists(nodeModulesBakDir))
                    {
                        Directory.Delete(nodeModulesBakDir, true);
                    }

                    if (lockfileHash != null)
                    {
                        File.WriteAllText(stampPath, lockfileHash);
                    }
                }

                nodeExe = node;
                runnerPath = Path.Combine(homeTool, RunnerName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] 도구 준비 예외: {e.Message}");
                return false;
            }
        }

        private static string GetHomeToolDir()
        {
            string basePath = AITPlatformHelper.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(basePath, ".ait-unity-sdk", "audio-transcode");
        }

        /// <summary>SDK 패키지에 동봉된 러너 소스 디렉토리. UPM/embedded 설치 모두 해석.</summary>
        private static string ResolveToolSourceDir()
        {
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AITAudioStreamTranscoder).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    return Path.Combine(pkg.resolvedPath, "Editor", ToolDirName);
                }
            }
            catch
            {
                // PackageInfo 미해석(Assets 내 임베드 개발) → 소스 파일 위치 폴백
            }

            string here = CallerDir();
            return string.IsNullOrEmpty(here) ? null : Path.Combine(here, ToolDirName);
        }

        private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

        private static bool RunNpmInstall(string npm, string nodeBin, string cwd)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                // package-lock.json이 있으면 npm ci로 전이 의존성까지 잠긴 버전 그대로 재현 설치한다
                // (없으면 구 SDK 배포본 등 하위 호환을 위해 install로 폴백). 두 경우 모두
                // --ignore-scripts로 lifecycle 스크립트를 차단한다(현재 의존성 트리엔 없지만 방어적으로).
                bool hasLockfile = File.Exists(Path.Combine(cwd, "package-lock.json"));
                string npmSubcommand = hasLockfile ? "ci" : "install";
                if (AITPlatformHelper.IsWindows)
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c \"\"{npm}\" {npmSubcommand} --no-audit --no-fund --ignore-scripts --loglevel=error\"";
                }
                else
                {
                    psi.FileName = npm;
                    psi.Arguments = $"{npmSubcommand} --no-audit --no-fund --ignore-scripts --loglevel=error";
                }

                string sep = AITPlatformHelper.IsWindows ? ";" : ":";
                string existing = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                psi.EnvironmentVariables["PATH"] = nodeBin + sep + existing;

                using (var p = new Process { StartInfo = psi })
                {
                    p.Start();
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(180000))
                    {
                        try { p.Kill(); } catch { /* 이미 종료됨 */ }
                        return false;
                    }

                    return p.ExitCode == 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] npm install 예외: {e.Message}");
                return false;
            }
        }

        /// <summary>lockfile 내용의 SHA-256 해시(소문자 hex). 설치 재현성 판정에 사용.</summary>
        private static string ComputeFileHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>스탬프 파일에 기록된 lockfile 해시. 파일 없음/읽기 실패 시 null(불일치로 간주).</summary>
        private static string ReadStampHash(string stampPath)
        {
            try
            {
                return File.Exists(stampPath) ? File.ReadAllText(stampPath).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────── 러너 실행 (Brotli 패턴) ───────────────────────────

        /// <summary>대상 목록을 러너에 일괄 위임. 반환: 대상 인덱스 → Result (실패 시 빈 딕셔너리).</summary>
        private static Dictionary<int, Result> RunBatch(string node, string runner, IReadOnlyList<Candidate> targets, int targetKbps)
        {
            var map = new Dictionary<int, Result>();
            try
            {
                var input = new StringBuilder();
                input.Append("{\"targetKbps\":").Append(targetKbps).Append(",\"files\":[");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0)
                    {
                        input.Append(',');
                    }

                    input.Append("{\"idx\":").Append(i)
                         .Append(",\"src\":").Append(JsonStr(targets[i].AbsPath))
                         .Append(",\"dst\":").Append(JsonStr(targets[i].AbsPath + TmpSuffix)).Append('}');
                }

                input.Append("]}");

                if (!RunNode(node, runner, input.ToString(), out string stdout, out string stderr))
                {
                    Debug.LogWarning($"[AIT-AudioTranscode] 일괄 재인코딩 실행 실패 — 원본 유지: {Truncate(stderr ?? stdout)}");
                    return map;
                }

                var batch = UnityEngine.JsonUtility.FromJson<Batch>(stdout);
                if (batch?.results == null)
                {
                    Debug.LogWarning("[AIT-AudioTranscode] 재인코딩 결과 파싱 실패 — 원본 유지.");
                    return map;
                }

                foreach (var r in batch.results)
                {
                    if (r != null && r.idx >= 0 && r.idx < targets.Count)
                    {
                        map[r.idx] = r;
                    }
                }

                return map;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioTranscode] 재인코딩 예외 — 원본 유지: {e.Message}");
                map.Clear();
                return map;
            }
        }

        private static bool RunNode(string node, string runner, string stdinJson, out string stdout, out string stderr)
        {
            stdout = null;
            stderr = null;
            var psi = new ProcessStartInfo
            {
                FileName = node,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(runner),
            };
            psi.ArgumentList.Add(runner);

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();

                // stderr 는 이벤트로 비동기 수집 — stdout ReadToEnd 중 stderr 버퍼 교착 방지.
                var errSb = new StringBuilder();
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) { errSb.AppendLine(ev.Data); } };
                p.BeginErrorReadLine();

                p.StandardInput.Write(stdinJson);
                p.StandardInput.Close();

                stdout = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(TimeoutMs))
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
