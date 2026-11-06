// -----------------------------------------------------------------------
// AITTextureStreamJpegTranscoder.cs - 불투명 스트리밍 텍스처 PNG 사본의 빌드타임 JPEG 전환
//
// 대상: AITLargeTextureExternalizer 가 StreamingAssets 에 만든 PNG 사본(프로젝트 원본 아님) 중
//   '완전 불투명' 파일 — (a) 헤더 빠른 경로: IHDR colortype==2(RGB) & tRNS 없음, 또는
//   (b) colortype==6(RGBA)를 디코드해 알파 전량 255 로 확인된 파일(특히 다운스케일 3-1 이
//   Unity EncodeToPNG 로 재인코딩한 RGBA 산출물이 여기 해당 — 헤더만으론 전부 놓친다).
//   gray(0)/palette(3)/grayA(4)는 마스크·플랫 아트 ringing 위험으로 계속 제외한다.
//   다운스케일(3-1) '이후', 무손실 재압축(3-1.5) '이전'에 실행 — JPEG 로 전환된 파일은
//   oxipng 대상에서 자연 제외된다.
//
// lossy: JPEG DCT 는 픽셀을 바꾼다(사진류 near-transparent, 플랫 아트는 ringing 위험) —
//   auto(-1)는 ON(GetDefaultTextureStreamJpeg() == true) — 위 '완전 불투명' 게이트가 안전장치이고,
//   textureStreamJpeg=0 으로 끈다.
//
// 자동(-1) 한정 품질 안전장치: 임포터 textureType!=Default / Point 필터 / sRGB=false / 데이터맵 이름 힌트 제외,
//   인코드 후 되디코드 휘도 PSNR<32dB 제외. 명시(=1)는 기존 동작(로그만).
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
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 불투명(RGB, 또는 알파 전량 255 인 RGBA) 스트리밍 텍스처 PNG 사본의 빌드타임 JPEG 전환기.
    /// <see cref="AITLargeTextureExternalizer"/> 가 다운스케일 직후, 무손실 재압축 전에 호출한다.
    /// </summary>
    internal static class AITTextureStreamJpegTranscoder
    {
        /// <summary>채택 최소 이득(%). lossy 전환이라 경계 이득에서는 원본 PNG 를 유지한다.</summary>
        internal const int MinGainPercent = 25;

        /// <summary>자동 모드 측정 품질 게이트: 휘도 PSNR 이 이 값(dB) 미만이면 원본 PNG 유지.</summary>
        internal const double MinLumaPsnrDb = 32.0;

        /// <summary>PSNR 계산 픽셀 간격(속도용 서브샘플링).</summary>
        internal const int PsnrPixelStride = 4;

        private const int DefaultQuality = 90;
        private const int MinQuality = 50;
        private const int MaxQuality = 100;

        // ─────────────────────────── 판정 (순수 함수, Level 0 테스트 대상) ───────────────────────────

        /// <summary>tri-state 해석. auto(-1)는 ON(불투명 텍스처 한정은 호출 경로의 알파 판정이 보장), 0 이면 끔.</summary>
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

        /// <summary>
        /// PNG 헤더가 colortype==6(RGBA truecolor+alpha)인지 판정한다. IsOpaquePng 의 헤더 빠른 경로가
        /// 놓치는 '알파 채널은 있으나 실제로는 전량 불투명일 수 있는' 파일 — 특히 다운스케일(3-1)이
        /// Unity EncodeToPNG 로 재인코딩한 RGBA 산출물 — 만 실 알파 스캔 대상으로 좁히기 위한 게이트다.
        /// gray(0)/palette(3)/grayA(4)는 마스크·플랫 아트 ringing 위험으로 스캔 대상에서 제외한다.
        /// (colortype 6 은 전체 알파 채널을 가지므로 tRNS 를 쓰지 않는다 — 투명 여부는 알파 스캔이 판정.)
        /// </summary>
        internal static bool IsRgbaPng(byte[] bytes)
        {
            // 시그니처(8) + IHDR 청크 헤더(8) + IHDR 데이터(colortype 은 인덱스 25).
            if (bytes == null || bytes.Length < 34)
            {
                return false;
            }

            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47
                || bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
            {
                return false;
            }

            return bytes[25] == 6; // colortype 6 = RGBA.
        }

        /// <summary>
        /// 디코드된 RGBA32 픽셀의 알파가 전부 255(완전 불투명)인지 스캔한다. 하나라도 255 미만이면
        /// 즉시 false(early-out). 헤더가 알파 채널 보유(colortype 6)로 표시하지만 실제 알파는 전부
        /// 255 인 파일 — 특히 다운스케일(3-1)이 EncodeToPNG 로 재인코딩해 RGBA 가 된 불투명 산출물 —
        /// 을 JPEG 전환 대상에 되살리기 위한 판정. 비용은 O(픽셀 수) 선형 스캔이며 전환 기능
        /// 활성(textureStreamJpeg==1) 시에만 RGBA 후보당 1회(GetPixels32 사본 1개 + 조기 종료 스캔).
        /// </summary>
        internal static bool IsFullyOpaque(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 255)
                {
                    return false;
                }
            }

            return true;
        }

        // ─────────────────────────── 자동 모드 품질 안전장치 (순수 함수) ───────────────────────────

        private static readonly HashSet<string> DataMapSuffixTokens = new HashSet<string>
        {
            "n", "nrm", "nor", "normal", "normals", "normalmap", "mask", "rough", "roughness",
            "metal", "metallic", "ao", "height", "heightmap", "disp", "displacement", "orm", "mrao",
        };

        /// <summary>
        /// 파일명(확장자 무관)의 마지막 토큰(_ - 공백 . 구분, 대소문자 무시)이 데이터맵 접미사
        /// (_n/_normal/_nrm/_mask/_rough/_metal/_ao/_height/_disp/_orm/_mrao 등)면 true.
        /// 데이터맵은 색이 아니라 값이라 DCT 손실이 곧 셰이딩 오류가 된다.
        /// </summary>
        internal static bool HasDataMapNameHint(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath))
            {
                return false;
            }

            string name = Path.GetFileNameWithoutExtension(nameOrPath);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string[] tokens = name.ToLowerInvariant().Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                return false; // 접미사가 아니라 이름 자체("n", "mask")인 경우는 힌트로 보지 않는다.
            }

            return DataMapSuffixTokens.Contains(tokens[tokens.Length - 1]);
        }

        /// <summary>
        /// 자동(-1) 모드에서 임포터 설정/이름으로 JPEG 전환을 건너뛸 사유. 전환 가능이면 null.
        /// Default 외 textureType(Sprite/GUI/Cursor/Cookie/Lightmap/SingleChannel/NormalMap 등),
        /// Point 필터(픽셀아트), sRGB 끔(데이터맵), 데이터맵 이름 힌트가 대상.
        /// </summary>
        internal static string GetAutoSkipReason(TextureImporterType textureType, FilterMode filterMode, bool sRgb, string assetPathOrName)
        {
            if (textureType != TextureImporterType.Default)
            {
                return "textureType=" + textureType;
            }

            if (filterMode == FilterMode.Point)
            {
                return "filterMode=Point";
            }

            if (!sRgb)
            {
                return "sRGB=false";
            }

            if (HasDataMapNameHint(assetPathOrName))
            {
                return "데이터맵 이름 힌트";
            }

            return null;
        }

        /// <summary>RGBA 4바이트/픽셀 배열 변환(Color32[] → byte[]).</summary>
        internal static byte[] ToRgbaBytes(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }

            return bytes;
        }

        /// <summary>
        /// 두 RGBA(4바이트/픽셀) 배열의 휘도(Rec.601) PSNR(dB). stride 픽셀마다 샘플링.
        /// 동일하면 +Infinity, 길이 불일치/비어 있으면 NaN(판정 불가).
        /// </summary>
        internal static double ComputeLumaPsnr(byte[] srcRgba, byte[] decRgba, int stride = PsnrPixelStride)
        {
            if (srcRgba == null || decRgba == null || srcRgba.Length != decRgba.Length
                || srcRgba.Length < 4 || srcRgba.Length % 4 != 0)
            {
                return double.NaN;
            }

            if (stride < 1)
            {
                stride = 1;
            }

            double sumSq = 0;
            long n = 0;
            int pixelCount = srcRgba.Length / 4;
            for (int p = 0; p < pixelCount; p += stride)
            {
                int o = p * 4;
                double ys = 0.299 * srcRgba[o] + 0.587 * srcRgba[o + 1] + 0.114 * srcRgba[o + 2];
                double yd = 0.299 * decRgba[o] + 0.587 * decRgba[o + 1] + 0.114 * decRgba[o + 2];
                double d = ys - yd;
                sumSq += d * d;
                n++;
            }

            if (n == 0)
            {
                return double.NaN;
            }

            double mse = sumSq / n;
            return mse <= 0 ? double.PositiveInfinity : 10.0 * Math.Log10(255.0 * 255.0 / mse);
        }

        /// <summary>PSNR 게이트: NaN(판정 불가)은 보수적으로 불통과, 최소값 이상이면 통과.</summary>
        internal static bool PassesPsnrGate(double psnrDb)
        {
            return !double.IsNaN(psnrDb) && psnrDb >= MinLumaPsnrDb;
        }

        /// <summary>textureStreamJpeg 가 자동(-1)인지(자동일 때만 안전 게이트 적용, 명시 1 은 기존 동작).</summary>
        internal static bool IsAutoMode(AITEditorScriptObject config)
        {
            return config != null && config.textureStreamJpeg < 0;
        }

        // ─────────────────────────── 실행 ───────────────────────────

        private static string FormatPsnr(double psnr)
        {
            return double.IsNaN(psnr) ? "n/a" : (double.IsInfinity(psnr) ? "inf" : psnr.ToString("0.0"));
        }

        /// <summary>스트림 사본 경로(<guid>.png)에서 원본 에셋 임포터를 찾아 자동 제외 사유를 반환. 못 찾으면 보수적으로 사유 반환.</summary>
        private static string GetImporterSkipReason(string streamAbsPath)
        {
            string guid = Path.GetFileNameWithoutExtension(streamAbsPath);
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var ti = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null)
            {
                return "임포터 확인 불가";
            }

            return GetAutoSkipReason(ti.textureType, ti.filterMode, ti.sRGBTexture, assetPath);
        }

        /// <summary>JPEG 를 되디코드해 원본(tex) 대비 휘도 PSNR 을 잰다. 디코드 실패/차원 불일치는 NaN.</summary>
        private static double MeasureJpegPsnr(Texture2D src, byte[] jpgBytes)
        {
            Texture2D dec = null;
            try
            {
                dec = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!dec.LoadImage(jpgBytes, false) || dec.width != src.width || dec.height != src.height)
                {
                    return double.NaN;
                }

                return ComputeLumaPsnr(ToRgbaBytes(src.GetPixels32()), ToRgbaBytes(dec.GetPixels32()));
            }
            finally
            {
                if (dec != null)
                {
                    UnityEngine.Object.DestroyImmediate(dec);
                }
            }
        }

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
            bool auto = IsAutoMode(config);
            int considered = 0;
            int skippedSafety = 0;
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

                    // 불투명 판정 2단계(알파 손실 없는 전환만 허용):
                    //   (a) 헤더 빠른 경로 — IHDR colortype==2(RGB) & tRNS 없음이면 스캔 없이 불투명 확정.
                    //   (b) colortype==6(RGBA)는 디코드 후 알파를 스캔해 전부 255 면 채택. 주 대상인
                    //       다운스케일(3-1) 산출물은 EncodeToPNG 가 RGBA(6)로 재인코딩하므로 헤더만으론
                    //       전부 놓친다 — 실 알파 스캔이 이 케이스를 되살린다.
                    //   gray(0)/palette(3)/grayA(4)는 (a)(b) 어디에도 안 걸려 계속 제외된다(ringing 보수).
                    bool headerOpaque = IsOpaquePng(pngBytes);
                    bool rgbaCandidate = !headerOpaque && IsRgbaPng(pngBytes);
                    if (!headerOpaque && !rgbaCandidate)
                    {
                        continue; // RGB 불투명도, RGBA 후보도 아님 — 대상 아님(무경고).
                    }

                    string fileName = Path.GetFileName(src);

                    // 자동 모드 임포터/이름 게이트(명시 1 은 건너뛰지 않고 사유만 로그). 스트림 사본 파일명 = <guid>.png.
                    string skipReason = GetImporterSkipReason(src);
                    if (skipReason != null)
                    {
                        if (auto)
                        {
                            skippedSafety++;
                            Debug.Log($"[AIT-JpegTranscode]   자동 제외({skipReason}): {fileName}");
                            continue;
                        }

                        Debug.Log($"[AIT-JpegTranscode]   (명시 활성이라 진행, 권장하지 않음: {skipReason}) {fileName}");
                    }

                    // LoadImage 는 임의 크기 재할당을 위해 임시 텍스처가 필요하다(차원 무관).
                    // linear=false: 픽셀 바이트 그대로 디코드→인코드하는 passthrough 라 색공간 변환 없음.
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    if (!tex.LoadImage(pngBytes, false))
                    {
                        // 헤더상 불투명(RGB) 후보만 실패를 경고 — RGBA 후보는 스캔 전까지 대상 미확정이라 무경고.
                        if (headerOpaque)
                        {
                            Debug.LogWarning($"[AIT-JpegTranscode]   디코드 실패(원본 유지): {Path.GetFileName(src)}");
                        }

                        continue;
                    }

                    // RGBA 후보는 실제 알파가 전량 255 일 때만 채택(하나라도 투명하면 JPEG 전환 시 손실).
                    if (rgbaCandidate && !IsFullyOpaque(tex.GetPixels32()))
                    {
                        continue; // 실제 알파 보유 — 대상 아님(무경고).
                    }

                    considered++;

                    byte[] jpgBytes = tex.EncodeToJPG(quality);
                    if (jpgBytes == null || jpgBytes.Length == 0 || !ShouldAdopt(pngBytes.LongLength, jpgBytes.LongLength))
                    {
                        continue; // 인코드 실패 또는 이득 미달(<25%) — 원본 유지.
                    }

                    // 측정 품질 게이트: JPEG 를 되디코드해 원본 픽셀과 휘도 PSNR 비교(4픽셀 간격 샘플).
                    // 자동 모드는 <32dB 면 원본 유지, 명시 활성은 로그만.
                    double psnr = MeasureJpegPsnr(tex, jpgBytes);
                    if (auto && !PassesPsnrGate(psnr))
                    {
                        skippedSafety++;
                        Debug.Log($"[AIT-JpegTranscode]   자동 제외(PSNR {FormatPsnr(psnr)}dB < {MinLumaPsnrDb:0}dB, 원본 유지): {fileName}");
                        continue;
                    }

                    string dst = Path.ChangeExtension(src, ".jpg");
                    File.WriteAllBytes(dst, jpgBytes);
                    File.Delete(src);
                    renamed[src] = dst;
                    savedBytes += pngBytes.LongLength - jpgBytes.LongLength;
                    Debug.Log($"[AIT-JpegTranscode]   전환 {Path.GetFileName(src)} → .jpg (q{quality}): "
                        + $"{pngBytes.LongLength / 1048576f:0.00}→{jpgBytes.LongLength / 1048576f:0.00}MB, PSNR {FormatPsnr(psnr)}dB");
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

            if (considered > 0 || skippedSafety > 0)
            {
                Debug.Log($"[AIT-JpegTranscode] ✓ 불투명 스트림 PNG {renamed.Count}/{considered}개 JPEG 전환(q{quality}), "
                    + $"{savedBytes / 1048576f:0.0}MB 절감" + (skippedSafety > 0 ? $", 품질 안전장치 제외 {skippedSafety}개" : string.Empty));
            }

            return renamed;
        }
    }
}
