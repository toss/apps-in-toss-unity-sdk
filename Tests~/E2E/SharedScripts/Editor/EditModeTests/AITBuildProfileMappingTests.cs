// -----------------------------------------------------------------------
// AITBuildProfileMappingTests.cs - 설정 저장값 → Unity enum 매핑 검증
// Level 0: AITBuildInitializer.ConvertTo* 순수 변환 로직 검증
// UI 저장값(드롭다운 순서)과 Unity enum 정수값의 순서 불일치로 인한
// 오적용 회귀 방지 (Managed Stripping "High" 선택 시 Minimal이 적용되던 버그 등)
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEditor;
using AppsInToss;
using AppsInToss.Editor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif

[TestFixture]
public class AITBuildProfileMappingTests
{
    // =====================================================
    // Unity enum 레이아웃 가드
    // 변환 함수가 전제하는 Unity enum 정수값이 바뀌면 여기서 먼저 실패해야 함
    // =====================================================

    [Test]
    public void UnityEnumLayout_ManagedStrippingLevel_MatchesConversionAssumptions()
    {
        // Minimal은 나중에 추가되어 강도는 최약이지만 정수값은 4 — 이 비직관적 레이아웃이 원래 버그의 원인
        Assert.AreEqual(0, (int)ManagedStrippingLevel.Disabled);
        Assert.AreEqual(1, (int)ManagedStrippingLevel.Low);
        Assert.AreEqual(2, (int)ManagedStrippingLevel.Medium);
        Assert.AreEqual(3, (int)ManagedStrippingLevel.High);
        Assert.AreEqual(4, (int)ManagedStrippingLevel.Minimal);
    }

    [Test]
    public void UnityEnumLayout_WebGLExceptionSupport_MatchesConversionAssumptions()
    {
        Assert.AreEqual(0, (int)WebGLExceptionSupport.None);
        Assert.AreEqual(1, (int)WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly);
        Assert.AreEqual(2, (int)WebGLExceptionSupport.FullWithoutStacktrace);
        Assert.AreEqual(3, (int)WebGLExceptionSupport.FullWithStacktrace);
    }

#if UNITY_2023_3_OR_NEWER
    [Test]
    public void UnityEnumLayout_WebGLPowerPreference_MatchesConversionAssumptions()
    {
        Assert.AreEqual(0, (int)WebGLPowerPreference.Default);
        Assert.AreEqual(1, (int)WebGLPowerPreference.LowPower);
        Assert.AreEqual(2, (int)WebGLPowerPreference.HighPerformance);
    }
#endif

    // =====================================================
    // ConvertToManagedStrippingLevel 전수 매핑
    // 저장값(UI 순서): -1=자동, 0=Disabled(레거시), 1=Minimal, 2=Low, 3=Medium, 4=High
    // =====================================================

    [TestCase(0, ManagedStrippingLevel.Minimal)]  // 레거시 Disabled — IL2CPP 미지원이라 Minimal로 폴백
    [TestCase(1, ManagedStrippingLevel.Minimal)]
    [TestCase(2, ManagedStrippingLevel.Low)]
    [TestCase(3, ManagedStrippingLevel.Medium)]
    [TestCase(4, ManagedStrippingLevel.High)]
    public void ConvertToManagedStrippingLevel_Maps_StoredValue_To_ActualEnum(int stored, ManagedStrippingLevel expected)
    {
        Assert.AreEqual(expected, AITBuildInitializer.ConvertToManagedStrippingLevel(stored));
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void ConvertToManagedStrippingLevel_OutOfRange_Falls_Back_To_Default(int stored)
    {
        Assert.AreEqual(AITDefaultSettings.GetDefaultManagedStrippingLevel(),
            AITBuildInitializer.ConvertToManagedStrippingLevel(stored));
    }

    [Test]
    public void ConvertToManagedStrippingLevel_High_Is_Not_Minimal_Regression()
    {
        // 원래 버그: 저장값 4("High")를 직접 캐스팅해 Minimal(=4)이 적용됨 (강도 역전)
        Assert.AreEqual(ManagedStrippingLevel.High,
            AITBuildInitializer.ConvertToManagedStrippingLevel(4),
            "드롭다운 'High'(저장값 4)는 반드시 ManagedStrippingLevel.High로 매핑되어야 함");
    }

    [Test]
    public void DevServerProfile_Default_Stripping_Applies_Minimal()
    {
        // Dev Server 프로필 기본값(저장값 1)은 주석("Minimal - 빌드 속도 우선")대로 Minimal이어야 함
        // 원래 버그: 직접 캐스팅으로 Low가 적용됨
        var profile = AITBuildProfile.CreateDevServerProfile();
        Assert.AreEqual(ManagedStrippingLevel.Minimal,
            AITBuildInitializer.ConvertToManagedStrippingLevel(profile.managedStrippingLevel));
    }

    // =====================================================
    // ConvertToExceptionSupport 전수 매핑
    // 저장값(UI 순서): -1=자동, 0=None, 1=ExplicitlyThrownOnly, 2=FullWithStacktrace, 3=FullWithoutStacktrace
    // =====================================================

    [TestCase(0, WebGLExceptionSupport.None)]
    [TestCase(1, WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly)]
    [TestCase(2, WebGLExceptionSupport.FullWithStacktrace)]
    [TestCase(3, WebGLExceptionSupport.FullWithoutStacktrace)]
    public void ConvertToExceptionSupport_Maps_StoredValue_To_ActualEnum(int stored, WebGLExceptionSupport expected)
    {
        Assert.AreEqual(expected, AITBuildInitializer.ConvertToExceptionSupport(stored));
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void ConvertToExceptionSupport_OutOfRange_Falls_Back_To_Default(int stored)
    {
        Assert.AreEqual(AITDefaultSettings.GetDefaultExceptionSupport(),
            AITBuildInitializer.ConvertToExceptionSupport(stored));
    }

    [Test]
    public void ConvertToExceptionSupport_FullWithStacktrace_Regression()
    {
        // 원래 버그: 저장값 2("FullWithStacktrace")를 직접 캐스팅해 FullWithoutStacktrace가 적용됨
        // → Sentry 스택트레이스 목적으로 선택해도 실제로는 스택트레이스가 꺼졌음
        Assert.AreEqual(WebGLExceptionSupport.FullWithStacktrace,
            AITBuildInitializer.ConvertToExceptionSupport(2),
            "드롭다운 'FullWithStacktrace'(저장값 2)는 반드시 FullWithStacktrace로 매핑되어야 함");
    }

#if UNITY_2023_3_OR_NEWER
    // =====================================================
    // ConvertToPowerPreference 전수 매핑 (Unity 2023.3+)
    // 저장값(UI 순서): -1=자동, 0=Default, 1=HighPerformance, 2=LowPower
    // =====================================================

    [TestCase(0, WebGLPowerPreference.Default)]
    [TestCase(1, WebGLPowerPreference.HighPerformance)]
    [TestCase(2, WebGLPowerPreference.LowPower)]
    public void ConvertToPowerPreference_Maps_StoredValue_To_ActualEnum(int stored, WebGLPowerPreference expected)
    {
        Assert.AreEqual(expected, AITBuildInitializer.ConvertToPowerPreference(stored));
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void ConvertToPowerPreference_OutOfRange_Falls_Back_To_Default(int stored)
    {
        Assert.AreEqual(AITDefaultSettings.GetDefaultPowerPreference(),
            AITBuildInitializer.ConvertToPowerPreference(stored));
    }

    [Test]
    public void ConvertToPowerPreference_HighPerformance_Regression()
    {
        // 원래 버그: 저장값 1("HighPerformance")을 직접 캐스팅해 LowPower가 적용됨 (반전)
        Assert.AreEqual(WebGLPowerPreference.HighPerformance,
            AITBuildInitializer.ConvertToPowerPreference(1),
            "드롭다운 'HighPerformance'(저장값 1)는 반드시 HighPerformance로 매핑되어야 함");
    }
#endif

    // =====================================================
    // ConvertToCompressionFormat 전수 매핑 (기존 정상 동작 가드)
    // 저장값(UI 순서): -1=자동, 0=Disabled, 1=Gzip, 2=Brotli
    // =====================================================

    [TestCase(0, WebGLCompressionFormat.Disabled)]
    [TestCase(1, WebGLCompressionFormat.Gzip)]
    [TestCase(2, WebGLCompressionFormat.Brotli)]
    public void ConvertToCompressionFormat_Maps_StoredValue_To_ActualEnum(int stored, WebGLCompressionFormat expected)
    {
        Assert.AreEqual(expected, AITBuildInitializer.ConvertToCompressionFormat(stored));
    }

    [Test]
    public void ConvertToCompressionFormat_Auto_Falls_Back_To_Default()
    {
        Assert.AreEqual(AITDefaultSettings.GetDefaultCompressionFormat(),
            AITBuildInitializer.ConvertToCompressionFormat(-1));
    }

    // =====================================================
    // PlayerSettings 라운드트립 — WebGL 타깃이 변환 결과를 실제로 수용하는지
    // =====================================================

    [Test]
    public void SetManagedStrippingLevel_RoundTrips_ConvertedMinimal()
    {
#if UNITY_6000_0_OR_NEWER
        var original = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
        try
        {
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,
                AITBuildInitializer.ConvertToManagedStrippingLevel(1));
            Assert.AreEqual(ManagedStrippingLevel.Minimal,
                PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL));
        }
        finally
        {
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, original);
        }
#else
        var original = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);
        try
        {
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL,
                AITBuildInitializer.ConvertToManagedStrippingLevel(1));
            Assert.AreEqual(ManagedStrippingLevel.Minimal,
                PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL));
        }
        finally
        {
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, original);
        }
#endif
    }
}
