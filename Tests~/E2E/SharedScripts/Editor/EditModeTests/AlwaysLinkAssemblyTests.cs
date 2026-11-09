// -----------------------------------------------------------------------
// AlwaysLinkAssemblyTests.cs - SDK 런타임 어셈블리의 IL2CPP 링크 보존 검증
// 게임 코드가 직접 참조하지 않는 SDK 어셈블리가 [RuntimeInitializeOnLoadMethod] 훅을 가지면
// [assembly: AlwaysLinkAssembly] 가 있어야 IL2CPP 링커가 어셈블리째 지우지 않는다.
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Scripting;

[TestFixture]
public class AlwaysLinkAssemblyTests
{
    // 게임 코드가 API 를 직접 부르는 어셈블리. 게임이 참조하므로 링커가 따라 들어간다.
    private static readonly string[] ReferencedByGames = { "AppsInTossSDK" };

    [Test]
    public void Helpers_HasAlwaysLinkAssembly()
    {
        var helpers = typeof(AppsInToss.AITVersion).Assembly;
        Assert.AreEqual("AppsInToss.Helpers", helpers.GetName().Name);
        Assert.IsNotNull(helpers.GetCustomAttribute<AlwaysLinkAssemblyAttribute>(),
            "AppsInToss.Helpers 에 [assembly: AlwaysLinkAssembly] 가 없으면 게임 빌드에서 자동 훅이 어셈블리째 지워집니다");
    }

    [Test]
    public void RuntimeAssembliesWithBootHooks_HaveAlwaysLinkAssembly()
    {
        var missing = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name.StartsWith("AppsInToss", StringComparison.Ordinal))
            .Where(a => !a.GetName().Name.Contains("Editor") && !a.GetName().Name.Contains("Test"))
            .Where(a => !ReferencedByGames.Contains(a.GetName().Name))
            .Where(HasBootHook)
            .Where(a => a.GetCustomAttribute<AlwaysLinkAssemblyAttribute>() == null)
            .Select(a => a.GetName().Name)
            .ToArray();
        Assert.IsEmpty(missing, "RuntimeInitializeOnLoadMethod 훅이 있는데 AlwaysLinkAssembly 가 없는 어셈블리: " + string.Join(", ", missing));
    }

    private static bool HasBootHook(Assembly a)
    {
        Type[] types;
        try { types = a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        return types.Any(t => t.GetMethods(all).Any(m => m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() != null));
    }
}
