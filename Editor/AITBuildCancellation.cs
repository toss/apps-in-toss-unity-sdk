using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// 빌드 취소 상태 관리 (AITConvertCore에서 분리 — #36).
    /// 백그라운드 빌드 스레드와 메인 스레드(취소 요청)가 공유하는 취소 플래그,
    /// 진행 중인 비동기 작업 핸들, 병렬 pnpm install 컨텍스트를 한 곳에서 관리한다.
    /// AITConvertCore의 공개 취소 API는 이 클래스로 위임하며 동작은 기존과 동일하다.
    /// </summary>
    internal static class AITBuildCancellation
    {
        // volatile: 백그라운드 빌드 스레드와 메인 스레드(취소 요청) 간 가시성 보장.
        // 취소 플래그/핸들 참조의 stale 캐시 읽기를 막는다.
        private static volatile bool isCancelled = false;
        private static volatile Editor.AITAsyncCommandRunner.CommandTask currentAsyncTask = null;
        private static volatile Editor.AITPackageBuilder.EarlyPackageContext currentEarlyContext = null;

        /// <summary>
        /// 빌드 취소 요청
        /// </summary>
        public static void CancelBuild()
        {
            isCancelled = true;
            Debug.Log("[AIT] 빌드 취소 요청됨");

            // 병렬 pnpm install 취소
            if (currentEarlyContext != null)
            {
                currentEarlyContext.CancelAndDisposePnpm();
                currentEarlyContext = null;
            }

            // 현재 실행 중인 비동기 작업이 있으면 취소
            if (currentAsyncTask != null)
            {
                Editor.AITAsyncCommandRunner.CancelTask(currentAsyncTask);
                currentAsyncTask = null;
            }
        }

        /// <summary>
        /// 빌드 취소 플래그 리셋
        /// </summary>
        public static void ResetCancellation()
        {
            isCancelled = false;
            currentAsyncTask = null;
            currentEarlyContext = null;
        }

        /// <summary>
        /// 빌드가 취소되었는지 확인
        /// </summary>
        public static bool IsCancelled()
        {
            return isCancelled;
        }

        /// <summary>
        /// 비동기 빌드 작업이 진행 중인지 확인
        /// </summary>
        public static bool HasRunningAsyncTask()
        {
            return currentAsyncTask != null;
        }

        /// <summary>
        /// 현재 비동기 작업 설정 (취소용)
        /// </summary>
        public static void SetCurrentAsyncTask(Editor.AITAsyncCommandRunner.CommandTask task)
        {
            currentAsyncTask = task;
        }

        /// <summary>
        /// 병렬 pnpm install 컨텍스트 설정 (취소 시 정리 대상). null 전달로 해제.
        /// </summary>
        public static void SetCurrentEarlyContext(Editor.AITPackageBuilder.EarlyPackageContext context)
        {
            currentEarlyContext = context;
        }
    }
}
