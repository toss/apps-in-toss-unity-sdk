using UnityEngine;
using AppsInToss.Editor;

/// <summary>
/// 자동이 꺼져 있는(opt-in) 런타임 레버를 켜서 기본 빌드와 쌍 측정하는 perf 픽스처 변형. 등록 규약은 <see cref="HeavyBuildVariants"/> 참조.
/// 자동 활성 여부를 정하려면 이 변형을 기본 빌드와 pair_run_id 로 묶어 Unity 버전별 Δ 와 E2E 통과를 본다.
///
///   datarel   : releaseConsumedData=1 (global-metadata.dat 등 한 번 읽는 data 구간을 첫 프레임 뒤 해제).
///   audio-cmp : audioForceCompressedPlayback=1 (외부화되지 않은 긴 클립도 압축 상태로 재생하는 framework 패치).
/// </summary>
public static class HeavyOptInVariants
{
    [HeavyVariant("datarel")]
    public static void ApplyDataRel(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        config.releaseConsumedData = 1;
        Debug.Log("[heavy] datarel: releaseConsumedData=1");
    }

    [HeavyVariant("audio-cmp")]
    public static void ApplyAudioCmp(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        config.audioForceCompressedPlayback = 1;
        Debug.Log("[heavy] audio-cmp: audioForceCompressedPlayback=1");
    }
}
