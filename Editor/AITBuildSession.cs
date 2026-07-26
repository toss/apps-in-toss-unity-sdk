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

    internal enum BuildStage
    {
        None = 0,
        Preparing,
        WebGLBuild,
        Packaging,
        Completing,
    }

    /// <summary>
    /// 빌드 진행 상태를 리로드/강제종료 이후에도 생존시키는 저장소.
    /// Library/ScriptableSingleton/AITBuildSession.asset 로 Unity 가 자동 직렬화.
    ///
    /// 스레드 안전성:
    /// - BeginBuild 는 반드시 메인 스레드에서 호출되어야 한다 (Unity 메뉴 클릭 경로 보장).
    ///   이때 instance 가 워밍업되고 mainThreadId 가 캡처된다.
    /// - 이후 RecordPid / ClearPid / SetStage / RecordPlayerSettingsSnapshot 은
    ///   PnpmRunner.RunPnpmInstallInThread 같은 백그라운드 스레드에서 호출될 수 있으므로
    ///   _lock 으로 필드 변경을 직렬화한다.
    /// - Save(true) 는 UnityEngine.Object 디스크 쓰기이므로 메인 스레드 전용.
    ///   백그라운드에서 호출된 경우 MainThreadDispatcher.Enqueue 로 마샬링.
    ///   리로드가 Save 직전에 발생하면 마지막 변경이 디스크에 반영되지 못할 수 있으나,
    ///   이는 "자식 PID 하나 누락" 정도의 손실이고 asset corruption 은 방지된다.
    /// </summary>
    [FilePath("Library/AppsInToss/AITBuildSession.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class AITBuildSession : ScriptableSingleton<AITBuildSession>
    {
        [SerializeField] public string sessionId;
        [SerializeField] public long startedAtUnixSec;
        [SerializeField] public string unityVersion;
        [SerializeField] public string sdkVersion;
        [SerializeField] public PlayerSettingsSnapshot playerSettings;
        [SerializeField] public System.Collections.Generic.List<int> childPids
            = new System.Collections.Generic.List<int>();
        [SerializeField] public BuildStage stage = BuildStage.None;
        [SerializeField] public string entrypoint;

        private const long StaleThresholdSec = 24 * 3600;

        // 모든 필드 변경 + Save 호출은 이 lock 으로 직렬화.
        private static readonly object _lock = new object();

        // BeginBuild 시 캡처한 메인 스레드 ID. 이후 mutation 호출이 메인/백그라운드인지 판정.
        private static int _mainThreadId;

        private static bool IsMainThread()
            => _mainThreadId != 0 && System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// Save 를 메인 스레드에서 수행한다. 백그라운드에서 호출된 경우 MainThreadDispatcher 로
        /// 디스패치 — 한 프레임 지연이 발생하지만 asset corruption 은 방지됨.
        /// </summary>
        private static void InvokeSaveOnMainThread()
        {
            if (IsMainThread())
            {
                try { instance.Save(true); }
                catch (System.Exception e)
                {
                    AITLog.Warning($"[AIT] BuildSession.Save 실패 (무시됨): {e.Message}",
                        sentryCapture: false);
                }
                return;
            }

            Menu.MainThreadDispatcher.Enqueue(() =>
            {
                try { instance.Save(true); }
                catch (System.Exception e)
                {
                    AITLog.Warning($"[AIT] BuildSession.Save (디스패치) 실패 (무시됨): {e.Message}",
                        sentryCapture: false);
                }
            });
        }

        public static void BeginBuild(string entrypoint)
        {
            // BeginBuild 는 항상 메인 스레드에서 호출됨 (메뉴 클릭 경로).
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            try
            {
                lock (_lock)
                {
                    var s = instance;
                    s.sessionId = System.Guid.NewGuid().ToString("N");
                    s.startedAtUnixSec = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    s.unityVersion = Application.unityVersion;
                    s.sdkVersion = AppsInToss.AITVersion.Version;
                    s.playerSettings = default;
                    s.childPids.Clear();
                    s.stage = BuildStage.Preparing;
                    s.entrypoint = entrypoint ?? "Unknown";
                    s.Save(true);
                }
            }
            catch (System.Exception e)
            {
                // Begin 이 실패하면 복구가 아예 작동 불가 — Sentry 로 알림.
                AITLog.Error($"[AIT] BuildSession.BeginBuild 저장 실패: {e.Message}",
                    sentryCapture: true);
            }
        }

        public static void RecordPlayerSettingsSnapshot(PlayerSettingsSnapshot snapshot)
        {
            try
            {
                lock (_lock)
                {
                    var s = instance;
                    if (string.IsNullOrEmpty(s.sessionId)) return;
                    s.playerSettings = snapshot;
                }
                InvokeSaveOnMainThread();
            }
            catch (System.Exception e)
            {
                AITLog.Warning($"[AIT] BuildSession.Snapshot 저장 실패 (무시됨): {e.Message}",
                    sentryCapture: false);
            }
        }

        public static void SetStage(BuildStage stage)
        {
            try
            {
                lock (_lock)
                {
                    var s = instance;
                    if (string.IsNullOrEmpty(s.sessionId)) return;
                    s.stage = stage;
                }
                InvokeSaveOnMainThread();
            }
            catch (System.Exception e)
            {
                AITLog.Warning($"[AIT] BuildSession.SetStage 저장 실패 (무시됨): {e.Message}",
                    sentryCapture: false);
            }
        }

        public static void RecordPid(int pid)
        {
            try
            {
                lock (_lock)
                {
                    var s = instance;
                    if (string.IsNullOrEmpty(s.sessionId)) return;
                    s.childPids.Add(pid);
                }
                InvokeSaveOnMainThread();
            }
            catch (System.Exception e)
            {
                AITLog.Warning($"[AIT] BuildSession.RecordPid 저장 실패 (무시됨): {e.Message}",
                    sentryCapture: false);
            }
        }

        public static void ClearPid(int pid)
        {
            try
            {
                lock (_lock)
                {
                    var s = instance;
                    if (string.IsNullOrEmpty(s.sessionId)) return;
                    s.childPids.Remove(pid);
                }
                InvokeSaveOnMainThread();
            }
            catch (System.Exception e)
            {
                AITLog.Warning($"[AIT] BuildSession.ClearPid 저장 실패 (무시됨): {e.Message}",
                    sentryCapture: false);
            }
        }

        public static void EndBuild()
        {
            try
            {
                lock (_lock)
                {
                    var s = instance;
                    s.sessionId = null;
                    s.startedAtUnixSec = 0;
                    s.unityVersion = null;
                    s.sdkVersion = null;
                    s.playerSettings = default;
                    s.childPids.Clear();
                    s.stage = BuildStage.None;
                    s.entrypoint = null;
                    s.Save(true);
                }
            }
            catch (System.Exception e)
            {
                // End 가 실패하면 stale session 이 남아 오동작 가능 — Sentry 로 알림.
                AITLog.Error($"[AIT] BuildSession.EndBuild 저장 실패: {e.Message}",
                    sentryCapture: true);
            }
        }

        public static bool TryLoadPendingSession(out AITBuildSession session)
        {
            session = instance;
            return !string.IsNullOrEmpty(session.sessionId);
        }

        public static bool IsStale(AITBuildSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.sessionId)) return true;
            if (session.startedAtUnixSec <= 0) return true;
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - session.startedAtUnixSec > StaleThresholdSec) return true;
            if (session.unityVersion != Application.unityVersion) return true;
            if (session.sdkVersion != AppsInToss.AITVersion.Version) return true;
            return false;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Test-only: directly mutate fields without re-initializing.</summary>
        internal static void ForceTestOverride(
            long? startedAtUnixSec = null,
            string unityVersion = null,
            string sdkVersion = null)
        {
            var s = instance;
            if (startedAtUnixSec.HasValue) s.startedAtUnixSec = startedAtUnixSec.Value;
            if (unityVersion != null) s.unityVersion = unityVersion;
            if (sdkVersion != null) s.sdkVersion = sdkVersion;
            s.Save(true);
        }
#endif
    }
}
