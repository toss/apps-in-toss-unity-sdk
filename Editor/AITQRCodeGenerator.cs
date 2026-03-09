// Minimal QR Code generator for Unity Editor
// Generates QR codes locally without external API calls.
// Supports byte-mode encoding for URLs (Version 1-10, Error Correction Level L).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppsInToss.Editor
{
    internal static class AITQRCodeGenerator
    {
        /// <summary>
        /// URL 문자열로부터 QR 코드 Texture2D를 생성합니다.
        /// 외부 서비스를 사용하지 않고 로컬에서 생성합니다.
        /// </summary>
        /// <param name="text">인코딩할 텍스트 (URL 등)</param>
        /// <param name="pixelSize">출력 텍스처의 픽셀 크기 (정사각형)</param>
        /// <returns>QR 코드 Texture2D. 인코딩 실패 시 null</returns>
        public static Texture2D Generate(string text, int pixelSize = 200)
        {
            if (string.IsNullOrEmpty(text)) return null;

            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
            int version = GetMinVersion(data.Length);
            if (version < 0) return null; // 너무 긴 데이터

            int size = 17 + version * 4;
            var modules = new bool[size, size];
            var isFunction = new bool[size, size];

            // 기능 패턴 배치
            PlaceFinderPatterns(modules, isFunction, size);
            PlaceAlignmentPatterns(modules, isFunction, size, version);
            PlaceTimingPatterns(modules, isFunction, size);
            PlaceDarkModule(modules, isFunction, version);
            ReserveFormatBits(isFunction, size);
            if (version >= 7) ReserveVersionBits(isFunction, size);

            // 데이터 인코딩
            var encoded = EncodeData(data, version);
            PlaceDataBits(modules, isFunction, size, encoded);

            // 마스크 적용 (마스크 0 고정 — 최적 마스크 선택은 생략, 대부분의 스캐너에서 정상 동작)
            int mask = 0;
            ApplyMask(modules, isFunction, size, mask);

            // 포맷 정보 배치
            PlaceFormatBits(modules, size, mask);
            if (version >= 7) PlaceVersionBits(modules, size, version);

            // 텍스처 생성
            return RenderTexture(modules, size, pixelSize);
        }

        #region Version / Capacity

        // EC Level L, Byte mode 용량 (version 1-10)
        private static readonly int[] ByteCapacityL = { 0, 17, 32, 53, 78, 106, 134, 154, 192, 230, 271 };

        private static int GetMinVersion(int dataLength)
        {
            for (int v = 1; v < ByteCapacityL.Length; v++)
            {
                if (dataLength <= ByteCapacityL[v]) return v;
            }
            return -1;
        }

        #endregion

        #region Finder Patterns

        private static void PlaceFinderPatterns(bool[,] m, bool[,] f, int size)
        {
            PlaceFinderPattern(m, f, 0, 0);
            PlaceFinderPattern(m, f, size - 7, 0);
            PlaceFinderPattern(m, f, 0, size - 7);
        }

        private static void PlaceFinderPattern(bool[,] m, bool[,] f, int row, int col)
        {
            int size = m.GetLength(0);
            for (int r = -1; r <= 7; r++)
            {
                for (int c = -1; c <= 7; c++)
                {
                    int rr = row + r, cc = col + c;
                    if (rr < 0 || rr >= size || cc < 0 || cc >= size) continue;
                    bool dark = (r >= 0 && r <= 6 && (c == 0 || c == 6)) ||
                                (c >= 0 && c <= 6 && (r == 0 || r == 6)) ||
                                (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                    m[rr, cc] = dark;
                    f[rr, cc] = true;
                }
            }
        }

        #endregion

        #region Alignment Patterns

        private static readonly int[][] AlignmentPositions =
        {
            null,               // v0 placeholder
            null,               // v1 - 없음
            new[] { 6, 18 },    // v2
            new[] { 6, 22 },
            new[] { 6, 26 },
            new[] { 6, 30 },
            new[] { 6, 34 },    // v6
            new[] { 6, 22, 38 },
            new[] { 6, 24, 42 },
            new[] { 6, 26, 46 },
            new[] { 6, 28, 50 },// v10
        };

        private static void PlaceAlignmentPatterns(bool[,] m, bool[,] f, int size, int version)
        {
            if (version < 2 || version >= AlignmentPositions.Length) return;
            var positions = AlignmentPositions[version];
            if (positions == null) return;

            foreach (int row in positions)
            {
                foreach (int col in positions)
                {
                    // 파인더 패턴과 겹치면 스킵
                    if (f[row, col]) continue;

                    for (int r = -2; r <= 2; r++)
                    {
                        for (int c = -2; c <= 2; c++)
                        {
                            int rr = row + r, cc = col + c;
                            bool dark = Math.Abs(r) == 2 || Math.Abs(c) == 2 || (r == 0 && c == 0);
                            m[rr, cc] = dark;
                            f[rr, cc] = true;
                        }
                    }
                }
            }
        }

        #endregion

        #region Timing Patterns

        private static void PlaceTimingPatterns(bool[,] m, bool[,] f, int size)
        {
            for (int i = 8; i < size - 8; i++)
            {
                if (!f[6, i]) { m[6, i] = i % 2 == 0; f[6, i] = true; }
                if (!f[i, 6]) { m[i, 6] = i % 2 == 0; f[i, 6] = true; }
            }
        }

        #endregion

        #region Dark Module & Reserved Areas

        private static void PlaceDarkModule(bool[,] m, bool[,] f, int version)
        {
            int row = 4 * version + 9;
            m[row, 8] = true;
            f[row, 8] = true;
        }

        private static void ReserveFormatBits(bool[,] f, int size)
        {
            for (int i = 0; i <= 8; i++)
            {
                if (!f[8, i]) f[8, i] = true;
                if (!f[i, 8]) f[i, 8] = true;
            }
            for (int i = 0; i < 8; i++)
            {
                f[8, size - 1 - i] = true;
                f[size - 1 - i, 8] = true;
            }
        }

        private static void ReserveVersionBits(bool[,] f, int size)
        {
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    f[i, size - 11 + j] = true;
                    f[size - 11 + j, i] = true;
                }
            }
        }

        #endregion

        #region Data Encoding (Byte Mode, EC Level L)

        // Total codewords per version (Level L)
        private static readonly int[] TotalCodewords = { 0, 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };
        // EC codewords per block (Level L)
        private static readonly int[] ECCodewordsPerBlock = { 0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18 };
        // Number of EC blocks (Level L)
        private static readonly int[] NumECBlocks = { 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4 };

        private static List<bool> EncodeData(byte[] data, int version)
        {
            var bits = new List<bool>();

            // Mode indicator (0100 = Byte)
            AddBits(bits, 0b0100, 4);

            // Character count indicator
            int ccBits = version <= 9 ? 8 : 16;
            AddBits(bits, data.Length, ccBits);

            // Data
            foreach (byte b in data)
                AddBits(bits, b, 8);

            // Terminator
            int totalBits = TotalCodewords[version] * 8;
            int ecBits = ECCodewordsPerBlock[version] * NumECBlocks[version] * 8;
            int dataBits = totalBits - ecBits;
            int terminatorLen = Math.Min(4, dataBits - bits.Count);
            for (int i = 0; i < terminatorLen; i++) bits.Add(false);

            // Pad to byte boundary
            while (bits.Count % 8 != 0) bits.Add(false);

            // Pad codewords
            byte[] padBytes = { 0xEC, 0x11 };
            int padIdx = 0;
            while (bits.Count < dataBits)
            {
                AddBits(bits, padBytes[padIdx % 2], 8);
                padIdx++;
            }

            // Convert to codewords
            int dataCodewordCount = dataBits / 8;
            byte[] dataCodewords = new byte[dataCodewordCount];
            for (int i = 0; i < dataCodewordCount; i++)
            {
                int val = 0;
                for (int j = 0; j < 8; j++)
                    val = (val << 1) | (bits[i * 8 + j] ? 1 : 0);
                dataCodewords[i] = (byte)val;
            }

            // EC 생성
            int numBlocks = NumECBlocks[version];
            int ecPerBlock = ECCodewordsPerBlock[version];
            int dataPerBlock = dataCodewordCount / numBlocks;
            int shortBlocks = numBlocks - (dataCodewordCount % numBlocks);

            var dataBlocks = new List<byte[]>();
            var ecBlocks = new List<byte[]>();
            int offset = 0;

            for (int b = 0; b < numBlocks; b++)
            {
                int blockLen = dataPerBlock + (b >= shortBlocks ? 1 : 0);
                byte[] block = new byte[blockLen];
                Array.Copy(dataCodewords, offset, block, 0, blockLen);
                offset += blockLen;
                dataBlocks.Add(block);
                ecBlocks.Add(ReedSolomonEncode(block, ecPerBlock));
            }

            // 인터리빙
            var result = new List<bool>();
            int maxDataLen = dataPerBlock + 1;
            for (int i = 0; i < maxDataLen; i++)
            {
                foreach (var block in dataBlocks)
                {
                    if (i < block.Length)
                        AddBits(result, block[i], 8);
                }
            }
            for (int i = 0; i < ecPerBlock; i++)
            {
                foreach (var block in ecBlocks)
                {
                    AddBits(result, block[i], 8);
                }
            }

            return result;
        }

        private static void AddBits(List<bool> bits, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
                bits.Add(((value >> i) & 1) == 1);
        }

        #endregion

        #region Reed-Solomon

        private static byte[] ReedSolomonEncode(byte[] data, int ecLen)
        {
            int[] gen = GetGeneratorPolynomial(ecLen);
            // gen has ecLen+1 coefficients, gen[0] is always 1
            int[] remainder = new int[ecLen];

            foreach (byte b in data)
            {
                int lead = b ^ remainder[0];
                for (int i = 0; i < ecLen - 1; i++)
                    remainder[i] = remainder[i + 1] ^ GfMul(gen[i + 1], lead);
                remainder[ecLen - 1] = GfMul(gen[ecLen], lead);
            }

            byte[] output = new byte[ecLen];
            for (int i = 0; i < ecLen; i++)
                output[i] = (byte)remainder[i];
            return output;
        }

        private static int[] GetGeneratorPolynomial(int degree)
        {
            int[] poly = { 1 };
            for (int i = 0; i < degree; i++)
            {
                int[] newPoly = new int[poly.Length + 1];
                int factor = GfPow(2, i);
                for (int j = 0; j < poly.Length; j++)
                {
                    newPoly[j] ^= poly[j];
                    newPoly[j + 1] ^= GfMul(poly[j], factor);
                }
                poly = newPoly;
            }
            return poly;
        }

        // GF(2^8) with polynomial 0x11D
        private static readonly int[] GfExp = new int[512];
        private static readonly int[] GfLog = new int[256];

        static AITQRCodeGenerator()
        {
            int val = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = val;
                GfLog[val] = i;
                val <<= 1;
                if (val >= 256) val ^= 0x11D;
            }
            for (int i = 255; i < 512; i++)
                GfExp[i] = GfExp[i - 255];
        }

        private static int GfMul(int a, int b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[GfLog[a] + GfLog[b]];
        }

        private static int GfPow(int b, int e)
        {
            return GfExp[(GfLog[b] * e) % 255];
        }

        #endregion

        #region Data Placement

        private static void PlaceDataBits(bool[,] m, bool[,] f, int size, List<bool> data)
        {
            int bitIdx = 0;
            // Right-to-left, bottom-to-top zigzag
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5; // 타이밍 패턴 열 스킵

                bool upward = ((size - 1 - right) / 2) % 2 == 0;

                for (int i = 0; i < size; i++)
                {
                    int row = upward ? (size - 1 - i) : i;

                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int col = right - dx;
                        if (col < 0 || f[row, col]) continue;
                        if (bitIdx < data.Count)
                            m[row, col] = data[bitIdx];
                        bitIdx++;
                    }
                }
            }
        }

        #endregion

        #region Masking

        private static void ApplyMask(bool[,] m, bool[,] f, int size, int mask)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (f[r, c]) continue;
                    bool invert = false;
                    switch (mask)
                    {
                        case 0: invert = (r + c) % 2 == 0; break;
                        case 1: invert = r % 2 == 0; break;
                        case 2: invert = c % 3 == 0; break;
                        case 3: invert = (r + c) % 3 == 0; break;
                        case 4: invert = (r / 2 + c / 3) % 2 == 0; break;
                        case 5: invert = (r * c) % 2 + (r * c) % 3 == 0; break;
                        case 6: invert = ((r * c) % 2 + (r * c) % 3) % 2 == 0; break;
                        case 7: invert = ((r + c) % 2 + (r * c) % 3) % 2 == 0; break;
                    }
                    if (invert) m[r, c] = !m[r, c];
                }
            }
        }

        #endregion

        #region Format & Version Info

        // Format bits for EC Level L (00), masks 0-7, BCH encoded
        private static readonly int[] FormatBitsL =
        {
            0x77C4, 0x72F3, 0x7DAA, 0x789D, 0x662F, 0x6318, 0x6C41, 0x6976,
        };

        private static void PlaceFormatBits(bool[,] m, int size, int mask)
        {
            int bits = FormatBitsL[mask];

            // Around top-left finder
            int[] rowPositions = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };
            int[] colPositions = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };

            for (int i = 0; i < 15; i++)
            {
                bool dark = ((bits >> (14 - i)) & 1) == 1;
                m[rowPositions[i], colPositions[i]] = dark;
            }

            // Around top-right and bottom-left finders
            for (int i = 0; i < 8; i++)
            {
                bool dark = ((bits >> (14 - i)) & 1) == 1;
                m[8, size - 1 - i] = dark;
            }
            for (int i = 8; i < 15; i++)
            {
                bool dark = ((bits >> (14 - i)) & 1) == 1;
                m[size - 15 + i, 8] = dark;
            }
        }

        // Version info bits (versions 7-10), BCH(18,6) encoded
        private static readonly int[] VersionInfoBits =
        {
            0, 0, 0, 0, 0, 0, 0,       // v0-v6: no version info
            0x07C94, 0x085BC, 0x09A99, 0x0A4D3,  // v7-v10
        };

        private static void PlaceVersionBits(bool[,] m, int size, int version)
        {
            if (version < 7 || version >= VersionInfoBits.Length) return;
            int bits = VersionInfoBits[version];

            for (int i = 0; i < 18; i++)
            {
                bool dark = ((bits >> i) & 1) == 1;
                int row = i / 3;
                int col = size - 11 + (i % 3);
                m[row, col] = dark;
                m[col, row] = dark;
            }
        }

        #endregion

        #region Rendering

        private static Texture2D RenderTexture(bool[,] modules, int qrSize, int pixelSize)
        {
            int quietZone = 4;
            int totalModules = qrSize + quietZone * 2;
            int scale = Math.Max(1, pixelSize / totalModules);
            int texSize = totalModules * scale;

            var tex = new Texture2D(texSize, texSize, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;

            var pixels = new Color32[texSize * texSize];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    int moduleX = x / scale - quietZone;
                    int moduleY = (texSize - 1 - y) / scale - quietZone;

                    bool isDark = moduleX >= 0 && moduleX < qrSize &&
                                  moduleY >= 0 && moduleY < qrSize &&
                                  modules[moduleY, moduleX];

                    pixels[y * texSize + x] = isDark ? black : white;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        #endregion
    }
}
