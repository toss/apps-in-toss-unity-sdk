using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

/// <summary>
/// perf mobilegame posture 픽스처(탭 점프 러너)의 씬 진입점.
///
/// 씬에는 사용자 스크립트를 직렬화하지 않는다. Unity 6 배치모드에서 같은 세션에 AddComponent 로 붙여 저장한 스크립트는
/// 콜드 컴파일 빌드에서 필드 없이 구워져 WebGL 플레이어가 씬을 "corrupted" 로 거부한다(game posture 와 같은 이유).
/// 그래서 에셋 참조는 엔진 내장 컴포넌트(AudioSource·SpriteRenderer) 자식에 담고, 씬 로드 때 루트 "AITRunScene" 에
/// 이 컴포넌트를 붙여 자식 이름으로 읽은 뒤 <see cref="AITRunGame"/> 을 만든다.
/// 생성기는 Editor/MobileGameBuilder.cs 다.
/// </summary>
[Preserve]
public class AITRunScene : MonoBehaviour
{
    public const string RootName = "AITRunScene";

    public AudioClip Bgm, SfxJump, SfxCoin, SfxHit, SfxClick;
    public Sprite Player0, Player1, PlayerJump, Obstacle, ObstacleTall, Coin, Ground, BgFar, BgNear, Cloud, Spark, Button;
    public Font Font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookSceneLoads()
    {
        SceneManager.sceneLoaded -= AttachToScene;
        SceneManager.sceneLoaded += AttachToScene;
    }

    private static void AttachToScene(Scene scene, LoadSceneMode loadMode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == RootName && root.GetComponent<AITRunScene>() == null) root.AddComponent<AITRunScene>();
        }
    }

    private void Awake()
    {
        ReadRefs();
        var game = gameObject.AddComponent<AITRunGame>();
        game.Init(this);
    }

    private void ReadRefs()
    {
        foreach (Transform child in transform)
        {
            var src = child.GetComponent<AudioSource>();
            if (src != null)
            {
                switch (child.name)
                {
                    case "bgm": Bgm = src.clip; break;
                    case "sfxJump": SfxJump = src.clip; break;
                    case "sfxCoin": SfxCoin = src.clip; break;
                    case "sfxHit": SfxHit = src.clip; break;
                    case "sfxClick": SfxClick = src.clip; break;
                }
                continue;
            }
            var sr = child.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            switch (child.name)
            {
                case "player0": Player0 = sr.sprite; break;
                case "player1": Player1 = sr.sprite; break;
                case "playerJump": PlayerJump = sr.sprite; break;
                case "obstacle": Obstacle = sr.sprite; break;
                case "obstacleTall": ObstacleTall = sr.sprite; break;
                case "coin": Coin = sr.sprite; break;
                case "ground": Ground = sr.sprite; break;
                case "bgFar": BgFar = sr.sprite; break;
                case "bgNear": BgNear = sr.sprite; break;
                case "cloud": Cloud = sr.sprite; break;
                case "spark": Spark = sr.sprite; break;
                case "button": Button = sr.sprite; break;
            }
        }
#if UNITY_2022_2_OR_NEWER
        Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }
}
