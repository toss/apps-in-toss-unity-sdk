using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

/// <summary>
/// perf mobileheavy posture: 탭 러너(<see cref="AITRunGame"/>) 위에 상용 모바일 게임 수준의 부하를 얹는 표현 계층.
///
/// - 콘텐츠: 스테이지 4개. 스테이지 1~3 의 배경(2048² 원경·2048×1024 근경·바닥)·3D 소품 텍스처·앰비언스 루프는 Resources 에 있고
///   <see cref="StageSteps"/> 스텝마다 비동기로 읽어 교체한 뒤 이전 스테이지 에셋을 Resources.UnloadUnusedAssets 로 내린다.
/// - 3D: Standard 셰이더 건물 160채·풍차 24기(회전)·그림자 드리우는 방향광. 패럴랙스로 매 프레임 이동한다.
/// - 물리: 통 안에서 회전 패들이 휘젓는 공 60개(상시 접촉), 충돌 시 파편 24개.
/// - 파티클: 스테이지 날씨 120개 상시 + 코인마다 색종이 24개(스프라이트 파티클, ParticleSystem 모듈 없이).
/// - UI: 타이틀 리더보드 ScrollRect(행 50개, 자동 스크롤), 플레이 HUD(스테이지·진행 막대).
///
/// 이 계층은 시뮬레이션 상태를 읽기만 하고 바꾸지 않는다. 체크섬은 mobilegame 과 같다.
/// 씬 구성(이름)은 Editor/MobileGameBuilder.cs 의 heavy 생성과 짝을 이룬다.
/// </summary>
[Preserve]
public class AITRunHeavy : MonoBehaviour
{
    public const string MarkerName = "heavy";
    public const string Group3D = "Heavy3D";
    public const int StageSteps = 375;               // 7.5초마다 스테이지 전환(체크섬 1500 스텝까지 1→2→3 세 번 읽는다)
    public const int StageCount = 4;
    private const float CitySpan = 12f;
    private const int WeatherCount = 120;
    private const int ConfettiPool = 240;

    private struct Particle { public bool Active; public Vector2 Pos; public Vector2 Vel; public float Life; public float MaxLife; }

    private AITRunGame game;
    private AITRunScene refs;

    private Transform[] buildings;
    private float[] buildingBaseX;
    private Transform[] windmills;
    private float[] windmillBaseX;
    private Transform[] rotors;
    private Material propsMaterial;
    private Texture stage0Props;
    private Rigidbody paddle;
    private Rigidbody[] balls;
    private Vector3[] ballHome;
    private Rigidbody[] debris;
    private float debrisUntil = -1f;

    private SpriteRenderer[] weatherSr;
    private Particle[] weather;
    private SpriteRenderer[] confettiSr;
    private Particle[] confetti;
    private int confettiCursor;

    private AudioSource ambience;
    private AudioClip stage0Ambience;
    private int stage;
    private bool loading;
    private int stageLoads;
    private int stageErrors;
    private float stageLoadMsLast;
    private float stageLoadMsMax;
    private int unloads;

    private ScrollRect board;
    private Text hudStage;
    private Image hudBar;
    private GameObject hud;

    private static readonly Color[] WeatherTint =
    {
        new Color(1f, 1f, 1f, 0.8f),        // 꽃가루
        new Color(1f, 0.75f, 0.35f, 0.85f), // 낙엽
        new Color(0.85f, 0.95f, 1f, 0.9f),  // 눈
        new Color(0.6f, 1f, 0.7f, 0.8f),    // 반딧불
    };

    public void Init(AITRunGame g, AITRunScene r)
    {
        game = g;
        refs = r;
        BindScene();
        SetupParticles();
        SetupUi();
        ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.loop = true;
        ambience.volume = 0.25f;
        game.CoinCollected += OnCoin;
        game.Crashed += OnCrash;
        game.ExtraReport = AppendReport;
        StartCoroutine(LoadStage0Ambience());
    }

    private void OnDestroy()
    {
        if (game == null) return;
        game.CoinCollected -= OnCoin;
        game.Crashed -= OnCrash;
    }

    // ---------------------------------------------------------------- 씬 바인딩

    private void BindScene()
    {
        var g3 = transform.Find(Group3D);
        if (g3 == null) { Debug.LogError("[AITRunHeavy] " + Group3D + " 그룹이 없다"); return; }

        var city = g3.Find("City");
        buildings = new Transform[city.childCount];
        buildingBaseX = new float[city.childCount];
        for (int i = 0; i < city.childCount; i++)
        {
            buildings[i] = city.GetChild(i);
            buildingBaseX[i] = buildings[i].localPosition.x;
            if (propsMaterial == null)
            {
                var mr = buildings[i].GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.name.StartsWith("Props"))
                    propsMaterial = mr.sharedMaterial;
            }
        }
        if (propsMaterial != null) stage0Props = propsMaterial.mainTexture;

        var mills = g3.Find("Windmills");
        windmills = new Transform[mills.childCount];
        windmillBaseX = new float[mills.childCount];
        rotors = new Transform[mills.childCount];
        for (int i = 0; i < mills.childCount; i++)
        {
            windmills[i] = mills.GetChild(i);
            windmillBaseX[i] = windmills[i].localPosition.x;
            rotors[i] = windmills[i].Find("rotor");
        }

        var hopper = g3.Find("Hopper");
        var p = hopper.Find("paddle");
        if (p != null) paddle = p.GetComponent<Rigidbody>();
        var ballRoot = hopper.Find("balls");
        balls = new Rigidbody[ballRoot.childCount];
        ballHome = new Vector3[ballRoot.childCount];
        for (int i = 0; i < ballRoot.childCount; i++)
        {
            balls[i] = ballRoot.GetChild(i).GetComponent<Rigidbody>();
            ballHome[i] = balls[i].transform.position;
        }

        var deb = g3.Find("Debris");
        debris = new Rigidbody[deb.childCount];
        for (int i = 0; i < deb.childCount; i++)
        {
            debris[i] = deb.GetChild(i).GetComponent<Rigidbody>();
            debris[i].gameObject.SetActive(false);
        }
    }

    private SpriteRenderer MakeSprite(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = refs.Spark;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    private void SetupParticles()
    {
        weatherSr = new SpriteRenderer[WeatherCount];
        weather = new Particle[WeatherCount];
        uint seed = 12345u;
        for (int i = 0; i < WeatherCount; i++)
        {
            weatherSr[i] = MakeSprite("weather" + i, 15);
            seed = seed * 1664525u + 1013904223u;
            float fx = (seed >> 8) / 16777216f;
            seed = seed * 1664525u + 1013904223u;
            float fy = (seed >> 8) / 16777216f;
            weather[i] = new Particle { Active = true, Pos = new Vector2((fx - 0.5f) * 6f, fy * 11f - 5.5f), Vel = new Vector2(-0.3f - fx * 0.4f, -0.6f - fy * 0.8f), Life = 1f, MaxLife = 1f };
            weatherSr[i].transform.localScale = Vector3.one * (0.5f + fx * 0.6f);
        }
        confettiSr = new SpriteRenderer[ConfettiPool];
        confetti = new Particle[ConfettiPool];
        for (int i = 0; i < ConfettiPool; i++) confettiSr[i] = MakeSprite("confetti" + i, 26);
    }

    private Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = refs.Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform Stretch(GameObject go, Vector2 min, Vector2 max)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void SetupUi()
    {
        // 타이틀 리더보드: START 버튼(0.37~0.47) 아래쪽 영역에만 둔다.
        var view = new GameObject("Leaderboard", typeof(RectTransform));
        view.transform.SetParent(game.TitlePanel, false);
        Stretch(view, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.31f));
        var viewImg = view.AddComponent<Image>();
        viewImg.color = new Color(0f, 0f, 0f, 0.35f);
        view.AddComponent<Mask>().showMaskGraphic = true;
        board = view.AddComponent<ScrollRect>();
        board.horizontal = false;
        board.movementType = ScrollRect.MovementType.Clamped;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(view.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        board.content = crt;
        board.viewport = view.GetComponent<RectTransform>();

        for (int i = 0; i < 50; i++)
        {
            var row = new GameObject("Row" + i, typeof(RectTransform));
            row.transform.SetParent(content.transform, false);
            var bg = row.AddComponent<Image>();
            bg.color = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0.06f);
            bg.raycastTarget = false;
            row.AddComponent<LayoutElement>().preferredHeight = 64f;
            var nameText = MakeText(row.transform, "Name", 40, TextAnchor.MiddleLeft);
            Stretch(nameText.gameObject, new Vector2(0.03f, 0f), new Vector2(0.7f, 1f));
            nameText.text = (i + 1).ToString(CultureInfo.InvariantCulture) + ".  RUNNER_" + ((i * 7919) % 10000).ToString("0000", CultureInfo.InvariantCulture);
            var scoreText = MakeText(row.transform, "Score", 40, TextAnchor.MiddleRight);
            Stretch(scoreText.gameObject, new Vector2(0.7f, 0f), new Vector2(0.97f, 1f));
            scoreText.text = (9000 - i * 151).ToString(CultureInfo.InvariantCulture);
        }

        // 플레이 HUD: 스테이지 이름 + 다음 스테이지까지 진행 막대.
        hud = new GameObject("Hud", typeof(RectTransform));
        hud.transform.SetParent(game.UiRoot, false);
        Stretch(hud, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.9f));
        var barBg = hud.AddComponent<Image>();
        barBg.color = new Color(0f, 0f, 0f, 0.4f);
        barBg.raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(hud.transform, false);
        Stretch(fill, Vector2.zero, Vector2.one);
        hudBar = fill.AddComponent<Image>();
        hudBar.sprite = refs.Button;
        hudBar.type = Image.Type.Filled;
        hudBar.fillMethod = Image.FillMethod.Horizontal;
        hudBar.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        hudBar.raycastTarget = false;
        hudStage = MakeText(hud.transform, "Stage", 40, TextAnchor.MiddleCenter);
        Stretch(hudStage.gameObject, Vector2.zero, Vector2.one);
        hud.SetActive(false);
    }

    // ---------------------------------------------------------------- 스테이지 콘텐츠

    private IEnumerator LoadStage0Ambience()
    {
        var req = Resources.LoadAsync<AudioClip>("RunHeavy/s0/amb");
        yield return req;
        stage0Ambience = req.asset as AudioClip;
        if (stage0Ambience == null) { stageErrors++; yield break; }
        ambience.clip = stage0Ambience;
    }

    private IEnumerator LoadStage(int target)
    {
        loading = true;
        float t0 = Time.realtimeSinceStartup;
        Sprite far = null, near = null, ground = null;
        Texture props = null;
        AudioClip amb = null;
        if (target == 0)
        {
            far = refs.BgFar;
            near = refs.BgNear;
            ground = refs.Ground;
            props = stage0Props;
            amb = stage0Ambience;
        }
        else
        {
            string dir = "RunHeavy/s" + target.ToString(CultureInfo.InvariantCulture) + "/";
            var rFar = Resources.LoadAsync<Sprite>(dir + "far");
            var rNear = Resources.LoadAsync<Sprite>(dir + "near");
            var rGround = Resources.LoadAsync<Sprite>(dir + "ground");
            var rProps = Resources.LoadAsync<Texture2D>(dir + "props");
            var rAmb = Resources.LoadAsync<AudioClip>(dir + "amb");
            yield return rFar;
            yield return rNear;
            yield return rGround;
            yield return rProps;
            yield return rAmb;
            far = rFar.asset as Sprite;
            near = rNear.asset as Sprite;
            ground = rGround.asset as Sprite;
            props = rProps.asset as Texture;
            amb = rAmb.asset as AudioClip;
            if (far == null || near == null || ground == null || props == null || amb == null) stageErrors++;
        }

        game.SetStageArt(far, near, ground);
        if (propsMaterial != null && props != null) propsMaterial.mainTexture = props;
        if (amb != null && ambience.clip != amb)
        {
            ambience.clip = amb;
            if (game.IsPlaying) ambience.Play();
        }
        stage = target;
        stageLoads++;
        stageLoadMsLast = (Time.realtimeSinceStartup - t0) * 1000f;
        if (stageLoadMsLast > stageLoadMsMax) stageLoadMsMax = stageLoadMsLast;
        loading = false;

        // 이전 스테이지 텍스처·오디오를 내린다(스테이지 0 은 씬이 참조하므로 남는다).
        yield return Resources.UnloadUnusedAssets();
        unloads++;
    }

    // ---------------------------------------------------------------- 이벤트

    private void OnCoin(Vector2 at)
    {
        for (int k = 0; k < 24; k++)
        {
            int i = confettiCursor;
            confettiCursor = (confettiCursor + 1) % ConfettiPool;
            float a = k * (Mathf.PI * 2f / 24f);
            float sp = 2.5f + (k % 4) * 0.8f;
            confetti[i] = new Particle { Active = true, Pos = at, Vel = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + 2f), Life = 0.9f, MaxLife = 0.9f };
            confettiSr[i].color = Color.HSVToRGB((k / 24f + stage * 0.25f) % 1f, 0.8f, 1f);
        }
    }

    private void OnCrash(Vector2 at)
    {
        for (int i = 0; i < debris.Length; i++)
        {
            var rb = debris[i];
            rb.gameObject.SetActive(true);
            rb.transform.position = new Vector3(at.x + (i % 4) * 0.08f, at.y + (i / 4) * 0.08f, -0.5f);
            rb.transform.rotation = Quaternion.identity;
            SetVelocity(rb, Vector3.zero);
            rb.angularVelocity = Vector3.zero;
            rb.AddExplosionForce(9f, new Vector3(at.x - 0.3f, at.y - 0.2f, -0.5f), 2f, 0.5f, ForceMode.Impulse);
        }
        debrisUntil = Time.time + 2.5f;
    }

    private static void SetVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    // ---------------------------------------------------------------- 프레임

    private void FixedUpdate()
    {
        if (paddle != null)
            paddle.MoveRotation(Quaternion.Euler(0f, 0f, Time.fixedTime * 140f));
        for (int i = 0; i < balls.Length; i++)
        {
            var rb = balls[i];
            if (rb.position.y < ballHome[i].y - 6f)
            {
                rb.position = ballHome[i];
                SetVelocity(rb, Vector3.zero);
            }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float wx = game.WorldX;

        // 3D 패럴랙스(건물 0.25, 풍차 0.3) + 풍차 회전
        for (int i = 0; i < buildings.Length; i++)
        {
            var t = buildings[i];
            var p = t.localPosition;
            p.x = Mathf.Repeat(buildingBaseX[i] - wx * 0.25f + CitySpan * 0.5f, CitySpan) - CitySpan * 0.5f;
            t.localPosition = p;
        }
        for (int i = 0; i < windmills.Length; i++)
        {
            var t = windmills[i];
            var p = t.localPosition;
            p.x = Mathf.Repeat(windmillBaseX[i] - wx * 0.3f + CitySpan * 0.5f, CitySpan) - CitySpan * 0.5f;
            t.localPosition = p;
            if (rotors[i] != null) rotors[i].localRotation = Quaternion.Euler(0f, 0f, -Time.time * (90f + i * 7f));
        }

        if (debrisUntil > 0f && Time.time > debrisUntil)
        {
            debrisUntil = -1f;
            for (int i = 0; i < debris.Length; i++) debris[i].gameObject.SetActive(false);
        }

        UpdateWeather(dt);
        UpdateConfetti(dt);

        // 스테이지: 플레이 중에만 스텝으로 정한다(결정론적 전환 시점).
        if (game.IsPlaying)
        {
            int target = (game.Step / StageSteps) % StageCount;
            if (target != stage && !loading) StartCoroutine(LoadStage(target));
            if (!ambience.isPlaying && ambience.clip != null) ambience.Play();
        }
        else if (ambience.isPlaying)
        {
            ambience.Stop();
        }

        if (hud.activeSelf != game.IsPlaying) hud.SetActive(game.IsPlaying);
        if (game.IsPlaying)
        {
            hudBar.fillAmount = (game.Step % StageSteps) / (float)StageSteps;
            hudStage.text = "STAGE " + (stage + 1) + "  " + (game.Step % StageSteps * 100 / StageSteps) + "%";
        }
        else if (board.gameObject.activeInHierarchy)
        {
            board.verticalNormalizedPosition = 1f - Mathf.PingPong(Time.time * 0.08f, 1f);
        }
    }

    private void UpdateWeather(float dt)
    {
        var tint = WeatherTint[stage];
        float half = game.HalfWidth + 0.5f;
        for (int i = 0; i < WeatherCount; i++)
        {
            var p = weather[i];
            p.Pos += new Vector2(p.Vel.x + Mathf.Sin(Time.time * 1.3f + i) * 0.3f, p.Vel.y) * dt;
            if (p.Pos.y < -5.6f) p.Pos.y += 11.2f;
            if (p.Pos.x < -half) p.Pos.x += half * 2f;
            weather[i] = p;
            var sr = weatherSr[i];
            if (!sr.enabled) sr.enabled = true;
            sr.transform.position = new Vector3(p.Pos.x, p.Pos.y, -0.2f);
            sr.color = tint;
        }
    }

    private void UpdateConfetti(float dt)
    {
        for (int i = 0; i < ConfettiPool; i++)
        {
            var sr = confettiSr[i];
            if (!confetti[i].Active) { if (sr.enabled) sr.enabled = false; continue; }
            var p = confetti[i];
            p.Life -= dt;
            if (p.Life <= 0f) { confetti[i].Active = false; sr.enabled = false; continue; }
            p.Vel.y -= 7f * dt;
            p.Pos += p.Vel * dt;
            confetti[i] = p;
            sr.enabled = true;
            sr.transform.position = new Vector3(p.Pos.x, p.Pos.y, -1.2f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, p.Life * 720f);
            var c = sr.color;
            c.a = p.Life / p.MaxLife;
            sr.color = c;
        }
    }

    // ---------------------------------------------------------------- 보고

    private int ActiveConfetti()
    {
        int n = 0;
        for (int i = 0; i < ConfettiPool; i++) if (confetti[i].Active) n++;
        return n;
    }

    private void AppendReport(StringBuilder sb)
    {
        int bodies = balls.Length;
        for (int i = 0; i < debris.Length; i++) if (debris[i].gameObject.activeSelf) bodies++;
        sb.Append(",\"heavy\":true")
          .Append(",\"stage\":").Append(stage)
          .Append(",\"stageLoads\":").Append(stageLoads)
          .Append(",\"stageErrors\":").Append(stageErrors)
          .Append(",\"stageUnloads\":").Append(unloads)
          .Append(",\"stageLoadMsLast\":").Append(stageLoadMsLast.ToString("0.#", CultureInfo.InvariantCulture))
          .Append(",\"stageLoadMsMax\":").Append(stageLoadMsMax.ToString("0.#", CultureInfo.InvariantCulture))
          .Append(",\"bodies\":").Append(bodies)
          .Append(",\"buildings\":").Append(buildings.Length + windmills.Length)
          .Append(",\"particles\":").Append(WeatherCount + ActiveConfetti())
          .Append(",\"ambiencePlaying\":").Append(ambience.isPlaying ? "true" : "false");
    }
}
