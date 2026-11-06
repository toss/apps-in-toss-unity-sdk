// -----------------------------------------------------------------------
// <copyright file="AIT.FirstFrame.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - 부팅 훅 지연 실행기
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// RuntimeInitializeOnLoadMethod 훅이 하던 "GameObject/컴포넌트 생성, 로그 전송" 같은 일을
    /// 엔진 초기화(callMain) 밖, 첫 프레임이 지나간 뒤로 미루는 실행기.
    /// </summary>
    /// <remarks>
    /// PlayerLoop(Update)에 시스템 하나를 끼워 넣어 매 프레임 <see cref="Tick"/> 을 부른다.
    /// GameObject 를 만들지 않으므로 호스트 생성 비용도 없다. PlayerLoop 주입이 실패하면
    /// 숨김 GameObject 하나로 폴백한다.
    /// 큐에 쌓인 작업은 <see cref="DeferFrames"/> 번째 Update 에서 등록 순서대로 실행된다.
    /// </remarks>
    internal static class AITFirstFrame
    {
        /// <summary>큐를 비우는 Update 번호(1부터). 1프레임째 렌더가 끝난 뒤 실행되도록 2로 둔다.</summary>
        internal const int DeferFrames = 2;

        private static readonly List<Action> _queue = new List<Action>(8);
        private static bool _installed;
        private static bool _drained;
        private static int _updates;
        private static GameObject _fallbackHost;

        /// <summary>큐가 비워진 뒤(=첫 프레임 이후) true.</summary>
        internal static bool Drained => _drained;

        /// <summary>매 프레임(Update) 호출되는 구독자. 큐 실행 뒤부터가 아니라 설치 직후 첫 프레임부터 불린다.</summary>
        internal static event Action EveryFrame;

        /// <summary>
        /// 첫 프레임 이후에 실행할 작업을 등록한다. 이미 지난 뒤라면 즉시 실행한다.
        /// 예외는 삼킨다(부팅/프레임 루프를 깨지 않는다).
        /// </summary>
        internal static void Post(Action action)
        {
            if (action == null) return;
            if (_drained)
            {
                RunSafe(action);
                return;
            }
            _queue.Add(action);
            EnsureInstalled();
        }

        /// <summary>PlayerLoop 시스템(또는 폴백 호스트)을 설치한다. 여러 번 불러도 한 번만 설치된다.</summary>
        internal static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                if (TryInstallPlayerLoop()) return;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIT] PlayerLoop 주입 실패, GameObject 호스트로 폴백: " + e.Message);
            }

            try
            {
                _fallbackHost = new GameObject("AITFirstFrameHost");
                _fallbackHost.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_fallbackHost);
                _fallbackHost.AddComponent<AITFirstFrameHost>();
            }
            catch (Exception e)
            {
                // 호스트도 못 만들면 큐를 지금 비운다 — 작업이 영영 안 도는 것보다 낫다.
                Debug.LogWarning("[AIT] 첫 프레임 호스트 생성 실패, 즉시 실행: " + e.Message);
                Drain();
            }
        }

        private static bool TryInstallPlayerLoop()
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var subs = loop.subSystemList;
            if (subs == null) return false;

            var updateType = typeof(UnityEngine.PlayerLoop.Update);
            for (int i = 0; i < subs.Length; i++)
            {
                if (subs[i].type != updateType) continue;

                var update = subs[i];
                var children = update.subSystemList ?? new PlayerLoopSystem[0];

                // 이전 플레이 세션이 남긴 같은 시스템이 있으면 걷어낸 뒤 다시 넣는다(에디터 도메인 리로드 비활성 대비).
                var list = new List<PlayerLoopSystem>(children.Length + 1);
                foreach (var c in children)
                {
                    if (c.type != typeof(AITFirstFrame)) list.Add(c);
                }
                list.Add(new PlayerLoopSystem
                {
                    type = typeof(AITFirstFrame),
                    updateDelegate = Tick
                });
                update.subSystemList = list.ToArray();
                subs[i] = update;
                loop.subSystemList = subs;
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);

                Application.quitting += Uninstall;
                return true;
            }
            return false;
        }

        private static void Uninstall()
        {
            try
            {
                var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                var subs = loop.subSystemList;
                if (subs == null) return;
                var updateType = typeof(UnityEngine.PlayerLoop.Update);
                for (int i = 0; i < subs.Length; i++)
                {
                    if (subs[i].type != updateType || subs[i].subSystemList == null) continue;
                    var update = subs[i];
                    var list = new List<PlayerLoopSystem>(update.subSystemList.Length);
                    foreach (var c in update.subSystemList)
                    {
                        if (c.type != typeof(AITFirstFrame)) list.Add(c);
                    }
                    update.subSystemList = list.ToArray();
                    subs[i] = update;
                    loop.subSystemList = subs;
                    UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                    return;
                }
            }
            catch
            {
                // 종료 중 정리 실패는 무시.
            }
        }

        /// <summary>Update 마다 호출된다(PlayerLoop 시스템 또는 폴백 호스트).</summary>
        internal static void Tick()
        {
            if (!_drained && ++_updates >= DeferFrames)
            {
                Drain();
            }

            var handler = EveryFrame;
            if (handler != null)
            {
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[AIT] 프레임 구독자 오류: " + e.Message);
                }
            }
        }

        /// <summary>테스트 전용: 큐/카운터를 초기화한다. PlayerLoop 는 건드리지 않도록 설치된 것으로 표시한다.</summary>
        internal static void ResetForTests()
        {
            _queue.Clear();
            _drained = false;
            _updates = 0;
            _installed = true;
            EveryFrame = null;
        }

        private static void Drain()
        {
            if (_drained) return;
            _drained = true;

            // 실행 중 Post 되는 작업은 _drained 가 true 라 즉시 실행되므로 스냅샷을 복사해 돌린다.
            var snapshot = _queue.ToArray();
            _queue.Clear();
            foreach (var action in snapshot)
            {
                RunSafe(action);
            }
        }

        private static void RunSafe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIT] 지연 부팅 작업 오류: " + e.Message);
            }
        }
    }

    /// <summary>PlayerLoop 주입이 불가능할 때만 쓰는 폴백 호스트.</summary>
    [Preserve]
    internal sealed class AITFirstFrameHost : MonoBehaviour
    {
        private void Update()
        {
            AITFirstFrame.Tick();
        }
    }
}
