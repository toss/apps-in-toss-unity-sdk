// -----------------------------------------------------------------------
// AITTextureStreamJpegTranscoder.cs - 불투명 스트리밍 텍스처 PNG 사본의 빌드타임 JPEG 전환
//
// 대상: AITLargeTextureExternalizer 가 StreamingAssets 에 만든 PNG 사본(프로젝트 원본 아님) 중
//   알파가 없는 파일(IHDR colortype==2 RGB & tRNS 없음)만. 다운스케일(3-1) '이후',
//   무손실 재압축(3-1.5) '이전'에 실행 — JPEG 로 전환된 파일은 oxipng 대상에서 자연 제외된다.
//
// lossy: JPEG DCT 는 픽셀을 바꾼다(사진류 near-transparent, 플랫 아트는 ringing 위험) —
//   시각 검증 게이트 전까지 auto 는 OFF(GetDefaultTextureStreamJpeg() == false),
//   명시 활성(textureStreamJpeg==1)에서만 동작한다. audioStreamTranscode 와 동일 posture.
//
// 채택 게이트: ShouldKeep(≥25%) — lossy 전환은 큰 이득이 있을 때만 정당화된다
//   (불투명 사진류 실측 −68~86%로 통상 크게 상회, 경계 파일만 원본 유지).
//
// 확장자 계약: 전환 시 <guid>.png → <guid>.jpg 로 개명한다. 호출자(외부화기)가 레코드/매니페스트에
//   새 이름을 반영하며, 런타임 Texture2D.LoadImage 는 매직 바이트로 PNG/JPG 를 자동 감지한다
//   (소스가 원래 .jpg 인 스트림 엔트리가 이미 같은 경로로 동작 중).
//
// 실패 정책: 파일 단위 격리 — 어떤 실패에서도 원본 PNG 사본이 유지된다. 외부 도구 없음(순수 C#).
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 불투명(RGB) 스트리밍 텍스처 PNG 사본의 빌드타임 JPEG 전환기.
    /// <see cref="AITLargeTextureExternalizer"/> 가 다운스케일 직후, 무손실 재압축 전에 호출한다.
    /// </summary>
    internal static class AITTextureStreamJpegTranscoder
    {
        /// <summary>채택 최소 이득(%). lossy 전환이라 경계 이득에서는 원본 PNG 를 유지한다.</summary>
        internal const int MinGainPercent = 25;

        private const int DefaultQuality = 90;
        private const int MinQuality = 50;
        private const int MaxQuality = 100;

        // ─────────────────────────── 판정 (순수 함수, Level 0 테스트 대상) ───────────────────────────

        /// <summary>tri-state 해석. lossy 라 auto(-1)는 시각 검증 게이트 전 OFF — 명시 활성(1)만 동작.</summary>
        internal static bool IsEnabled(AITEditorScriptObject config)
        {
            if (config == null)
            {
                return false;
            }

            return config.textureStreamJpeg >= 0
                ? config.textureStreamJpeg == 1
                : AITDefaultSettings.GetDefaultTextureStreamJpeg();
        }

        /// <summary>JPEG 품질 해석(50~100 클램프, 비정상 값은 기본 90).</summary>
        internal static int ResolveQuality(AITEditorScriptObject config)
        {
            int v = config != null ? config.textureStreamJpegQuality : 0;
            if (v <= 0)
            {
                return DefaultQuality;
            }

            return Math.Max(MinQuality, Math.Min(MaxQuality, v));
        }

        /// <summary>채택 판정: lossy 전환이므로 ≥25% 이득일 때만 교체(경계 이득은 원본 유지).</summary>
        internal static bool ShouldAdopt(long rawBytes, long outBytes)
        {
            return AITBrotliCompressor.ShouldKeep(rawBytes, outBytes, MinGainPercent);
        }

        /// <summary>
        /// PNG 바이트가 '확실히 불투명'인지 판정한다: IHDR colortype==2(RGB)이고 IDAT 전에
        /// tRNS 청크가 없어야 true. gray(0)/palette(3)는 마스크·플랫 아트일 가능성이 높아
        /// 보수적으로 제외하고, grayA(4)/RGBA(6)는 알파 보유라 당연히 제외한다.
        /// </summary>
        internal static bool IsOpaquePng(byte[] bytes)
        {
            // 시그니처(8) + IHDR 청크 헤더(8) + IHDR 데이터 13바이트(colortype 은 데이터의 10번째).
            if (bytes == null || bytes.Length < 34)
            {
                return false;
            }

            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47
                || bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
            {
                return false;
            }

            if (bytes[25] != 2)
            {
                return false; // RGB 외 전부 제외(보수적).
            }

            // IDAT 전 메타 청크 구간에서 tRNS 존재 여부 스캔(비정상 길이는 즉시 중단 → 제외).
            long off = 8;
            while (off + 8 <= bytes.Length)
            {
                long len = ((long)bytes[off] << 24) | ((long)bytes[off + 1] << 16)
                    | ((long)bytes[off + 2] << 8) | bytes[off + 3];
                if (len < 0 || off + 12 + len > bytes.Length)
                {
                    return false;
                }

                uint type = ((uint)bytes[off + 4] << 24) | ((uint)bytes[off + 5] << 16)
                    | ((uint)bytes[off + 6] << 8) | bytes[off + 7];
                if (type == 0x74524E53) // "tRNS"
                {
                    return false;
                }

                if (type == 0x49444154) // "IDAT"
                {
                    return true;
                }

                off += 12 + len;
            }

            return false; // IDAT 를 못 만난 잘린 파일 — 제외.
        }

        // ─────────────────────────── 실행 ───────────────────────────

        /// <summary>
        /// 불투명 스트림 PNG 사본을 JPEG 로 전환해 <guid>.jpg 로 교체한다.
        /// 반환: 전환된 파일의 (기존 절대경로 → 새 절대경로) 매핑 — 호출자가 레코드/매니페스트에 반영.
        /// 실패는 파일 단위로 격리되며 어떤 실패에서도 원본 PNG 가 유지된다.
        /// </summary>
        internal static Dictionary<string, string> TranscodeInPlace(AITEditorScriptObject config, IReadOnlyList<string> absPaths)
        {
            var renamed = new Dictionary<string, string>();
            if (!IsEnabled(config) || absPaths == null || absPaths.Count == 0)
            {
                return renamed;
            }

            int quality = ResolveQuality(config);
            int considered = 0;
            long savedBytes = 0;
            foreach (var src in absPaths)
            {
                if (string.IsNullOrEmpty(src)
                    || !string.Equals(Path.GetExtension(src), ".png", StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(src))
                {
                    continue;
                }

                Texture2D tex = null;
                try
                {
                    byte[] pngBytes = File.ReadAllBytes(src);
                    if (!IsOpaquePng(pngBytes))
                    {
                        continue; // 알파 보유/비-RGB — 대상 아님(무경고).
                    }

                    considered++;

                    // LoadImage 는 임의 크기 재할당을 위해 임시 텍스처가 필요하다(차원 무관).
                    // linear=false: 픽셀 바이트 그대로 디코드→인코드하는 passthrough 라 색공간 변환 없음.
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    if (!tex.LoadImage(pngBytes, false))
                    {
                        Debug.LogWarning($"[AIT-JpegTranscode]   디코드 실패(원본 유지): {Path.GetFileName(src)}");
                        continue;
                    }

                    byte[] jpgBytes = tex.EncodeToJPG(quality);
                    if (jpgBytes == null || jpgBytes.Length == 0 || !ShouldAdopt(pngBytes.LongLength, jpgBytes.LongLength))
                    {
                        continue; // 인코드 실패 또는 이득 미달(<25%) — 원본 유지.
                    }

                    string dst = Path.ChangeExtension(src, ".jpg");
                    File.WriteAllBytes(dst, jpgBytes);
                    File.Delete(src);
                    renamed[src] = dst;
                    savedBytes += pngBytes.LongLength - jpgBytes.LongLength;
                    Debug.Log($"[AIT-JpegTranscode]   전환 {Path.GetFileName(src)} → .jpg (q{quality}): "
                        + $"{pngBytes.LongLength / 1048576f:0.00}→{jpgBytes.LongLength / 1048576f:0.00}MB");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-JpegTranscode]   전환 예외(원본 유지) {Path.GetFileName(src)}: {e.Message}");
                }
                finally
                {
                    if (tex != null)
                    {
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
            }

            if (considered > 0)
            {
                Debug.Log($"[AIT-JpegTranscode] ✓ 불투명 스트림 PNG {renamed.Count}/{considered}개 JPEG 전환(q{quality}), "
                    + $"{savedBytes / 1048576f:0.0}MB 절감");
            }

            return renamed;
        }
    }
}
