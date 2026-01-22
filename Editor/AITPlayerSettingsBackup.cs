using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// PlayerSettings 백업 및 복원을 담당하는 구조체
    /// 빌드 전 설정을 저장하고, 빌드 후 원래 상태로 복원합니다.
    /// </summary>
    internal struct AITPlayerSettingsBackup
    {
        // WebGL 설정
        public string webGLTemplate;
        public WebGLLinkerTarget linkerTarget;
        public int memorySize;
        public WebGLCompressionFormat compressionFormat;
        public bool threadsSupport;
        public bool dataCaching;
        public WebGLExceptionSupport exceptionSupport;
        public bool decompressionFallback;
        public bool nameFilesAsHashes;

        // 일반 설정
        public Texture2D defaultCursor;
        public Vector2 cursorHotspot;
        public bool runInBackground;
        public bool stripEngineCode;

        // IL2CPP/Stripping 설정
        public ScriptingImplementation scriptingBackend;
        public ManagedStrippingLevel managedStrippingLevel;
        public Il2CppCompilerConfiguration il2cppCompilerConfiguration;

#if UNITY_2022_3_OR_NEWER
        public WebGLDebugSymbolMode debugSymbolMode;
#endif

#if UNITY_2023_3_OR_NEWER
        public WebGLPowerPreference powerPreference;
#if !UNITY_6000_0_OR_NEWER
        public bool wasmStreaming;
#endif
#endif

#if UNITY_2022_2_OR_NEWER
        public bool showDiagnostics;
#endif

        /// <summary>
        /// 현재 PlayerSettings를 캡처하여 백업 생성
        /// </summary>
        public static AITPlayerSettingsBackup Capture()
        {
            var backup = new AITPlayerSettingsBackup
            {
                // WebGL 설정
                webGLTemplate = PlayerSettings.WebGL.template,
                linkerTarget = PlayerSettings.WebGL.linkerTarget,
                memorySize = PlayerSettings.WebGL.memorySize,
                compressionFormat = PlayerSettings.WebGL.compressionFormat,
                threadsSupport = PlayerSettings.WebGL.threadsSupport,
                dataCaching = PlayerSettings.WebGL.dataCaching,
                exceptionSupport = PlayerSettings.WebGL.exceptionSupport,
                decompressionFallback = PlayerSettings.WebGL.decompressionFallback,
                nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes,

                // 일반 설정
                defaultCursor = PlayerSettings.defaultCursor,
                cursorHotspot = PlayerSettings.cursorHotspot,
                runInBackground = PlayerSettings.runInBackground,
                stripEngineCode = PlayerSettings.stripEngineCode,

                // IL2CPP/Stripping 설정
#if UNITY_6000_0_OR_NEWER
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL),
                managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL),
                il2cppCompilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL),
#else
                scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.WebGL),
                managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL),
                il2cppCompilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL),
#endif
            };

#if UNITY_2022_3_OR_NEWER
            backup.debugSymbolMode = PlayerSettings.WebGL.debugSymbolMode;
#endif

#if UNITY_2023_3_OR_NEWER
            backup.powerPreference = PlayerSettings.WebGL.powerPreference;
#if !UNITY_6000_0_OR_NEWER
            backup.wasmStreaming = PlayerSettings.WebGL.wasmStreaming;
#endif
#endif

#if UNITY_2022_2_OR_NEWER
            backup.showDiagnostics = PlayerSettings.WebGL.showDiagnostics;
#endif

            return backup;
        }

        /// <summary>
        /// 백업된 PlayerSettings를 복원
        /// </summary>
        public void Restore()
        {
            // WebGL 설정
            PlayerSettings.WebGL.template = webGLTemplate;
            PlayerSettings.WebGL.linkerTarget = linkerTarget;
            PlayerSettings.WebGL.memorySize = memorySize;
            PlayerSettings.WebGL.compressionFormat = compressionFormat;
            PlayerSettings.WebGL.threadsSupport = threadsSupport;
            PlayerSettings.WebGL.dataCaching = dataCaching;
            PlayerSettings.WebGL.exceptionSupport = exceptionSupport;
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
            PlayerSettings.WebGL.nameFilesAsHashes = nameFilesAsHashes;

            // 일반 설정
            PlayerSettings.defaultCursor = defaultCursor;
            PlayerSettings.cursorHotspot = cursorHotspot;
            PlayerSettings.runInBackground = runInBackground;
            PlayerSettings.stripEngineCode = stripEngineCode;

            // IL2CPP/Stripping 설정
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, scriptingBackend);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, managedStrippingLevel);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, il2cppCompilerConfiguration);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, scriptingBackend);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, managedStrippingLevel);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, il2cppCompilerConfiguration);
#endif

#if UNITY_2022_3_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = debugSymbolMode;
#endif

#if UNITY_2023_3_OR_NEWER
            PlayerSettings.WebGL.powerPreference = powerPreference;
#if !UNITY_6000_0_OR_NEWER
            PlayerSettings.WebGL.wasmStreaming = wasmStreaming;
#endif
#endif

#if UNITY_2022_2_OR_NEWER
            PlayerSettings.WebGL.showDiagnostics = showDiagnostics;
#endif

            Debug.Log("[AIT] PlayerSettings가 원래 상태로 복원되었습니다.");
        }
    }
}
