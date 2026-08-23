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
//
// 레버 목록은 원래 세 곳(ResetWebGLOptimizationDefaults / CountModifiedWebGLSettings /
// 이 파일의 WebGLLeverFields)에 손으로 중복돼 있어 세 번 드리프트했다
// (playerPrefsPersistence·meshCompression 이 목록에서, textureStreaming·textureStreamDownscale·
// fontStreaming 이 Count 에서 누락). 그래서 아래 두 테스트는 목록을 손으로 대조하는 대신
// ResetWebGLOptimizationDefaults 를 단일 기준으로 삼아 나머지 두 곳을 리플렉션으로 유도·대조한다:
//   · WebGLLeverFields_MatchesFieldsActuallyRestoredByReset  — Reset 커버리지 ↔ 목록 (양방향)
//   · CountModifiedWebGLSettings_DetectsEveryTriStateLever   — Reset 대상 tri-state ↔ Count

using System;
using System.Collections.Generic;
using System.Linq;
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
        // (dataCaching 은 UI 숨김 베타 설정이라 리셋 대상에서 의도적으로 제외 — #1002 정책.
        //  화면에 보이지 않는 값을 복원 버튼이 조용히 덮어쓰면 안 되므로 이 목록에도 넣지 않는다.)
        // (brotliRecompress 도 같은 사유로 제외 — UI 미노출 숨김 스파이크 설정이라 "기본값 복원"이
        //  조용히 덮어쓰면 안 되므로 ResetWebGLOptimizationDefaults 대상 및 이 목록에서 제외한다.)
        "memorySize", "threadsSupport", "nameFilesAsHashes", "firstInteractiveLog",
        "playerPrefsPersistence",
        // 페이지 캐시 / warm / 네이티브 프리페치
        "pageCache", "pageCacheName", "warmManifest", "warmPage", "nativeAssetSource",
        // 오디오 스트리밍
        "audioStreaming", "audioStreamingMinBytes", "audioStreamingDirs",
        // 스트림 사본 저비트레이트 재인코딩
        "audioStreamTranscode", "audioStreamTranscodeBitrateKbps", "audioStreamTranscodeMinSourceKbps",
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
        // Mesh 압축
        "meshCompression",
        // 폰트 CJK subset
        "fontSubset", "fontSubsetTargetPaths", "fontSubsetUnicodeRanges",
        "fontSubsetExtraRanges", "fontSubsetExcludeTargetPaths", "fontSubsetLanguages",
        // 폰트 subset — 선택 언어 lazy 확장(실험적)
        "fontSubsetLazyLanguages",
        // 대형 텍스처 스트리밍
        "textureStreaming", "textureStreamingMinBytes", "textureStreamingDirs",
        "textureStreamingExcludeDirs", "textureStreamingMaxConcurrent",
        // 스트림 사본 다운스케일
        "textureStreamDownscale", "textureStreamDownscaleMaxSize",
        // 스트림 PNG 무손실 재압축
        "textureStreamRecompress",
        // 불투명 스트림 PNG → JPEG 전환
        "textureStreamJpeg", "textureStreamJpegQuality",
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

    /// <summary>
    /// WebGLLeverFields 가 ResetWebGLOptimizationDefaults 의 실제 커버리지와 정확히 일치하는지 검증한다.
    ///
    /// 위 테스트는 "목록에 있는 필드가 복원되는가"만 보므로 반대 방향(= Reset 은 복원하는데 목록에
    /// 없는 필드)을 놓친다. 실제로 playerPrefsPersistence·meshCompression 이 그 틈으로 두 번 새어나갔다.
    /// 그래서 여기서는 목록을 쓰지 않고, AITEditorScriptObject 의 public 필드를 전부 오염시킨 뒤
    /// Reset 이 되돌려 놓은 필드 집합을 ★실험으로 유도★해 목록과 양방향 대조한다.
    /// (Reset 이 손대지 않는 비-WebGL 필드는 오염값을 유지하므로 자동으로 제외된다.)
    /// </summary>
    [Test]
    public void WebGLLeverFields_MatchesFieldsActuallyRestoredByReset()
    {
        var fresh = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        var dirty = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            // (1) 오염 가능한 모든 public 인스턴스 필드를 기본값과 다른 값으로 오염.
            var mutated = new List<FieldInfo>();
            foreach (FieldInfo f in typeof(AITEditorScriptObject)
                         .GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!TryMakeDifferent(f.FieldType, f.GetValue(fresh), out object dirtyValue))
                {
                    continue; // 스칼라/문자열이 아닌 필드(중첩 설정 클래스 등)는 레버가 아니므로 대상 외.
                }

                f.SetValue(dirty, dirtyValue);
                mutated.Add(f);
            }

            // 선언 목록의 필드가 전부 오염 대상에 포함돼야 유도 결과가 유효하다
            // (지원하지 않는 타입의 레버가 생기면 이 단언이 먼저 깨져 알려준다).
            var mutatedNames = mutated.Select(f => f.Name).ToList();
            foreach (string name in WebGLLeverFields)
            {
                CollectionAssert.Contains(mutatedNames, name,
                    $"'{name}' 를 오염시키지 못했다 — TryMakeDifferent 가 이 필드 타입을 지원하도록 확장하라.");
            }

            // (2) 복원 후, 기본값으로 되돌아온 필드 = Reset 이 실제로 커버하는 레버 집합.
            AITConfigurationWindow.ResetWebGLOptimizationDefaults(dirty);

            string[] restored = mutated
                .Where(f => Equals(f.GetValue(fresh), f.GetValue(dirty)))
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            string[] declared = WebGLLeverFields
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(declared, restored,
                "WebGLLeverFields 와 ResetWebGLOptimizationDefaults 의 커버리지가 어긋난다.\n" +
                $"  목록에만 있음(= Reset 누락): {string.Join(", ", declared.Except(restored))}\n" +
                $"  Reset 에만 있음(= 목록 누락): {string.Join(", ", restored.Except(declared))}\n" +
                "레버를 추가/삭제할 때는 두 곳을 함께 갱신해야 한다. " +
                "의도적으로 리셋 대상에서 뺀 UI 미노출 설정(dataCaching·brotliRecompress)은 " +
                "Reset 이 손대지 않으므로 양쪽 모두에 없어야 정상이다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fresh);
            UnityEngine.Object.DestroyImmediate(dirty);
        }
    }

    /// <summary>
    /// CountModifiedWebGLSettings("N개 설정이 기본값에서 변경됨" 배지)가 Reset 대상 tri-state 레버를
    /// 하나도 빠뜨리지 않는지 검증한다.
    ///
    /// Count 에서 레버가 빠지면 사용자가 그 값을 바꿔도 배지가 뜨지 않고, 배지에 딸린
    /// "모든 WebGL 설정 기본값으로 복원" 버튼 자체가 나타나지 않는다(조용한 기능 상실).
    /// 실제로 textureStreaming·textureStreamDownscale·fontStreaming 이 이 상태로 방치돼 있었다.
    /// 대상은 목록을 손으로 적지 않고 (선언 기본 -1) + (AITDefaultSettings.GetDefault{Pascal} 존재)
    /// 규약으로 유도하므로, 같은 규약을 따르는 신규 레버는 자동으로 이 가드에 편입된다.
    /// </summary>
    [Test]
    public void CountModifiedWebGLSettings_DetectsEveryTriStateLever()
    {
        var fresh = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            Assert.AreEqual(0, AITConfigurationWindow.CountModifiedWebGLSettings(fresh),
                "갓 만든 설정은 '기본값에서 변경됨' 이 0건이어야 한다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fresh);
        }

        int covered = 0;
        foreach (string name in WebGLLeverFields)
        {
            FieldInfo f = ResolveField(name);
            if (f.FieldType != typeof(int)) continue;

            MethodInfo getDefault = typeof(AITDefaultSettings).GetMethod(
                "GetDefault" + char.ToUpperInvariant(name[0]) + name.Substring(1),
                BindingFlags.Public | BindingFlags.Static);
            if (getDefault == null || getDefault.ReturnType != typeof(bool)) continue;

            var probe = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                if ((int)f.GetValue(probe) != -1) continue; // tri-state(자동=-1) 레버만 대상
                covered++;

                // 자동 기준 실효값과 ★반대되는 명시 값★으로 설정 → 정확히 1건으로 집계돼야 한다.
                bool defaultValue = (bool)getDefault.Invoke(null, null);
                f.SetValue(probe, defaultValue ? 0 : 1);

                Assert.AreEqual(1, AITConfigurationWindow.CountModifiedWebGLSettings(probe),
                    $"'{name}' 를 기본값과 다르게 설정했는데 CountModifiedWebGLSettings 가 집계하지 않는다. " +
                    "CountModifiedWebGLSettings 에 이 레버의 비교를 추가하라.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        Assert.Greater(covered, 0, "tri-state 레버를 한 개도 찾지 못했다면 유도 규약이 깨진 것이다.");
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
        if (!TryMakeDifferent(t, def, out object different))
        {
            Assert.Fail($"미지원 레버 필드 타입: {t} — 테스트의 TryMakeDifferent 를 확장하라.");
        }
        return different;
    }

    // MakeDifferent 의 실패 허용 판(版). 필드 전수 스윕에서는 레버가 될 수 없는 타입
    // (중첩 설정 클래스 등)을 조용히 건너뛰어야 하므로 Assert 대신 false 를 돌려준다.
    private static bool TryMakeDifferent(Type t, object def, out object different)
    {
        if (t == typeof(int))
        {
            different = (int)def == 99999 ? 12345 : 99999;
            return true;
        }
        if (t == typeof(long))
        {
            different = (long)def == 99999L ? 12345L : 99999L;
            return true;
        }
        if (t == typeof(float))
        {
            different = (float)def == 99999f ? 12345f : 99999f;
            return true;
        }
        if (t == typeof(bool))
        {
            different = !(bool)def;
            return true;
        }
        if (t == typeof(string))
        {
            different = (string)def == "AIT_DIRTY_SENTINEL" ? "AIT_DIRTY_SENTINEL_2" : "AIT_DIRTY_SENTINEL";
            return true;
        }

        different = null;
        return false;
    }
}
