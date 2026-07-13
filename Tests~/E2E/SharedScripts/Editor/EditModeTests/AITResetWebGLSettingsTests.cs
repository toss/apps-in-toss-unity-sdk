// AITResetWebGLSettingsTests
//
// ResetWebGLOptimizationDefaults("모든 WebGL 설정 기본값으로 복원" 버튼의 실제 로직) 회귀 가드.
//
// 배경(감사 #10): 기존 ResetWebGLSettings 는 일부 레버의 master 토글·스코프 디렉터리·수치
// 파라미터를 초기화하지 않았다(특히 textureStreaming/fontStreaming master 자체가 누락,
// audio/crunch/clamp/texStream 의 *Dirs 도 누락). 그 결과 "기본값 복원"을 눌러도 stale 스코프/
// 임계값이 남아 재활성 시 좁은 범위만 처리되거나(효과 축소) master 가 켜진 채로 남았다.
//
// 이 테스트는 fresh 인스턴스(=선언 기본값)와 비교하는 방식으로:
//   (1) 모든 레버 필드를 기본값과 다른 값으로 오염시키고,
//   (2) ResetWebGLOptimizationDefaults 로 복원한 뒤,
//   (3) 전 필드가 fresh 기본값과 동일한지 단언한다.
// 누락된 필드가 있으면(과거 버그처럼) 그 필드가 오염값을 유지해 RED 가 된다. 신규 레버를
// 추가하면 WebGLLeverFields 목록과 ResetWebGLOptimizationDefaults 양쪽을 갱신해야 한다.

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITResetWebGLSettingsTests
{
    // ResetWebGLOptimizationDefaults 가 선언 기본값으로 되돌려야 하는 WebGL 레버 필드 전체.
    private static readonly string[] WebGLLeverFields =
    {
        // 엔진 / 전송
        "memorySize", "threadsSupport", "dataCaching", "nameFilesAsHashes", "firstInteractiveLog",
        // 페이지 캐시 / warm / 네이티브 프리페치
        "pageCache", "pageCacheName", "warmManifest", "warmPage", "nativeAssetSource",
        // 오디오 스트리밍
        "audioStreaming", "audioStreamingMinBytes", "audioStreamingDirs",
        // 오디오 재인코딩
        "audioReencode", "audioReencodeQuality", "audioReencodeMinBytes",
        "audioReencodeDirs", "audioReencodeExcludeDirs",
        // 텍스처 crunch
        "textureCrunch", "textureCrunchMaxSize", "textureCrunchQuality",
        "textureCrunchAtlas", "textureCrunchAtlasMaxSize", "textureCrunchDirs",
        // 텍스처 크기 클램프
        "textureSizeClamp", "textureClampMaxSize", "textureClampMinBytes",
        "textureClampDirs", "textureClampExcludeDirs",
        // ASTC 블록 에스컬레이션
        "astcBlockEscalation", "astcBlockSize", "astcBlockMaxSize",
        "astcBlockAtlas", "astcBlockDirs", "astcBlockExcludeDirs",
        // 폰트 CJK subset
        "fontSubset", "fontSubsetTargetPaths", "fontSubsetUnicodeRanges",
        // 대형 텍스처 스트리밍
        "textureStreaming", "textureStreamingMinBytes", "textureStreamingDirs",
        "textureStreamingExcludeDirs", "textureStreamingMaxConcurrent",
        // 스트림 사본 다운스케일
        "textureStreamDownscale", "textureStreamDownscaleMaxSize",
        // 대형 폰트 deferral
        "fontStreaming", "fontStreamingTargetPaths", "fontStreamingMaxConcurrent",
    };

    [Test]
    public void ResetWebGLOptimizationDefaults_RestoresEveryLeverToFreshDefault()
    {
        var fresh = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        var dirty = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            // (1) 모든 레버 필드를 선언 기본값과 다른 값으로 오염.
            foreach (var name in WebGLLeverFields)
            {
                FieldInfo f = ResolveField(name);
                object def = f.GetValue(fresh);
                f.SetValue(dirty, MakeDifferent(f.FieldType, def));
            }

            // 오염이 실제로 일어났는지 사전 단언(테스트가 조용히 무의미해지는 것 방지).
            foreach (var name in WebGLLeverFields)
            {
                FieldInfo f = ResolveField(name);
                Assert.AreNotEqual(f.GetValue(fresh), f.GetValue(dirty),
                    $"사전조건: '{name}' 가 오염되어 기본값과 달라야 함(테스트 유효성).");
            }

            // (2) 복원.
            AITConfigurationWindow.ResetWebGLOptimizationDefaults(dirty);

            // (3) 전 필드가 fresh 기본값과 동일해야 함.
            foreach (var name in WebGLLeverFields)
            {
                FieldInfo f = ResolveField(name);
                Assert.AreEqual(f.GetValue(fresh), f.GetValue(dirty),
                    $"'{name}' 가 ResetWebGLOptimizationDefaults 후 선언 기본값으로 복원되어야 한다. " +
                    "불일치는 reset 누락(레버 추가 후 reset 미갱신) 또는 잘못된 기본값을 의미한다.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fresh);
            UnityEngine.Object.DestroyImmediate(dirty);
        }
    }

    [Test]
    public void ResetWebGLOptimizationDefaults_NullConfig_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => AITConfigurationWindow.ResetWebGLOptimizationDefaults(null));
    }

    private static FieldInfo ResolveField(string name)
    {
        FieldInfo f = typeof(AITEditorScriptObject).GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, $"AITEditorScriptObject 에 public 필드 '{name}' 가 존재해야 함");
        return f;
    }

    // 선언 기본값과 ★확실히 다른★ 값을 만든다(타입별). Range 속성은 인스펙터 전용이라
    // 리플렉션 직접 대입에는 영향 없음 → 범위 밖 값(99999)이어도 필드는 그대로 보관한다.
    private static object MakeDifferent(Type t, object def)
    {
        if (t == typeof(int))
        {
            return (int)def == 99999 ? 12345 : 99999;
        }
        if (t == typeof(long))
        {
            return (long)def == 99999L ? 12345L : 99999L;
        }
        if (t == typeof(float))
        {
            return (float)def == 99999f ? 12345f : 99999f;
        }
        if (t == typeof(bool))
        {
            return !(bool)def;
        }
        if (t == typeof(string))
        {
            return (string)def == "AIT_DIRTY_SENTINEL" ? "AIT_DIRTY_SENTINEL_2" : "AIT_DIRTY_SENTINEL";
        }
        Assert.Fail($"미지원 레버 필드 타입: {t} — 테스트의 MakeDifferent 를 확장하라.");
        return null;
    }
}
