using UnityEngine;
using UnityEngine.UI;
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

    // uGUI 참조
    private Text _statusTextUI;
    private Text _infoTextUI;
    private Button _toggleButton;
    private Text _toggleButtonText;

    void Start()
    {
        Debug.Log("[VisibilityBGMTester] Initializing...");
        EnsureInitialized();
        RegisterVisibilityCallback();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;
        InitializeAudio();
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
        UpdateUI();
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
    /// uGUI 기반 UI를 생성합니다.
    /// </summary>
    public void SetupUI(Transform parent)
    {
        EnsureInitialized();

        var section = UIBuilder.CreatePanel(parent, UIBuilder.Theme.SectionBg);
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingNormal;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        section.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(section, "Visibility BGM Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "백그라운드 전환 시 BGM 일시정지/재생 테스트",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        _statusTextUI = UIBuilder.CreateText(section, statusText,
            UIBuilder.Theme.FontNormal, lastVisibilityState ? Color.green : Color.yellow, fontStyle: FontStyle.Bold);

        _infoTextUI = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        _toggleButton = UIBuilder.CreateButton(section, "", onClick: ToggleBGM);
        _toggleButtonText = _toggleButton.GetComponentInChildren<Text>();

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_statusTextUI != null)
        {
            _statusTextUI.text = statusText;
            _statusTextUI.color = lastVisibilityState ? Color.green : Color.yellow;
        }

        if (_infoTextUI != null)
        {
            _infoTextUI.text = $"IsVisible: {AITVisibilityHelper.IsVisible} | 이벤트 수신: {visibilityChangeCount}회";
        }

        if (_toggleButtonText != null)
        {
            _toggleButtonText.text = isPlaying ? "BGM 정지" : "BGM 재생";
        }
    }

    private void ToggleBGM()
    {
        if (isPlaying) StopBGM(); else StartBGM();
        UpdateUI();
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
