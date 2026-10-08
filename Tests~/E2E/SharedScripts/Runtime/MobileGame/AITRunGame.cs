using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UI;

/// <summary>
/// perf mobilegame posture 픽스처: 세로 화면 탭 점프 러너(하이퍼캐주얼 미니게임 형태).
///
/// 화면 구성: 패럴랙스 배경 2겹 + 구름 + 스크롤 바닥 타일, 달리기 애니메이션 플레이어, 장애물(낮음/높음), 코인 아크, 코인 획득 파티클,
/// uGUI(점수·최고 점수·타이틀·시작/다시하기 버튼·게임오버 패널), BGM 루프와 효과음, PlayerPrefs 최고 점수 저장.
///
/// 시뮬레이션은 FixedUpdate 에서만 돌고 엔진 물리를 쓰지 않는다(AABB 판정). 난수는 고정 시드 xorshift 라서 같은 입력이면
/// 프레임레이트와 무관하게 같은 결과가 나오고, 자동 플레이(autoplay)로 <see cref="ChecksumStep"/> 스텝까지 돈 상태의 해시를
/// 체크섬으로 보고한다. SDK 최적화 ON/OFF 또는 SDK 버전이 달라도 체크섬이 같아야 게임 동작이 같다는 뜻이다.
///
/// 상태는 0.25초마다 window.__AIT_RUN 으로 보고하고(AITRunBridge), unityInstance.SendMessage("AITRunScene", "Cmd", ...) 로
/// 제어한다: start, retry, autoplay, manual, stats(프레임 통계 초기화), over(강제 종료).
/// </summary>
[Preserve]
public class AITRunGame : MonoBehaviour
{
    public const string BestKey = "ait_run_best";
    public const int ChecksumStep = 1500;           // 50Hz 고정 스텝 기준 30초

    // 월드 상수(세로 화면, 카메라 orthographicSize 5 → 높이 10 유닛)
    private const float GroundTop = -3f;
    private const float PlayerX = -1.6f;
    private const float PlayerHalfW = 0.32f;
    private const float PlayerHalfH = 0.42f;
    private const float Gravity = 34f;
    private const float JumpVelocity = 13f;
    private const float StartSpeed = 5.5f;
    private const float MaxSpeed = 11f;
    private const int ObstaclePool = 8;
    private const int CoinPool = 16;
    private const int SparkPool = 40;
    // 시뮬레이션 생성·제거 경계(화면 비율과 무관한 고정값 — 체크섬이 뷰포트에 따라 바뀌지 않게)
    private const float SimSpawnAhead = 9f;
    private const float SimDespawnBehind = 10f;
    private static readonly float[] CoinArc = { 0f, 0.45f, 0.6f, 0.45f, 0f };

    private enum State { Title, Playing, Over }

    private struct Obstacle { public bool Active; public float X; public float HalfW; public float HalfH; public bool Tall; }
    private struct CoinItem { public bool Active; public float X; public float Y; }
    private struct Particle { public bool Active; public Vector2 Pos; public Vector2 Vel; public float Life; }

    private AITRunScene refs;
    private Camera cam;
    private float halfWidth;

    // 시뮬레이션 상태(체크섬 대상)
    private State state = State.Title;
    private int step;
    private float worldX;
    private float speed;
    private float playerY;
    private float playerVy;
    private bool grounded;
    private bool jumpQueued;
    private int jumps;
    private int coins;
    private int score;
    private float nextSpawnX;
    private int spawned;
    private uint rng;
    private readonly Obstacle[] obstacles = new Obstacle[ObstaclePool];
    private readonly CoinItem[] coinItems = new CoinItem[CoinPool];
    private string checksum = "";
    private int checksumAt;

    // 진행 정보
    private bool autoplay;
    private int runs;
    private int best;
    private int bootBest;
    private int taps;
    private int sfxCount;
    private int errorCount;
    private string lastError = "";
    private int pauseEvents;
    private int focusEvents;
    private int commandCount;
    private string lastCommand = "";

    // 프레임 통계(플레이 중 unscaledDeltaTime, ms)
    private readonly List<float> frameMs = new List<float>(4096);
    private float nextReport;

    // 표현
    private SpriteRenderer playerSr;
    private readonly SpriteRenderer[] obstacleSr = new SpriteRenderer[ObstaclePool];
    private readonly SpriteRenderer[] coinSr = new SpriteRenderer[CoinPool];
    private readonly SpriteRenderer[] sparkSr = new SpriteRenderer[SparkPool];
    private readonly Particle[] sparks = new Particle[SparkPool];
    private readonly List<SpriteRenderer> groundTiles = new List<SpriteRenderer>();
    private SpriteRenderer[] farLayer;
    private SpriteRenderer[] nearLayer;
    private SpriteRenderer[] clouds;
    private float groundTileW = 2f;
    private float farW = 10f;
    private float nearW = 10f;

    private AudioSource bgm;
    private AudioSource sfx;

    private Text scoreText;
    private Text bestText;
    private GameObject titlePanel;
    private GameObject overPanel;
    private Text overScoreText;
    private RectTransform startButton;
    private RectTransform retryButton;

    public void Init(AITRunScene src)
    {
        refs = src;
        Application.logMessageReceived += OnLog;
        best = bootBest = PlayerPrefs.GetInt(BestKey, 0);

        SetupCamera();
        SetupAudio();
        SetupWorld();
        SetupUi();
        ResetRun();
        SyncVisuals();
        Report();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    private void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errorCount++;
            lastError = message.Length > 200 ? message.Substring(0, 200) : message;
        }
    }

    // ---------------------------------------------------------------- 셋업

    private void SetupCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.72f, 0.95f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        go.AddComponent<AudioListener>();
        halfWidth = 5f * Mathf.Max(0.4f, (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    private void SetupAudio()
    {
        bgm = gameObject.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.loop = true;
        bgm.volume = 0.45f;
        bgm.clip = refs.Bgm;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
    }

    private SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private void SetupWorld()
    {
        if (refs.BgFar != null) farW = refs.BgFar.bounds.size.x;
        if (refs.BgNear != null) nearW = refs.BgNear.bounds.size.x;
        if (refs.Ground != null) groundTileW = refs.Ground.bounds.size.x;

        farLayer = new SpriteRenderer[3];
        nearLayer = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            farLayer[i] = MakeSprite("bgFar" + i, refs.BgFar, -30, new Vector3(0f, 0f, 5f));
            float nearH = refs.BgNear != null ? refs.BgNear.bounds.size.y : 5f;
            nearLayer[i] = MakeSprite("bgNear" + i, refs.BgNear, -20, new Vector3(0f, GroundTop + nearH * 0.5f - 0.2f, 4f));
        }
        clouds = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++) clouds[i] = MakeSprite("cloud" + i, refs.Cloud, -25, new Vector3(0f, 2.2f + i * 0.9f, 4.5f));

        int tiles = Mathf.CeilToInt(halfWidth * 2f / groundTileW) + 2;
        float groundH = refs.Ground != null ? refs.Ground.bounds.size.y : 2f;
        for (int i = 0; i < tiles; i++)
            groundTiles.Add(MakeSprite("ground" + i, refs.Ground, -10, new Vector3(0f, GroundTop - groundH * 0.5f, 0f)));

        playerSr = MakeSprite("player", refs.Player0, 10, new Vector3(PlayerX, GroundTop + PlayerHalfH, 0f));
        for (int i = 0; i < ObstaclePool; i++)
        {
            obstacleSr[i] = MakeSprite("obstacle" + i, refs.Obstacle, 5, Vector3.zero);
            obstacleSr[i].enabled = false;
        }
        for (int i = 0; i < CoinPool; i++)
        {
            coinSr[i] = MakeSprite("coin" + i, refs.Coin, 6, Vector3.zero);
            coinSr[i].enabled = false;
        }
        for (int i = 0; i < SparkPool; i++)
        {
            sparkSr[i] = MakeSprite("spark" + i, refs.Spark, 20, Vector3.zero);
            sparkSr[i].enabled = false;
        }
    }

    private Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchorPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchorPos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = refs.Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(3f, -3f);
        return t;
    }

    private RectTransform MakeButton(Transform parent, string name, string label, Vector2 anchorPos, Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchorPos;
        rt.sizeDelta = new Vector2(560f, 200f);
        var img = go.AddComponent<Image>();
        img.sprite = refs.Button;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        var t = MakeText(go.transform, "Label", 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(560f, 200f));
        t.text = label;
        return rt;
    }

    private GameObject MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private void SetupUi()
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = canvasGo.transform;

        scoreText = MakeText(root, "Score", 72, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(600f, 120f));
        scoreText.rectTransform.pivot = new Vector2(0f, 1f);
        scoreText.rectTransform.anchoredPosition = new Vector2(48f, -48f);
        bestText = MakeText(root, "Best", 52, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(600f, 100f));
        bestText.rectTransform.pivot = new Vector2(1f, 1f);
        bestText.rectTransform.anchoredPosition = new Vector2(-48f, -60f);

        titlePanel = MakePanel(root, "TitlePanel", new Color(0f, 0f, 0f, 0.25f));
        var title = MakeText(titlePanel.transform, "Title", 140, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.68f), new Vector2(1000f, 220f));
        title.text = "TAP RUNNER";
        var hint = MakeText(titlePanel.transform, "Hint", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.58f), new Vector2(1000f, 100f));
        hint.text = "Tap to jump. Grab coins!";
        startButton = MakeButton(titlePanel.transform, "StartButton", "START", new Vector2(0.5f, 0.42f), StartRun);

        overPanel = MakePanel(root, "OverPanel", new Color(0f, 0f, 0f, 0.55f));
        var over = MakeText(overPanel.transform, "GameOver", 130, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.66f), new Vector2(1000f, 200f));
        over.text = "GAME OVER";
        overScoreText = MakeText(overPanel.transform, "FinalScore", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.56f), new Vector2(1000f, 120f));
        retryButton = MakeButton(overPanel.transform, "RetryButton", "RETRY", new Vector2(0.5f, 0.42f), Retry);
        overPanel.SetActive(false);
    }

    // ---------------------------------------------------------------- 상태 전환

    private void ResetRun()
    {
        step = 0;
        worldX = 0f;
        speed = StartSpeed;
        playerY = GroundTop + PlayerHalfH;
        playerVy = 0f;
        grounded = true;
        jumpQueued = false;
        jumps = 0;
        coins = 0;
        score = 0;
        spawned = 0;
        nextSpawnX = 9f;
        rng = 0x9E3779B9u;
        checksum = "";
        checksumAt = 0;
        for (int i = 0; i < ObstaclePool; i++) obstacles[i].Active = false;
        for (int i = 0; i < CoinPool; i++) coinItems[i].Active = false;
    }

    public void StartRun()
    {
        if (state == State.Playing) return;
        PlaySfx(refs.SfxClick);
        ResetRun();
        runs++;
        state = State.Playing;
        titlePanel.SetActive(false);
        overPanel.SetActive(false);
        if (bgm.clip != null && !bgm.isPlaying) bgm.Play();
        Report();
    }

    public void Retry()
    {
        if (state != State.Over) return;
        state = State.Title;   // StartRun 가드 통과용
        StartRun();
    }

    private void EndRun()
    {
        if (state != State.Playing) return;
        state = State.Over;
        PlaySfx(refs.SfxHit);
        if (checksum.Length == 0) RecordChecksum();
        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
        }
        overScoreText.text = "SCORE " + score + "   BEST " + best;
        overPanel.SetActive(true);
        Report();
    }

    // ---------------------------------------------------------------- 명령(JS SendMessage)

    public void Cmd(string command)
    {
        commandCount++;
        lastCommand = command ?? "";
        switch (lastCommand)
        {
            case "start": StartRun(); break;
            case "retry": Retry(); break;
            case "autoplay": autoplay = true; break;
            case "manual": autoplay = false; break;
            case "stats": frameMs.Clear(); break;
            case "over": EndRun(); break;
        }
        Report();
    }

    private void OnApplicationPause(bool paused)
    {
        pauseEvents++;
    }

    private void OnApplicationFocus(bool focused)
    {
        focusEvents++;
    }

    // ---------------------------------------------------------------- 시뮬레이션

    private uint NextRand()
    {
        unchecked
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return rng;
        }
    }

    private float RandRange(float min, float max)
    {
        return min + (NextRand() % 10000u) / 10000f * (max - min);
    }

    private void FixedUpdate()
    {
        if (state != State.Playing) return;
        const float dt = 0.02f;   // Time.fixedDeltaTime 기본값과 같은 고정 값(설정과 무관하게 결정론 유지)
        step++;

        speed = Mathf.Min(MaxSpeed, StartSpeed + step * 0.004f);
        worldX += speed * dt;

        if (autoplay) AutoplayDecide();
        if (jumpQueued && grounded)
        {
            playerVy = JumpVelocity;
            grounded = false;
            jumps++;
            PlaySfx(refs.SfxJump);
        }
        jumpQueued = false;

        playerVy -= Gravity * dt;
        playerY += playerVy * dt;
        float floorY = GroundTop + PlayerHalfH;
        if (playerY <= floorY)
        {
            playerY = floorY;
            playerVy = 0f;
            grounded = true;
        }

        while (worldX + SimSpawnAhead >= nextSpawnX) Spawn();

        // 판정(월드 좌표: 플레이어는 worldX + PlayerX 에 있다)
        float px = worldX + PlayerX;
        for (int i = 0; i < ObstaclePool; i++)
        {
            if (!obstacles[i].Active) continue;
            var o = obstacles[i];
            if (o.X + o.HalfW < worldX - SimDespawnBehind) { obstacles[i].Active = false; continue; }
            float oy = GroundTop + o.HalfH;
            if (Mathf.Abs(o.X - px) < o.HalfW + PlayerHalfW * 0.8f &&
                Mathf.Abs(oy - playerY) < o.HalfH + PlayerHalfH * 0.85f)
            {
                EndRun();
                return;
            }
        }
        for (int i = 0; i < CoinPool; i++)
        {
            if (!coinItems[i].Active) continue;
            var c = coinItems[i];
            if (c.X < worldX - SimDespawnBehind) { coinItems[i].Active = false; continue; }
            if (Mathf.Abs(c.X - px) < 0.55f && Mathf.Abs(c.Y - playerY) < 0.65f)
            {
                coinItems[i].Active = false;
                coins++;
                PlaySfx(refs.SfxCoin);
                Burst(new Vector2(PlayerX + (c.X - px), c.Y));
            }
        }

        score = (int)worldX + coins * 10;
        if (step == ChecksumStep) RecordChecksum();
    }

    private void AutoplayDecide()
    {
        if (!grounded) return;
        float px = worldX + PlayerX;
        float nearest = float.MaxValue;
        for (int i = 0; i < ObstaclePool; i++)
        {
            if (!obstacles[i].Active) continue;
            float front = obstacles[i].X - obstacles[i].HalfW - (px + PlayerHalfW);
            if (front > -0.1f && front < nearest) nearest = front;
        }
        if (nearest < speed * 0.2f + 0.05f) jumpQueued = true;
    }

    private void Spawn()
    {
        spawned++;
        uint roll = NextRand() % 100u;
        if (roll < 62)
        {
            bool tall = (NextRand() % 100u) < 35;
            for (int i = 0; i < ObstaclePool; i++)
            {
                if (obstacles[i].Active) continue;
                obstacles[i] = new Obstacle { Active = true, X = nextSpawnX, HalfW = 0.42f, HalfH = tall ? 0.9f : 0.5f, Tall = tall };
                break;
            }
            // 장애물 위 코인(점프 경로)
            if ((NextRand() % 100u) < 50) PlaceCoin(nextSpawnX, GroundTop + (tall ? 3.0f : 2.4f));
        }
        else
        {
            int n = 3 + (int)(NextRand() % 3u);
            float baseY = GroundTop + RandRange(0.5f, 1.6f);
            for (int k = 0; k < n; k++) PlaceCoin(nextSpawnX + k * 0.9f, baseY + CoinArc[k]);
        }
        nextSpawnX += RandRange(5.5f, 9f) + speed * 0.25f;
    }

    private void PlaceCoin(float x, float y)
    {
        for (int i = 0; i < CoinPool; i++)
        {
            if (coinItems[i].Active) continue;
            coinItems[i] = new CoinItem { Active = true, X = x, Y = y };
            return;
        }
    }

    private void RecordChecksum()
    {
        unchecked
        {
            uint h = 2166136261u;
            void Mix(int v)
            {
                for (int b = 0; b < 4; b++)
                {
                    h ^= (uint)((v >> (b * 8)) & 0xFF);
                    h *= 16777619u;
                }
            }
            Mix(step);
            Mix(score);
            Mix(coins);
            Mix(jumps);
            Mix(spawned);
            Mix((int)Mathf.Round(worldX * 1000f));
            Mix((int)Mathf.Round(playerY * 1000f));
            Mix((int)rng);
            for (int i = 0; i < ObstaclePool; i++)
                if (obstacles[i].Active) Mix((int)Mathf.Round(obstacles[i].X * 100f));
            checksum = h.ToString("x8");
            checksumAt = step;
        }
    }

    // ---------------------------------------------------------------- 입력·표현

    private void Update()
    {
        if (state == State.Playing)
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            if (frameMs.Count > 6000) frameMs.RemoveRange(0, 1000);
            if (Input.GetMouseButtonDown(0))
            {
                taps++;
                if (!autoplay) jumpQueued = true;
            }
        }
        UpdateSparks(Time.deltaTime);
        SyncVisuals();
        if (Time.unscaledTime >= nextReport)
        {
            nextReport = Time.unscaledTime + 0.25f;
            Report();
        }
    }

    private void SyncVisuals()
    {
        // 배경 패럴랙스(화면 폭을 덮도록 3장 순환)
        Scroll(farLayer, farW, worldX * 0.1f, 0f);
        Scroll(nearLayer, nearW, worldX * 0.4f, nearLayer[0].transform.position.y);
        for (int i = 0; i < clouds.Length; i++)
        {
            float span = halfWidth * 2f + 4f;
            float x = Mathf.Repeat(i * span / 3f - worldX * 0.2f - Time.time * 0.15f, span) - span * 0.5f;
            clouds[i].transform.position = new Vector3(x, clouds[i].transform.position.y, 4.5f);
        }
        float gOffset = Mathf.Repeat(worldX, groundTileW);
        for (int i = 0; i < groundTiles.Count; i++)
        {
            var t = groundTiles[i].transform;
            t.position = new Vector3(-halfWidth - groundTileW * 0.5f + i * groundTileW - gOffset + groundTileW, t.position.y, 0f);
        }

        playerSr.transform.position = new Vector3(PlayerX, playerY, 0f);
        if (!grounded) playerSr.sprite = refs.PlayerJump;
        else playerSr.sprite = (state == State.Playing && ((int)(Time.time * 10f) & 1) == 1) ? refs.Player1 : refs.Player0;

        for (int i = 0; i < ObstaclePool; i++)
        {
            var o = obstacles[i];
            var sr = obstacleSr[i];
            sr.enabled = o.Active;
            if (!o.Active) continue;
            sr.sprite = o.Tall ? refs.ObstacleTall : refs.Obstacle;
            sr.transform.position = new Vector3(o.X - worldX, GroundTop + o.HalfH, 0f);
        }
        float spin = Mathf.Abs(Mathf.Sin(Time.time * 4f));
        for (int i = 0; i < CoinPool; i++)
        {
            var c = coinItems[i];
            var sr = coinSr[i];
            sr.enabled = c.Active;
            if (!c.Active) continue;
            sr.transform.position = new Vector3(c.X - worldX, c.Y, 0f);
            sr.transform.localScale = new Vector3(0.3f + 0.7f * spin, 1f, 1f);
        }

        scoreText.text = "SCORE " + score;
        bestText.text = "BEST " + best;
    }

    private static void Scroll(SpriteRenderer[] layer, float w, float offset, float y)
    {
        float o = Mathf.Repeat(offset, w);
        for (int i = 0; i < layer.Length; i++)
            layer[i].transform.position = new Vector3((i - 1) * w - o, y, layer[i].transform.position.z);
    }

    private void Burst(Vector2 at)
    {
        int made = 0;
        for (int i = 0; i < SparkPool && made < 8; i++)
        {
            if (sparks[i].Active) continue;
            float a = made * Mathf.PI / 4f;
            sparks[i] = new Particle { Active = true, Pos = at, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.6f) * 3.2f, Life = 0.5f };
            made++;
        }
    }

    private void UpdateSparks(float dt)
    {
        for (int i = 0; i < SparkPool; i++)
        {
            var sr = sparkSr[i];
            if (!sparks[i].Active) { if (sr.enabled) sr.enabled = false; continue; }
            sparks[i].Life -= dt;
            if (sparks[i].Life <= 0f) { sparks[i].Active = false; sr.enabled = false; continue; }
            sparks[i].Vel.y -= 9f * dt;
            sparks[i].Pos += sparks[i].Vel * dt;
            sr.enabled = true;
            sr.transform.position = new Vector3(sparks[i].Pos.x, sparks[i].Pos.y, -1f);
            sr.color = new Color(1f, 0.9f, 0.3f, sparks[i].Life / 0.5f);
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        sfx.PlayOneShot(clip, 0.8f);
        sfxCount++;
    }

    // ---------------------------------------------------------------- 보고

    private static string F(float v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void AppendRect(StringBuilder sb, string name, RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);   // 오버레이 캔버스: 화면 픽셀(좌하단 원점)
        bool visible = rt.gameObject.activeInHierarchy;
        sb.Append(",\"").Append(name).Append("\":{\"visible\":").Append(visible ? "true" : "false")
          .Append(",\"x\":").Append(F(c[0].x)).Append(",\"y\":").Append(F(Screen.height - c[1].y))
          .Append(",\"w\":").Append(F(c[2].x - c[0].x)).Append(",\"h\":").Append(F(c[1].y - c[0].y)).Append('}');
    }

    private void AppendFrameStats(StringBuilder sb)
    {
        int n = frameMs.Count;
        sb.Append(",\"frames\":").Append(n);
        if (n == 0) return;
        var sorted = frameMs.ToArray();
        Array.Sort(sorted);
        float sum = 0f;
        int longFrames = 0;
        for (int i = 0; i < n; i++)
        {
            sum += sorted[i];
            if (sorted[i] > 50f) longFrames++;
        }
        sb.Append(",\"avgMs\":").Append(F(sum / n))
          .Append(",\"p50Ms\":").Append(F(sorted[n / 2]))
          .Append(",\"p95Ms\":").Append(F(sorted[Mathf.Min(n - 1, (int)(n * 0.95f))]))
          .Append(",\"p99Ms\":").Append(F(sorted[Mathf.Min(n - 1, (int)(n * 0.99f))]))
          .Append(",\"maxMs\":").Append(F(sorted[n - 1]))
          .Append(",\"longFrames\":").Append(longFrames);
    }

    private void Report()
    {
        if (scoreText == null) return;
        var sb = new StringBuilder(768);
        sb.Append("{\"state\":\"").Append(state == State.Title ? "title" : state == State.Playing ? "playing" : "over").Append('"')
          .Append(",\"step\":").Append(step)
          .Append(",\"score\":").Append(score)
          .Append(",\"coins\":").Append(coins)
          .Append(",\"jumps\":").Append(jumps)
          .Append(",\"taps\":").Append(taps)
          .Append(",\"spawned\":").Append(spawned)
          .Append(",\"speed\":").Append(F(speed))
          .Append(",\"playerY\":").Append(F(playerY))
          .Append(",\"runs\":").Append(runs)
          .Append(",\"best\":").Append(best)
          .Append(",\"bootBest\":").Append(bootBest)
          .Append(",\"autoplay\":").Append(autoplay ? "true" : "false")
          .Append(",\"checksum\":\"").Append(checksum).Append('"')
          .Append(",\"checksumAt\":").Append(checksumAt)
          .Append(",\"checksumStep\":").Append(ChecksumStep)
          .Append(",\"sfxCount\":").Append(sfxCount)
          .Append(",\"bgmPlaying\":").Append(bgm.isPlaying ? "true" : "false")
          .Append(",\"bgmTime\":").Append(F(bgm.time))
          .Append(",\"bgmLength\":").Append(F(bgm.clip != null ? bgm.clip.length : 0f))
          .Append(",\"errorCount\":").Append(errorCount)
          .Append(",\"lastError\":\"").Append(lastError.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " ")).Append('"')
          .Append(",\"pauseEvents\":").Append(pauseEvents)
          .Append(",\"focusEvents\":").Append(focusEvents)
          .Append(",\"commands\":").Append(commandCount)
          .Append(",\"lastCommand\":\"").Append(lastCommand.Replace("\"", "'")).Append('"')
          .Append(",\"screenW\":").Append(Screen.width)
          .Append(",\"screenH\":").Append(Screen.height)
          .Append(",\"time\":").Append(F(Time.unscaledTime))
          .Append(",\"unity\":\"").Append(Application.unityVersion).Append('"');
        AppendFrameStats(sb);
        AppendRect(sb, "startButton", startButton);
        AppendRect(sb, "retryButton", retryButton);
        sb.Append('}');
        AITRunBridge.Report(sb.ToString());
    }
}
