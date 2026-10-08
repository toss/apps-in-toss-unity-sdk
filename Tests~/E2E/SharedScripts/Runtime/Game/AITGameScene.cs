using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// game posture 픽스처의 씬 컨트롤러. 씬 파일에는 이 컴포넌트 하나(에셋 참조 포함)만 직렬화되고 나머지는 전부 런타임에 만든다.
/// mode: title | play | result
/// </summary>
public class AITGameScene : MonoBehaviour
{
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

    private void Awake()
    {
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
