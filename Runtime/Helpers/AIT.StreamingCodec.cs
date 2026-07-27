// -----------------------------------------------------------------------
// <copyright file="AIT.StreamingCodec.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming payload codec (brotli)
// </copyright>
// -----------------------------------------------------------------------
//
// 스트리밍 에셋(ait-stream-*)의 brotli(.br) 페이로드를 런타임에서 판별/해제한다.
//
// 서빙 경로가 둘로 갈린다:
//   1) 서버/CDN이 .br 파일에 Content-Encoding: br 을 붙이면 브라우저(fetch)가 투명 해제
//      → UnityWebRequest 가 받은 바이트는 이미 원본 포맷(PNG/JPG/UnityFS). 로컬 vite
//      dev/preview 는 unityWebContentEncodingPlugin 이 모든 *.br 에 이 헤더를 붙인다.
//   2) 헤더가 없으면 raw brotli 바이트가 그대로 도착 → 여기서 명시적으로 해제.
// 프로덕션 CDN이 ait-stream-* 경로의 .br 에 헤더를 줄지는 배포 인프라 의존이라 단정할 수
// 없으므로, 매직 바이트 스니핑으로 두 경우를 판별해 필요할 때만 해제한다(brotli 는 포맷
// 자체에 매직 바이트가 없어 "이미 기대 포맷인지"를 기준으로 역판별한다).
//
// BrotliStream 은 .NET Standard 2.1 프로파일에만 존재한다. .NET Framework(4.x) API 레벨
// 프로젝트에서는 컴파일이 불가하므로 조건부 컴파일로 분리하고, 미지원 프로파일에서는 해제
// 없이 원본을 반환한다(그 경우 서버 Content-Encoding 경로(1)가 유일한 성공 경로 — 경고로
// 가시화). 해제는 메인 스레드에서 일어나지만 스트리밍 재수화는 첫 프레임(TTFF) 이후의
// 배경 작업이라 부팅 임계 경로에 끼지 않는다.

using System;
using UnityEngine;
#if NET_STANDARD_2_1 || NET_STANDARD
using System.IO;
using System.IO.Compression;
#endif

namespace AppsInToss
{
    /// <summary>
    /// ait-stream-* 페이로드의 brotli 인코딩 판별/해제 유틸(순수 로직 — EditMode 테스트 가능).
    /// 매니페스트 entry 의 encoding 필드("br")와 짝을 이루는 런타임 소비자.
    /// </summary>
    internal static class AITStreamingCodec
    {
        /// <summary>manifest entry.encoding 의 brotli 값(빌드타임 AITBrotliCompressor 채택 시 기록).</summary>
        internal const string EncodingBrotli = "br";

        /// <summary>
        /// 현재 컴파일 프로파일에서 raw brotli 클라이언트 해제(BrotliStream)가 가능한지.
        /// 이 어셈블리(런타임)가 어떤 API 레벨로 컴파일됐는지에 따라 결정된다 — Editor 테스트
        /// 어셈블리의 자체 define(NET_4_6)과 무관하므로, 테스트는 자신의 #if 가 아니라 이 값을
        /// 조회해 기대치를 분기해야 한다(그러지 않으면 프로파일 불일치로 오검출).
        /// </summary>
        internal static bool CanDecompressBrotli =>
#if NET_STANDARD_2_1 || NET_STANDARD
            true;
#else
            false;
#endif

        internal static bool LooksLikePng(byte[] d)
        {
            return d != null && d.Length >= 4 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47;
        }

        internal static bool LooksLikeJpg(byte[] d)
        {
            return d != null && d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF;
        }

        /// <summary>Texture2D.LoadImage 가 처리 가능한 포맷(PNG/JPG)인지.</summary>
        internal static bool LooksLikeImage(byte[] d)
        {
            return LooksLikePng(d) || LooksLikeJpg(d);
        }

        /// <summary>UnityFS(AssetBundle) 시그니처("UnityFS\0")인지.</summary>
        internal static bool LooksLikeUnityFs(byte[] d)
        {
            return d != null && d.Length >= 8
                && d[0] == (byte)'U' && d[1] == (byte)'n' && d[2] == (byte)'i' && d[3] == (byte)'t'
                && d[4] == (byte)'y' && d[5] == (byte)'F' && d[6] == (byte)'S' && d[7] == 0x00;
        }

        /// <summary>
        /// encoding=="br" 페이로드를 소비 가능한 원본 포맷으로 정규화한다.
        /// 이미 기대 포맷이면(서버가 Content-Encoding 으로 해제) 그대로, raw brotli 면 해제해서
        /// 반환한다. 해제 불가/실패 시 원본을 그대로 돌려주고 경고를 남긴다(호출부의 기존
        /// 실패 처리에 자연 합류).
        /// </summary>
        internal static byte[] DecodePayload(string encoding, byte[] data, Func<byte[], bool> looksDecoded, string label)
        {
            if (data == null || encoding != EncodingBrotli)
            {
                return data;
            }

            if (looksDecoded != null && looksDecoded(data))
            {
                return data; // 경로 1: 서버가 이미 해제
            }

            if (TryBrotliDecompress(data, out byte[] decoded) && (looksDecoded == null || looksDecoded(decoded)))
            {
                return decoded; // 경로 2: 클라이언트 해제
            }

            Debug.LogWarning($"[AIT-Streaming] brotli 해제 실패 — 수신 바이트를 그대로 사용합니다: {label}");
            return data;
        }

        /// <summary>
        /// brotli 해제 시도. 미지원 프로파일(.NET Framework API 레벨)이거나 유효한 brotli
        /// 스트림이 아니면 false.
        /// </summary>
        internal static bool TryBrotliDecompress(byte[] src, out byte[] result)
        {
            result = null;
            if (src == null || src.Length == 0)
            {
                return false;
            }

#if NET_STANDARD_2_1 || NET_STANDARD
            try
            {
                using (var input = new MemoryStream(src, false))
                using (var brotli = new BrotliStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream(src.Length * 3))
                {
                    brotli.CopyTo(output);
                    if (output.Length == 0)
                    {
                        return false;
                    }

                    result = output.ToArray();
                    return true;
                }
            }
            catch
            {
                // 유효한 brotli 스트림이 아님(예: 서버가 이미 해제했지만 매직 스니핑이 못 알아본
                // 비정형 페이로드) — 호출부가 원본으로 진행하도록 false.
                result = null;
                return false;
            }
#else
            // .NET Framework(4.x) API 레벨: BrotliStream 부재 — 클라이언트 해제 불가.
            // 서버 Content-Encoding 경로만 유효하며, DecodePayload 가 경고로 가시화한다.
            return false;
#endif
        }
    }
}
