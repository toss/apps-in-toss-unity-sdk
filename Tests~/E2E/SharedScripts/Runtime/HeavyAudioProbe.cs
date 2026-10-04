using System.Collections;
using UnityEngine;

/// <summary>
/// perf heavy 빌드 전용: 생성된 장시간 오디오(HeavyGen/Audio/heavy_audio_00)를 루프 BGM 처럼 붙여,
/// 외부화된 클립을 런타임이 재수화하는 경로(AITStreamingAudio)와 그 디코드 메모리를 계측 대상으로 만든다.
/// 일반 E2E 빌드에는 이 리소스가 없어 아무것도 하지 않는다.
/// </summary>
public class HeavyAudioProbe : MonoBehaviour
{
    private const string ClipResourcePath = "HeavyGen/Audio/heavy_audio_00";

    private AudioSource source;

    // AudioSource.time 단조성 점검: 압축 재생 경로의 estimatePlaybackPosition(media element currentTime)이 되감기거나 멈추면 안 된다.
    private float lastTime = -1f;
    private int timeSamples;
    private int timeRewinds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        var clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
        {
            return;
        }

        var go = new GameObject("HeavyAudioProbe");
        DontDestroyOnLoad(go);
        var probe = go.AddComponent<HeavyAudioProbe>();
        probe.source = go.AddComponent<AudioSource>();
        probe.source.clip = clip;
        probe.source.loop = true;
        probe.source.Play();
        Debug.Log($"[HeavyAudioProbe] BGM 프로브 부착: {clip.name} (len {clip.length:0.00}s)");
    }

    private IEnumerator Start()
    {
        // 재수화 진행을 하네스 콘솔 수집 창(첫 방문 ~30초) 안에서 몇 번 남긴다.
        float[] marks = { 3f, 10f, 20f, 28f };
        float elapsed = 0f;
        foreach (float m in marks)
        {
            yield return new WaitForSecondsRealtime(m - elapsed);
            elapsed = m;
            var c = source != null ? source.clip : null;
            float now = source != null ? source.time : -1f;
            // 루프 BGM 이라 클립 길이를 넘기면 0 근처로 돌아온다. 그 래핑은 되감기로 세지 않는다.
            if (lastTime >= 0f && now + 0.05f < lastTime && !(c != null && c.length > 0f && lastTime > c.length - 1f))
            {
                timeRewinds++;
            }
            lastTime = now;
            timeSamples++;
            Debug.Log($"[HeavyAudioProbe] t={m:0}s playing={(source != null && source.isPlaying)} " +
                (c != null ? $"clip={c.name} len={c.length:0.0}s loadState={c.loadState} loadType={c.loadType} " : "clip=null ") +
                $"time={now:0.00}s timeRewinds={timeRewinds}/{timeSamples} lengthOk={(c != null && c.length > 0f)}");
        }
    }
}
