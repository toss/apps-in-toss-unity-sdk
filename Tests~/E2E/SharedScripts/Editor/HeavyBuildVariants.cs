using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using AppsInToss;

/// <summary>
/// perf heavy 픽스처 변형(variant) 레지스트리.
///
/// 환경 변수 <c>AIT_PERF_VARIANT</c>(쉼표 구분 이름 목록)에 적힌 변형을 <see cref="HeavyBuildRunner"/> 가 순서대로 적용한다.
/// perf.yml 의 workflow_dispatch 입력 <c>variant</c> 가 unity-build.yml 을 거쳐 이 환경 변수로 전달된다.
/// 변수가 비어 있으면 아무것도 하지 않는다(기존 동작과 동일).
///
/// === 적용 시점 ===
/// 헤비 콘텐츠 생성과 posture 레버 적용이 끝난 뒤, E2EBuildRunner.BuildWithSDK() 호출 직전이다.
/// 변형은 SDK 설정(<see cref="AITEditorScriptObject"/>)뿐 아니라 PlayerSettings/QualitySettings 도 바꿀 수 있고,
/// HeavyGen 아래에 에셋을 추가할 수도 있다(추가한 에셋은 변형이 직접 AssetDatabase.ImportAsset 해야 한다).
/// 변형이 던지면 빌드는 exit 1 로 실패한다(오탈자·미등록 이름도 마찬가지 — 조용히 기본 빌드가 나가는 것을 막는다).
///
/// === 변형 등록 (후속 배치가 자기 파일에서 한다) ===
/// 방법 1 (권장, 파일 간 충돌 없음): 정적 메서드에 속성을 붙인다. 이 어셈블리(AppsInTossTestScripts.Editor)나 그것을 참조하는
///   에디터 어셈블리 어디든 괜찮다. TypeCache 로 처음 조회할 때 한 번 수집한다.
/// <code>
/// public static class AudioFixtureVariants
/// {
///     [HeavyVariant("audio-bgm-3min")]
///     public static void ApplyBgm3Min(AITEditorScriptObject config) { /* ... */ }
/// }
/// </code>
/// 방법 2: <see cref="Register"/> 를 직접 호출한다(테스트나 [InitializeOnLoad] 정적 생성자에서).
///
/// 이름은 대소문자를 구분하지 않으며 같은 이름을 두 번 등록하면 예외를 던진다(속성·Register 모두).
/// 내장 변형: <c>all0</c>(모바일 런타임 최적화 레버 전부 0 — A/B 비교용 대조군).
/// </summary>
public static class HeavyBuildVariants
{
    /// <summary>변형 목록을 전달하는 환경 변수 이름.</summary>
    public const string EnvVar = "AIT_PERF_VARIANT";

    /// <summary>모든 모바일 런타임 최적화 레버를 끄는 내장 변형 이름.</summary>
    public const string All0 = "all0";

    private static readonly Dictionary<string, Action<AITEditorScriptObject>> Registry =
        new Dictionary<string, Action<AITEditorScriptObject>>(StringComparer.OrdinalIgnoreCase);

    private static bool _attributesCollected;

    static HeavyBuildVariants()
    {
        Registry[All0] = ApplyAll0;
    }

    /// <summary>변형을 등록한다. 이미 있는 이름이면 <see cref="InvalidOperationException"/>.</summary>
    public static void Register(string name, Action<AITEditorScriptObject> apply)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("변형 이름이 비어 있습니다.", nameof(name));
        if (apply == null) throw new ArgumentNullException(nameof(apply));

        string key = name.Trim();
        if (Registry.ContainsKey(key))
        {
            throw new InvalidOperationException($"[heavy] 이미 등록된 변형 이름입니다: {key}");
        }
        Registry[key] = apply;
    }

    /// <summary>테스트용: 등록을 제거한다(내장 all0 은 제거할 수 없다). 제거했으면 true.</summary>
    public static bool Unregister(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(), All0, StringComparison.OrdinalIgnoreCase)) return false;
        return Registry.Remove(name.Trim());
    }

    public static bool IsRegistered(string name)
    {
        CollectAttributedVariants();
        return !string.IsNullOrWhiteSpace(name) && Registry.ContainsKey(name.Trim());
    }

    /// <summary>등록된 이름 전체(정렬). 속성 기반 변형 포함.</summary>
    public static IReadOnlyList<string> RegisteredNames()
    {
        CollectAttributedVariants();
        return Registry.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>쉼표(또는 공백·세미콜론) 구분 문자열을 이름 목록으로 나눈다. 중복은 첫 등장만 남긴다.</summary>
    public static List<string> ParseNames(string raw)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return names;

        foreach (string part in raw.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string name = part.Trim();
            if (name.Length == 0) continue;
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
        }
        return names;
    }

    /// <summary>
    /// <see cref="EnvVar"/> 에 적힌 변형을 적용하고 적용한 이름 목록을 돌려준다(변수가 비면 빈 목록).
    /// 미등록 이름이 있으면 아무것도 적용하기 전에 <see cref="ArgumentException"/>.
    /// </summary>
    public static List<string> ApplyFromEnvironment(AITEditorScriptObject config)
    {
        return Apply(ParseNames(Environment.GetEnvironmentVariable(EnvVar)), config);
    }

    /// <summary>
    /// 이름 순서대로 변형을 적용한다. 미등록 이름이 하나라도 있으면 아무것도 적용하기 전에 예외를 던진다.
    /// </summary>
    public static List<string> Apply(IEnumerable<string> names, AITEditorScriptObject config)
    {
        var list = names == null ? new List<string>() : names.ToList();
        if (list.Count == 0) return list;
        if (config == null) throw new ArgumentNullException(nameof(config));

        CollectAttributedVariants();

        var unknown = list.Where(n => !Registry.ContainsKey(n)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"[heavy] 등록되지 않은 변형: {string.Join(", ", unknown)} (등록됨: {string.Join(", ", RegisteredNames())})");
        }

        foreach (string name in list)
        {
            Registry[name](config);
            Debug.Log($"[heavy] variant 적용: {name}");
        }
        return list;
    }

    /// <summary>
    /// 내장 변형 all0: 모바일 런타임 최적화 신규 tri-state 레버 전부를 0(비활성)으로 두고,
    /// 같은 부류의 기존 런타임 최적화(긴 외부화 클립 압축 재생)도 끈다. "최적화 전부 OFF" 대조군 빌드용이다.
    /// 콘텐츠·로드타임 최적화(오디오 스트리밍, 텍스처 crunch 등)는 건드리지 않는다 — 비교 대상이 런타임 레버이기 때문이다.
    /// </summary>
    public static void ApplyAll0(AITEditorScriptObject config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        config.webglAntialiasOpt = 0;
        config.webglContextRecovery = 0;
        config.frameRateCap = 0;
        config.adaptiveFrameRate = 0;
        config.mobileLifecycle = 0;
        config.memoryTelemetry = 0;
        config.exactDataBody = 0;
        config.releaseConsumedData = 0;
        config.audioForceCompressedPlayback = 0;
        config.lowMemoryTier = 0;
        config.pageCacheDeferredPut = 0;
        config.audioStreamingCompressedPlayback = 0;   // 기존 레버: 외부화된 긴 클립 압축 재생
    }

    // [HeavyVariant] 속성이 붙은 정적 메서드를 한 번 수집한다.
    private static void CollectAttributedVariants()
    {
        if (_attributesCollected) return;
        _attributesCollected = true;

        foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<HeavyVariantAttribute>())
        {
            if (!method.IsStatic)
            {
                throw new InvalidOperationException($"[heavy] [HeavyVariant] 메서드는 static 이어야 합니다: {method.DeclaringType?.FullName}.{method.Name}");
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(AITEditorScriptObject))
            {
                throw new InvalidOperationException(
                    $"[heavy] [HeavyVariant] 메서드 시그니처는 (AITEditorScriptObject) 여야 합니다: {method.DeclaringType?.FullName}.{method.Name}");
            }

            var apply = (Action<AITEditorScriptObject>)Delegate.CreateDelegate(typeof(Action<AITEditorScriptObject>), method);
            foreach (HeavyVariantAttribute attr in method.GetCustomAttributes(typeof(HeavyVariantAttribute), false))
            {
                Register(attr.Name, apply);
            }
        }
    }
}

/// <summary>
/// perf heavy 픽스처 변형으로 등록할 정적 메서드에 붙인다. 메서드는
/// <c>public static void X(AITEditorScriptObject config)</c> 형태여야 한다. 자세한 사용법은 <see cref="HeavyBuildVariants"/> 참조.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class HeavyVariantAttribute : Attribute
{
    public string Name { get; }

    public HeavyVariantAttribute(string name)
    {
        Name = name;
    }
}
