using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

/// <summary>
/// game posture 픽스처의 영속 오브젝트("AITGameDriver"). 오디오·점수 저장·체크섬·상태 보고·JS 명령(SendMessage)을 맡는다.
/// 첫 AITGameScene 이 Awake 에서 만든다(AITGameScene 의 씬 로드 훅은 루트 "AITGameScene" 이 있는 픽스처 씬에서만 동작한다).
/// </summary>
[Preserve]
public class AITGameDriver : MonoBehaviour
{
    public const string HighKey = "ait_game_high";
    public static AITGameDriver Instance;

    // 에셋(첫 AITGameScene 에서 복사)
    private AudioClip sfxHit;
    private AudioClip sfxBreak;
    private AudioClip sfxClick;
    public Font UiFont;
    public Material[] BrickMaterials;
    public Material PaddleMaterial;
    public Material BallMaterial;
    public Material BackgroundMaterial;

    private AudioSource bgm;
    private AudioSource sfx;
    private int sfxCount;

    // 게임 진행
    public int RoundSeconds = 30;
    public float BallSpeed = 7.5f;
    public int Rows = 5;
    public int Cols = 8;
    private int lastScore;
    private int rounds;
    private int bootHighScore;
    private int titleHighScore;
    private bool sceneReady;
    private bool finishing;
    private readonly List<KeyValuePair<string, RectTransform>> buttons = new List<KeyValuePair<string, RectTransform>>();

    // 보고
    private int frame;
    private float nextReport;
    private int resTexW;
    private int resTexH;
    private bool jsonLoaded;
    private string jsonValue = "";
    private string jsonError = "";
    private int pauseEvents;
    private int focusEvents;
    private bool lastPause;
    private bool lastFocus = true;
    private int errorCount;
    private int warnCount;
    private readonly List<string> lastErrors = new List<string>();
    private int coroutineTicks;
    private int asyncTicks;
    private bool asyncDone;
    private string caught = "";
    private int commandCount;
    private string lastCommand = "";
    private bool bootStarted;
    private readonly AITGameChecksumInfo checksum = new AITGameChecksumInfo();

    public static AITGameDriver Ensure(AITGameScene src)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AITGameDriver");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<AITGameDriver>();
        d.Init(src);
        return d;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Application.logMessageReceived += OnLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        if (Instance == this) Instance = null;
    }

    private void Init(AITGameScene src)
    {
        sfxHit = src.sfxHit;
        sfxBreak = src.sfxBreak;
        sfxClick = src.sfxClick;
        UiFont = src.font;
        BrickMaterials = src.matBricks;
        PaddleMaterial = src.matPaddle;
        BallMaterial = src.matBall;
        BackgroundMaterial = src.matBackground;
        bootHighScore = PlayerPrefs.GetInt(HighKey, 0);

        gameObject.AddComponent<AudioListener>();
        gameObject.AddComponent<EventSystem>();
        gameObject.AddComponent<StandaloneInputModule>();

        bgm = gameObject.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.loop = true;
        bgm.volume = 0.5f;
        bgm.clip = src.bgm;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        if (bgm.clip != null) bgm.Play();
    }

    // ---- 씬 훅 ----

    public void OnSceneBegin()
    {
        sceneReady = false;
        finishing = false;
        buttons.Clear();
    }

    public void OnSceneReady()
    {
        sceneReady = true;
        ReportNow();
    }

    public void RegisterButton(string name, RectTransform rt)
    {
        buttons.Add(new KeyValuePair<string, RectTransform>(name, rt));
    }

    public int BootHighScore { get { return bootHighScore; } }

    public void SetTitleHighScore(int v)
    {
        titleHighScore = v;
    }

    public void SetResourceTexture(int w, int h)
    {
        resTexW = w;
        resTexH = h;
    }

    public int LastScore { get { return lastScore; } }

    public void StartBootOnce()
    {
        if (bootStarted) return;
        bootStarted = true;
        StartCoroutine(Boot());
    }

    // ---- 사운드 ----

    private void Sfx(AudioClip c)
    {
        if (c == null || sfx == null) return;
        sfx.PlayOneShot(c, 0.7f);
        sfxCount++;
    }

    public void PlayHit() { Sfx(sfxHit); }
    public void PlayBreak() { Sfx(sfxBreak); }
    public void PlayClick() { Sfx(sfxClick); }

    // ---- 라운드 / 씬 전환 ----

    public void LoadScene(string name)
    {
        if (finishing) return;
        finishing = true;
        SceneManager.LoadScene(name);
    }

    public void FinishRound(int score)
    {
        if (finishing) return;
        lastScore = score;
        rounds++;
        int prev = PlayerPrefs.GetInt(HighKey, 0);
        if (score > prev) PlayerPrefs.SetInt(HighKey, score);
        PlayerPrefs.Save();
        ReportNow();
        LoadScene("GameResult");
    }

    // ---- 부팅 작업(체크섬·코루틴·async·예외·StreamingAssets) ----

    private IEnumerator Boot()
    {
        yield return null;
        yield return null;

        // try/catch of an explicitly thrown exception (ExplicitlyThrownExceptionsOnly 에서도 잡혀야 한다)
        try
        {
            ThrowProbe();
        }
        catch (InvalidOperationException e)
        {
            caught = e.Message;
        }

        var ignored = AsyncProbe();
        StartCoroutine(CoroutineProbe());
        StartCoroutine(LoadStreamingJson());

        float t0 = Time.realtimeSinceStartup;
        checksum.rng = AITGameChecksum.Rng();
        checksum.math = AITGameChecksum.MathSum();
        checksum.hash = AITGameChecksum.Hashing();
        checksum.collections = AITGameChecksum.Collections();
        checksum.json = AITGameChecksum.JsonRoundTrip();
        yield return null;
        string phys = "";
        yield return AITGameChecksum.PhysicsProbe(r => phys = r);
        checksum.physics = phys;
        checksum.combined = AITGameChecksum.Fnv(string.Join("|", new string[]
        {
            checksum.rng, checksum.math, checksum.hash, checksum.collections, checksum.json, checksum.physics
        }));
        checksum.computeMs = Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f);
        checksum.done = true;
        ReportNow();
    }

    private static void ThrowProbe()
    {
        throw new InvalidOperationException("ait-game-expected-exception");
    }

    private async Task AsyncProbe()
    {
        try
        {
            for (int i = 0; i < 3; i++)
            {
                await Task.Yield();
                asyncTicks++;
            }
            asyncDone = true;
        }
        catch (Exception e)
        {
            caught += "|async:" + e.Message;
        }
    }

    private IEnumerator CoroutineProbe()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.2f);
            coroutineTicks++;
        }
    }

    private IEnumerator LoadStreamingJson()
    {
        string url = Application.streamingAssetsPath + "/ait-game/level.json";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var cfg = JsonUtility.FromJson<AITGameLevelConfig>(req.downloadHandler.text);
                if (cfg != null)
                {
                    jsonValue = cfg.secret ?? "";
                    if (cfg.ballSpeed > 0f) BallSpeed = cfg.ballSpeed;
                    if (cfg.brickRows > 0) Rows = cfg.brickRows;
                    if (cfg.brickCols > 0) Cols = cfg.brickCols;
                    jsonLoaded = true;
                }
                else
                {
                    jsonError = "parse-null";
                }
            }
            else
            {
                jsonError = req.error ?? "unknown";
            }
        }
        ReportNow();
    }

    // ---- 로그 / 생명주기 ----

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            errorCount++;
            string msg = condition ?? "";
            if (msg.Length > 200) msg = msg.Substring(0, 200);
            lastErrors.Add(type + ": " + msg);
            if (lastErrors.Count > 5) lastErrors.RemoveAt(0);
        }
        else if (type == LogType.Warning)
        {
            warnCount++;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        pauseEvents++;
        lastPause = paused;
    }

    private void OnApplicationFocus(bool focused)
    {
        focusEvents++;
        lastFocus = focused;
    }

    // ---- JS 명령 (unityInstance.SendMessage('AITGameDriver','Command', json)) ----

    [Preserve]
    public void Command(string json)
    {
        commandCount++;
        AITGameCmd cmd = null;
        try
        {
            cmd = JsonUtility.FromJson<AITGameCmd>(json);
        }
        catch (Exception)
        {
            lastCommand = "bad-json";
            ReportNow();
            return;
        }
        if (cmd == null || string.IsNullOrEmpty(cmd.cmd))
        {
            lastCommand = "empty";
            ReportNow();
            return;
        }
        lastCommand = cmd.cmd;
        switch (cmd.cmd)
        {
            case "ping":
                break;
            case "setRoundSeconds":
                RoundSeconds = Mathf.Max(1, Mathf.RoundToInt(cmd.value));
                if (AITGamePlay.Active != null) AITGamePlay.Active.SetTimeLeft(RoundSeconds);
                break;
            case "endRound":
                if (AITGamePlay.Active != null) AITGamePlay.Active.EndRound();
                break;
            case "clearPrefs":
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                bootHighScore = 0;
                titleHighScore = 0;
                break;
            default:
                lastCommand = "unknown:" + cmd.cmd;
                break;
        }
        ReportNow();
    }

    // ---- 상태 보고 ----

    private void Update()
    {
        frame++;
        if (Time.unscaledTime >= nextReport)
        {
            nextReport = Time.unscaledTime + 0.2f;
            ReportNow();
        }
    }

    public void ReportNow()
    {
        try
        {
            AITGameBridge.Report(JsonUtility.ToJson(BuildState()));
        }
        catch (Exception e)
        {
            // 보고 실패가 게임을 죽이면 안 된다. Debug.Log 는 오류 카운트에 안 잡힌다.
            Debug.Log("[AITGameDriver] report failed: " + e.Message);
        }
    }

    private AITGameState BuildState()
    {
        var s = new AITGameState();
        s.scene = SceneManager.GetActiveScene().name;
        s.ready = sceneReady;
        s.frame = frame;
        s.time = AITGameUI.Safe(Time.realtimeSinceStartup);
        s.screenW = Screen.width;
        s.screenH = Screen.height;
        s.unityVersion = Application.unityVersion;

        var bl = new List<AITGameButtonInfo>();
        for (int i = 0; i < buttons.Count; i++)
        {
            var rt = buttons[i].Value;
            if (rt != null && rt.gameObject.activeInHierarchy) bl.Add(AITGameUI.Describe(buttons[i].Key, rt));
        }
        s.buttons = bl.ToArray();

        s.ball = new AITGameVec();
        s.paddle = new AITGameVec();
        s.pointer = new AITGameVec();
        var play = AITGamePlay.Active;
        if (play != null)
        {
            play.FillState(s);
        }
        else
        {
            s.score = lastScore;
        }
        s.roundSeconds = RoundSeconds;
        s.rounds = rounds;
        s.highScore = PlayerPrefs.GetInt(HighKey, 0);
        s.bootHighScore = bootHighScore;
        s.titleHighScore = titleHighScore;
        s.lastScore = lastScore;

        s.audio = new AITGameAudioInfo();
        if (bgm != null)
        {
            s.audio.bgmPlaying = bgm.isPlaying;
            s.audio.bgmTime = AITGameUI.Safe(bgm.time);
            s.audio.bgmLength = bgm.clip != null ? AITGameUI.Safe(bgm.clip.length) : 0f;
        }
        s.audio.sfxCount = sfxCount;

        s.resTexW = resTexW;
        s.resTexH = resTexH;
        s.jsonLoaded = jsonLoaded;
        s.jsonValue = jsonValue;
        s.jsonError = jsonError;
        s.pauseEvents = pauseEvents;
        s.focusEvents = focusEvents;
        s.lastPause = lastPause;
        s.lastFocus = lastFocus;
        s.errorCount = errorCount;
        s.warnCount = warnCount;
        s.lastErrors = lastErrors.ToArray();
        s.coroutineTicks = coroutineTicks;
        s.asyncTicks = asyncTicks;
        s.asyncDone = asyncDone;
        s.caught = caught;
        s.commandCount = commandCount;
        s.lastCommand = lastCommand;
        s.checksum = checksum;
        return s;
    }
}
