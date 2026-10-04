using UnityEngine;

/// <summary>
/// perf heavy 빌드 전용: 생성된 장시간 오디오(HeavyGen/Audio/heavy_audio_00)를 루프 BGM 처럼 붙여,
/// 외부화된 클립을 런타임이 재수화하는 경로(AITStreamingAudio)와 그 디코드 메모리를 계측 대상으로 만든다.
/// 일반 E2E 빌드에는 이 리소스가 없어 아무것도 하지 않는다.
/// </summary>
public static class HeavyAudioProbe
{
    private const string ClipResourcePath = "HeavyGen/Audio/heavy_audio_00";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        var clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
        {
            return;
        }

        var go = new GameObject("HeavyAudioProbe");
        Object.DontDestroyOnLoad(go);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.Play();
        Debug.Log($"[HeavyAudioProbe] BGM 프로브 부착: {clip.name} (len {clip.length:0.00}s)");
    }
}
