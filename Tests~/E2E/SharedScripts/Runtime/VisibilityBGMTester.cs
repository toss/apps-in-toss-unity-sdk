using UnityEngine;
using AppsInToss;

/// <summary>
/// AITVisibilityHelper 테스트용 BGM 컴포넌트
/// 백그라운드 전환 시 BGM 일시정지/재생 테스트
/// InteractiveAPITester의 섹션으로 표시됨
/// </summary>
public class VisibilityBGMTester : MonoBehaviour
{
    private AudioSource audioSource;
    private bool isInitialized = false;
    private bool isPlaying = false;

    // UI 상태
    private string statusText = "준비 중...";
    private int visibilityChangeCount = 0;
    private bool lastVisibilityState = true;

    void Start()
    {
        Debug.Log("[VisibilityBGMTester] Initializing...");
        InitializeAudio();
        RegisterVisibilityCallback();
    }

    void OnDestroy()
    {
        AITVisibilityHelper.OnVisibilityChanged -= OnVisibilityChanged;
        Debug.Log("[VisibilityBGMTester] Cleaned up visibility callback");
    }

    private void InitializeAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = 0.3f;

        // 간단한 멜로디 생성 (C-E-G-E 아르페지오)
        int sampleRate = 44100;
        float noteDuration = 0.4f;
        int noteCount = 4;
        int totalSamples = (int)(sampleRate * noteDuration * noteCount);

        float[] notes = { 523.25f, 659.25f, 783.99f, 659.25f };

        AudioClip clip = AudioClip.Create("TestBGM", totalSamples, 1, sampleRate, false);
        float[] samples = new float[totalSamples];

        int samplesPerNote = (int)(sampleRate * noteDuration);

        for (int noteIndex = 0; noteIndex < noteCount; noteIndex++)
        {
            float freq = notes[noteIndex];
            int startSample = noteIndex * samplesPerNote;

            for (int i = 0; i < samplesPerNote; i++)
            {
                int sampleIndex = startSample + i;
                if (sampleIndex >= totalSamples) break;

                float t = (float)i / sampleRate;
                float wave = Mathf.Sin(2 * Mathf.PI * freq * t);

                float envelope = 1f;
                float attackTime = 0.05f;
                float releaseTime = 0.15f;
                float noteTime = (float)i / sampleRate;

                if (noteTime < attackTime)
                    envelope = noteTime / attackTime;
                else if (noteTime > noteDuration - releaseTime)
                    envelope = (noteDuration - noteTime) / releaseTime;

                samples[sampleIndex] = wave * envelope * 0.4f;
            }
        }

        clip.SetData(samples, 0);
        audioSource.clip = clip;
        audioSource.Play();

        isInitialized = true;
        isPlaying = true;
        lastVisibilityState = AITVisibilityHelper.IsVisible;
        UpdateStatus();
    }

    private void RegisterVisibilityCallback()
    {
        AITVisibilityHelper.OnVisibilityChanged += OnVisibilityChanged;
        Debug.Log("[VisibilityBGMTester] Visibility callback registered");
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        visibilityChangeCount++;
        lastVisibilityState = isVisible;

        if (audioSource == null || !isPlaying) return;

        if (isVisible)
        {
            audioSource.UnPause();
        }
        else
        {
            audioSource.Pause();
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (audioSource == null) return;

        string state = audioSource.isPlaying ? "재생 중" : "일시정지";
        statusText = visibilityChangeCount > 0
            ? $"{state} ({visibilityChangeCount}회 전환)"
            : state;

        Debug.Log($"[VisibilityBGMTester] {state} (isPlaying: {audioSource.isPlaying}, count: {visibilityChangeCount})");
    }

    /// <summary>
    /// Visibility BGM 테스터 UI를 렌더링합니다.
    /// </summary>
    public void DrawUI(GUIStyle boxStyle, GUIStyle groupHeaderStyle, GUIStyle labelStyle, GUIStyle buttonStyle)
    {
        if (!isInitialized) return;

        GUILayout.BeginVertical(boxStyle);

        // 섹션 헤더
        GUILayout.Label("🎵 Visibility BGM Tester", groupHeaderStyle);
        GUILayout.Label("백그라운드 전환 시 BGM 일시정지/재생 테스트", labelStyle);

        GUILayout.Space(10);

        // 상태 표시
        GUIStyle statusStyle = new GUIStyle(labelStyle);
        statusStyle.fontStyle = FontStyle.Bold;
        statusStyle.normal.textColor = lastVisibilityState ? Color.green : Color.yellow;
        GUILayout.Label(statusText, statusStyle);

        string infoText = $"IsVisible: {AITVisibilityHelper.IsVisible} | 이벤트 수신: {visibilityChangeCount}회";
        GUILayout.Label(infoText, labelStyle);

        GUILayout.Space(10);

        // 컨트롤 버튼
        if (!isPlaying)
        {
            if (GUILayout.Button("▶ BGM 재생", buttonStyle, GUILayout.Height(40)))
            {
                StartBGM();
            }
        }
        else
        {
            if (GUILayout.Button("⏹ BGM 정지", buttonStyle, GUILayout.Height(40)))
            {
                StopBGM();
            }
        }

        GUILayout.EndVertical();
    }

    private void StartBGM()
    {
        if (audioSource != null)
        {
            audioSource.Play();
            isPlaying = true;
            UpdateStatus();
        }
    }

    private void StopBGM()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            isPlaying = false;
            statusText = "정지됨";
            Debug.Log("[VisibilityBGMTester] 정지됨 (isPlaying: false)");
        }
    }
}
