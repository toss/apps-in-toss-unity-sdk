// -----------------------------------------------------------------------
// GeneratedApiCatalogGuardTests.cs - EditMode 가드 테스트
// Level 0: 생성기가 내보낸 하네스 카탈로그(AITApiCatalog.g.cs)가 컴파일된
// AppsInToss 표면과 어긋나지 않는지 검증한다.
//
// 카탈로그는 sdk-runtime-generator가 Runtime/SDK와 함께 쓴다. 손으로 고치는
// 표가 없으므로, 이 테스트는 web-framework 버전이 바뀌어도 그대로 통과해야 한다.
// 실패한다면 카탈로그를 고치는 대신 생성기(pnpm generate)를 확인한다.
//
// 확인하는 것:
// - 모든 API 메서드에 카탈로그 항목이 있다 (UI에서 "Other" 폴백으로 새지 않는다)
// - 카탈로그 키는 모두 실제 메서드다 (제거된 API가 남지 않는다)
// - 카탈로그의 모든 카테고리가 순서 목록에 있고, 순서 목록에 죽은 카테고리가 없다
// - AppsInToss 네임스페이스의 모든 enum이 표에 있고 멤버 이름이 Enum.GetNames와 같다
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class GeneratedApiCatalogGuardTests
{
    /// <summary>
    /// APIParameterInspector.GetAllAPIMethods()가 쓰는 것과 같은 기준
    /// (public static, IsSpecialName 제외, 반환 타입이 Task/Awaitable/Action)
    /// 으로 뽑은 실제 API 메서드 이름 목록. GetAllAPIMethods()를 그대로
    /// 재사용해 필터 기준이 프로덕션 코드와 어긋나지 않게 한다.
    /// </summary>
    private static List<string> GetRealApiMethodNames()
    {
        var methods = APIParameterInspector.GetAllAPIMethods();
        Assert.IsNotEmpty(methods,
            "AIT API 메서드를 하나도 찾지 못했습니다 - AppsInToss.AIT 타입 로드에 실패했을 수 있습니다");
        return methods.Select(m => m.Name).Distinct().ToList();
    }

    /// <summary>
    /// AIT와 같은 어셈블리의 AppsInToss 네임스페이스 최상위 public enum 전부.
    /// </summary>
    private static List<Type> GetRuntimeEnums()
    {
        var aitType = Type.GetType("AppsInToss.AIT, AppsInTossSDK");
        if (aitType == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                aitType = assembly.GetType("AppsInToss.AIT");
                if (aitType != null) break;
            }
        }
        Assert.IsNotNull(aitType, "AppsInToss.AIT 타입을 찾을 수 없습니다");

        return aitType.Assembly.GetTypes()
            .Where(t => t.IsEnum && t.IsPublic && !t.IsNested && t.Namespace == "AppsInToss")
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }

    // =====================================================
    // (a) 실제 API 전수 -> 카탈로그에 명시 항목 존재 (Other 폴백 아님)
    // =====================================================

    [Test]
    public void Every_Real_API_Method_Has_Catalog_Entry()
    {
        var realNames = GetRealApiMethodNames();

        var missing = realNames.Where(name => !AITApiCatalog.ApiCategories.ContainsKey(name)).ToList();

        Assert.IsEmpty(missing,
            "다음 API가 생성된 카탈로그(AITApiCatalog.g.cs)에 없습니다. " +
            "생성기 출력이 오래됐을 수 있으니 'cd sdk-runtime-generator~ && pnpm generate'를 다시 실행하세요: " +
            string.Join(", ", missing));
    }

    // =====================================================
    // (b) 카탈로그의 모든 키 -> 실제 표면에 존재 (죽은 항목 방지)
    // =====================================================

    [Test]
    public void Every_Catalog_Key_Exists_In_Real_API_Surface()
    {
        var realNames = new HashSet<string>(GetRealApiMethodNames());

        var ghosts = AITApiCatalog.ApiCategories.Keys.Where(key => !realNames.Contains(key)).ToList();

        Assert.IsEmpty(ghosts,
            "다음 항목은 카탈로그에는 있지만 실제 AIT API 표면(GetAllAPIMethods() 기준)에는 없습니다. " +
            "카탈로그와 Runtime/SDK가 서로 다른 시점에 생성됐을 수 있으니 pnpm generate를 다시 실행하세요: " +
            string.Join(", ", ghosts));
    }

    // =====================================================
    // (c) 카탈로그의 모든 카테고리 값 -> 순서 목록에 존재
    // =====================================================

    [Test]
    public void Every_Catalog_Category_Exists_In_CategoryOrder()
    {
        var order = AITApiCatalog.CategoryOrder;

        var missing = AITApiCatalog.ApiCategories.Values.Distinct()
            .Where(category => !order.Contains(category))
            .ToList();

        Assert.IsEmpty(missing,
            "다음 카테고리는 API에 쓰이지만 CategoryOrder에 없어 UI 정렬에서 누락됩니다: " +
            string.Join(", ", missing));
    }

    // =====================================================
    // (d) 순서 목록의 모든 카테고리 -> 최소 1개 API가 사용 (빈 섹션 방지)
    // =====================================================

    [Test]
    public void Every_CategoryOrder_Entry_Has_At_Least_One_API()
    {
        var used = new HashSet<string>(AITApiCatalog.ApiCategories.Values);

        var dead = AITApiCatalog.CategoryOrder.Where(category => !used.Contains(category)).ToList();

        Assert.IsEmpty(dead,
            "다음 카테고리는 CategoryOrder에 있지만 쓰는 API가 없습니다: " + string.Join(", ", dead));
        Assert.AreEqual(AITApiCatalog.CategoryOrder.Length, AITApiCatalog.CategoryOrder.Distinct().Count(),
            "CategoryOrder에 중복 항목이 있습니다");
    }

    // =====================================================
    // (e) 실제 enum 전수 -> 표에 존재
    // =====================================================

    [Test]
    public void Every_Runtime_Enum_Is_In_Catalog()
    {
        var enums = GetRuntimeEnums();
        Assert.IsNotEmpty(enums, "AppsInToss 네임스페이스에서 enum을 하나도 찾지 못했습니다");

        var missing = enums.Where(t => !AITApiCatalog.EnumNames.ContainsKey(t)).Select(t => t.Name).ToList();

        Assert.IsEmpty(missing,
            "다음 enum이 생성된 카탈로그의 EnumNames에 없습니다. pnpm generate를 다시 실행하세요: " +
            string.Join(", ", missing));
    }

    // =====================================================
    // (f) 표의 enum 멤버 이름 == Enum.GetNames (선언 순서 포함)
    // =====================================================

    [Test]
    public void Catalog_Enum_Names_Match_Enum_GetNames()
    {
        var runtimeEnums = new HashSet<Type>(GetRuntimeEnums());
        var problems = new List<string>();

        foreach (var kv in AITApiCatalog.EnumNames)
        {
            if (!runtimeEnums.Contains(kv.Key))
            {
                problems.Add(kv.Key.Name + " (AppsInToss 네임스페이스의 enum이 아님)");
                continue;
            }

            var expected = Enum.GetNames(kv.Key);
            if (!expected.SequenceEqual(kv.Value))
            {
                problems.Add(kv.Key.Name + ": 카탈로그=[" + string.Join(",", kv.Value) +
                             "] 실제=[" + string.Join(",", expected) + "]");
            }
        }

        Assert.IsEmpty(problems, "카탈로그의 enum 멤버 이름이 실제와 다릅니다:\n" + string.Join("\n", problems));
    }

    // =====================================================
    // (g) GetEnumNames는 카탈로그 enum과 미등록 enum 모두에서 동작
    // =====================================================

    [Test]
    public void GetEnumNames_Falls_Back_To_Reflection_For_Unknown_Enum()
    {
        Assert.AreEqual(Enum.GetNames(typeof(DayOfWeek)), APIParameterInspector.GetEnumNames(typeof(DayOfWeek)));

        var first = AITApiCatalog.EnumNames.First();
        Assert.AreEqual(first.Value, APIParameterInspector.GetEnumNames(first.Key));
    }
}
