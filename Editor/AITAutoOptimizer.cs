using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 에디터 로드 시 물리 백엔드(PhysX)와 UI Toolkit 모듈을 감지 결과에 따라 대화상자 없이 자동으로 끈다.
    ///
    /// 프로젝트 파일(DynamicsManager.asset, Packages/manifest.json)을 바꾸므로 빌드 도중이 아니라 에디터 로드 때만 돈다.
    /// 세션당 한 번이며, 배치 모드에서는 돌지 않는다(CI는 빌드 스크립트가 두 설정을 명시적으로 제어한다).
    /// 끄려면 AITConfig의 physicsBackendAutoDisable / uiToolkitAutoRemove를 0으로 둔다.
    /// 사용자가 고급 설정에서 '켜기'를 누르면 해당 필드가 0으로 바뀌어 자동 적용이 되돌리지 않는다.
    /// </summary>
    [InitializeOnLoad]
    internal static class AITAutoOptimizer
    {
        private const string SessionKey = "AIT_AutoOptimizerRan";
        private const string ConfigPath = "Assets/AppsInToss/Editor/AITConfig.asset";

        static AITAutoOptimizer()
        {
            AITPhysicsBackendAdvisor.RecordLaunchBackend();
            // Editor 가 도메인을 완전히 준비한 뒤 실행하도록 한 프레임 지연.
            EditorApplication.delayCall += OnLoad;
        }

        internal enum Decision { Skip, Apply, OptOut }

        /// <summary>
        /// 순수 판단 함수. fieldValue는 tri-state(-1 자동=켜짐, 1 켜짐, 0 끔)이고,
        /// recommended는 해당 advisor의 Analyze().Recommended(적용 가능, 켜져 있음, 사용 흔적 없음, 검사 실패 아님)이다.
        /// autoAppliedBefore는 프로젝트에 저장된 "예전에 자동 적용함" 표시, leverEnabled는 지금 기능이 다시 켜져 있는지다.
        /// 자동 적용한 적이 있는데 다시 켜져 있으면 사용자가 직접 켠 것이므로 OptOut(필드를 0으로)을 돌려준다.
        /// </summary>
        internal static Decision Decide(int fieldValue, bool isBatchMode, bool configExists, bool recommended,
            bool alreadyRanThisSession, bool autoAppliedBefore, bool leverEnabled)
        {
            if (fieldValue == 0 || isBatchMode || !configExists || alreadyRanThisSession)
                return Decision.Skip;
            if (autoAppliedBefore && leverEnabled)
                return Decision.OptOut;
            return recommended ? Decision.Apply : Decision.Skip;
        }

        internal static bool ShouldAutoApply(int fieldValue, bool isBatchMode, bool configExists, bool recommended, bool alreadyRanThisSession)
        {
            return Decide(fieldValue, isBatchMode, configExists, recommended, alreadyRanThisSession, false, false) == Decision.Apply;
        }

        private const double RetryIntervalSec = 1.0;
        private const double RetryGiveUpSec = 60.0;
        private static double retryStart;
        private static double nextTry;

        private static void OnLoad()
        {
            retryStart = EditorApplication.timeSinceStartup;
            nextTry = 0;
            EditorApplication.update += Tick;
        }

        // 컴파일/임포트 중에는 최대 60초 동안 1초 간격으로만 다시 확인한다.
        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextTry)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                nextTry = now + RetryIntervalSec;
                if (now - retryStart > RetryGiveUpSec)
                    EditorApplication.update -= Tick;
                return;
            }
            EditorApplication.update -= Tick;
            Run();
        }

        private static void Run()
        {
            try
            {
                if (SessionState.GetBool(SessionKey, false))
                    return;
                if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                    return;
                SessionState.SetBool(SessionKey, true);

                var config = AssetDatabase.LoadAssetAtPath<AITEditorScriptObject>(ConfigPath);
                bool configExists = config != null;
                bool batch = Application.isBatchMode;
                if (!configExists || batch)
                    return;

                var phys = AITPhysicsBackendAdvisor.Analyze();
                switch (Decide(config.physicsBackendAutoDisable, batch, configExists, phys.Recommended, false,
                    config.physicsBackendAutoApplied == 1, phys.Applicable && phys.PhysXEnabled))
                {
                    case Decision.Apply:
                        if (AITPhysicsBackendAdvisor.ApplyBackendSilently(AITPhysicsBackendAdvisor.NoneBackendId))
                        {
                            OptOut(c => c.physicsBackendAutoApplied = 1);
                            AITLog.Info(
                                "[AIT-Physics] 3D 물리를 쓰는 흔적이 없어 물리 백엔드(PhysX)를 자동으로 껐습니다(wasm 약 0.8MB(brotli) 감소). " +
                                "에디터를 다시 시작하면 적용됩니다. 되돌리려면 AIT Configuration > 고급 설정 > 'PhysX 켜기'를 누르거나 " +
                                "AITConfig의 physicsBackendAutoDisable를 0으로 설정하세요.");
                        }
                        break;
                    case Decision.OptOut:
                        OptOut(c => c.physicsBackendAutoDisable = 0);
                        AITLog.Info("[AIT-Physics] 자동으로 껐던 PhysX가 다시 켜져 있어 직접 켠 것으로 보고 physicsBackendAutoDisable를 0으로 바꿨습니다. 다시 자동으로 끄지 않습니다.");
                        break;
                }

                var ui = AITUIToolkitModuleAdvisor.Analyze();
                switch (Decide(config.uiToolkitAutoRemove, batch, configExists, ui.Recommended, false,
                    config.uiToolkitAutoApplied == 1, ui.ModuleEnabled))
                {
                    case Decision.Apply:
                        OptOut(c => c.uiToolkitAutoApplied = 1);
                        AITLog.Info(
                            "[AIT-UIToolkit] UI Toolkit을 쓰는 흔적이 없어 내장 모듈(com.unity.modules.uielements)을 자동으로 제거합니다. " +
                            "도메인 리로드가 일어날 수 있습니다. 되돌리려면 AIT Configuration > 고급 설정 > '모듈 켜기'를 누르거나 " +
                            "AITConfig의 uiToolkitAutoRemove를 0으로 설정하세요.");
                        AITUIToolkitModuleAdvisor.RemoveSilently();
                        break;
                    case Decision.OptOut:
                        OptOut(c => c.uiToolkitAutoRemove = 0);
                        AITLog.Info("[AIT-UIToolkit] 자동으로 제거했던 모듈이 다시 켜져 있어 직접 켠 것으로 보고 uiToolkitAutoRemove를 0으로 바꿨습니다. 다시 제거하지 않습니다.");
                        break;
                }
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT] 자동 최적화 중 예외 발생(무시): {e.Message}", sentryCapture: false);
            }
        }

        /// <summary>사용자가 직접 '켜기'를 눌렀을 때 호출한다. 설정 에셋이 있으면 값을 바꿔 저장한다.</summary>
        internal static void OptOut(Action<AITEditorScriptObject> mutate)
        {
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<AITEditorScriptObject>(ConfigPath);
                if (config == null)
                    return;
                mutate(config);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT] 자동 최적화 옵트아웃 저장 실패: {e.Message}", sentryCapture: false);
            }
        }
    }
}
