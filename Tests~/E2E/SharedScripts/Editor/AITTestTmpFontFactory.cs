using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;

/// <summary>
/// TMP_FontAsset(TextMeshPro) 리플렉션 생성 공용 유틸.
///
/// 배경: TMP_FontAsset 을 리플렉션으로 생성하는 관용구는 <see cref="DeployProbeBuildRunner"/>
/// (TryGenerateProbeFontAsset/EnsureTmpEssentialResources/TryAddSubAsset/TryAddAtlasTextures)에
/// 이미 구현돼 있다. 이 파일은 그 관용구를 그대로 옮기되 로그 프리픽스를 인자화해
/// <see cref="HeavyBuildRunner"/> 에서 재사용 가능하게 만든 것이다. DeployProbeBuildRunner 자체는
/// 2022.3 deploy-probe leg 회귀 위험을 피하기 위해 이관하지 않는다(TODO 후속 — 3중 복제 정리).
///
/// SharedScripts 의 Runtime/Editor asmdef 는 Unity.TextMeshPro 를 참조하지 않는다(5개
/// SampleUnityProject 컴파일 호환 유지). 따라서 TMP 타입은 전부 리플렉션으로만 접근한다.
/// </summary>
internal static class AITTestTmpFontFactory
{
    /// <summary>TMPro.TMP_FontAsset 타입 해석. TMP 미설치면 null + 경고.</summary>
    internal static Type ResolveFontAssetType(string logPrefix)
    {
        Type t = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
        if (t == null)
        {
            Debug.LogWarning($"{logPrefix} ⚠ TMP(Unity.TextMeshPro) 미설치 — TMP_FontAsset 생성을 건너뜁니다.");
        }
        return t;
    }

    /// <summary>
    /// TMP Essential Resources 를 1회 임포트(headless 안전, CI 결정성, DeployProbeBuildRunner.
    /// EnsureTmpEssentialResources 와 동일 관용구 — marker 기반 멱등 skip)하고, SDF 셰이더 가용성까지
    /// 확인한다. CreateFontAsset(Font) 은 내부에서 TMP SDF 셰이더로 머티리얼을 만드는데, Essential
    /// Resources 임포트가 아직 반영되지 않아 셰이더가 없으면 new Material(null) 로 즉사하므로 호출측이
    /// 이 게이트를 통과한 뒤에만 CreateDynamicFontAsset 을 호출해야 한다.
    /// </summary>
    internal static bool EnsureEssentials(Type fontAssetType, string logPrefix)
    {
        try
        {
            const string marker = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(marker) == null)
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(fontAssetType.Assembly);
                if (pkg == null || string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    Debug.LogWarning($"{logPrefix} ⚠ TMP 패키지 경로 해석 실패 — Essential Resources 임포트를 건너뜁니다.");
                    return false;
                }

                string unityPackagePath = Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (!File.Exists(unityPackagePath))
                {
                    Debug.LogWarning($"{logPrefix} ⚠ TMP Essential Resources.unitypackage 없음: {unityPackagePath} — 건너뜁니다.");
                    return false;
                }

                // ImportPackage(비대화식)는 배치 모드에서도 비동기로 남아 후속 Shader.Find 가 임포트
                // 완료 전에 null 을 본다(Refresh 로도 unitypackage 임포트는 플러시되지 않음 — DeployProbe
                // 실측). Unity 내부의 동기 API ImportPackageImmediately 를 리플렉션으로 우선 시도하고,
                // 없으면 비동기+Refresh 폴백.
                var importImmediately = typeof(AssetDatabase).GetMethod(
                    "ImportPackageImmediately",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string) },
                    null);
                if (importImmediately != null)
                {
                    importImmediately.Invoke(null, new object[] { unityPackagePath });
                }
                else
                {
                    AssetDatabase.ImportPackage(unityPackagePath, false); // false = 다이얼로그 없이(headless 안전).
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"{logPrefix} TMP Essential Resources 임포트 완료 (동기 API: {importImmediately != null}).");
            }

            if (Shader.Find("TextMeshPro/Distance Field") == null &&
                Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                Debug.LogWarning($"{logPrefix} ⚠ TMP SDF 셰이더 미가용(Essential Resources 임포트 미반영?) — TMP_FontAsset 생성을 건너뜁니다.");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{logPrefix} ⚠ TMP Essential Resources 임포트 예외(무시): {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// srcOtfAssetPath(.otf) → destAssetPath(.asset) 로 Dynamic TMP_FontAsset 을 생성한다. 실패 시 null.
    /// 호출 전 <see cref="EnsureEssentials"/> 가 true 를 반환했어야 한다(셰이더 가용 전제).
    /// </summary>
    internal static string CreateDynamicFontAsset(Type t, string srcOtfAssetPath, string destAssetPath, string logPrefix)
    {
        try
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(srcOtfAssetPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"{logPrefix} ⚠ 소스 Font 로드 실패: {srcOtfAssetPath} — TMP_FontAsset 생성 스킵.");
                return null;
            }

            // 버전 간 가장 안정적인 단일 오버로드만 사용한다: CreateFontAsset(Font). 다중 파라미터
            // 오버로드(atlas 크기/렌더모드 등)는 TMP 버전별 시그니처가 달라 리플렉션 안정성이 낮다
            // (DeployProbeBuildRunner.TryGenerateProbeFontAsset 과 동일 판단).
            var createMethod = t.GetMethod(
                "CreateFontAsset",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(Font) },
                null);
            if (createMethod == null)
            {
                Debug.LogWarning($"{logPrefix} ⚠ TMP_FontAsset.CreateFontAsset(Font) 오버로드를 찾지 못함 — 스킵.");
                return null;
            }

            object fontAssetObj = createMethod.Invoke(null, new object[] { sourceFont });
            var mainAsset = fontAssetObj as UnityEngine.Object;
            if (mainAsset == null)
            {
                Debug.LogWarning($"{logPrefix} ⚠ TMP_FontAsset 생성 실패(null 반환) — 스킵.");
                return null;
            }

            // atlasPopulationMode = Dynamic — 런타임 즉석 래스터화, static 베이킹(15MB급 CJK 폰트에서
            // 빌드 시간·아틀라스 수십 MB 폭발) 회피. 글리프를 하나도 굽지 않으므로 실제 텍스처는 0×0.
            var atlasModeProp = t.GetProperty("atlasPopulationMode");
            if (atlasModeProp != null && atlasModeProp.CanWrite)
            {
                object dynamicValue = Enum.Parse(atlasModeProp.PropertyType, "Dynamic");
                atlasModeProp.SetValue(fontAssetObj, dynamicValue);
            }

            AssetDatabase.CreateAsset(mainAsset, destAssetPath);

            // material/atlas 텍스처가 있으면 서브에셋으로 동봉(없거나 버전별 API 차이가 있어도 치명적이지 않음).
            TryAddSubAsset(t, fontAssetObj, mainAsset, "material");
            TryAddAtlasTextures(t, fontAssetObj, mainAsset);

            // 추가 방어선: 생성 직후 아틀라스를 명시적으로 0×0 클리어한다. TMP 버전에 따라 빈 아틀라스가
            // 기본 크기(1024² 등)로 직렬화되면 .data 에 폰트당 1MB 가까이 얹혀 fontStreaming 의 defer
            // 서사가 오염된다 — LogAtlasFootprint 가 실측치를 남긴다.
            TryClearFontAssetData(t, fontAssetObj);

            EditorUtility.SetDirty(mainAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(destAssetPath, ImportAssetOptions.ForceSynchronousImport);

            LogAtlasFootprint(logPrefix, destAssetPath, t, fontAssetObj);

            return destAssetPath;
        }
        catch (Exception e)
        {
            // TMP 버전별 API 차이로 인한 실패는 이 폰트 1개만 스킵하고 나머지는 계속 진행한다
            // (DeployProbeBuildRunner 와 동일한 관용적 실패 처리 철학). TargetInvocationException 은
            // 메시지가 무의미하므로 inner 를 끝까지 벗겨 실제 원인을 남긴다.
            Exception root = e;
            while (root is TargetInvocationException tie && tie.InnerException != null)
            {
                root = tie.InnerException;
            }
            Debug.LogWarning($"{logPrefix} ⚠ TMP_FontAsset 생성 예외({srcOtfAssetPath} → {destAssetPath}): " +
                $"{root.GetType().Name}: {root.Message}\n{root.StackTrace}");
            return null;
        }
    }

    /// <summary>ClearFontAssetData(bool) → 없으면 ClearFontAssetData() → 둘 다 없으면 skip. 아틀라스를
    /// 0×0 으로 보장하는 방어선(위 CreateDynamicFontAsset 참조).</summary>
    private static void TryClearFontAssetData(Type t, object obj)
    {
        try
        {
            var withArg = t.GetMethod(
                "ClearFontAssetData",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(bool) },
                null);
            if (withArg != null)
            {
                withArg.Invoke(obj, new object[] { true });
                return;
            }

            var noArg = t.GetMethod("ClearFontAssetData", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            noArg?.Invoke(obj, null);
        }
        catch
        {
            // 무시 — 클리어 실패는 LogAtlasFootprint 의 dimension 로그로 사후 드러난다(치명적이지 않음).
        }
    }

    /// <summary>atlasTextures[0].width/height + destAssetPath 파일 바이트를 로그로 남긴다(계측 —
    /// 아틀라스가 의도치 않게 0×0 이 아닌 크기로 직렬화되는지 실측 가능하게 한다).</summary>
    private static void LogAtlasFootprint(string logPrefix, string assetPath, Type t, object obj)
    {
        try
        {
            string dimension = "?";
            var prop = t.GetProperty("atlasTextures");
            if (prop?.GetValue(obj) is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item is Texture2D tex && tex != null)
                    {
                        dimension = $"{tex.width}x{tex.height}";
                        break;
                    }
                }
            }

            long assetBytes = 0;
            string fullPath = Path.GetFullPath(assetPath);
            if (File.Exists(fullPath))
            {
                assetBytes = new FileInfo(fullPath).Length;
            }

            Debug.Log($"{logPrefix} TMP_FontAsset 아틀라스 계측: {assetPath} atlas={dimension}, asset={assetBytes / 1024.0:F1}KB");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{logPrefix} ⚠ 아틀라스 계측 로그 실패(무시): {e.Message}");
        }
    }

    private static void TryAddSubAsset(Type fontAssetType, object fontAssetObj, UnityEngine.Object mainAsset, string propertyName)
    {
        try
        {
            var prop = fontAssetType.GetProperty(propertyName);
            var sub = prop?.GetValue(fontAssetObj) as UnityEngine.Object;
            if (sub != null && AssetDatabase.GetAssetPath(sub) != AssetDatabase.GetAssetPath(mainAsset))
            {
                AssetDatabase.AddObjectToAsset(sub, mainAsset);
            }
        }
        catch
        {
            // 무시 — 서브에셋 동봉 실패는 치명적이지 않음(TMP 버전별 API 차이 방어).
        }
    }

    private static void TryAddAtlasTextures(Type fontAssetType, object fontAssetObj, UnityEngine.Object mainAsset)
    {
        try
        {
            var prop = fontAssetType.GetProperty("atlasTextures");
            if (prop?.GetValue(fontAssetObj) is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item is UnityEngine.Object tex && tex != null)
                    {
                        AssetDatabase.AddObjectToAsset(tex, mainAsset);
                    }
                }
            }
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>GameObject 에 TextMeshProUGUI 를 부착하고 font 프로퍼티만 대입한다(text 프로퍼티는
    /// 절대 세팅하지 않는다 — 에디터에서 문자열을 넣으면 즉시 글리프를 래스터화해 Dynamic 아틀라스가
    /// 부풀고 그대로 직렬화되어 .data/번들에 MB 단위가 얹힌다). TMP 미설치/타입 미발견/예외 시 false.</summary>
    internal static bool TryAttachEmptyTmpText(GameObject go, string fontAssetPath, string logPrefix)
    {
        try
        {
            Type tmpUguiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Type fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            if (tmpUguiType == null || fontAssetType == null)
            {
                return false;
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath(fontAssetPath, fontAssetType);
            if (fontAsset == null)
            {
                Debug.LogWarning($"{logPrefix} ⚠ TMP_FontAsset 로드 실패: {fontAssetPath} — 프로브 부착 스킵.");
                return false;
            }

            var component = go.AddComponent(tmpUguiType);
            if (component == null)
            {
                return false;
            }

            var fontProp = tmpUguiType.GetProperty("font");
            fontProp?.SetValue(component, fontAsset);

            // ★ text 프로퍼티는 절대 세팅하지 않는다(위 요약 참조) — 기본값(빈 문자열) 유지.
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{logPrefix} ⚠ TextMeshProUGUI 부착 예외(무시): {e.Message}");
            return false;
        }
    }
}
