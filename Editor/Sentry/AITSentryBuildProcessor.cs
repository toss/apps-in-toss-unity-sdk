// -----------------------------------------------------------------------
// <copyright file="AITSentryBuildProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry Build Processor
// </copyright>
// -----------------------------------------------------------------------

using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AppsInToss.Sentry.Editor
{
    /// <summary>
    /// WebGL 빌드 시 IL2CPP 스택트레이스 라인번호 설정을 시도합니다.
    /// </summary>
    internal class AITSentryBuildProcessor : IPreprocessBuildWithReport
    {
        private const string Tag = "[AITSentry]";

        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            TryEnableIl2CppLineNumbers();
        }

        /// <summary>
        /// IL2CPP MethodFileLineNumber 설정을 시도합니다.
        /// WebGL/WASM에서 C# 라인 번호를 얻을 수 있는지 테스트하는 PoC입니다.
        /// 실패 시에도 빌드를 중단하지 않습니다.
        /// </summary>
        private static void TryEnableIl2CppLineNumbers()
        {
            try
            {
#if UNITY_6000_0_OR_NEWER
                PlayerSettings.SetIl2CppStacktraceInformation(
                    UnityEditor.Build.NamedBuildTarget.WebGL,
                    Il2CppStacktraceInformation.MethodFileLineNumber
                );
                Debug.Log($"{Tag} IL2CPP StacktraceInformation을 MethodFileLineNumber로 설정했습니다. (WebGL PoC)");
#else
                // Unity 2022.3 이하에서는 Il2CppStacktraceInformation API가 없으므로 스킵
                Debug.Log($"{Tag} IL2CPP 라인번호 설정은 Unity 6+ 에서만 지원됩니다.");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} IL2CPP 라인번호 설정 실패 (빌드에는 영향 없음): {ex.Message}");
            }
        }
    }
}
