// -----------------------------------------------------------------------
// AITSfntLiteTests.cs - AITSfntLite(sfnt/cmap 경량 파서) 단위 테스트
// Level 0: 실폰트 픽스처(NotoSansKR) cmap 판독 커버리지 검증 + 합성 sfnt 바이트로 외곽선 테이블
// 판정/경계 검사(잘리거나 쓰레기 입력)를 검증한다.
//
// 배경: AITFontLazyExtensionBuilder.HasAnyCoverage 가 과거 UnityEngine.Font.HasCharacter 를 썼는데,
// 에디터의 HasCharacter 는 OS 폰트 폴백을 포함해 판정해 소스 폰트에 실제로 없는 문자체계(예:
// NotoSansKR 소스에 없는 th)에도 true 를 반환하는 거짓 양성이 있었다. AITSfntLite.CmapCoversAny 는
// 폰트 파일의 cmap 테이블을 직접 판독해 이 거짓 양성을 없앤다. 아래 기대값은 fonttools 로 사전
// 확정된 사실이다: NotoSansKR cmap 에 ko/ja(한자 공유)는 매핑이 있고 th/ar 매핑은 전혀 없다.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITSfntLiteTests
{
    private static byte[] s_notoSansKrBytes;
    private static bool s_fixtureLoadAttempted;

    private static byte[] NotoSansKrBytes
    {
        get
        {
            if (!s_fixtureLoadAttempted)
            {
                s_fixtureLoadAttempted = true;
                string path = ResolveNotoSansKrPath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    s_notoSansKrBytes = File.ReadAllBytes(path);
                }
            }

            return s_notoSansKrBytes;
        }
    }

    /// <summary>
    /// NotoSansKR-Regular.otf 픽스처 경로를 해석한다. 1순위: Packages/im.toss.sdk-test-scripts(UPM
    /// 가상 경로, 이 패키지가 등록된 EditMode 실행 컨텍스트에서 접근 가능). 실행 컨텍스트에 따라
    /// "Packages/&lt;name&gt;" 가 실제 파일시스템 경로로 해석되지 않을 수 있어(로컬 file: 참조 패키지가
    /// 실제 Packages/&lt;name&gt; 물리 폴더 없이 매니페스트로만 매핑되는 경우), 2순위로 이 테스트 파일
    /// 자신의 물리적 위치(Tests~/E2E/SharedScripts/Editor/EditModeTests/) 기준 상대 경로로 폴백한다
    /// (SharedScripts 루트 바로 아래 비임포트 Runtime/Fonts~/ — AITFontSubsetProcessor.CallerDir 와
    /// 동일한 CallerFilePath 관용구). 폰트는 "패키지 Runtime/Resources/ 는 무조건 빌드 포함" 규칙을
    /// 피하기 위해 Runtime/Fonts~/(비임포트, "~" 접미)에 있다 — Resources/ 가 아니다.
    /// </summary>
    private static string ResolveNotoSansKrPath()
    {
        const string relativeFromFontDir = "NotoSansKR-Regular.otf";

        string viaPackages = Path.GetFullPath(
            "Packages/im.toss.sdk-test-scripts/Runtime/Fonts~/" + relativeFromFontDir);
        if (File.Exists(viaPackages))
        {
            return viaPackages;
        }

        string thisFileDir = Path.GetDirectoryName(CallerFilePath());
        if (string.IsNullOrEmpty(thisFileDir))
        {
            return null;
        }

        // Editor/EditModeTests/ → (상위 2단계) → SharedScripts/ → Runtime/Fonts~/
        string viaSharedScriptsRelative = Path.GetFullPath(Path.Combine(
            thisFileDir, "..", "..", "Runtime", "Fonts~", relativeFromFontDir));
        return File.Exists(viaSharedScriptsRelative) ? viaSharedScriptsRelative : null;
    }

    private static string CallerFilePath([CallerFilePath] string path = "") => path;

    private static void RequireRealFontFixture()
    {
        if (NotoSansKrBytes == null)
        {
            Assert.Inconclusive(
                "NotoSansKR-Regular.otf 픽스처를 찾지 못했습니다(Packages/im.toss.sdk-test-scripts 및 " +
                "SharedScripts 상대 경로 폴백 모두 실패) — 이 실행 컨텍스트에서는 검증을 건너뜁니다.");
        }
    }

    // ============================================================
    // 실폰트 픽스처(NotoSansKR): HasOutlineTable
    // ============================================================

    [Test]
    public void RealFont_HasOutlineTable_ReturnsTrue()
    {
        RequireRealFontFixture();
        Assert.IsTrue(AITSfntLite.HasOutlineTable(NotoSansKrBytes),
            "NotoSansKR(OTTO/CFF) 는 외곽선 테이블(CFF)을 가지고 있어야 함");
    }

    // ============================================================
    // 실폰트 픽스처(NotoSansKR): CmapCoversAny — 사전 확정된 커버리지(fonttools 기준)
    // ============================================================

    [Test]
    public void RealFont_CmapCoversAny_Korean_ReturnsTrue()
    {
        RequireRealFontFixture();
        Assert.IsTrue(AITSfntLite.CmapCoversAny(NotoSansKrBytes, new[] { 0xAC00 }),
            "'가'(U+AC00) 는 NotoSansKR cmap 에 매핑되어야 함");
    }

    [Test]
    public void RealFont_CmapCoversAny_Japanese_Hiragana_ReturnsTrue()
    {
        RequireRealFontFixture();
        Assert.IsTrue(AITSfntLite.CmapCoversAny(NotoSansKrBytes, new[] { 0x3042 }),
            "あ(U+3042, 히라가나) 는 NotoSansKR cmap 에 매핑되어야 함");
    }

    [Test]
    public void RealFont_CmapCoversAny_Japanese_Han_ReturnsTrue()
    {
        RequireRealFontFixture();
        Assert.IsTrue(AITSfntLite.CmapCoversAny(NotoSansKrBytes, new[] { 0x4E00 }),
            "一(U+4E00, 한자 — ko/ja 공유) 는 NotoSansKR cmap 에 매핑되어야 함");
    }

    [Test]
    public void RealFont_CmapCoversAny_Thai_ReturnsFalse()
    {
        RequireRealFontFixture();

        var thaiBlock = new List<int>();
        for (int cp = 0x0E01; cp <= 0x0E7F; cp++)
        {
            thaiBlock.Add(cp);
        }

        Assert.IsFalse(AITSfntLite.CmapCoversAny(NotoSansKrBytes, thaiBlock),
            "태국어 블록(U+0E01-0E7F) 전체는 NotoSansKR cmap 에 매핑이 전혀 없어야 함(B2 커버리지 " +
            "폴백이 실제로 걸러내야 하는 케이스)");
    }

    [Test]
    public void RealFont_CmapCoversAny_Arabic_ReturnsFalse()
    {
        RequireRealFontFixture();

        var arabicSample = new List<int>();
        for (int cp = 0x0600; cp <= 0x06FF; cp += 0x08)
        {
            arabicSample.Add(cp);
        }

        Assert.IsFalse(AITSfntLite.CmapCoversAny(NotoSansKrBytes, arabicSample),
            "아랍어 블록(U+0600-06FF) 샘플은 NotoSansKR cmap 에 매핑이 전혀 없어야 함");
    }

    // ============================================================
    // 합성 sfnt 픽스처: 외곽선 테이블 유무 판정
    // ============================================================

    [Test]
    public void SyntheticSfnt_CmapOnly_HasOutlineTable_ReturnsFalse()
    {
        byte[] data = BuildMinimalSfnt(("cmap", MinimalCmapFormat4Payload()));
        Assert.IsFalse(AITSfntLite.HasOutlineTable(data),
            "cmap 만 있고 glyf/CFF/CFF2 가 없는 합성 sfnt 는 외곽선 테이블이 없어야 함(harfbuzz " +
            "wasm 조용한 드롭 산출물 회귀 방지)");
    }

    [Test]
    public void SyntheticSfnt_WithGlyfTable_HasOutlineTable_ReturnsTrue()
    {
        byte[] data = BuildMinimalSfnt(("glyf", new byte[] { 0x00, 0x01, 0x02, 0x03 }));
        Assert.IsTrue(AITSfntLite.HasOutlineTable(data));
    }

    [Test]
    public void SyntheticSfnt_WithCffTable_HasOutlineTable_ReturnsTrue()
    {
        byte[] data = BuildMinimalSfnt(("CFF ", new byte[] { 0x01, 0x00, 0x04, 0x02 }));
        Assert.IsTrue(AITSfntLite.HasOutlineTable(data));
    }

    [Test]
    public void SyntheticSfnt_WithCff2Table_HasOutlineTable_ReturnsTrue()
    {
        byte[] data = BuildMinimalSfnt(("CFF2", new byte[] { 0x02, 0x00, 0x00, 0x00 }));
        Assert.IsTrue(AITSfntLite.HasOutlineTable(data));
    }

    // ============================================================
    // 경계 검사: 잘리거나 쓰레기 입력에서 예외 없이 false
    // ============================================================

    [Test]
    public void NullData_ReturnsFalse_WithoutThrowing()
    {
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.HasOutlineTable(null)));
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.CmapCoversAny(null, new[] { 0x41 })));
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.TryGetTable(null, "glyf", out _, out _)));
    }

    [Test]
    public void EmptyArray_ReturnsFalse_WithoutThrowing()
    {
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.HasOutlineTable(Array.Empty<byte>())));
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.CmapCoversAny(Array.Empty<byte>(), new[] { 0x41 })));
    }

    [Test]
    public void TruncatedBeforeHeader_ReturnsFalse_WithoutThrowing()
    {
        // sfnt 헤더(12바이트) 미달 — numTables 를 읽을 수조차 없어야 함.
        byte[] data = { 0x00, 0x01, 0x00, 0x00, 0x00 };
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.HasOutlineTable(data)));
    }

    [Test]
    public void TruncatedTableDirectory_ReturnsFalse_WithoutThrowing()
    {
        // 헤더는 정상(numTables=1) 이지만 레코드(16바이트)가 다 없는 경우.
        byte[] data = new byte[16];
        WriteU16(data, 4, 1); // numTables = 1
        Assert.DoesNotThrow(() => Assert.IsFalse(AITSfntLite.HasOutlineTable(data)));
    }

    [Test]
    public void TryGetTable_OffsetLengthBeyondData_ReturnsFalse()
    {
        // 태그는 일치하지만 테이블 데이터(offset+length)가 파일 크기를 초과 — 잘리거나 쓰레기 입력.
        byte[] data = BuildSfntHeaderWithOneRawRecord("glyf", tableOffset: 100, tableLength: 1000, totalSize: 200);
        Assert.IsFalse(AITSfntLite.TryGetTable(data, "glyf", out _, out _));
    }

    [Test]
    public void GarbageBytes_ReturnsFalse_WithoutThrowing()
    {
        var rnd = new Random(20260804);
        byte[] data = new byte[512];
        rnd.NextBytes(data);

        Assert.DoesNotThrow(() =>
        {
            AITSfntLite.HasOutlineTable(data);
            AITSfntLite.CmapCoversAny(data, new[] { 0x41, 0xAC00 });
        });
    }

    [Test]
    public void TryGetTable_InvalidTagLength_ReturnsFalse()
    {
        byte[] data = BuildMinimalSfnt(("glyf", new byte[] { 0x00 }));
        Assert.IsFalse(AITSfntLite.TryGetTable(data, "glyfx", out _, out _), "4문자가 아닌 태그는 false");
        Assert.IsFalse(AITSfntLite.TryGetTable(data, "", out _, out _));
        Assert.IsFalse(AITSfntLite.TryGetTable(data, null, out _, out _));
    }

    // ============================================================
    // 헬퍼: 합성 sfnt 바이트 조립
    // ============================================================

    private static void WriteU16(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)(value >> 8);
        buf[offset + 1] = (byte)value;
    }

    private static void WriteU32(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    /// <summary>지정한 (tag, payload) 테이블들로만 구성된 최소 sfnt 컨테이너를 조립한다. 체크섬은
    /// 검증 대상이 아니라 0 으로 고정한다.</summary>
    private static byte[] BuildMinimalSfnt(params (string tag, byte[] data)[] tables)
    {
        const int headerSize = 12;
        int dirSize = tables.Length * 16;
        int dataStart = headerSize + dirSize;

        int totalDataSize = 0;
        foreach (var t in tables)
        {
            totalDataSize += t.data.Length;
        }

        byte[] buf = new byte[dataStart + totalDataSize];
        WriteU32(buf, 0, 0x4F54544F); // 'OTTO' — 파서는 sfntVersion 값 자체를 검사하지 않음.
        WriteU16(buf, 4, (ushort)tables.Length);

        int cursor = dataStart;
        for (int i = 0; i < tables.Length; i++)
        {
            int recPos = headerSize + i * 16;
            byte[] tagBytes = System.Text.Encoding.ASCII.GetBytes(tables[i].tag);
            Array.Copy(tagBytes, 0, buf, recPos, 4);
            WriteU32(buf, recPos + 4, 0); // checkSum(미검증)
            WriteU32(buf, recPos + 8, (uint)cursor);
            WriteU32(buf, recPos + 12, (uint)tables[i].data.Length);

            Array.Copy(tables[i].data, 0, buf, cursor, tables[i].data.Length);
            cursor += tables[i].data.Length;
        }

        return buf;
    }

    /// <summary>테이블 디렉토리 레코드 1개만 있고 실제 테이블 데이터는 없는(경계 위반용) sfnt 바이트.</summary>
    private static byte[] BuildSfntHeaderWithOneRawRecord(string tag, uint tableOffset, uint tableLength, int totalSize)
    {
        byte[] buf = new byte[totalSize];
        WriteU32(buf, 0, 0x00010000);
        WriteU16(buf, 4, 1);

        byte[] tagBytes = System.Text.Encoding.ASCII.GetBytes(tag);
        Array.Copy(tagBytes, 0, buf, 12, 4);
        WriteU32(buf, 16, 0);
        WriteU32(buf, 20, tableOffset);
        WriteU32(buf, 24, tableLength);

        return buf;
    }

    /// <summary>format 4(세그먼트 배열) 서브테이블 1개(플랫폼 3/인코딩 1, BMP)만 있는 최소 cmap 페이로드.
    /// 세그먼트는 종단(0xFFFF-0xFFFF) 1개뿐 — 이 테스트 스위트에서는 cmap "존재" 자체만 필요하고 내용
    /// 커버리지는 검사하지 않으므로 최소 형태로 충분하다.</summary>
    private static byte[] MinimalCmapFormat4Payload()
    {
        // cmap 헤더: version u16 + numTables u16.
        // encoding record: platformID u16 + encodingID u16 + offset u32(서브테이블까지, cmap 시작 기준).
        // 서브테이블(format 4, 세그먼트 1개=종단만): format,length,language,segCountX2,searchRange,
        //   entrySelector,rangeShift(각 u16=14바이트) + endCode[1] + reservedPad + startCode[1] +
        //   idDelta[1] + idRangeOffset[1] (각 u16).
        const int cmapHeaderSize = 4;
        const int recordSize = 8;
        int subtableOffset = cmapHeaderSize + recordSize;
        int subtableSize = 14 + 2 + 2 + 2 + 2 + 2; // endCode + reservedPad + startCode + idDelta + idRangeOffset

        byte[] buf = new byte[subtableOffset + subtableSize];
        WriteU16(buf, 0, 0); // version
        WriteU16(buf, 2, 1); // numTables = 1
        WriteU16(buf, 4, 3); // platformID = 3(Windows)
        WriteU16(buf, 6, 1); // encodingID = 1(BMP)
        WriteU32(buf, 8, (uint)subtableOffset);

        int s = subtableOffset;
        WriteU16(buf, s, 4);       // format
        WriteU16(buf, s + 2, (ushort)subtableSize); // length
        WriteU16(buf, s + 4, 0);   // language
        WriteU16(buf, s + 6, 2);   // segCountX2 = 2(세그먼트 1개)
        WriteU16(buf, s + 8, 0);   // searchRange
        WriteU16(buf, s + 10, 0);  // entrySelector
        WriteU16(buf, s + 12, 0);  // rangeShift
        WriteU16(buf, s + 14, 0xFFFF); // endCode[0]
        WriteU16(buf, s + 16, 0);      // reservedPad
        WriteU16(buf, s + 18, 0xFFFF); // startCode[0]
        WriteU16(buf, s + 20, 1);      // idDelta[0]
        WriteU16(buf, s + 22, 0);      // idRangeOffset[0]

        return buf;
    }
}
