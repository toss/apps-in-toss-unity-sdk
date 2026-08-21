// -----------------------------------------------------------------------
// AITWarmPageEmitterTests.cs - EditMode warm page 산출기 테스트
// Level 0: AITWarmPageEmitter.WritePage 의 게이팅/내용/결정성/
//          마커잔존/플레이스홀더 안전 검증 (순수 파일 I/O, 빌드 불필요)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITWarmPageEmitterTests
{
    private string _tempDir;
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        // GUID 기반 임시 디렉토리(충돌 방지).
        _tempDir = Path.Combine(Path.GetTempPath(), "AITWarmPageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // 세 값 모두 명시적 활성(tri-state: 1=활성) 인 기본 config.
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.pageCache     = 1;  // 명시적 활성
        _config.warmManifest  = 1;  // 명시적 활성
        _config.warmPage      = 1;  // 명시적 활성
        _config.pageCacheName = "ait-page-cache";
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // 정리 실패는 무시.
        }
    }

    // ===== 케이스 1: config null → 파일 미산출, stale 있으면 삭제 =====

    [Test]
    public void WritePage_ConfigNull_DoesNotEmit()
    {
        // stale 파일 사전 배치.
        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        File.WriteAllText(pagePath, "stale", Encoding.UTF8);

        AITWarmPageEmitter.WritePage(null, _tempDir);

        Assert.IsFalse(File.Exists(pagePath),
            "config null 이면 stale 파일도 삭제되고 신규 산출 없어야 한다");
    }

    // ===== 케이스 1b: warmPage=-1(자동=ON) + 업스트림 ON → 산출됨 =====

    [Test]
    public void AutoDefault_EmitsFile()
    {
        // GetDefaultWarmPage()=true 전제: warmPage=-1 이면 기본 ON.
        _config.warmPage = -1; // 자동 (기본값 true)
        _config.pageCache    = 1;
        _config.warmManifest = 1;
        WritePage();

        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        Assert.IsTrue(File.Exists(pagePath),
            "warmPage=-1(자동=ON) + pageCache·warmManifest 활성이면 ait-warm.html 을 산출해야 한다");
    }

    // ===== 케이스 2: warmPage=0(명시적 비활성) → stale 삭제 =====

    [Test]
    public void WritePage_WarmPageOff_DeletesStale()
    {
        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        File.WriteAllText(pagePath, "stale", Encoding.UTF8);

        _config.warmPage = 0; // 명시적 비활성
        WritePage();

        Assert.IsFalse(File.Exists(pagePath),
            "warmPage=0(명시적 비활성) 이면 기존 stale 파일이 삭제되어야 한다");
    }

    // ===== 케이스 2b: warmPage=-1(자동=ON) + pageCache=0(명시적 비활성) → 미산출 =====

    [Test]
    public void AutoDefault_PageCacheOff_DoesNotEmit()
    {
        _config.warmPage  = -1; // 자동 (기본값 true)
        _config.pageCache = 0;  // 명시적 비활성

        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);

        LogAssert.Expect(LogType.Warning, new Regex(@"ait-warm\.html 미산출"));

        WritePage();

        Assert.IsFalse(File.Exists(pagePath),
            "warmPage=-1(자동=ON) + pageCache=0(비활성) 이면 ait-warm.html 미산출이어야 한다");
    }

    // ===== 케이스 3: warmPage=1, warmManifest=0(명시적 비활성) → 경고 + 미산출 =====

    [Test]
    public void WritePage_WarmManifestOff_Warns_AndDeletesStale()
    {
        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        File.WriteAllText(pagePath, "stale", Encoding.UTF8);

        _config.warmManifest = 0; // 명시적 비활성

        LogAssert.Expect(LogType.Warning, new Regex(@"ait-warm\.html 미산출"));

        WritePage();

        Assert.IsFalse(File.Exists(pagePath),
            "warmManifest=0(명시적 비활성) 이면 ait-warm.html 미산출 + stale 삭제");
    }

    // ===== 케이스 4: warmPage=1, pageCache=0(명시적 비활성) → 경고 + 미산출 =====

    [Test]
    public void WritePage_PageCacheOff_Warns_AndDeletesStale()
    {
        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        File.WriteAllText(pagePath, "stale", Encoding.UTF8);

        _config.pageCache = 0; // 명시적 비활성

        LogAssert.Expect(LogType.Warning, new Regex(@"ait-warm\.html 미산출"));

        WritePage();

        Assert.IsFalse(File.Exists(pagePath),
            "pageCache=0(명시적 비활성) 이면 ait-warm.html 미산출 + stale 삭제");
    }

    // ===== 케이스 5: 세 값 모두 명시적 활성 → 파일 존재, 길이 > 0 =====

    [Test]
    public void WritePage_AllFlagsOn_EmitsFile()
    {
        WritePage();

        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        Assert.IsTrue(File.Exists(pagePath), "세 값 모두 명시적 활성이면 파일이 산출되어야 한다");
        Assert.Greater(new FileInfo(pagePath).Length, 0L, "산출 파일 크기가 0 보다 커야 한다");
    }

    // ===== 케이스 6: 산출물에 cacheName 과 매니페스트 파일명 포함 =====

    [Test]
    public void WritePage_OutputContainsCacheNameAndManifestFileName()
    {
        _config.pageCacheName = "my-cache";
        WritePage();

        string html = ReadPage();
        StringAssert.Contains("my-cache", html,
            "지정한 pageCacheName 이 산출물에 포함되어야 한다");
        StringAssert.Contains(AITWarmManifestEmitter.FileName, html,
            "매니페스트 파일명(" + AITWarmManifestEmitter.FileName + ")이 산출물에 포함되어야 한다");
    }

    // ===== 케이스 7: pageCacheName 비어 있으면 기본값 사용 =====

    [Test]
    public void WritePage_EmptyCacheName_FallsBackToDefault()
    {
        _config.pageCacheName = "";
        WritePage();

        string html = ReadPage();
        StringAssert.Contains(AITPageCacheEmitter.DefaultCacheName, html,
            "pageCacheName 이 비면 기본값 ait-page-cache 로 보정되어야 한다");
    }

    // ===== 케이스 8: 결정성 — 동일 config 2회 호출 산출물 byte-identical =====

    [Test]
    public void WritePage_Deterministic_TwoCallsByteIdentical()
    {
        WritePage();
        byte[] first = ReadPageBytes();

        WritePage();
        byte[] second = ReadPageBytes();

        Assert.AreEqual(first.Length, second.Length,
            "두 번 호출의 산출물 크기가 같아야 한다");
        for (int i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
            {
                Assert.Fail("산출물이 byte-identical 이어야 한다 (오프셋 " + i + " 불일치)");
            }
        }
    }

    // ===== 케이스 8b: mtime 보존 — 내용이 같으면 재작성하지 않는다 =====
    //
    // Package.PackageBuildStateMarker 는 ait-build/public 트리를 (경로, 길이, mtimeTicks)로만
    // 해시하며 "mtime 불변 == 내용 불변"을 전제한다. 이 산출기는 public/ 안에 쓰므로 내용이
    // 같은데도 재작성하면 패키징 스킵이 영원히 발동하지 않는다 (AITWarmManifestEmitter 와 동일 사유).

    [Test]
    public void WritePage_SameContent_PreservesMtime()
    {
        WritePage();
        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        Assert.IsTrue(File.Exists(pagePath), "SetUp: 1차 산출이 성공해야 한다");

        // 파일시스템 타임스탬프 해상도에 의존하지 않도록 과거 시각을 명시적으로 심는다.
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(pagePath, stamp);

        WritePage();

        Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(pagePath),
            "내용이 동일하면 재작성을 생략해 mtime 이 보존되어야 한다 (PackageBuildStateMarker 의 public/ 해시 불변식)");
    }

    // ===== 케이스 8c: 내용이 달라지면 반드시 재작성한다 =====

    [Test]
    public void WritePage_ChangedContent_RewritesFile()
    {
        WritePage();
        string pagePath = Path.Combine(_tempDir, AITWarmPageEmitter.FileName);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(pagePath, stamp);

        _config.pageCacheName = "ait-page-cache-changed";
        WritePage();

        Assert.AreNotEqual(stamp, File.GetLastWriteTimeUtc(pagePath),
            "내용이 달라지면 재작성되어 mtime 이 전진해야 한다");
        StringAssert.Contains("ait-page-cache-changed", ReadPage(),
            "재작성된 산출물에 변경된 캐시 이름이 반영되어야 한다");
    }

    // ===== 케이스 9: HTML 구조 최소 검사 =====

    [Test]
    public void WritePage_HtmlStructureSanity()
    {
        WritePage();
        string html = ReadPage();

        StringAssert.StartsWith("<!DOCTYPE html>", html.TrimStart(),
            "HTML 파일이 <!DOCTYPE html> 로 시작해야 한다");
        StringAssert.Contains("<script>", html,
            "인라인 <script> 블록이 있어야 한다");
        StringAssert.Contains("ait:warm:progress", html,
            "progress 신호 타입 문자열이 포함되어야 한다");
        StringAssert.Contains("ait:warm:done", html,
            "done 신호 타입 문자열이 포함되어야 한다");
        StringAssert.Contains("ait:warm:error", html,
            "error 신호 타입 문자열이 포함되어야 한다");
    }

    // ===== 케이스 10: 마커 잔존 없음 + %[A-Z0-9_]+% 없음 =====

    [Test]
    public void WritePage_NoMarkerOrPlaceholderRemains()
    {
        WritePage();
        string html = ReadPage();

        StringAssert.DoesNotContain("__AIT_", html,
            "__AIT_ 마커가 산출물에 잔존하면 안 된다");
        Assert.IsFalse(Regex.IsMatch(html, @"%[A-Z0-9_]+%"),
            "산출물에 %대문자_퍼센트% 토큰이 있으면 ValidatePlaceholderSubstitution 이 빌드를 실패시킨다");
    }

    // ===== 케이스 11: 합성 Response 에 Content-Encoding 을 헤더로 넣지 않음 =====

    [Test]
    public void WritePage_NoContentEncodingInSyntheticResponse()
    {
        WritePage();
        string html = ReadPage();

        // storeDecodeFree 에서 new Response 생성 시 content-encoding 을 헤더에 추가하지 않음을 검증.
        // 합성 Response 생성부(new Response(buf, { status:200, headers: ... })) 직후에
        // 'content-encoding' 을 headers 객체 키로 넣는 코드가 없어야 한다.
        // 구현이 단일따옴표 위주이므로 양쪽 모두 체크.
        int syntheticIdx = html.IndexOf("new Response(buf", StringComparison.Ordinal);
        Assert.Greater(syntheticIdx, -1, "합성 Response 생성 코드가 존재해야 한다");

        // 합성 Response 생성 이후 150자 이내에서 content-encoding 헤더 추가 여부 체크.
        int checkEnd = Math.Min(syntheticIdx + 300, html.Length);
        string surroundingCode = html.Substring(syntheticIdx, checkEnd - syntheticIdx);

        StringAssert.DoesNotContain("content-encoding", surroundingCode.ToLowerInvariant(),
            "합성 Response headers 에 content-encoding 을 추가하면 decode-free 계약 위반이다");
    }

    // -----------------------------------------------------------------------
    // 헬퍼
    // -----------------------------------------------------------------------

    private void WritePage()
    {
        AITWarmPageEmitter.WritePage(_config, _tempDir);
    }

    private string ReadPage()
    {
        return File.ReadAllText(Path.Combine(_tempDir, AITWarmPageEmitter.FileName), Encoding.UTF8);
    }

    private byte[] ReadPageBytes()
    {
        return File.ReadAllBytes(Path.Combine(_tempDir, AITWarmPageEmitter.FileName));
    }
}
