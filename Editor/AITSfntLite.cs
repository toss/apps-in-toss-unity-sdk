// -----------------------------------------------------------------------
// <copyright file="AITSfntLite.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - sfnt/cmap lightweight binary parser (build-time)
// </copyright>
// -----------------------------------------------------------------------
//
// 에디터 Font.HasCharacter 는 OS 폰트 폴백을 포함해 판정하므로, 소스 폰트에 실제로 없는 문자체계
// (예: NotoSansKR 소스에 없는 th)에도 true 를 반환하는 거짓 양성이 있다(AITFontLazyExtensionBuilder
// 의 HasAnyCoverage 커버리지 게이트가 이 문제로 빈 lazy 번들을 만든 사례 참조). 이 파일은 폰트 파일의
// sfnt 테이블 디렉토리와 cmap 테이블을 직접(바이너리 레벨로) 판독해 신뢰 가능한 커버리지 판정을
// 제공한다. 또한 subset-font-runner.mjs(harfbuzz wasm) 가 대규모 CFF 서브셋에서 외곽선 테이블을
// 조용히 드롭하는 버그의 산출물 검증(HasOutlineTable)에도 재사용된다.
//
// 모든 공개 API 는 잘리거나 쓰레기 입력에도 예외를 던지지 않고 false 를 반환한다(안전 방향 fallback).

using System;
using System.Collections.Generic;
using System.Text;

namespace AppsInToss.Editor
{
    /// <summary>
    /// sfnt(OpenType/TrueType) 컨테이너와 cmap 테이블을 직접 파싱하는 경량 유틸리티. 파일 IO 없음
    /// (호출부가 이미 읽은 바이트 배열을 넘긴다) — 순수 함수, 단위 테스트 대상.
    /// </summary>
    internal static class AITSfntLite
    {
        /// <summary>
        /// sfnt 테이블 디렉토리에서 <paramref name="tag"/> 테이블의 위치를 찾는다. 빅엔디안 파싱.
        /// 첫 4바이트가 'ttcf'(TTC 컬렉션)이면 첫 폰트의 오프셋(offset 12 의 u32)으로 이동한 뒤
        /// 그 지점을 sfnt 헤더로 다시 파싱한다. 모든 경로에서 배열 경계를 검사하며, 잘리거나 쓰레기
        /// 입력이면(헤더 미달/레코드 경계 초과/테이블 데이터 경계 초과) 예외 없이 false 를 반환한다.
        /// </summary>
        /// <param name="data">폰트 파일 전체 바이트.</param>
        /// <param name="tag">4문자 sfnt 테이블 태그(예: "glyf", "CFF ", "cmap"). 짧은 태그는 공백 패딩 필요.</param>
        /// <param name="offset">찾으면 data 시작 기준 절대 오프셋(바이트).</param>
        /// <param name="length">찾으면 테이블 길이(바이트).</param>
        /// <returns>테이블을 찾았고 그 범위가 data 경계 안이면 true.</returns>
        internal static bool TryGetTable(byte[] data, string tag, out int offset, out int length)
        {
            offset = 0;
            length = 0;

            try
            {
                if (data == null || string.IsNullOrEmpty(tag) || tag.Length != 4)
                {
                    return false;
                }

                int baseOffset = 0;
                if (data.Length >= 16 && data[0] == (byte)'t' && data[1] == (byte)'t' && data[2] == (byte)'c' && data[3] == (byte)'f')
                {
                    if (!TryReadU32(data, 12, out uint ttcFontOffset) || ttcFontOffset > int.MaxValue)
                    {
                        return false;
                    }

                    baseOffset = (int)ttcFontOffset;
                }

                if (!TryReadU16(data, baseOffset + 4, out ushort numTables))
                {
                    return false;
                }

                byte[] tagBytes = Encoding.ASCII.GetBytes(tag);

                for (int i = 0; i < numTables; i++)
                {
                    long recPosLong = (long)baseOffset + 12 + (long)i * 16;
                    if (recPosLong < 0 || recPosLong + 16 > data.Length)
                    {
                        return false; // 레코드가 잘림 — 이 시점 이후는 신뢰 불가.
                    }

                    int recPos = (int)recPosLong;
                    bool tagMatches = data[recPos] == tagBytes[0]
                        && data[recPos + 1] == tagBytes[1]
                        && data[recPos + 2] == tagBytes[2]
                        && data[recPos + 3] == tagBytes[3];
                    if (!tagMatches)
                    {
                        continue;
                    }

                    if (!TryReadU32(data, recPos + 8, out uint tblOffset) || !TryReadU32(data, recPos + 12, out uint tblLength))
                    {
                        return false;
                    }

                    if (tblOffset > int.MaxValue || tblLength > int.MaxValue)
                    {
                        return false;
                    }

                    long end = (long)tblOffset + tblLength;
                    if (end > data.Length)
                    {
                        return false; // 테이블 데이터 자체가 경계를 벗어남 — 잘린/쓰레기 입력.
                    }

                    offset = (int)tblOffset;
                    length = (int)tblLength;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>glyf(TrueType) 또는 CFF/CFF2(OpenType CFF) 중 하나라도 있으면 외곽선 보유로 판정.
        /// subset-font-runner.mjs 의 대규모 CFF 서브셋 조용한 드롭 방어(S1)와 동일 기준을 C# 쪽에서도
        /// 재사용하기 위한 것 — boot subset/lazy 확장 산출물 검증에 쓰인다.</summary>
        internal static bool HasOutlineTable(byte[] data)
        {
            try
            {
                return TryGetTable(data, "glyf", out _, out _)
                    || TryGetTable(data, "CFF ", out _, out _)
                    || TryGetTable(data, "CFF2", out _, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 폰트의 cmap 테이블에서 유니코드 서브테이블을 골라 <paramref name="codepoints"/> 중 하나라도
        /// 매핑 구간(세그먼트)에 포함되는지 확인한다. 서브테이블 선택 우선순위: (platformID 3,
        /// encodingID 10) 또는 (0, 4/6) 의 format 12(SequentialMapGroup) 우선, 없으면 (3,1) 또는
        /// (0,3) 의 format 4(세그먼트 배열). glyphID 0(.notdef) 해석까지는 하지 않는다 — 세그먼트
        /// 포함 여부만으로 커버리지 판정에 충분하다(format 4 의 0xFFFF-0xFFFF 종단 세그먼트만 제외하면
        /// 실질적인 거짓 양성이 없다).
        /// </summary>
        internal static bool CmapCoversAny(byte[] data, IEnumerable<int> codepoints)
        {
            try
            {
                if (data == null || codepoints == null)
                {
                    return false;
                }

                var cps = new List<int>(codepoints);
                if (cps.Count == 0)
                {
                    return false;
                }

                if (!TryGetTable(data, "cmap", out int cmapOffset, out int cmapLength))
                {
                    return false;
                }

                if (!TryFindUnicodeSubtable(data, cmapOffset, cmapLength, out int subtableOffset, out int format))
                {
                    return false;
                }

                return format == 12
                    ? Format12CoversAny(data, subtableOffset, cps)
                    : Format4CoversAny(data, subtableOffset, cps);
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────── cmap 서브테이블 선택 ───────────────────────────

        private static bool TryFindUnicodeSubtable(byte[] data, int cmapOffset, int cmapLength, out int subtableOffset, out int format)
        {
            subtableOffset = 0;
            format = 0;

            if (!TryReadU16(data, cmapOffset + 2, out ushort numTables))
            {
                return false;
            }

            int best12 = -1;
            int best4 = -1;

            for (int i = 0; i < numTables; i++)
            {
                long recPosLong = (long)cmapOffset + 4 + (long)i * 8;
                if (recPosLong < 0 || recPosLong + 8 > data.Length)
                {
                    break; // 레코드 테이블이 잘림 — 지금까지 찾은 결과로 진행.
                }

                int recPos = (int)recPosLong;
                if (!TryReadU16(data, recPos, out ushort platformId) || !TryReadU16(data, recPos + 2, out ushort encodingId)
                    || !TryReadU32(data, recPos + 4, out uint subOffsetRaw))
                {
                    break;
                }

                long subAbsLong = (long)cmapOffset + subOffsetRaw;
                if (subAbsLong < 0 || subAbsLong + 2 > data.Length)
                {
                    continue; // 이 서브테이블만 무효 — 나머지 레코드는 계속 확인.
                }

                int subAbs = (int)subAbsLong;
                if (!TryReadU16(data, subAbs, out ushort subFormat))
                {
                    continue;
                }

                bool isPreferred12Slot = (platformId == 3 && encodingId == 10)
                    || (platformId == 0 && (encodingId == 4 || encodingId == 6));
                bool isPreferred4Slot = (platformId == 3 && encodingId == 1)
                    || (platformId == 0 && encodingId == 3);

                if (isPreferred12Slot && subFormat == 12 && best12 < 0)
                {
                    best12 = subAbs;
                }
                else if (isPreferred4Slot && subFormat == 4 && best4 < 0)
                {
                    best4 = subAbs;
                }
            }

            if (best12 >= 0)
            {
                subtableOffset = best12;
                format = 12;
                return true;
            }

            if (best4 >= 0)
            {
                subtableOffset = best4;
                format = 4;
                return true;
            }

            return false;
        }

        // ─────────────────────────── format 12: SequentialMapGroup[] ───────────────────────────

        /// <summary>헤더: format u16(0) + reserved u16(2) + length u32(4) + language u32(8) +
        /// numGroups u32(12). 그룹은 offset 16 부터 12바이트씩(startCharCode/endCharCode/startGlyphID
        /// 각 u32).</summary>
        private static bool Format12CoversAny(byte[] data, int subOffset, List<int> codepoints)
        {
            if (!TryReadU32(data, subOffset + 12, out uint numGroups))
            {
                return false;
            }

            int groupsStart = subOffset + 16;
            for (uint g = 0; g < numGroups; g++)
            {
                long recPosLong = (long)groupsStart + (long)g * 12;
                if (recPosLong < 0 || recPosLong + 12 > data.Length)
                {
                    return false; // 그룹 배열이 잘림.
                }

                int recPos = (int)recPosLong;
                if (!TryReadU32(data, recPos, out uint startCharCode) || !TryReadU32(data, recPos + 4, out uint endCharCode))
                {
                    return false;
                }

                foreach (var cp in codepoints)
                {
                    if (cp >= 0 && (uint)cp >= startCharCode && (uint)cp <= endCharCode)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ─────────────────────────── format 4: 세그먼트 배열 ───────────────────────────

        /// <summary>헤더: format u16(0) + length u16(2) + language u16(4) + segCountX2 u16(6) +
        /// searchRange/entrySelector/rangeShift 각 u16(8/10/12). endCode[segCount] 은 offset 14 부터,
        /// reservedPad u16 다음 startCode[segCount] 가 이어진다(idDelta/idRangeOffset 은 커버리지
        /// 판정에 불필요해 읽지 않는다). 0xFFFF-0xFFFF 종단 세그먼트는 제외.</summary>
        private static bool Format4CoversAny(byte[] data, int subOffset, List<int> codepoints)
        {
            if (!TryReadU16(data, subOffset + 6, out ushort segCountX2))
            {
                return false;
            }

            int segCount = segCountX2 / 2;
            int endCodeArrayStart = subOffset + 14;
            int startCodeArrayStart = endCodeArrayStart + segCountX2 + 2; // +2: reservedPad(u16).

            for (int s = 0; s < segCount; s++)
            {
                if (!TryReadU16(data, endCodeArrayStart + s * 2, out ushort endCode)
                    || !TryReadU16(data, startCodeArrayStart + s * 2, out ushort startCode))
                {
                    return false; // 배열이 잘림.
                }

                if (startCode == 0xFFFF && endCode == 0xFFFF)
                {
                    continue; // format 4 필수 종단 세그먼트 — 실제 매핑 아님.
                }

                foreach (var cp in codepoints)
                {
                    if (cp >= startCode && cp <= endCode)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ─────────────────────────── 빅엔디안 원시 읽기(경계 검사 포함) ───────────────────────────

        private static bool TryReadU16(byte[] data, int offset, out ushort value)
        {
            value = 0;
            if (data == null || offset < 0 || (long)offset + 2 > data.Length)
            {
                return false;
            }

            value = (ushort)((data[offset] << 8) | data[offset + 1]);
            return true;
        }

        private static bool TryReadU32(byte[] data, int offset, out uint value)
        {
            value = 0;
            if (data == null || offset < 0 || (long)offset + 4 > data.Length)
            {
                return false;
            }

            value = ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
            return true;
        }
    }
}
