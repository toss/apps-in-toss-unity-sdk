using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AppsInToss.Editor
{
    /// <summary>
    /// WebGL 빌드에 남은 엔진 모듈과 포함 사유를 콘솔에 남긴다(StrippingInfo).
    ///
    /// 빈 씬에서도 UI Toolkit(UIElements) 모듈이 남으면 첫 프레임 전에 관리 코드 초기화
    /// (UIElementsInitialization.RegisterBuiltInPropertyBags 등)가 돈다. 어떤 모듈이 왜 남았는지
    /// 보여야 다음 레버를 고를 수 있어서 남기는 순수 진단이다. 빌드를 바꾸거나 실패시키지 않는다.
    /// StrippingInfo 접근 API가 Unity 버전마다 달라 리플렉션으로 찾는다.
    /// </summary>
    internal class AITEngineModuleReport : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10001;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                if (report == null || report.summary.platform != BuildTarget.WebGL)
                    return;

                var info = FindStrippingInfo(report);
                if (info == null)
                {
                    AITLog.Info("[AIT-EngineModules] StrippingInfo가 없습니다(엔진 코드 스트리핑 꺼짐 또는 미지원 버전).");
                    return;
                }

                var modules = info.includedModules?.ToList() ?? new List<string>();
                AITLog.Info($"[AIT-EngineModules] 포함된 엔진 모듈 {modules.Count}개: {string.Join(", ", modules)}");
                foreach (var module in modules)
                {
                    var reasons = info.GetReasonsForIncluding(module)?.Take(5).ToList();
                    if (reasons != null && reasons.Count > 0)
                        AITLog.Info($"[AIT-EngineModules]   {module} ← {string.Join(" | ", reasons)}");
                }
            }
            catch (Exception e)
            {
                AITLog.Warning(
                    $"[AIT-EngineModules] 진단 실행 중 예외 발생(무시 — 빌드 산출물에는 영향 없음): {e.Message}",
                    sentryCapture: false);
            }
        }

        private static StrippingInfo FindStrippingInfo(BuildReport report)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var prop = typeof(BuildReport).GetProperty("strippingInfo", flags);
            if (prop?.GetValue(report) is StrippingInfo fromProp)
                return fromProp;

            foreach (var method in typeof(BuildReport).GetMethods(flags))
            {
                if (method.Name != "GetAppendices" || !method.IsGenericMethodDefinition || method.GetParameters().Length != 0)
                    continue;
                if (method.MakeGenericMethod(typeof(StrippingInfo)).Invoke(report, null) is IEnumerable list)
                {
                    foreach (var item in list)
                        if (item is StrippingInfo found)
                            return found;
                }
            }
            return null;
        }
    }
}
