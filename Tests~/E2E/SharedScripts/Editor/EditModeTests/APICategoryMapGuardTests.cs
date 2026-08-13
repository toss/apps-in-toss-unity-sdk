// -----------------------------------------------------------------------
// APICategoryMapGuardTests.cs - EditMode 가드 테스트
// Level 0: APIParameterInspector의 APICategoryMap이 실제 AIT API 표면과
// 어긋나지 않는지(누락/유령 항목) 그리고 CategoryOrder와 일관되는지 검증.
//
// 재발 방지 목적:
// - SDK 업데이트로 신규 API가 추가됐는데 카테고리 매핑을 깜빡하면
//   InteractiveAPITester UI에서 "Other" 폴백으로 조용히 새는 문제를 막는다.
// - SDK API가 제거됐는데 매핑에 남아있는 죽은 항목을 막는다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
public class APICategoryMapGuardTests
{
    /// <summary>
    /// APIParameterInspector 내부의 private static APICategoryMap 필드를
    /// 리플렉션으로 가져온다. (SDKAPIReflectionTests.cs의 리플렉션 패턴 참고)
    /// </summary>
    private static Dictionary<string, string> GetCategoryMap()
    {
        var field = typeof(APIParameterInspector).GetField(
            "APICategoryMap", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "APIParameterInspector.APICategoryMap 필드를 찾을 수 없습니다");
        return (Dictionary<string, string>)field.GetValue(null);
    }

    /// <summary>
    /// APIParameterInspector.GetAllAPIMethods()가 사용하는 것과 동일한 기준
    /// (public static, IsSpecialName 제외, 반환 타입이 Task/Awaitable/Action)
    /// 으로 뽑힌 실제 API 메서드 이름 목록. GetAllAPIMethods()를 그대로
    /// 재사용하여 필터 기준이 프로덕션 코드와 어긋나지 않도록 한다.
    /// </summary>
    private static List<string> GetRealApiMethodNames()
    {
        var methods = APIParameterInspector.GetAllAPIMethods();
        Assert.IsNotEmpty(methods,
            "AIT API 메서드를 하나도 찾지 못했습니다 - AppsInToss.AIT 타입 로드에 실패했을 수 있습니다");
        return methods.Select(m => m.Name).Distinct().ToList();
    }

    // =====================================================
    // (a) 실제 API 전수 -> APICategoryMap에 명시 항목 존재 (Other 폴백 아님)
    // =====================================================

    [Test]
    public void Every_Real_API_Method_Has_Explicit_CategoryMap_Entry()
    {
        var realNames = GetRealApiMethodNames();
        var categoryMap = GetCategoryMap();

        var missing = realNames.Where(name => !categoryMap.ContainsKey(name)).ToList();

        Assert.IsEmpty(missing,
            "다음 API가 APICategoryMap에 명시적으로 매핑되어 있지 않아 'Other' 폴백으로 샙니다. " +
            "APIParameterInspector.cs의 APICategoryMap에 카테고리를 추가하세요: " +
            string.Join(", ", missing));
    }

    // =====================================================
    // (b) APICategoryMap의 모든 키 -> 실제 표면에 존재 (죽은 항목 재유입 방지)
    //
    // 이 방향만 sdk_version_override(하위호환 빌드)에서 성립하지 않는다. override는
    // package.json 버전을 덮고 Runtime/SDK를 그 web-framework 버전으로 재생성하므로,
    // 그 이후 추가된 API의 매핑이 전부 "유령키"로 보인다. 매핑은 최신 표면 기준으로
    // 유지하는 것이 맞으므로 테스트를 최신 빌드에서만 켠다.
    // (a)/(c)/(d)는 실제 표면 또는 매핑 내부 정합성만 보므로 전 버전에서 그대로 유효하다.
    // =====================================================

    [Test]
    public void Every_CategoryMap_Key_Exists_In_Real_API_Surface()
    {
#if !AIT_SDK_3_0_OR_LATER
        Assert.Ignore("sdk_version_override 하위호환 빌드에서는 신규 API 매핑이 유령키로 보이는 것이 정상");
#else
        var realNames = GetRealApiMethodNames();
        var categoryMap = GetCategoryMap();

        var ghosts = categoryMap.Keys.Where(key => !realNames.Contains(key)).ToList();

        Assert.IsEmpty(ghosts,
            "다음 항목은 APICategoryMap에는 있지만 실제 AIT API 표면(GetAllAPIMethods() 기준)에는 " +
            "존재하지 않는 죽은 항목입니다. SDK에서 제거되었거나 이름이 바뀌었을 수 있으니 " +
            "APIParameterInspector.cs의 APICategoryMap에서 제거하거나 이름을 수정하세요: " +
            string.Join(", ", ghosts));
#endif
    }

    // =====================================================
    // (c) APICategoryMap의 모든 카테고리 값 -> CategoryOrder에 존재
    // =====================================================

    [Test]
    public void Every_CategoryMap_Value_Exists_In_CategoryOrder()
    {
        var categoryMap = GetCategoryMap();
        var categoryOrder = APIParameterInspector.CategoryOrder;

        var missingCategories = categoryMap.Values.Distinct()
            .Where(category => !categoryOrder.Contains(category))
            .ToList();

        Assert.IsEmpty(missingCategories,
            "다음 카테고리는 APICategoryMap 값으로 쓰이지만 CategoryOrder 배열에는 없어 " +
            "UI 정렬 순서에서 누락됩니다. APIParameterInspector.cs의 CategoryOrder에 추가하세요: " +
            string.Join(", ", missingCategories));
    }

    // =====================================================
    // (d) CategoryOrder의 모든 카테고리 -> APICategoryMap 값으로 최소 1회 이상 사용
    // (역방향: 어떤 API도 매핑되지 않은 죽은 카테고리 문자열 검출)
    // =====================================================

    [Test]
    public void Every_CategoryOrder_Entry_Has_At_Least_One_Mapped_API()
    {
        var categoryMap = GetCategoryMap();
        var categoryOrder = APIParameterInspector.CategoryOrder;
        var usedCategories = new HashSet<string>(categoryMap.Values);

        var deadCategories = categoryOrder.Where(category => !usedCategories.Contains(category)).ToList();

        Assert.IsEmpty(deadCategories,
            "다음 카테고리는 CategoryOrder 배열에 있지만 APICategoryMap에서 실제로 사용하는 API가 " +
            "하나도 없는 죽은 카테고리입니다. UI에 빈 섹션으로 남으니 CategoryOrder에서 제거하거나 " +
            "해당 카테고리를 쓰는 API 매핑을 추가하세요: " +
            string.Join(", ", deadCategories));
    }
}
