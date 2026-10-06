// -----------------------------------------------------------------------
// <copyright file="AIT.LowMemoryTier.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Low Memory Tier (C# side)
// </copyright>
// -----------------------------------------------------------------------
//
// 부팅이 메모리로 죽은 기기를 다음 부팅에서 저메모리 모드로 올리는 정책의 C# 쪽 소비자.
// 티어는 템플릿의 ait-mem.js 가 부팅 사망 횟수(localStorage 부팅 마커, 10분 창)와 24시간 만료 저장값으로 정한다.
//   tier 0: 정상 기기. 이 파일은 아무것도 하지 않는다.
//   tier 1: 부팅 사망 1회. 스트리밍 동시성을 1 로 제한하고, QualitySettings 전역 텍스처 mip 제한을 1 로 올린다
//           (2022.2+ globalTextureMipmapLimit, 2021.3 masterTextureLimit). mip 이 있는 텍스처의 GPU·업로드 일시 메모리가 약 75% 줄고,
//           mip 이 없는 UI 텍스처는 영향이 없다. 동시성 제한만으로는 첫 씬 텍스처 업로드 사망을 못 막아 tier 1 에도 건다.
//   tier 2: 같은 티어로 또 죽음. mip 제한을 2 로 더 올린다(약 94% 감소).
// mip 제한은 BeforeSplashScreen 에서 건다 — 첫 씬이 원본 크기로 업로드된 뒤에 걸면 재업로드가 생긴다.
// (게임이 이후 QualitySettings.SetQualityLevel 을 부르면 품질 레벨의 mip 설정으로 덮일 수 있다. 그 경우 이 제한은 풀린다.)
//
// 티어 값은 AITMemoryBridge.LowMemTier(jslib __AITMemoryBridge_GetLowMemTier → window.AITMemory.lowMemTier)에서 읽는다.
// WebGL 빌드가 아니거나 lowMemoryTier/memoryTelemetry 설정이 꺼져 있으면 0 이다.

using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    [Preserve]
    internal static class AITLowMemoryTier
    {
        internal const string LogTag = "[AIT-LowMem]";

        /// <summary>tier 1 에서 거는 전역 텍스처 mip 제한(0 = 원본 해상도, 1 = 한 단계 낮춤).</summary>
        internal const int Tier1MipmapLimit = 1;

        /// <summary>tier 2 에서 거는 전역 텍스처 mip 제한(두 단계 낮춤).</summary>
        internal const int Tier2MipmapLimit = 2;

        /// <summary>스트리밍 동시성 상한이 없을 때(tier 0)의 값.</summary>
        internal const int Unlimited = int.MaxValue;

        private static int _tier = -1;

        /// <summary>부팅 때 한 번 정해지는 저사양 티어(0..2). 첫 읽기에서 캐시한다.</summary>
        internal static int Tier
        {
            get
            {
                if (_tier < 0) _tier = ReadTier();
                return _tier;
            }
        }

        /// <summary>JS 가 정한 티어를 읽는다. 읽기에 실패하거나 WebGL 이 아니면 0.</summary>
        internal static int ReadTier()
        {
            try
            {
                return Mathf.Clamp(AITMemoryBridge.LowMemTier, 0, 2);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>티어별 전역 텍스처 mip 제한. tier 0 은 0, tier 1 은 1, tier 2 이상은 2.</summary>
        internal static int MipmapLimitFor(int tier)
        {
            if (tier <= 0) return 0;
            return tier >= 2 ? Tier2MipmapLimit : Tier1MipmapLimit;
        }

        /// <summary>티어별 스트리밍 동시성 상한. tier 1 이상이면 1, tier 0 이면 제한 없음.</summary>
        internal static int StreamingConcurrencyCapFor(int tier)
        {
            return tier >= 1 ? 1 : Unlimited;
        }

        /// <summary>현재 티어 기준 스트리밍 동시성 상한.</summary>
        internal static int StreamingConcurrencyCap
        {
            get { return StreamingConcurrencyCapFor(Tier); }
        }

        /// <summary>
        /// 스트리밍 헬퍼(텍스처 등)가 매니페스트의 동시성 값에 적용한다. tier 0 이면 값을 그대로 돌려주고,
        /// tier 1 이상이면 1 로 줄인다. 0 이하(미지정)는 건드리지 않는다.
        /// </summary>
        internal static int ClampStreamingConcurrency(int requested)
        {
            if (requested <= 0) return requested;
            return Math.Min(requested, StreamingConcurrencyCap);
        }

        /// <summary>전역 텍스처 mip 제한을 적용한다(2022.2 미만은 masterTextureLimit).</summary>
        internal static void ApplyMipmapLimit(int limit)
        {
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(limit, 0, 3);
#else
            QualitySettings.masterTextureLimit = Mathf.Clamp(limit, 0, 3);
#endif
        }

        /// <summary>티어에 맞는 정책을 적용한다. tier 0 은 아무것도 하지 않는다.</summary>
        internal static void Apply(int tier)
        {
            if (tier <= 0) return;

            int mip = MipmapLimitFor(tier);
            if (mip > 0)
            {
                try
                {
                    ApplyMipmapLimit(mip);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{LogTag} 텍스처 mip 제한 적용 실패(무시): {e.Message}");
                    mip = 0;
                }
            }

            Debug.Log($"{LogTag} 저메모리 tier={tier} 적용: 텍스처 mip 제한={mip}, 스트리밍 동시성 상한={StreamingConcurrencyCapFor(tier)}");
        }

        // BeforeSplashScreen: 첫 씬의 텍스처 업로드보다 먼저 mip 제한이 걸려야 한다(늦으면 원본 크기로 올라간 뒤 재업로드).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        [Preserve]
        private static void Bootstrap()
        {
            try
            {
                Apply(Tier);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} 초기화 실패(무시): {e.Message}");
            }
        }
    }
}
