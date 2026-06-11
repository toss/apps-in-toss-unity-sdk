// -----------------------------------------------------------------------
// AITAstcBlockProcessorTests.cs - ASTC 블록 에스컬레이션 순수 로직 검증
// Level 0: AssetDatabase 비의존 순수 헬퍼 함수 EditMode 테스트
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITAstcBlockProcessorTests
{
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    // =====================================================
    // 1) BlockFormatFor — 6개 블록 전수 매핑
    // =====================================================

    [TestCase(4,  TextureImporterFormat.ASTC_4x4)]
    [TestCase(5,  TextureImporterFormat.ASTC_5x5)]
    [TestCase(6,  TextureImporterFormat.ASTC_6x6)]
    [TestCase(8,  TextureImporterFormat.ASTC_8x8)]
    [TestCase(10, TextureImporterFormat.ASTC_10x10)]
    [TestCase(12, TextureImporterFormat.ASTC_12x12)]
    public void BlockFormatFor_KnownValues_ReturnsCorrectFormat(int block, TextureImporterFormat expected)
    {
        var result = AITAstcBlockProcessor.BlockFormatFor(block);
        Assert.AreEqual(expected, result, $"블록 크기 {block}에 대한 포맷 매핑이 올바르지 않습니다.");
    }

    // =====================================================
    // 2) BlockFormatFor — 무효값(0, 7, 99, -1) → ASTC_12x12 폴백
    // =====================================================

    [TestCase(0)]
    [TestCase(7)]
    [TestCase(99)]
    [TestCase(-1)]
    public void BlockFormatFor_InvalidValues_FallsBackToAstc12x12(int block)
    {
        var result = AITAstcBlockProcessor.BlockFormatFor(block);
        Assert.AreEqual(TextureImporterFormat.ASTC_12x12, result,
            $"무효 블록 크기 {block}은 ASTC_12x12 로 폴백해야 합니다.");
    }

    // =====================================================
    // 3) IsExcludedPath — 내장 휴리스틱(/font, " sdf", textmesh — 대소문자 무시)
    // =====================================================

    [TestCase("Assets/8.Fonts/UI/sprite.png", true,        "폰트 디렉터리 /font 포함")]
    [TestCase("Assets/UI/Button SDF.png",     true,        "' sdf' 토큰 포함")]
    [TestCase("Assets/TextMesh Pro/atlas.png", true,       "textmesh 포함")]
    [TestCase("Assets/Textures/FONT/bg.png",  true,        "대문자 /FONT 도 제외")]
    [TestCase("Assets/Textures/sprite.png",   false,       "일반 텍스처 경로")]
    public void IsExcludedPath_BuiltinHeuristics(string path, bool expectedExcluded, string reason)
    {
        var result = AITAstcBlockProcessor.IsExcludedPath(path, null);
        Assert.AreEqual(expectedExcluded, result, reason);
    }

    // =====================================================
    // 4) IsExcludedPath — 사용자 제외 폴더 접두 일치/불일치
    // =====================================================

    [Test]
    public void IsExcludedPath_UserExcludeDirs_MatchesPrefixCaseInsensitive()
    {
        // "Assets/UI" 제외 지정 시 하위 경로 모두 제외
        var excludeDirs = new[] { "Assets/UI" };

        Assert.IsTrue(AITAstcBlockProcessor.IsExcludedPath("Assets/UI/Button.png", excludeDirs),
            "사용자 제외 폴더의 직접 하위 경로는 제외되어야 합니다.");
        Assert.IsTrue(AITAstcBlockProcessor.IsExcludedPath("Assets/ui/Button.png", excludeDirs),
            "대소문자 무시로 'Assets/ui'도 제외되어야 합니다.");
        Assert.IsFalse(AITAstcBlockProcessor.IsExcludedPath("Assets/Textures/Button.png", excludeDirs),
            "제외 폴더 외부의 경로는 포함되어야 합니다.");
    }

    // =====================================================
    // 5) IsExcludedPath — 일반 경로는 false
    // =====================================================

    [Test]
    public void IsExcludedPath_NormalPath_ReturnsFalse()
    {
        var result = AITAstcBlockProcessor.IsExcludedPath("Assets/Sprites/character.png", null);
        Assert.IsFalse(result, "일반 텍스처 경로는 제외되어서는 안 됩니다.");
    }

    // =====================================================
    // 6) ResolveTargetMaxSize — cap=0 / cap<현재 / cap>현재
    // =====================================================

    [Test]
    public void ResolveTargetMaxSize_CapZero_ReturnsCurrent()
    {
        Assert.AreEqual(2048, AITAstcBlockProcessor.ResolveTargetMaxSize(2048, 0),
            "cap=0 이면 현재 maxTextureSize 를 그대로 반환해야 합니다.");
    }

    [Test]
    public void ResolveTargetMaxSize_CapLessThanCurrent_ReturnsCap()
    {
        Assert.AreEqual(512, AITAstcBlockProcessor.ResolveTargetMaxSize(2048, 512),
            "cap < 현재 크기 이면 cap 을 반환해야 합니다.");
    }

    [Test]
    public void ResolveTargetMaxSize_CapGreaterThanCurrent_ReturnsCurrent()
    {
        Assert.AreEqual(1024, AITAstcBlockProcessor.ResolveTargetMaxSize(1024, 4096),
            "cap > 현재 크기 이면 현재 크기를 반환해야 합니다(Min).");
    }

    // =====================================================
    // 7) WouldChange — 이미 목표 상태면 false
    // =====================================================

    [Test]
    public void WouldChange_AlreadyTargetState_ReturnsFalse()
    {
        var fmt = TextureImporterFormat.ASTC_12x12;
        var result = AITAstcBlockProcessor.WouldChange(
            overridden: true,
            currentFormat: fmt,
            currentMax: 1024,
            targetFormat: fmt,
            targetMax: 1024
        );
        Assert.IsFalse(result, "이미 목표 상태(overridden=true, 포맷 일치, max 일치)이면 WouldChange=false 여야 합니다.");
    }

    // =====================================================
    // 8) WouldChange — 포맷 상이 / 비오버라이드 / max 초과 각 true
    // =====================================================

    [Test]
    public void WouldChange_FormatDiffers_ReturnsTrue()
    {
        var result = AITAstcBlockProcessor.WouldChange(
            overridden: true,
            currentFormat: TextureImporterFormat.ASTC_4x4,
            currentMax: 1024,
            targetFormat: TextureImporterFormat.ASTC_12x12,
            targetMax: 1024
        );
        Assert.IsTrue(result, "포맷이 다를 때 WouldChange=true 여야 합니다.");
    }

    [Test]
    public void WouldChange_NotOverridden_ReturnsTrue()
    {
        var fmt = TextureImporterFormat.ASTC_12x12;
        var result = AITAstcBlockProcessor.WouldChange(
            overridden: false,
            currentFormat: fmt,
            currentMax: 1024,
            targetFormat: fmt,
            targetMax: 1024
        );
        Assert.IsTrue(result, "오버라이드가 없으면 WouldChange=true 여야 합니다.");
    }

    [Test]
    public void WouldChange_MaxDiffers_ReturnsTrue()
    {
        var fmt = TextureImporterFormat.ASTC_12x12;
        var result = AITAstcBlockProcessor.WouldChange(
            overridden: true,
            currentFormat: fmt,
            currentMax: 2048,
            targetFormat: fmt,
            targetMax: 512
        );
        Assert.IsTrue(result, "maxTextureSize 가 다를 때 WouldChange=true 여야 합니다.");
    }

    // =====================================================
    // 9) IsUncompressedFormat — 양성/음성
    // =====================================================

    [TestCase(TextureImporterFormat.RGBA32,    true)]
    [TestCase(TextureImporterFormat.ARGB32,    true)]
    [TestCase(TextureImporterFormat.RGB24,     true)]
    [TestCase(TextureImporterFormat.Alpha8,    true)]
    [TestCase(TextureImporterFormat.R8,        true)]
    [TestCase(TextureImporterFormat.RGBAHalf,  true)]
    [TestCase(TextureImporterFormat.RGBAFloat, true)]
    [TestCase(TextureImporterFormat.ASTC_4x4,  false)]
    [TestCase(TextureImporterFormat.ASTC_12x12, false)]
    [TestCase(TextureImporterFormat.DXT1,      false)]
    public void IsUncompressedFormat_Correctness(TextureImporterFormat fmt, bool expectedUncompressed)
    {
        var result = AITAstcBlockProcessor.IsUncompressedFormat(fmt);
        Assert.AreEqual(expectedUncompressed, result,
            $"IsUncompressedFormat({fmt}) = {expectedUncompressed} 이어야 합니다.");
    }

    // =====================================================
    // 10) SplitDirs / UnderAny — null/빈/정상 + 일치/불일치
    // =====================================================

    [Test]
    public void SplitDirs_Null_ReturnsNull()
    {
        Assert.IsNull(AITAstcBlockProcessor.SplitDirs(null),
            "null 입력 시 null 을 반환해야 합니다.");
    }

    [Test]
    public void SplitDirs_Empty_ReturnsNull()
    {
        Assert.IsNull(AITAstcBlockProcessor.SplitDirs(""),
            "빈 문자열 입력 시 null 을 반환해야 합니다.");
    }

    [Test]
    public void SplitDirs_Normal_SplitsByComma()
    {
        var result = AITAstcBlockProcessor.SplitDirs("Assets/UI,Assets/Textures");
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Length, "쉼표로 2개로 분리되어야 합니다.");
        Assert.AreEqual("Assets/UI", result[0]);
        Assert.AreEqual("Assets/Textures", result[1]);
    }

    [Test]
    public void UnderAny_MatchingDir_ReturnsTrue()
    {
        var dirs = new[] { "Assets/Textures" };
        Assert.IsTrue(AITAstcBlockProcessor.UnderAny("Assets/Textures/sprite.png", dirs),
            "지정 폴더 하위 경로는 UnderAny=true 여야 합니다.");
    }

    [Test]
    public void UnderAny_NonMatchingDir_ReturnsFalse()
    {
        var dirs = new[] { "Assets/Textures" };
        Assert.IsFalse(AITAstcBlockProcessor.UnderAny("Assets/Fonts/font.ttf", dirs),
            "지정 폴더 외부 경로는 UnderAny=false 여야 합니다.");
    }

    // =====================================================
    // 11) ApplyForBuild(null) / ApplyForBuild(enable=false) — 비활성 핸들 + 마커 파일 없음
    // =====================================================

    [Test]
    public void ApplyForBuild_NullConfig_ReturnsInactiveHandle()
    {
        var handle = AITAstcBlockProcessor.ApplyForBuild(null);
        Assert.IsNotNull(handle, "핸들은 non-null 이어야 합니다.");
        Assert.IsFalse(handle.Active, "null config 시 비활성 핸들이어야 합니다.");
    }

    [Test]
    public void ApplyForBuild_DisabledConfig_ReturnsInactiveHandleAndNoMarker()
    {
        _config.enableAstcBlockEscalation = false;

        var handle = AITAstcBlockProcessor.ApplyForBuild(_config);

        Assert.IsNotNull(handle, "핸들은 non-null 이어야 합니다.");
        Assert.IsFalse(handle.Active, "기능 비활성 시 비활성 핸들이어야 합니다.");

        // 마커 파일이 생성되지 않았는지 확인.
        // Application.dataPath 는 EditMode 테스트에서도 유효.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string markerPath = Path.Combine(projectRoot, "Assets/.ait-astcblock-active");
        Assert.IsFalse(File.Exists(markerPath),
            "비활성 config 시 마커 파일 'Assets/.ait-astcblock-active' 가 생성되어서는 안 됩니다.");
    }
}
