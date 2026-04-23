using System;
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 진입 시점의 PlayerSettings 스냅샷. Serializable 이라
    /// ScriptableSingleton 을 통한 리로드 생존 저장이 가능하다.
    /// </summary>
    [Serializable]
    internal struct PlayerSettingsSnapshot
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

        // Stack Trace Log Type (LogType별)
        public StackTraceLogType stackTraceLogTypeError;
        public StackTraceLogType stackTraceLogTypeAssert;
        public StackTraceLogType stackTraceLogTypeWarning;
        public StackTraceLogType stackTraceLogTypeLog;
        public StackTraceLogType stackTraceLogTypeException;

#if UNITY_2022_3_OR_NEWER
        public WebGLDebugSymbolMode debugSymbolMode;
#endif

#if UNITY_2023_3_OR_NEWER
        public WebGLPowerPreference powerPreference;
#if !UNITY_6000_0_OR_NEWER
        public bool wasmStreaming;
#endif
#endif

        public static PlayerSettingsSnapshot Capture()
        {
            var snapshot = new PlayerSettingsSnapshot
            {
                webGLTemplate = PlayerSettings.WebGL.template,
                linkerTarget = PlayerSettings.WebGL.linkerTarget,
                memorySize = PlayerSettings.WebGL.memorySize,
                compressionFormat = PlayerSettings.WebGL.compressionFormat,
                threadsSupport = PlayerSettings.WebGL.threadsSupport,
                dataCaching = PlayerSettings.WebGL.dataCaching,
                exceptionSupport = PlayerSettings.WebGL.exceptionSupport,
                decompressionFallback = PlayerSettings.WebGL.decompressionFallback,
                nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes,

                defaultCursor = PlayerSettings.defaultCursor,
                cursorHotspot = PlayerSettings.cursorHotspot,
                runInBackground = PlayerSettings.runInBackground,
                stripEngineCode = PlayerSettings.stripEngineCode,

#if UNITY_6000_0_OR_NEWER
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL),
                managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL),
                il2cppCompilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL),
#else
                scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.WebGL),
                managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL),
                il2cppCompilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL),
#endif

                stackTraceLogTypeError     = PlayerSettings.GetStackTraceLogType(LogType.Error),
                stackTraceLogTypeAssert    = PlayerSettings.GetStackTraceLogType(LogType.Assert),
                stackTraceLogTypeWarning   = PlayerSettings.GetStackTraceLogType(LogType.Warning),
                stackTraceLogTypeLog       = PlayerSettings.GetStackTraceLogType(LogType.Log),
                stackTraceLogTypeException = PlayerSettings.GetStackTraceLogType(LogType.Exception),
            };

#if UNITY_2022_3_OR_NEWER
            snapshot.debugSymbolMode = PlayerSettings.WebGL.debugSymbolMode;
#endif
#if UNITY_2023_3_OR_NEWER
            snapshot.powerPreference = PlayerSettings.WebGL.powerPreference;
#if !UNITY_6000_0_OR_NEWER
            snapshot.wasmStreaming = PlayerSettings.WebGL.wasmStreaming;
#endif
#endif
            return snapshot;
        }

        public void Restore()
        {
            EditorApplication.LockReloadAssemblies();
            try
            {
                RestoreInternal();
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }

            Debug.Log("[AIT] PlayerSettings 스냅샷 복원 완료.");
        }

        private void RestoreInternal()
        {
            PlayerSettings.WebGL.template = webGLTemplate;
            PlayerSettings.WebGL.linkerTarget = linkerTarget;
            PlayerSettings.WebGL.memorySize = memorySize;
            PlayerSettings.WebGL.compressionFormat = compressionFormat;
            PlayerSettings.WebGL.threadsSupport = threadsSupport;
            PlayerSettings.WebGL.dataCaching = dataCaching;
            PlayerSettings.WebGL.exceptionSupport = exceptionSupport;
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
            PlayerSettings.WebGL.nameFilesAsHashes = nameFilesAsHashes;

            PlayerSettings.defaultCursor = defaultCursor;
            PlayerSettings.cursorHotspot = cursorHotspot;
            PlayerSettings.runInBackground = runInBackground;
            PlayerSettings.stripEngineCode = stripEngineCode;

#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, scriptingBackend);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, managedStrippingLevel);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, il2cppCompilerConfiguration);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, scriptingBackend);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, managedStrippingLevel);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, il2cppCompilerConfiguration);
#endif

            PlayerSettings.SetStackTraceLogType(LogType.Error,     stackTraceLogTypeError);
            PlayerSettings.SetStackTraceLogType(LogType.Assert,    stackTraceLogTypeAssert);
            PlayerSettings.SetStackTraceLogType(LogType.Warning,   stackTraceLogTypeWarning);
            PlayerSettings.SetStackTraceLogType(LogType.Log,       stackTraceLogTypeLog);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, stackTraceLogTypeException);

#if UNITY_2022_3_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = debugSymbolMode;
#endif
#if UNITY_2023_3_OR_NEWER
            PlayerSettings.WebGL.powerPreference = powerPreference;
#if !UNITY_6000_0_OR_NEWER
            PlayerSettings.WebGL.wasmStreaming = wasmStreaming;
#endif
#endif
        }
    }
}
