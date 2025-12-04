using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using AppsInToss;

/// <summary>
/// SDK API 메서드를 리플렉션으로 분석하고 파라미터 정보를 추출하는 유틸리티
/// </summary>
public static class APIParameterInspector
{
    /// <summary>
    /// SDK에서 사용하는 모든 enum 타입 레지스트리 (IL2CPP 호환)
    /// </summary>
    private static readonly Dictionary<Type, string[]> EnumRegistry = new Dictionary<Type, string[]>
    {
        { typeof(HapticFeedbackType), new[] { "TickWeak", "Tap", "TickMedium", "SoftMedium", "BasicWeak", "BasicMedium", "Success", "Error", "Wiggle", "Confetti" } },
        { typeof(Accuracy), new[] { "Lowest", "Low", "Balanced", "High", "Highest", "BestForNavigation" } },
        { typeof(NetworkStatus), new[] { "OFFLINE", "WIFI", "_2G", "_3G", "_4G", "_5G", "WWAN", "UNKNOWN" } },
        { typeof(PermissionAccess), new[] { "Read", "Write", "Access" } }
    };

    /// <summary>
    /// Enum 이름 목록 반환 (레지스트리 우선, 폴백으로 리플렉션)
    /// </summary>
    public static string[] GetEnumNames(Type enumType)
    {
        if (EnumRegistry.TryGetValue(enumType, out var names))
            return names;

        // 폴백: 런타임 리플렉션 (Editor에서만 동작 보장)
        return Enum.GetNames(enumType);
    }

    /// <summary>
    /// Enum 인덱스로 값 반환
    /// </summary>
    public static object GetEnumValueByIndex(Type enumType, int index)
    {
        var names = GetEnumNames(enumType);
        if (index >= 0 && index < names.Length)
        {
            return Enum.Parse(enumType, names[index]);
        }
        return Enum.GetValues(enumType).GetValue(0);
    }

    /// <summary>
    /// 타입의 공개 필드 목록 반환 (IL2CPP 호환)
    /// </summary>
    public static FieldInfo[] GetPublicFields(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Instance);
    }

    /// <summary>
    /// 필드가 Action/Delegate 타입인지 확인 (UI 입력 불가)
    /// </summary>
    public static bool IsCallbackField(FieldInfo field)
    {
        return typeof(Delegate).IsAssignableFrom(field.FieldType) ||
               field.FieldType.Name.StartsWith("Action") ||
               field.FieldType.Name.StartsWith("Func");
    }

    /// <summary>
    /// 타입이 단순 타입인지 확인 (재사용을 위한 static 버전)
    /// </summary>
    public static bool IsSimpleType(Type type)
    {
        return type == typeof(string) ||
               type == typeof(int) ||
               type == typeof(double) ||
               type == typeof(float) ||
               type == typeof(bool);
    }

    /// <summary>
    /// 결과 객체를 구조화된 형태로 변환 (필드명: 값 형식)
    /// </summary>
    public static string FormatResultStructured(object result, int maxDepth = 5)
    {
        if (result == null) return "null";

        var sb = new StringBuilder();
        FormatObjectRecursive(result, sb, 0, maxDepth);
        return sb.ToString();
    }

    private static void FormatObjectRecursive(object obj, StringBuilder sb, int indent, int maxDepth)
    {
        if (obj == null)
        {
            sb.Append("null");
            return;
        }

        if (indent > maxDepth)
        {
            sb.Append("...");
            return;
        }

        var type = obj.GetType();
        var indentStr = new string(' ', indent * 2);

        // 단순 타입
        if (IsSimpleType(type))
        {
            if (type == typeof(string))
                sb.Append($"\"{obj}\"");
            else
                sb.Append(obj.ToString());
            return;
        }

        // Enum
        if (type.IsEnum)
        {
            sb.Append(obj.ToString());
            return;
        }

        // 배열
        if (type.IsArray)
        {
            var array = (Array)obj;
            if (array.Length == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.AppendLine("[");
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append($"{indentStr}  [{i}]: ");
                FormatObjectRecursive(array.GetValue(i), sb, indent + 1, maxDepth);
                if (i < array.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.Append($"{indentStr}]");
            return;
        }

        // 복합 객체
        var fields = GetPublicFields(type);
        if (fields.Length == 0)
        {
            sb.Append(obj.ToString());
            return;
        }

        sb.AppendLine("{");
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var value = field.GetValue(obj);
            sb.Append($"{indentStr}  {field.Name}: ");
            FormatObjectRecursive(value, sb, indent + 1, maxDepth);
            if (i < fields.Length - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.Append($"{indentStr}}}");
    }

    /// <summary>
    /// 카테고리 표시 순서
    /// </summary>
    public static readonly string[] CategoryOrder = {
        "Authentication",
        "Payment",
        "SystemInfo",
        "Location",
        "Permission",
        "GameCenter",
        "Share",
        "Media",
        "Clipboard",
        "Device",
        "Navigation",
        "Events",
        "Certificate",
        "Visibility",
        "Other"
    };

    /// <summary>
    /// API 이름 -> 카테고리 매핑 (IL2CPP에서 리플렉션 대신 사용)
    /// </summary>
    private static readonly Dictionary<string, string> APICategoryMap = new Dictionary<string, string>
    {
        // Authentication
        { "AppLogin", "Authentication" },
        { "GetIsTossLoginIntegratedService", "Authentication" },

        // Payment
        { "CheckoutPayment", "Payment" },

        // SystemInfo
        { "GetDeviceId", "SystemInfo" },
        { "GetLocale", "SystemInfo" },
        { "GetNetworkStatus", "SystemInfo" },
        { "GetOperationalEnvironment", "SystemInfo" },
        { "GetPlatformOS", "SystemInfo" },
        { "GetSchemeUri", "SystemInfo" },
        { "GetTossAppVersion", "SystemInfo" },

        // Location
        { "GetCurrentLocation", "Location" },
        { "StartUpdateLocation", "Location" },

        // Permission
        { "GetPermission", "Permission" },
        { "OpenPermissionDialog", "Permission" },
        { "RequestPermission", "Permission" },

        // GameCenter
        { "GetGameCenterGameProfile", "GameCenter" },
        { "GetUserKeyForGame", "GameCenter" },
        { "GrantPromotionRewardForGame", "GameCenter" },
        { "OpenGameCenterLeaderboard", "GameCenter" },
        { "SubmitGameCenterLeaderBoardScore", "GameCenter" },

        // Share
        { "ContactsViral", "Share" },
        { "FetchContacts", "Share" },
        { "GetTossShareLink", "Share" },
        { "Share", "Share" },

        // Media
        { "FetchAlbumPhotos", "Media" },
        { "OpenCamera", "Media" },
        { "SaveBase64Data", "Media" },

        // Clipboard
        { "GetClipboardText", "Clipboard" },
        { "SetClipboardText", "Clipboard" },

        // Device
        { "GenerateHapticFeedback", "Device" },
        { "SetDeviceOrientation", "Device" },
        { "SetIosSwipeGestureEnabled", "Device" },
        { "SetScreenAwakeMode", "Device" },
        { "SetSecureScreen", "Device" },

        // Navigation
        { "CloseView", "Navigation" },
        { "OpenURL", "Navigation" },

        // Events
        { "EventLog", "Events" },

        // Certificate
        { "AppsInTossSignTossCert", "Certificate" },

        // Visibility
        { "OnVisibilityChange", "Visibility" },
    };

    /// <summary>
    /// API 이름으로 카테고리 가져오기
    /// </summary>
    private static string GetCategoryByName(string apiName)
    {
        if (APICategoryMap.TryGetValue(apiName, out string category))
        {
            return category;
        }
        return "Other";
    }

    /// <summary>
    /// AIT 클래스의 모든 공개 정적 메서드 정보 반환
    /// </summary>
    public static List<APIMethodInfo> GetAllAPIMethods()
    {
        var methods = new List<APIMethodInfo>();

        // AppsInToss.AIT 타입 찾기 (AppsInTossSDK 어셈블리에서)
        Type aitType = Type.GetType("AppsInToss.AIT, AppsInTossSDK");
        if (aitType == null)
        {
            // Fallback: 어셈블리에서 직접 검색
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                aitType = assembly.GetType("AppsInToss.AIT");
                if (aitType != null) break;
            }
        }

        if (aitType == null)
        {
            Debug.LogError("[APIParameterInspector] Cannot find AppsInToss.AIT type");
            return methods;
        }

        Debug.Log($"[APIParameterInspector] AIT Assembly: {aitType.Assembly.GetName().Name}");

        // AIT와 같은 어셈블리에서 모든 타입 나열 (디버깅용)
        var allTypesInAssembly = aitType.Assembly.GetTypes();
        Debug.Log($"[APIParameterInspector] Total types in AIT assembly: {allTypesInAssembly.Length}");
        foreach (var t in allTypesInAssembly.Where(t => t.Namespace == "AppsInToss").Take(10))
        {
            Debug.Log($"[APIParameterInspector]   Type: {t.FullName}");
        }

        // APICategoryAttribute 타입 찾기 (AIT와 같은 어셈블리에서)
        Type categoryAttrType = aitType.Assembly.GetType("AppsInToss.APICategoryAttribute");
        Debug.Log($"[APIParameterInspector] APICategoryAttribute type: {(categoryAttrType != null ? categoryAttrType.FullName : "NOT FOUND")}");

        if (categoryAttrType == null)
        {
            // 모든 어셈블리에서 찾아보기
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes().Where(t => t.Name == "APICategoryAttribute").ToArray();
                    foreach (var t in types)
                    {
                        Debug.Log($"[APIParameterInspector] Found APICategoryAttribute in: {assembly.GetName().Name} -> {t.FullName}");
                        categoryAttrType = t; // 찾으면 사용
                    }
                }
                catch (Exception ex)
                {
                    // 일부 어셈블리는 GetTypes()가 실패할 수 있음
                    Debug.LogWarning($"[APIParameterInspector] Cannot get types from {assembly.GetName().Name}: {ex.Message}");
                }
            }

            if (categoryAttrType == null)
            {
                Debug.LogWarning("[APIParameterInspector] Cannot find APICategoryAttribute type, all APIs will be categorized as 'Other'");
            }
        }

        // 공개 정적 메서드만 가져오기
        var publicMethods = aitType.GetMethods(BindingFlags.Public | BindingFlags.Static);

        foreach (var method in publicMethods)
        {
            // 특수 메서드 제외 (get_, set_, add_, remove_ 등)
            if (method.IsSpecialName) continue;

            // Task 또는 Task<T> 반환 타입만 포함
            if (!method.ReturnType.Name.StartsWith("Task")) continue;

            // 카테고리 추출 (APICategoryAttribute에서)
            string category = "Other";
            if (categoryAttrType != null)
            {
                var attrs = method.GetCustomAttributes(categoryAttrType, false);
                if (attrs.Length > 0)
                {
                    var categoryProp = categoryAttrType.GetProperty("Category");
                    if (categoryProp != null)
                    {
                        category = categoryProp.GetValue(attrs[0]) as string ?? "Other";
                    }
                }
                else
                {
                    // 첫 번째 메서드에서만 디버그 출력
                    if (methods.Count == 0)
                    {
                        var allAttrs = method.GetCustomAttributes(false);
                        Debug.Log($"[APIParameterInspector] {method.Name} has {allAttrs.Length} attributes:");
                        foreach (var attr in allAttrs)
                        {
                            Debug.Log($"  - {attr.GetType().FullName}");
                        }
                    }
                }
            }

            // IL2CPP/WebGL에서는 reflection으로 attribute를 읽을 수 없으므로
            // 항상 hardcoded 매핑 사용
            string finalCategory = GetCategoryByName(method.Name);

            var methodInfo = new APIMethodInfo
            {
                Name = method.Name,
                Method = method,
                Category = finalCategory,
                Parameters = method.GetParameters()
                    .Select(p => new APIParameterInfo
                    {
                        Name = p.Name,
                        Type = p.ParameterType,
                        IsOptional = p.IsOptional,
                        DefaultValue = p.DefaultValue
                    })
                    .ToList(),
                ReturnType = method.ReturnType
            };

            methods.Add(methodInfo);
        }

        // 이름순 정렬
        methods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return methods;
    }

    /// <summary>
    /// API 목록을 카테고리별로 그룹핑
    /// </summary>
    public static Dictionary<string, List<APIMethodInfo>> GroupByCategory(List<APIMethodInfo> methods)
    {
        var grouped = new Dictionary<string, List<APIMethodInfo>>();

        // 먼저 모든 API를 카테고리별로 분류
        foreach (var method in methods)
        {
            string category = method.Category ?? "Other";
            if (!grouped.ContainsKey(category))
            {
                grouped[category] = new List<APIMethodInfo>();
            }
            grouped[category].Add(method);
        }

        // 카테고리 순서에 따라 정렬된 Dictionary 반환
        var sorted = new Dictionary<string, List<APIMethodInfo>>();
        foreach (var category in CategoryOrder)
        {
            if (grouped.ContainsKey(category))
            {
                sorted[category] = grouped[category];
            }
        }

        // CategoryOrder에 없는 카테고리들 추가
        foreach (var kvp in grouped)
        {
            if (!sorted.ContainsKey(kvp.Key))
            {
                sorted[kvp.Key] = kvp.Value;
            }
        }

        return sorted;
    }

    /// <summary>
    /// 파라미터 타입에 따라 기본값 생성
    /// </summary>
    public static object GetDefaultValueForType(Type type)
    {
        if (type == typeof(string))
            return "";
        if (type == typeof(int))
            return 0;
        if (type == typeof(double) || type == typeof(float))
            return 0.0;
        if (type == typeof(bool))
            return false;
        if (type.IsClass)
            return null;
        if (type.IsValueType)
            return Activator.CreateInstance(type);

        return null;
    }

    /// <summary>
    /// JSON 문자열에서 파라미터 객체 생성
    /// </summary>
    public static object ParseParameterFromJson(string json, Type type)
    {
        try
        {
            if (type == typeof(string))
                return json;
            if (type == typeof(int))
                return int.Parse(json);
            if (type == typeof(double))
                return double.Parse(json);
            if (type == typeof(float))
                return float.Parse(json);
            if (type == typeof(bool))
                return bool.Parse(json);

            // 복잡한 객체는 JsonUtility로 역직렬화
            return JsonUtility.FromJson(json, type);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[APIParameterInspector] Failed to parse JSON: {ex.Message}");
            return GetDefaultValueForType(type);
        }
    }

    /// <summary>
    /// 객체를 JSON 문자열로 변환
    /// </summary>
    public static string SerializeToJson(object obj)
    {
        if (obj == null)
            return "null";

        Type type = obj.GetType();

        if (type == typeof(string))
            return (string)obj;
        if (type.IsPrimitive || type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return obj.ToString();
        if (type == typeof(bool))
            return obj.ToString().ToLower();

        // 복잡한 객체는 JsonUtility로 직렬화
        try
        {
            return JsonUtility.ToJson(obj, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[APIParameterInspector] Failed to serialize: {ex.Message}");
            return obj.ToString();
        }
    }
}

/// <summary>
/// API 메서드 정보
/// </summary>
public class APIMethodInfo
{
    public string Name;
    public MethodInfo Method;
    public string Category;
    public List<APIParameterInfo> Parameters;
    public Type ReturnType;

    public bool HasParameters => Parameters != null && Parameters.Count > 0;

    public string GetDisplayName()
    {
        string paramStr = HasParameters
            ? $"({string.Join(", ", Parameters.Select(p => $"{GetSimpleTypeName(p.Type)} {p.Name}"))})"
            : "()";
        return $"{Name}{paramStr}";
    }

    public string GetReturnTypeName()
    {
        if (ReturnType.IsGenericType)
        {
            var genericArgs = ReturnType.GetGenericArguments();
            if (genericArgs.Length > 0)
                return GetSimpleTypeName(genericArgs[0]);
        }
        return "void";
    }

    private string GetSimpleTypeName(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(void)) return "void";
        return type.Name;
    }
}

/// <summary>
/// API 파라미터 정보
/// </summary>
public class APIParameterInfo
{
    public string Name;
    public Type Type;
    public bool IsOptional;
    public object DefaultValue;

    public bool IsSimpleType =>
        Type == typeof(string) ||
        Type == typeof(int) ||
        Type == typeof(double) ||
        Type == typeof(float) ||
        Type == typeof(bool);
}
