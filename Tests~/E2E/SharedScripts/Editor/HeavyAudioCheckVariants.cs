using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

/// <summary>
/// 실기기에서 들리는 소리가 콘텐츠인지 SDK 오디오 최적화의 부작용인지 가려내는 확인용 perf 픽스처 변형. 등록 규약은 <see cref="HeavyBuildVariants"/> 참조.
///
///   amb-mute      : mobileheavy 스테이지 앰비언스를 무음으로 바꾼다(SDK 설정은 그대로).
///   audio-opt-off : 오디오 관련 빌드·런타임 최적화(스트리밍 외부화·트랜스코드·재인코딩·압축 재생)를 전부 끈다(콘텐츠는 그대로).
/// </summary>
public static class HeavyAudioCheckVariants
{
    [HeavyVariant("amb-mute")]
    public static void ApplyAmbMute(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        MobileGameBuilder.MuteHeavyAmbience();
        Debug.Log("[heavy] amb-mute: 스테이지 앰비언스 무음");
    }

    [HeavyVariant("audio-opt-off")]
    public static void ApplyAudioOptOff(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        config.audioStreaming = 0;
        config.audioStreamingCompressedPlayback = 0;
        config.audioStreamTranscode = 0;
        config.audioStreamLoopTranscode = 0;
        config.audioReencode = 0;
        config.audioForceCompressedPlayback = 0;
        Debug.Log("[heavy] audio-opt-off: 오디오 최적화 전부 0");
    }
}
