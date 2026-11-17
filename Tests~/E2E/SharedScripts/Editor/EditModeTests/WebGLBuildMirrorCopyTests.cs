// -----------------------------------------------------------------------
// WebGLBuildMirrorCopyTests.cs - EditMode 변경분 미러 복사 검증 테스트
// Level 0: WebGLBuildCopier.CopyFileIfChanged / MirrorCopyDirectory의
//   "변경 시에만 복사 + stale 산출물 제거" 미러 의미론을 파일시스템 수준에서 검증하고,
//   PrepareAitBuildFolder가 public/ 미러 대상을 보존해 그 최적화가 실제 빌드에서
//   유효하게 유지되는지(= 매 빌드 전량 복사로 퇴화하지 않는지) 함께 검증한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using AppsInToss.Editor.Package;

[TestFixture]
public class WebGLBuildMirrorCopyTests
{
    private string tempDir;
    private string srcDir;
    private string destDir;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-mirror-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        srcDir = Path.Combine(tempDir, "src");
        destDir = Path.Combine(tempDir, "dest");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    // =====================================================
    // CopyFileIfChanged: 대상 파일이 없으면 복사
    // =====================================================

    [Test]
    public void CopyFileIfChanged_DestMissing_Copies()
    {
        string src = Path.Combine(srcDir, "a.txt");
        File.WriteAllText(src, "content");
        string dest = Path.Combine(destDir, "a.txt");

        bool copied = WebGLBuildCopier.CopyFileIfChanged(src, dest);

        Assert.IsTrue(copied, "대상 파일이 없으면 복사해야 함");
        Assert.IsTrue(File.Exists(dest));
        Assert.AreEqual("content", File.ReadAllText(dest));
    }

    // =====================================================
    // CopyFileIfChanged: 크기와 내용이 동일하면 스킵 (덮어쓰지 않음)
    // =====================================================

    [Test]
    public void CopyFileIfChanged_IdenticalContent_SkipsAndDoesNotOverwrite()
    {
        string src = Path.Combine(srcDir, "a.txt");
        File.WriteAllText(src, "same content");
        string dest = Path.Combine(destDir, "a.txt");
        File.WriteAllText(dest, "same content");

        // 대상 파일에 감시 가능한 표식(다른 mtime)을 남겨 실제로 건드려지지 않았는지 확인
        var sentinelTime = new DateTime(2020, 1, 1, 0, 0, 0);
        File.SetLastWriteTime(dest, sentinelTime);

        bool copied = WebGLBuildCopier.CopyFileIfChanged(src, dest);

        Assert.IsFalse(copied, "내용이 동일하면 스킵해야 함");
        Assert.AreEqual(sentinelTime, File.GetLastWriteTime(dest),
            "스킵된 파일은 실제로 재작성되지 않아야 함 (mtime 불변)");
    }

    // =====================================================
    // CopyFileIfChanged: 크기가 다르면 복사
    // =====================================================

    [Test]
    public void CopyFileIfChanged_DifferentSize_Copies()
    {
        string src = Path.Combine(srcDir, "a.txt");
        File.WriteAllText(src, "new content, longer than before");
        string dest = Path.Combine(destDir, "a.txt");
        File.WriteAllText(dest, "old");

        bool copied = WebGLBuildCopier.CopyFileIfChanged(src, dest);

        Assert.IsTrue(copied, "크기가 다르면 복사해야 함");
        Assert.AreEqual("new content, longer than before", File.ReadAllText(dest));
    }

    // =====================================================
    // CopyFileIfChanged: 크기는 같지만 내용이 다르면 복사 (mtime만으로 판정하지 않음)
    // =====================================================

    [Test]
    public void CopyFileIfChanged_SameSizeDifferentContent_Copies()
    {
        string src = Path.Combine(srcDir, "a.bin");
        File.WriteAllBytes(src, new byte[] { 1, 2, 3, 4 });
        string dest = Path.Combine(destDir, "a.bin");
        File.WriteAllBytes(dest, new byte[] { 1, 2, 3, 9 });

        // 소스보다 최신 mtime을 부여해도(Unity가 매 빌드 재작성하는 상황 재현) 내용 비교로 판정해야 함
        File.SetLastWriteTime(dest, DateTime.Now.AddMinutes(10));

        bool copied = WebGLBuildCopier.CopyFileIfChanged(src, dest);

        Assert.IsTrue(copied, "크기가 같아도 내용이 다르면 mtime과 무관하게 복사해야 함");
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(dest));
    }

    // =====================================================
    // MirrorCopyDirectory: 신규/변경 파일만 실제로 복사되고 동일 파일은 스킵
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_CopiesNewAndChanged_SkipsIdentical()
    {
        File.WriteAllText(Path.Combine(srcDir, "new.txt"), "new");
        File.WriteAllText(Path.Combine(srcDir, "changed.txt"), "changed-v2");
        File.WriteAllText(Path.Combine(srcDir, "same.txt"), "unchanged");

        File.WriteAllText(Path.Combine(destDir, "changed.txt"), "changed-v1");
        File.WriteAllText(Path.Combine(destDir, "same.txt"), "unchanged");
        var sameSentinel = new DateTime(2020, 1, 1);
        File.SetLastWriteTime(Path.Combine(destDir, "same.txt"), sameSentinel);

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.AreEqual(2, copied, "new.txt, changed.txt 두 개가 복사되어야 함");
        Assert.AreEqual(1, skipped, "same.txt는 스킵되어야 함");
        Assert.AreEqual(0, stale, "제거 대상 stale 파일 없음");

        Assert.AreEqual("new", File.ReadAllText(Path.Combine(destDir, "new.txt")));
        Assert.AreEqual("changed-v2", File.ReadAllText(Path.Combine(destDir, "changed.txt")));
        Assert.AreEqual(sameSentinel, File.GetLastWriteTime(Path.Combine(destDir, "same.txt")),
            "same.txt는 실제로 재작성되지 않아야 함");
    }

    // =====================================================
    // MirrorCopyDirectory: 소스에 없는 대상 파일은 stale로 제거
    // (미러 의미론 핵심 — 전체 삭제+재복사와 동일한 최종 상태를 보장)
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_RemovesStaleFilesNotInSource()
    {
        File.WriteAllText(Path.Combine(srcDir, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(destDir, "keep.txt"), "old-keep");
        File.WriteAllText(Path.Combine(destDir, "stale.txt"), "leftover from previous build");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.IsTrue(File.Exists(Path.Combine(destDir, "keep.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(destDir, "stale.txt")),
            "소스에 없는 파일은 dest에서 제거되어야 함 (stale 산출물 방지)");
        Assert.AreEqual(1, stale);
    }

    // =====================================================
    // MirrorCopyDirectory: 소스에 없는 대상 하위 디렉토리는 통째로 제거
    // Unity 버전 전환 등으로 산출물 폴더 구조 자체가 바뀌는 경우 대응
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_RemovesStaleSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(srcDir, "keepDir"));
        File.WriteAllText(Path.Combine(srcDir, "keepDir", "f.txt"), "f");

        Directory.CreateDirectory(Path.Combine(destDir, "staleDir"));
        File.WriteAllText(Path.Combine(destDir, "staleDir", "old.txt"), "old");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.IsTrue(Directory.Exists(Path.Combine(destDir, "keepDir")));
        Assert.IsTrue(File.Exists(Path.Combine(destDir, "keepDir", "f.txt")));
        Assert.IsFalse(Directory.Exists(Path.Combine(destDir, "staleDir")),
            "소스에 없는 하위 디렉토리는 통째로 제거되어야 함");
        Assert.GreaterOrEqual(stale, 1);
    }

    // =====================================================
    // MirrorCopyDirectory: 확장자 세트 전환 시나리오 (.br → .unityweb 등)
    // 압축 포맷 변경으로 파일명 전체가 바뀌는 경우에도 stale이 남지 않아야 함
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_ExtensionSetSwitch_NoStaleFilesRemain()
    {
        // 이전 빌드(dest): .br 확장자 산출물
        File.WriteAllText(Path.Combine(destDir, "build.data.br"), "old brotli data");
        File.WriteAllText(Path.Combine(destDir, "build.wasm.br"), "old brotli wasm");

        // 새 빌드(src): .unityweb 확장자로 전환
        File.WriteAllText(Path.Combine(srcDir, "build.data.unityweb"), "new unityweb data");
        File.WriteAllText(Path.Combine(srcDir, "build.wasm.unityweb"), "new unityweb wasm");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.AreEqual(2, copied);
        Assert.AreEqual(2, stale, "이전 포맷의 .br 파일 2개가 제거되어야 함");
        Assert.IsFalse(File.Exists(Path.Combine(destDir, "build.data.br")));
        Assert.IsFalse(File.Exists(Path.Combine(destDir, "build.wasm.br")));
        Assert.IsTrue(File.Exists(Path.Combine(destDir, "build.data.unityweb")));
        Assert.IsTrue(File.Exists(Path.Combine(destDir, "build.wasm.unityweb")));
    }

    // =====================================================
    // MirrorCopyDirectory: 재귀 — 하위 디렉토리 안의 변경분도 동일하게 판정
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_RecursesIntoSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(srcDir, "sub"));
        File.WriteAllText(Path.Combine(srcDir, "sub", "nested.txt"), "nested-v2");

        Directory.CreateDirectory(Path.Combine(destDir, "sub"));
        File.WriteAllText(Path.Combine(destDir, "sub", "nested.txt"), "nested-v1");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.AreEqual(1, copied, "중첩 디렉토리 안의 변경 파일도 복사되어야 함");
        Assert.AreEqual("nested-v2", File.ReadAllText(Path.Combine(destDir, "sub", "nested.txt")));
    }

    // =====================================================
    // PrepareAitBuildFolder: public/ 미러 대상 보존
    // 이 보존이 없으면 매 빌드 public/이 통째로 삭제되어 아래 "2회 연속 미러 복사 시 스킵"이
    // 실제 빌드에서는 절대 성립하지 않는다 (변경분 복사 최적화가 항상 무효).
    // =====================================================

    [Test]
    public void PrepareAitBuildFolder_PreservesMirroredPublicDirectories()
    {
        string buildProjectPath = Path.Combine(tempDir, "ait-build");
        Directory.CreateDirectory(buildProjectPath);

        foreach (var name in WebGLBuildCopier.MirroredPublicDirectories)
        {
            string dir = Path.Combine(buildProjectPath, "public", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "artifact.bin"), name);
        }

        // 미러 대상이 아닌 public/ 항목 + 빌드 산출물
        File.WriteAllText(Path.Combine(buildProjectPath, "public", "user-file.json"), "{}");
        Directory.CreateDirectory(Path.Combine(buildProjectPath, "dist"));

        WebGLBuildCopier.PrepareAitBuildFolder(buildProjectPath);

        foreach (var name in WebGLBuildCopier.MirroredPublicDirectories)
        {
            Assert.IsTrue(File.Exists(Path.Combine(buildProjectPath, "public", name, "artifact.bin")),
                $"public/{name}은 미러 대상이므로 보존되어야 함");
        }
        Assert.IsFalse(File.Exists(Path.Combine(buildProjectPath, "public", "user-file.json")),
            "미러 대상이 아닌 public/ 항목은 기존과 동일하게 매 빌드 정리되어야 함");
        Assert.IsFalse(Directory.Exists(Path.Combine(buildProjectPath, "dist")),
            "dist/는 계속 삭제되어야 함");
    }

    // =====================================================
    // 2회 연속 미러 복사: 두 번째 실행은 전부 스킵 (실제 빌드 반복 시나리오)
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_SecondRunWithUnchangedSource_SkipsEverything()
    {
        Directory.CreateDirectory(Path.Combine(srcDir, "sub"));
        File.WriteAllText(Path.Combine(srcDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(srcDir, "sub", "b.txt"), "b");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);
        Assert.AreEqual(2, copied, "1회차는 두 파일 모두 복사");

        // 스킵 여부를 mtime 표식으로 관찰 (재작성되면 mtime이 바뀐다)
        var sentinel = new DateTime(2020, 1, 1, 0, 0, 0);
        File.SetLastWriteTime(Path.Combine(destDir, "a.txt"), sentinel);
        File.SetLastWriteTime(Path.Combine(destDir, "sub", "b.txt"), sentinel);

        copied = 0; skipped = 0; stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.AreEqual(0, copied, "2회차는 변경분이 없으므로 복사가 없어야 함");
        Assert.AreEqual(2, skipped, "2회차는 두 파일 모두 스킵되어야 함");
        Assert.AreEqual(0, stale, "2회차에 제거할 잔여물은 없어야 함");
        Assert.AreEqual(sentinel, File.GetLastWriteTime(Path.Combine(destDir, "a.txt")),
            "스킵된 파일은 재작성되지 않아야 함");
        Assert.AreEqual(sentinel, File.GetLastWriteTime(Path.Combine(destDir, "sub", "b.txt")),
            "중첩 디렉토리의 스킵된 파일도 재작성되지 않아야 함");
    }

    // =====================================================
    // 2회 연속 미러 복사: 소스에서 사라진 파일은 2회차에서 제거
    // (public/을 보존하게 되면서 stale 제거 책임이 이 함수로 넘어왔다)
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_SecondRunAfterSourceDeletion_RemovesFromDest()
    {
        File.WriteAllText(Path.Combine(srcDir, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(srcDir, "gone.txt"), "gone");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);
        Assert.AreEqual(2, copied);

        // 소스에서 에셋이 삭제된 상황 (dev server가 계속 서빙하면 회귀)
        File.Delete(Path.Combine(srcDir, "gone.txt"));

        copied = 0; skipped = 0; stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.AreEqual(0, copied);
        Assert.AreEqual(1, skipped, "keep.txt는 스킵");
        Assert.AreEqual(1, stale, "gone.txt는 stale로 제거");
        Assert.IsTrue(File.Exists(Path.Combine(destDir, "keep.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(destDir, "gone.txt")),
            "소스에서 삭제된 파일은 대상에서도 제거되어야 함");
    }

    // =====================================================
    // MirrorCopyDirectory: .meta 파일은 복사·정리 대상에서 제외
    // (UnityUtil.CopyDirectory와 동일한 GUID 충돌 방지 규약)
    // =====================================================

    [Test]
    public void MirrorCopyDirectory_IgnoresMetaFiles()
    {
        File.WriteAllText(Path.Combine(srcDir, "asset.txt"), "asset");
        File.WriteAllText(Path.Combine(srcDir, "asset.txt.meta"), "guid: srcmeta");

        // dest에 기존 .meta가 있어도 stale로 삭제되면 안 됨
        File.WriteAllText(Path.Combine(destDir, "asset.txt.meta"), "guid: destmeta");

        int copied = 0, skipped = 0, stale = 0;
        WebGLBuildCopier.MirrorCopyDirectory(srcDir, destDir, ref copied, ref skipped, ref stale);

        Assert.IsTrue(File.Exists(Path.Combine(destDir, "asset.txt.meta")),
            "dest의 .meta 파일은 그대로 유지되어야 함 (건드리지 않음)");
        Assert.AreEqual("guid: destmeta", File.ReadAllText(Path.Combine(destDir, "asset.txt.meta")),
            "소스 .meta 내용으로 덮어써지면 안 됨 (Unity가 자체 관리)");
        Assert.AreEqual(0, stale, ".meta 파일은 stale 판정/삭제 대상이 아님");
    }
}
