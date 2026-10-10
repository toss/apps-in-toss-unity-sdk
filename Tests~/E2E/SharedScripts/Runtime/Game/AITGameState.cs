using System;

// game posture 픽스처가 JS(window.__AIT_GAME)로 올리는 상태 스키마. JsonUtility 직렬화 대상이라 전부 public 필드다.
// 좌표는 전부 0..1 정규화, 원점은 화면 좌상단(x 오른쪽, y 아래쪽)이다.

[Serializable]
public class AITGameVec
{
    public float x;
    public float y;
}

[Serializable]
public class AITGameButtonInfo
{
    public string name;
    public float x;   // 중심
    public float y;
    public float w;
    public float h;
}

[Serializable]
public class AITGameAudioInfo
{
    public bool bgmPlaying;
    public float bgmTime;
    public float bgmLength;
    public int sfxCount;
}

[Serializable]
public class AITGameChecksumInfo
{
    public bool done;
    public string rng;
    public string math;
    public string hash;
    public string collections;
    public string json;
    public string physics;
    public string combined;
    public int computeMs;
}

[Serializable]
public class AITGameState
{
    public string scene;
    public bool ready;
    public int frame;
    public float time;
    public int screenW;
    public int screenH;
    public string unityVersion;

    public AITGameButtonInfo[] buttons;
    public AITGameVec ball;
    public AITGameVec paddle;
    public AITGameVec pointer;
    public bool pointerSeen;

    public bool roundActive;
    public int score;
    public int combo;
    public int maxCombo;
    public int bricksDestroyed;
    public int bricksLeft;
    public int misses;
    public float timeLeft;
    public int roundSeconds;
    public int rounds;

    public int highScore;
    public int bootHighScore;
    public int titleHighScore;
    public int lastScore;

    public AITGameAudioInfo audio;

    public int resTexW;
    public int resTexH;
    public bool jsonLoaded;
    public string jsonValue;
    public string jsonError;

    public int pauseEvents;
    public int focusEvents;
    public bool lastPause;
    public bool lastFocus;

    public int errorCount;
    public int warnCount;
    public string[] lastErrors;

    public int coroutineTicks;
    public int asyncTicks;
    public bool asyncDone;
    public string caught;

    public int commandCount;
    public string lastCommand;

    public AITGameChecksumInfo checksum;
}

[Serializable]
public class AITGameCmd
{
    public string cmd;
    public float value;
}

[Serializable]
public class AITGameLevelConfig
{
    public string levelName;
    public int brickRows;
    public int brickCols;
    public float ballSpeed;
    public string secret;
}
