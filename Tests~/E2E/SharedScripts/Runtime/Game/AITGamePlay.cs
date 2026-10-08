using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 브레이크아웃 플레이 씬 로직(3D 물리·포인터 입력·uGUI HUD). AITGameScene(mode=play)가 런타임에 붙인다.
/// 필드는 x 가 ±4, y 가 ±6 이고 카메라는 직교(필드가 항상 화면에 들어오도록 크기를 맞춘다).
/// </summary>
public class AITGamePlay : MonoBehaviour
{
    public static AITGamePlay Active;

    private const float HalfW = 4f;
    private const float HalfH = 6f;
    private const float PaddleY = -5f;
    private const float PaddleHalf = 0.9f;

    private AITGameDriver driver;
    private AITGameScene src;
    private Camera cam;
    private Rigidbody paddleRb;
    private Rigidbody ballRb;
    private Transform ballTf;
    private readonly List<GameObject> bricks = new List<GameObject>();
    private readonly List<Collider> colliders = new List<Collider>();
    private Text scoreText;
    private Text timeText;
    private float hudAt;

    private bool active;
    private bool launched;
    private float launchAt;
    private float timeLeft;
    private float speed;
    private Vector3 lastVel;

    private int score;
    private int combo;
    private int maxCombo;
    private int destroyed;
    private int misses;
    private int wave;

    private Vector3 lastMouse = new Vector3(-1f, -1f, 0f);
    private bool pointerSeen;
    private float pointerVx = 0.5f;
    private float targetX;

    private void Awake()
    {
        Active = this;
        src = GetComponent<AITGameScene>();
        driver = AITGameDriver.Instance;
        speed = driver.BallSpeed;
        timeLeft = driver.RoundSeconds;

        float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
        cam = AITGameUI.CreateCamera(Mathf.Max(6.3f, (HalfW + 0.3f) / aspect));
        cam.transform.SetParent(transform, false);
        cam.transform.position = new Vector3(0f, 0f, -10f);

        BuildBackground();
        BuildWalls();
        BuildPaddle();
        BuildBall();
        BuildBricks();
        ApplyBounceMaterial();
        BuildHud();

        launchAt = Time.time + 0.6f;
        active = true;
        driver.OnSceneReady();
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    // ---- 구성 ----

    private void BuildBackground()
    {
        Material mat = src != null && src.matBackground != null ? src.matBackground : driver.BackgroundMaterial;
        if (mat == null) return;
        var bg = AITGameUI.MakeQuad("Backdrop", mat, new Vector3(0f, 0f, 5f), new Vector2(40f, 40f));
        // Resources.Load: Resources 폴더의 대형 텍스처를 런타임에 읽어 배경에 적용한다.
        var tex = Resources.Load<Texture2D>("AITGame/bg_large");
        if (tex != null)
        {
            bg.GetComponent<MeshRenderer>().material.mainTexture = tex;
            driver.SetResourceTexture(tex.width, tex.height);
        }
    }

    private GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, bool visible)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        var mr = go.GetComponent<MeshRenderer>();
        if (visible && mat != null) mr.sharedMaterial = mat;
        mr.enabled = visible;
        var col = go.GetComponent<BoxCollider>();
        colliders.Add(col);
        return go;
    }

    private void BuildWalls()
    {
        Box("WallL", new Vector3(-HalfW - 0.5f, 0f, 0f), new Vector3(1f, 16f, 2f), null, false);
        Box("WallR", new Vector3(HalfW + 0.5f, 0f, 0f), new Vector3(1f, 16f, 2f), null, false);
        Box("WallT", new Vector3(0f, HalfH + 0.5f, 0f), new Vector3(10f, 1f, 2f), null, false);
    }

    private void BuildPaddle()
    {
        var go = Box("Paddle", new Vector3(0f, PaddleY, 0f), new Vector3(PaddleHalf * 2f, 0.3f, 1f), src != null ? src.matPaddle : driver.PaddleMaterial, true);
        paddleRb = go.AddComponent<Rigidbody>();
        paddleRb.isKinematic = true;
        paddleRb.useGravity = false;
        paddleRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        targetX = 0f;
    }

    private void BuildBall()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Ball";
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        var mr = go.GetComponent<MeshRenderer>();
        Material mat = src != null ? src.matBall : driver.BallMaterial;
        if (mat != null) mr.sharedMaterial = mat;
        colliders.Add(go.GetComponent<SphereCollider>());
        ballRb = go.AddComponent<Rigidbody>();
        ballRb.useGravity = false;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ballRb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        var b = go.AddComponent<AITGameBall>();
        b.owner = this;
        ballTf = go.transform;
        ResetBall();
    }

    private void ResetBall()
    {
        launched = false;
        launchAt = Time.time + 0.6f;
        ballTf.position = new Vector3(paddleRb.position.x, PaddleY + 0.6f, 0f);
        SetVelocity(Vector3.zero);
    }

    private void BuildBricks()
    {
        int rows = Mathf.Max(1, driver.Rows);
        int cols = Mathf.Max(1, driver.Cols);
        Material[] mats = src != null ? src.matBricks : driver.BrickMaterials;
        float cw = (HalfW * 2f - 0.4f) / cols;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Material m = (mats != null && mats.Length > 0) ? mats[r % mats.Length] : null;
                var go = Box("Brick", new Vector3(-HalfW + 0.2f + cw * (c + 0.5f), 1.5f + r * 0.55f, 0f),
                    new Vector3(cw - 0.08f, 0.4f, 1f), m, true);
                bricks.Add(go);
            }
        }
    }

    private void ApplyBounceMaterial()
    {
        // 마찰 0 / 반발 1 인 PhysicMaterial. 타입 이름을 쓰지 않으려고 var 로 받는다(Unity 6 에서 PhysicsMaterial 로 개명).
        colliders.RemoveAll(x => x == null);   // 부서진 벽돌의 콜라이더는 Destroy 로 null 이 된다
        var pm = colliders[colliders.Count - 1].material;
        pm.bounciness = 1f;
        pm.dynamicFriction = 0f;
        pm.staticFriction = 0f;
        for (int i = 0; i < colliders.Count; i++) colliders[i].sharedMaterial = pm;
    }

    private void BuildHud()
    {
        var canvas = AITGameUI.CreateCanvas("HudCanvas");
        canvas.transform.SetParent(transform, false);
        Font f = src != null && src.font != null ? src.font : driver.UiFont;
        scoreText = AITGameUI.CreateText(canvas.transform, "Score", "SCORE 0", f, 56, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(420f, -90f), new Vector2(700f, 100f));
        timeText = AITGameUI.CreateText(canvas.transform, "Time", "TIME 0", f, 56, TextAnchor.MiddleRight,
            new Vector2(1f, 1f), new Vector2(-300f, -90f), new Vector2(500f, 100f));
    }

    // ---- 속도 보조(Unity 6 에서 velocity → linearVelocity) ----

    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return ballRb.linearVelocity;
#else
        return ballRb.velocity;
#endif
    }

    private void SetVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        ballRb.linearVelocity = v;
#else
        ballRb.velocity = v;
#endif
        lastVel = v;
    }

    private Vector3 Normalize(Vector3 v)
    {
        v.z = 0f;
        if (v.sqrMagnitude < 0.0001f) v = new Vector3(0.4f, 1f, 0f);
        float minY = 0.3f;
        Vector3 d = v.normalized;
        if (Mathf.Abs(d.y) < minY)
        {
            d.y = d.y >= 0f ? minY : -minY;
            d.x = Mathf.Sign(d.x == 0f ? 1f : d.x) * Mathf.Sqrt(1f - minY * minY);
        }
        return d * speed;
    }

    // ---- 루프 ----

    private void Update()
    {
        if (!active) return;

        // 포인터 입력: 터치 우선, 없으면 마우스. 움직임이 한 번이라도 보이기 전에는 패들을 가운데에 둔다.
        if (Input.touchCount > 0)
        {
            Vector2 tp = Input.GetTouch(0).position;
            pointerVx = tp.x / Mathf.Max(1, Screen.width);
            pointerSeen = true;
        }
        else
        {
            Vector3 mp = Input.mousePosition;
            if (mp != lastMouse)
            {
                lastMouse = mp;
                pointerVx = mp.x / Mathf.Max(1, Screen.width);
                pointerSeen = true;
            }
        }

        timeLeft -= Time.deltaTime;
        if (!launched && Time.time >= launchAt)
        {
            launched = true;
            SetVelocity(Normalize(new Vector3(0.45f, 1f, 0f)));
        }

        if (ballTf.position.y < -HalfH - 0.8f)
        {
            misses++;
            combo = 0;
            ResetBall();
        }

        if (Time.unscaledTime >= hudAt)
        {
            hudAt = Time.unscaledTime + 0.1f;
            scoreText.text = "SCORE " + score;
            timeText.text = "TIME " + Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        }

        if (timeLeft <= 0f) EndRound();
    }

    private void FixedUpdate()
    {
        if (!active) return;
        if (pointerSeen)
        {
            float wx = cam.ViewportToWorldPoint(new Vector3(pointerVx, 0.5f, 10f)).x;
            targetX = Mathf.Clamp(wx, -HalfW + PaddleHalf, HalfW - PaddleHalf);
        }
        float nx = Mathf.MoveTowards(paddleRb.position.x, targetX, 40f * Time.fixedDeltaTime);
        paddleRb.MovePosition(new Vector3(nx, PaddleY, 0f));

        if (launched)
        {
            Vector3 v = GetVelocity();
            SetVelocity(Normalize(v));
        }
        else
        {
            ballTf.position = new Vector3(paddleRb.position.x, PaddleY + 0.6f, 0f);
        }
    }

    public void OnBallCollision(Collision c)
    {
        if (!active || !launched) return;
        string n = c.collider.gameObject.name;
        Vector3 normal = c.contacts.Length > 0 ? c.contacts[0].normal : Vector3.up;
        Vector3 v = Vector3.Reflect(lastVel, normal);

        if (n == "Brick")
        {
            bricks.Remove(c.collider.gameObject);
            Destroy(c.collider.gameObject);
            combo++;
            if (combo > maxCombo) maxCombo = combo;
            score += 10 * combo;
            destroyed++;
            driver.PlayBreak();
            if (bricks.Count == 0)
            {
                wave++;
                BuildBricks();
                ApplyBounceMaterial();
            }
        }
        else if (n == "Paddle")
        {
            combo = 0;
            float offset = (ballTf.position.x - paddleRb.position.x) / PaddleHalf;
            v = new Vector3(Mathf.Clamp(offset, -1f, 1f) * 0.9f, 1f, 0f);
            driver.PlayHit();
        }
        SetVelocity(Normalize(v));
    }

    // ---- 외부 제어 ----

    public void SetTimeLeft(float seconds)
    {
        timeLeft = seconds;
    }

    public void EndRound()
    {
        if (!active) return;
        active = false;
        driver.FinishRound(score);
    }

    public void FillState(AITGameState s)
    {
        s.roundActive = active;
        s.score = score;
        s.combo = combo;
        s.maxCombo = maxCombo;
        s.bricksDestroyed = destroyed;
        s.bricksLeft = bricks.Count;
        s.misses = misses;
        s.timeLeft = AITGameUI.Safe(timeLeft);
        s.pointerSeen = pointerSeen;
        s.pointer.x = AITGameUI.Safe(pointerVx);
        s.pointer.y = 0f;
        if (cam != null && ballTf != null && paddleRb != null)
        {
            Vector3 b = cam.WorldToViewportPoint(ballTf.position);
            s.ball.x = AITGameUI.Safe(b.x);
            s.ball.y = AITGameUI.Safe(1f - b.y);
            Vector3 p = cam.WorldToViewportPoint(paddleRb.position);
            s.paddle.x = AITGameUI.Safe(p.x);
            s.paddle.y = AITGameUI.Safe(1f - p.y);
        }
    }
}
