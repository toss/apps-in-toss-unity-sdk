using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// game posture 픽스처의 씬 컨트롤러. mode: title | play | result
/// 씬 파일에는 엔진 내장 컴포넌트만 직렬화한다: 루트 "AITGameScene" 아래 "Mode:&lt;mode&gt;" 자식,
/// 클립을 든 AudioSource 자식(bgm·sfxHit·sfxBreak·sfxClick), 머티리얼을 든 꺼진 MeshRenderer 자식
/// (matBackground·matPaddle·matBall·matBricks). 이 컴포넌트는 씬 로드 때 런타임에 붙이고 그 자식들에서 참조를 읽는다.
/// Unity 6 배치모드에서 같은 세션에 AddComponent 로 붙여 저장한 사용자 스크립트는 콜드 컴파일 빌드에서
/// "missing script" 로 필드 없이 구워지고, 타입트리가 없는 WebGL 플레이어는 그 씬을 "corrupted" 로 거부한다
/// (E2EBootstrapper 가 런타임 생성으로 피한 것과 같은 문제).
/// </summary>
public class AITGameScene : MonoBehaviour
{
    public const string RootName = "AITGameScene";
    public const string ModePrefix = "Mode:";

    public string mode = "title";
    public AudioClip bgm;
    public AudioClip sfxHit;
    public AudioClip sfxBreak;
    public AudioClip sfxClick;
    public Font font;
    public Material matBackground;
    public Material matPaddle;
    public Material matBall;
    public Material[] matBricks;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookSceneLoads()
    {
        SceneManager.sceneLoaded -= AttachToScene;
        SceneManager.sceneLoaded += AttachToScene;
    }

    // 픽스처 씬(루트 "AITGameScene")에만 붙는다. 다른 E2E 프로젝트의 씬에서는 아무 일도 하지 않는다.
    private static void AttachToScene(Scene scene, LoadSceneMode loadMode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == RootName && root.GetComponent<AITGameScene>() == null) root.AddComponent<AITGameScene>();
        }
    }

    // 씬에 직렬화된 내장 컴포넌트에서 mode 와 에셋 참조를 읽는다.
    private void ReadSceneRefs()
    {
        foreach (Transform child in transform)
        {
            string n = child.name;
            if (n.StartsWith(ModePrefix)) { mode = n.Substring(ModePrefix.Length); continue; }
            var src = child.GetComponent<AudioSource>();
            if (src != null)
            {
                if (n == "bgm") bgm = src.clip;
                else if (n == "sfxHit") sfxHit = src.clip;
                else if (n == "sfxBreak") sfxBreak = src.clip;
                else if (n == "sfxClick") sfxClick = src.clip;
                continue;
            }
            var mr = child.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            if (n == "matBackground") matBackground = mr.sharedMaterial;
            else if (n == "matPaddle") matPaddle = mr.sharedMaterial;
            else if (n == "matBall") matBall = mr.sharedMaterial;
            else if (n == "matBricks") matBricks = mr.sharedMaterials;
        }
        if (font == null)
        {
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }
    }

    private void Awake()
    {
        ReadSceneRefs();
        var driver = AITGameDriver.Ensure(this);
        driver.OnSceneBegin();
        if (font == null) font = driver.UiFont;

        switch (mode)
        {
            case "play":
                gameObject.AddComponent<AITGamePlay>();
                break;
            case "result":
                BuildResult(driver);
                break;
            default:
                BuildTitle(driver);
                break;
        }
    }

    private void BuildBackdrop(Material mat)
    {
        var cam = AITGameUI.CreateCamera(6f);
        cam.transform.SetParent(transform, false);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        if (mat != null) AITGameUI.MakeQuad("Backdrop", mat, new Vector3(0f, 0f, 5f), new Vector2(40f, 40f));
    }

    private void BuildTitle(AITGameDriver driver)
    {
        BuildBackdrop(matBackground != null ? matBackground : driver.BackgroundMaterial);
        int high = PlayerPrefs.GetInt(AITGameDriver.HighKey, 0);
        driver.SetTitleHighScore(high);

        var canvas = AITGameUI.CreateCanvas("TitleCanvas");
        Font f = font;
        AITGameUI.CreateText(canvas.transform, "TitleText", "AIT BREAKOUT", f, 110, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 450f), new Vector2(1000f, 160f));
        AITGameUI.CreateText(canvas.transform, "HighScoreText", "HIGH SCORE: " + high, f, 64, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1000f, 100f));
        var btn = AITGameUI.CreateButton(canvas.transform, "StartButton", "START", f,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(600f, 200f), () =>
            {
                driver.PlayClick();
                driver.LoadScene("GamePlay");
            });
        driver.RegisterButton("start", (RectTransform)btn.transform);
        driver.OnSceneReady();
        driver.StartBootOnce();
    }

    private void BuildResult(AITGameDriver driver)
    {
        BuildBackdrop(matBackground != null ? matBackground : driver.BackgroundMaterial);
        int high = PlayerPrefs.GetInt(AITGameDriver.HighKey, 0);

        var canvas = AITGameUI.CreateCanvas("ResultCanvas");
        Font f = font;
        AITGameUI.CreateText(canvas.transform, "ResultText", "ROUND OVER", f, 100, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 450f), new Vector2(1000f, 160f));
        AITGameUI.CreateText(canvas.transform, "ScoreText", "SCORE: " + driver.LastScore, f, 72, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1000f, 110f));
        AITGameUI.CreateText(canvas.transform, "HighText", "HIGH SCORE: " + high, f, 56, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1000f, 100f));
        var btn = AITGameUI.CreateButton(canvas.transform, "RetryButton", "RETRY", f,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(600f, 200f), () =>
            {
                driver.PlayClick();
                driver.LoadScene("GamePlay");
            });
        driver.RegisterButton("retry", (RectTransform)btn.transform);
        driver.OnSceneReady();
    }
}
