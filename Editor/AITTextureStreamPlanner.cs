// -----------------------------------------------------------------------
// <copyright file="AITTextureStreamPlanner.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Texture streaming planner (pure build-time logic)
// </copyright>
// -----------------------------------------------------------------------
//
// AITLargeTextureExternalizer 가 쓰는 순수 판정/계산 모음(EditMode 테스트 대상).
//   - GPU 메모리 추정: 포맷 이름 + 차원 + mip 수 → 바이트(원본 GPU 바이트, 스텁 RGBA32 바이트).
//   - 메모리 예산 선택: auto 외부화는 "스텁 RGBA32 − 원본 GPU" 합계가 예산 이내인 텍스처만.
//   - GPU 포맷 보존(raw ASTC) 적격 판정과 매니페스트 엔트리 JSON 생성.
// UnityEditor/UnityEngine API 에 의존하지 않는다(포맷은 TextureFormat.ToString() 이름 문자열로 받는다).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AppsInToss.Editor
{
    internal static class AITTextureStreamPlanner
    {
        /// <summary>GPU 포맷 보존(원본 ASTC 블록 스트리밍)을 환경 변수로 덮어쓰는 이름. "1"=강제 켬, "0"=끔, 그 외/미설정=설정값.</summary>
        internal const string KeepGpuFormatEnvVar = "AIT_TEXTURE_STREAM_KEEP_GPU_FORMAT";

        /// <summary>설정 필드 이름(AITEditorScriptObject.textureStreamKeepGpuFormat, tri-state int). 필드가 없으면 -1(자동)로 본다.</summary>
        internal const string KeepGpuFormatFieldName = "textureStreamKeepGpuFormat";

        /// <summary>브라우저 디코드를 환경 변수로 덮어쓰는 이름. "1"=강제 켬, "0"=끔, 그 외/미설정=설정값.</summary>
        internal const string BrowserDecodeEnvVar = "AIT_TEXTURE_STREAM_BROWSER_DECODE";

        /// <summary>raw 스트림 사본 확장자(점 포함). 내용은 mip 체인 전체를 이어 붙인 GPU 블록 바이트.</summary>
        internal const string RawExtension = ".astc";

        /// <summary>런타임 동시 스트리밍 기본값(설정이 0 이하일 때).</summary>
        internal const int DefaultMaxConcurrent = 1;

        private static readonly Regex AstcName = new Regex("^ASTC_(\\d+)x(\\d+)$", RegexOptions.Compiled);

        // ─────────────────────── tri-state ───────────────────────

        /// <summary>tri-state(-1 자동, 0 끔, 1 강제)를 bool 로 푼다. 자동은 <paramref name="autoValue"/>.</summary>
        internal static bool ResolveTriState(int stored, bool autoValue)
        {
            return stored == 1 || (stored < 0 && autoValue);
        }

        /// <summary>환경 변수 값("1"/"0")을 tri-state 로. 그 외는 <paramref name="fallback"/>.</summary>
        internal static int ParseTriStateEnv(string env, int fallback)
        {
            if (env == null)
            {
                return fallback;
            }

            string t = env.Trim();
            if (t == "1" || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (t == "0" || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return fallback;
        }

        /// <summary>매니페스트 maxConcurrent 로 기록할 값. 0 이하(미설정)는 1.</summary>
        internal static int ResolveMaxConcurrent(int configValue)
        {
            return configValue > 0 ? configValue : DefaultMaxConcurrent;
        }

        // ─────────────────────── 포맷/크기 ───────────────────────

        /// <summary>ASTC_NxM 이름에서 블록 차원을 읽는다. HDR(ASTC_HDR_*)·구형 ASTC_RGB_* 는 false.</summary>
        internal static bool TryParseAstcBlock(string formatName, out int blockW, out int blockH)
        {
            blockW = 0;
            blockH = 0;
            if (string.IsNullOrEmpty(formatName))
            {
                return false;
            }

            var m = AstcName.Match(formatName);
            if (!m.Success)
            {
                return false;
            }

            blockW = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            blockH = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return blockW >= 4 && blockH >= 4;
        }

        /// <summary>
        /// TextureFormat 이름 → (블록 너비, 블록 높이, 블록당 바이트). 비압축은 블록 1x1, 픽셀당 바이트.
        /// 모르는 이름은 false(호출부가 RGBA32 로 가정한다).
        /// </summary>
        internal static bool TryGetBlockInfo(string formatName, out int blockW, out int blockH, out int blockBytes)
        {
            blockW = 1;
            blockH = 1;
            blockBytes = 4;
            if (string.IsNullOrEmpty(formatName))
            {
                return false;
            }

            if (TryParseAstcBlock(formatName, out blockW, out blockH))
            {
                blockBytes = 16;
                return true;
            }

            blockW = 4;
            blockH = 4;
            switch (formatName)
            {
                case "DXT1":
                case "DXT1Crunched":
                case "BC4":
                case "ETC_RGB4":
                case "ETC_RGB4Crunched":
                case "ETC_RGB4_3DS":
                case "ETC2_RGB":
                case "ETC2_RGBA1":
                case "EAC_R":
                case "EAC_R_SIGNED":
                case "PVRTC_RGB4":
                case "PVRTC_RGBA4":
                    blockBytes = 8;
                    return true;
                case "DXT5":
                case "DXT5Crunched":
                case "BC5":
                case "BC6H":
                case "BC7":
                case "ETC2_RGBA8":
                case "ETC2_RGBA8Crunched":
                case "ETC2_RGBA8_3DS":
                case "EAC_RG":
                case "EAC_RG_SIGNED":
                    blockBytes = 16;
                    return true;
                case "PVRTC_RGB2":
                case "PVRTC_RGBA2":
                    blockW = 8;
                    blockH = 4;
                    blockBytes = 8;
                    return true;
            }

            blockW = 1;
            blockH = 1;
            switch (formatName)
            {
                case "Alpha8":
                case "R8":
                    blockBytes = 1;
                    return true;
                case "RGB565":
                case "RGBA4444":
                case "ARGB4444":
                case "R16":
                case "RG16":
                case "RHalf":
                    blockBytes = 2;
                    return true;
                case "RGB24":
                    blockBytes = 3;
                    return true;
                case "RGBA32":
                case "ARGB32":
                case "BGRA32":
                case "RGHalf":
                case "RFloat":
                case "RGB9e5Float":
                    blockBytes = 4;
                    return true;
                case "RGBAHalf":
                case "RGFloat":
                case "RGBA64":
                    blockBytes = 8;
                    return true;
                case "RGBAFloat":
                    blockBytes = 16;
                    return true;
            }

            blockBytes = 4;
            return false;
        }

        /// <summary>mip 체인 전체의 바이트(레벨별 max(1, w>>i) 차원, 블록 단위 올림). mips&lt;1 은 1로 본다.</summary>
        internal static long ComputeMipChainBytes(int w, int h, int mips, int blockW, int blockH, int blockBytes)
        {
            if (w <= 0 || h <= 0 || blockW <= 0 || blockH <= 0 || blockBytes <= 0)
            {
                return 0;
            }

            if (mips < 1)
            {
                mips = 1;
            }

            long total = 0;
            for (int i = 0; i < mips; i++)
            {
                int lw = Math.Max(1, w >> i);
                int lh = Math.Max(1, h >> i);
                long bx = (lw + blockW - 1) / blockW;
                long by = (lh + blockH - 1) / blockH;
                total += bx * by * blockBytes;
            }

            return total;
        }

        /// <summary>원본 텍스처의 GPU(저장) 바이트 추정. 모르는 포맷은 RGBA32 로 가정한다(= 스텁과 같아 메모리 중립).</summary>
        internal static long EstimateGpuBytes(string formatName, int w, int h, int mips)
        {
            TryGetBlockInfo(formatName, out int bw, out int bh, out int bb);
            return ComputeMipChainBytes(w, h, mips, bw, bh, bb);
        }

        /// <summary>스텁(RGBA32 비압축)의 바이트. 원본과 같은 mip 수를 쓴다(임포터의 mipmapEnabled 가 그대로이므로).</summary>
        internal static long EstimateStubBytes(int w, int h, int mips)
        {
            return ComputeMipChainBytes(w, h, mips, 1, 1, 4);
        }

        /// <summary>외부화로 늘어나는 메모리(스텁 RGBA32 − 원본 GPU). 음수/0 이면 메모리 중립 이하.</summary>
        internal static long ComputeMemoryDelta(long stubBytes, long originalGpuBytes)
        {
            return stubBytes - originalGpuBytes;
        }

        // ─────────────────────── 예산 선택 ───────────────────────

        /// <summary>
        /// auto 외부화의 메모리 예산 선택. 반환 배열은 입력과 같은 순서이고 true 가 외부화.
        /// 예산 0 이하는 제한 없음(전부 true). delta 가 0 이하(메모리 중립 이하)는 항상 true.
        /// 양수 delta 는 작은 것부터 누적해 예산을 넘기 직전까지만 true — 같은 delta 는 입력 순서를 따른다(결정적).
        /// </summary>
        internal static bool[] SelectWithinBudget(IList<long> deltas, long budgetBytes)
        {
            int n = deltas == null ? 0 : deltas.Count;
            var pick = new bool[n];
            if (n == 0)
            {
                return pick;
            }

            if (budgetBytes <= 0)
            {
                for (int i = 0; i < n; i++)
                {
                    pick[i] = true;
                }

                return pick;
            }

            var positive = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (deltas[i] <= 0)
                {
                    pick[i] = true;
                }
                else
                {
                    positive.Add(i);
                }
            }

            positive.Sort((a, b) =>
            {
                int c = deltas[a].CompareTo(deltas[b]);
                return c != 0 ? c : a.CompareTo(b);
            });

            long used = 0;
            foreach (int idx in positive)
            {
                if (used + deltas[idx] > budgetBytes)
                {
                    break;
                }

                used += deltas[idx];
                pick[idx] = true;
            }

            return pick;
        }

        // ─────────────────────── raw(GPU 포맷 보존) 적격 ───────────────────────

        /// <summary>
        /// 원본 ASTC 블록을 그대로 스트리밍할 수 있는 텍스처인지.
        /// 비-HDR ASTC, 크런치 아님, mip 스트리밍 아님, 2D 단일 텍스처, mip 수가 차원에서 나오는 최대 체인 이하여야 한다.
        /// </summary>
        internal static bool IsRawEligible(string formatName, bool crunched, bool streamingMipmaps, bool isTexture2D, int w, int h, int mips)
        {
            if (crunched || streamingMipmaps || !isTexture2D || w <= 0 || h <= 0 || mips < 1)
            {
                return false;
            }

            if (!TryParseAstcBlock(formatName, out _, out _))
            {
                return false;
            }

            return mips <= MaxMipCount(w, h);
        }

        /// <summary>차원에서 나올 수 있는 최대 mip 레벨 수(1x1 까지).</summary>
        internal static int MaxMipCount(int w, int h)
        {
            int m = Math.Max(w, h);
            int count = 1;
            while (m > 1)
            {
                m >>= 1;
                count++;
            }

            return count;
        }

        /// <summary>raw 사본의 기대 바이트. ASTC 가 아니면 0.</summary>
        internal static long ExpectedRawBytes(string formatName, int w, int h, int mips)
        {
            if (!TryParseAstcBlock(formatName, out int bw, out int bh))
            {
                return 0;
            }

            return ComputeMipChainBytes(w, h, mips, bw, bh, 16);
        }

        // ─────────────────────── 매니페스트 ───────────────────────

        /// <summary>매니페스트 엔트리 1건의 입력.</summary>
        internal struct EntrySpec
        {
            public string guid;
            public string name;

            /// <summary>PNG/JPG 사본 파일명(raw 가 있어도 폴백으로 항상 채운다).</summary>
            public string file;
            public bool fileBrotli;
            public int width;
            public int height;

            /// <summary>스트림 사본 실제 차원(다운스케일). 0 이면 기록하지 않는다.</summary>
            public int sw;
            public int sh;

            /// <summary>원본이 non-readable 이었는지. true 면 복원 시 LoadImage markNonReadable / Apply(makeNoLongerReadable).</summary>
            public bool nonReadable;

            /// <summary>raw 사본 파일명(없으면 null).</summary>
            public string rawFile;
            public bool rawBrotli;

            /// <summary>raw 사본의 TextureFormat 정수값.</summary>
            public int rawFormat;
            public int rawMips;

            /// <summary>raw 사본의 해제 후 바이트(LoadRawTextureData 가 요구하는 정확한 크기).</summary>
            public long rawSize;
        }

        /// <summary>
        /// 엔트리 JSON. 필드 순서: guid, name, file, [encoding], width, height, [sw, sh], [nonReadable], [raw*].
        /// guid 는 AITDataBreakdownReport 가 정규식으로 읽는 공통 계약이라 항상 첫 필드다.
        /// 선택 필드는 기본값이면 쓰지 않는다 — 구 런타임/매니페스트와 호환(nonReadable 없음 = 기존처럼 readable 복원).
        /// </summary>
        internal static string BuildEntryJson(EntrySpec s)
        {
            var sb = new StringBuilder(192);
            sb.Append("{\"guid\":\"").Append(s.guid).Append("\",\"name\":").Append(JsonStr(s.name))
              .Append(",\"file\":").Append(JsonStr(s.file));
            if (s.fileBrotli)
            {
                sb.Append(",\"encoding\":\"br\"");
            }

            sb.Append(",\"width\":").Append(s.width).Append(",\"height\":").Append(s.height);
            if (s.sw > 0 && s.sh > 0)
            {
                sb.Append(",\"sw\":").Append(s.sw).Append(",\"sh\":").Append(s.sh);
            }

            if (s.nonReadable)
            {
                sb.Append(",\"nonReadable\":1");
            }

            if (!string.IsNullOrEmpty(s.rawFile))
            {
                sb.Append(",\"rawFile\":").Append(JsonStr(s.rawFile));
                if (s.rawBrotli)
                {
                    sb.Append(",\"rawEncoding\":\"br\"");
                }

                sb.Append(",\"rawFormat\":").Append(s.rawFormat)
                  .Append(",\"rawMips\":").Append(s.rawMips)
                  .Append(",\"rawSize\":").Append(s.rawSize.ToString(CultureInfo.InvariantCulture));
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>매니페스트 전체 JSON.</summary>
        internal static string BuildManifestJson(int maxConcurrent, IList<string> entryJsons)
        {
            return BuildManifestJson(maxConcurrent, entryJsons, false);
        }

        /// <summary>매니페스트 전체 JSON. browserDecode=true 면 최상위에 <c>"browserDecode":1</c> 을 쓴다(꺼짐이면 쓰지 않아 구 런타임/매니페스트와 호환).</summary>
        internal static string BuildManifestJson(int maxConcurrent, IList<string> entryJsons, bool browserDecode)
        {
            return "{\"maxConcurrent\":" + maxConcurrent + (browserDecode ? ",\"browserDecode\":1" : string.Empty)
                + ",\"entries\":[" + string.Join(",", entryJsons) + "]}";
        }

        internal static string JsonStr(string s)
        {
            return "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
