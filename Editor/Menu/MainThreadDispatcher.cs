using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// ThreadPool 스레드에서 Editor 메인 스레드로 작업을 디스패치하는 큐.
    /// EditorApplication.update에 직접 접근할 수 없는 백그라운드 스레드가
    /// 안전하게 메인 스레드 작업을 예약하기 위해 사용한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> mainThreadActionQueue = new ConcurrentQueue<Action>();
        private static int isMainThreadQueueProcessorRegistered = 0;

        static MainThreadDispatcher()
        {
            EnsureRegistered();

            EditorApplication.quitting += OnEditorQuitting;
            Events.registeredPackages += OnPackagesChanged;
        }

        /// <summary>
        /// 메인 스레드 작업 큐 프로세서가 등록되어 있는지 확인하고, 없으면 등록
        /// Domain reload 후에도 재등록됨
        /// </summary>
        public static void EnsureRegistered()
        {
            if (Interlocked.CompareExchange(ref isMainThreadQueueProcessorRegistered, 1, 0) == 0)
            {
                EditorApplication.update += ProcessQueue;
            }
        }

        /// <summary>
        /// 메인 스레드에서 실행할 작업을 큐에 추가
        /// ThreadPool 스레드에서 안전하게 호출 가능
        /// </summary>
        /// <param name="action">메인 스레드에서 실행할 작업</param>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            mainThreadActionQueue.Enqueue(action);
        }

        /// <summary>
        /// 메인 스레드 작업 큐를 처리
        /// EditorApplication.update에서 매 프레임 호출됨
        /// </summary>
        private static void ProcessQueue()
        {
            // 한 프레임에 최대 10개 작업 처리 (에디터 응답성 유지)
            int processedCount = 0;
            while (processedCount < 10 && mainThreadActionQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                processedCount++;
            }
        }

        private static void OnEditorQuitting()
        {
            AppsInTossMenu.HandleEditorQuitting();
        }

        private static void OnPackagesChanged(PackageRegistrationEventArgs args)
        {
            AppsInTossMenu.HandlePackagesChanged(args);
        }
    }
}
