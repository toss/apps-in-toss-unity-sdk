using UnityEngine;
using UnityEditor;

namespace AppsInToss
{
    /// <summary>
    /// 빌드 프로필 설정
    /// Dev Server (개발용, 빌드 속도 우선)와 Production (배포용, 최적화 우선)으로 구분
    /// </summary>
    [System.Serializable]
    public class AITBuildProfile
    {
        [Header("런타임 설정")]
        [Tooltip("Mock 브릿지 사용 (로컬 테스트용, 네이티브 API 없이 동작)")]
        public bool enableMockBridge = false;

        [Tooltip("디버그 콘솔 활성화 (개발/테스트 목적)")]
        public bool enableDebugConsole = false;

        [Header("빌드 설정")]
        [Tooltip("Development Build 활성화 (빌드 속도 향상, 디버깅 편의)")]
        public bool developmentBuild = false;

        [Tooltip("LZ4 압축으로 빌드 속도 향상")]
        public bool enableLZ4Compression = true;

        [Tooltip("압축 포맷: -1 = 자동, 0 = Disabled, 1 = Gzip, 2 = Brotli")]
        public int compressionFormat = -1;

        [Tooltip("Managed Stripping Level: -1 = 자동 (High), 0 = Disabled, 1 = Minimal, 2 = Low, 3 = Medium, 4 = High")]
        public int managedStrippingLevel = -1;

        [Tooltip("디버그 심볼을 외부 파일로 분리 (빌드 크기 감소)")]
        public bool debugSymbolsExternal = true;

        /// <summary>
        /// 프로필의 얕은 복사본 생성 (모든 필드가 값 타입이므로 안전)
        /// </summary>
        public AITBuildProfile Clone() => (AITBuildProfile)MemberwiseClone();

        /// <summary>
        /// Dev Server 기본 프로필 생성 (빌드 속도 우선)
        /// </summary>
        public static AITBuildProfile CreateDevServerProfile()
        {
            return new AITBuildProfile
            {
                enableMockBridge = true,
                enableDebugConsole = true,
                developmentBuild = true,
                enableLZ4Compression = true,
                compressionFormat = 0,  // Disabled - 빌드 속도 우선
                managedStrippingLevel = 1,  // Minimal - 빌드 속도 우선
                debugSymbolsExternal = false
            };
        }

        /// <summary>
        /// Production 기본 프로필 생성 (최적화 우선)
        /// Prod Server, Build & Package, Publish에서 공통으로 사용
        /// </summary>
        public static AITBuildProfile CreateProductionProfile()
        {
            return new AITBuildProfile
            {
                enableMockBridge = false,
                enableDebugConsole = false,
                developmentBuild = false,
                enableLZ4Compression = true,
                compressionFormat = -1,  // 자동 (Brotli)
                managedStrippingLevel = -1,  // 자동 (High)
                debugSymbolsExternal = true
            };
        }
    }

    /// <summary>
    /// 권한 설정 구성
    /// 문서: https://developers-apps-in-toss.toss.im/bedrock/reference/framework/권한/permission.html
    /// </summary>
    [System.Serializable]
    public class AITPermissionConfig
    {
        [Header("Clipboard")]
        [Tooltip("클립보드 읽기 권한")]
        public bool clipboardRead = false;

        [Tooltip("클립보드 쓰기 권한")]
        public bool clipboardWrite = false;

        [Header("Contacts")]
        [Tooltip("연락처 읽기 권한 (read only)")]
        public bool contacts = false;

        [Header("Photos")]
        [Tooltip("사진 앨범 읽기 권한 (read only)")]
        public bool photos = false;

        [Header("Camera")]
        [Tooltip("카메라 접근 권한 (access only)")]
        public bool camera = false;

        [Header("Geolocation")]
        [Tooltip("위치 정보 접근 권한 (access only)")]
        public bool geolocation = false;
    }

    /// <summary>
    /// Apps in Toss Editor 설정 오브젝트
    /// </summary>
    [System.Serializable]
    public class AITEditorScriptObject : ScriptableObject
    {
        [Header("앱 기본 정보")]
        public string appName = "";
        public string displayName = "";
        public string version = "0.0.1";
        public string description = "";

        [Header("브랜드 설정")]
        public string primaryColor = "#3182F6";
        public string iconUrl = "";

        [Header("WebView 설정")]
        [Tooltip("브릿지 색상 모드. 게임앱은 'inverted' (다크모드), 일반앱은 'basic'")]
        public int bridgeColorMode = 0; // 0=inverted (게임 기본), 1=basic

        [Tooltip("WebView 타입. 게임앱은 'game' (투명배경), 일반앱은 'partner' (흰색배경)")]
        public int webViewType = 0; // 0=game, 1=partner

        [Tooltip("상단 네비게이션 바 투명 배경. game 타입에서 풀스크린(노치/상단 영역까지 그리기)에 필요")]
        public bool navigationBarTransparentBackground = true; // 기본 ON (game 풀스크린 복구)

        [Tooltip("네비게이션 바 테마. 0=기본(미지정), 1=light, 2=dark")]
        public int navigationBarTheme = 0; // 0=기본(미지정), 1=light, 2=dark

        [Tooltip("인라인 미디어 재생 허용")]
        public bool allowsInlineMediaPlayback = false;

        [Tooltip("미디어 재생 시 사용자 액션 필요")]
        public bool mediaPlaybackRequiresUserAction = false;

        [Header("서버 설정")]
        [Tooltip("Granite (Metro) 서버 호스트. 기본값: 0.0.0.0")]
        public string graniteHost = "0.0.0.0";

        [Tooltip("Granite (Metro) 서버 포트. 기본값: 8081")]
        public int granitePort = 8081;

        [Tooltip("Vite 서버 호스트. 기본값: localhost")]
        public string viteHost = "localhost";

        [Tooltip("Vite 서버 포트. 기본값: 5173")]
        public int vitePort = 5173;

        [Header("빌드 출력 설정")]
        [Tooltip("granite build 출력 디렉토리. 기본값: dist")]
        public string outdir = "dist";

        [Header("빌드 프로필")]
        [Tooltip("Dev Server 실행 시 적용되는 빌드 설정 (빌드 속도 우선)")]
        public AITBuildProfile devServerProfile = AITBuildProfile.CreateDevServerProfile();

        [Tooltip("Production 빌드 시 적용되는 빌드 설정 (Prod Server, Build & Package, Publish)")]
        public AITBuildProfile productionProfile = AITBuildProfile.CreateProductionProfile();

        [Header("WebGL 최적화 설정")]
        [Tooltip("WebGL 초기 힙 메모리 크기(MB). 런타임에 필요 시 자동 확장됩니다. -1 = 자동 (Unity 버전별 권장값)")]
        public int memorySize = -1;

        [Tooltip("-1 = 자동, 0 = false, 1 = true")]
        public int threadsSupport = -1;

        [Tooltip("-1 = 자동, 0 = false, 1 = true")]
        public int dataCaching = -1;

        public bool nameFilesAsHashes = true;

        [Header("로딩 최적화 — 페이지 캐시(CacheStorage 재방문 서빙)")]
        [Tooltip("재방문 시 Build/* 자산을 CacheStorage 에서 직접 서빙합니다(ServiceWorker 불필요). " +
                 "첫 방문(콜드)에는 효과가 없고, 미지원/비보안 환경에서는 자동으로 원래 로드로 무해 통과합니다. " +
                 "호스트(슈퍼앱)가 decode-free(Content-Encoding 미포함 raw bytes)로 동일 캐시명에 pre-fill 하면 " +
                 "첫 방문도 가속됩니다. -1 = 자동 (true), 0 = 비활성, 1 = 활성.")]
        public int pageCache = -1;

        [Tooltip("페이지 캐시 버킷 이름. 호스트 백그라운드 pre-fill 페이지와 '동일한 이름'을 써야 같은 캐시를 공유합니다. " +
                 "비우면 appName(앱 식별자)에서 자동 파생합니다. 런타임 window.__AIT_CACHE_NAME 으로도 오버라이드 가능.")]
        public string pageCacheName = "";

        /// <summary>
        /// 빌드 시 ait-warm-manifest.json 산출 여부 (호스트 warm 연동용).
        /// tri-state: -1=자동(true, 기본 ON), 0=비활성, 1=활성.
        /// pageCache 실효값이 OFF 이면 warmManifest 도 no-op (AND 게이팅).
        /// pageCache 실효값이 OFF 이면서 warmManifest 실효값이 ON 이면 경고 로그를 출력합니다.
        /// </summary>
        [Tooltip("빌드 시 ait-warm-manifest.json 을 산출합니다. 호스트(슈퍼앱)가 선다운로드(warm) diff 기준으로 사용합니다. " +
                 "pageCache 실효값이 OFF 이면 회색 비활성 + no-op (AND 게이팅). -1=자동(true), 0=비활성, 1=활성.")]
        public int warmManifest = -1;

        /// <summary>
        /// 빌드 시 self-warming 페이지(ait-warm.html)를 함께 산출합니다.
        /// tri-state: -1=자동(true, 기본 ON), 0=비활성, 1=활성.
        /// warmManifest 실효값과 pageCache 실효값이 모두 ON 이어야 동작합니다(AND 게이팅).
        /// pageCache·warmManifest 실효값이 모두 ON 일 때만 실제 산출되는 AND 게이트이며,
        /// 산출물은 정적 파일 1개 추가일 뿐 게임 런타임 동작에 영향이 없어 기본 ON.
        /// </summary>
        [Tooltip("-1 = 자동 (true), 0 = 비활성, 1 = 활성. " +
                 "빌드 시 self-warming 페이지(ait-warm.html)를 함께 산출합니다. " +
                 "호스트가 숨김 WebView 로 열면 매니페스트 변경분을 미리 캐시에 적재합니다. " +
                 "warmManifest 실효값과 pageCache 실효값이 모두 ON 이어야 동작합니다.")]
        public int warmPage = -1;

        /// <summary>
        /// 페이지 캐시 인터셉터가 호스트 네이티브 프리페치 결과를 우선 소스로 사용할지 여부.
        /// tri-state: -1=자동(true, 기본 ON), 0=비활성, 1=활성.
        /// 인터셉터가 존재해야(pageCache 실효값 ON) 동작하는 AND 게이트이며,
        /// 호스트가 window.__aitResolveAsset(url) 리졸버를 주입하지 않으면 신호만 노출되고
        /// 자동으로 CacheStorage→network 폴백으로 흡수되어 런타임 동작에 영향이 없어 기본 ON.
        /// </summary>
        [Tooltip("-1 = 자동 (true), 0 = 비활성, 1 = 활성. " +
                 "페이지 캐시 인터셉터가 Build/* 요청에 대해 호스트 네이티브 프리페치 결과를 우선 사용합니다. " +
                 "호스트가 window.__aitResolveAsset 리졸버를 주입하면 native→CacheStorage→network 순으로 해석합니다. " +
                 "리졸버 미주입 시 신호만 노출되고 기존 캐시-퍼스트 동작으로 자동 폴백됩니다. " +
                 "pageCache 실효값이 ON 이어야 동작합니다.")]
        public int nativeAssetSource = -1;

        [Header("렌더링 품질 설정")]
        [Tooltip("devicePixelRatio 설정: -1 = auto (기기 성능에 따라 자동 결정), 1/2/3 = 고정값. 높을수록 고품질이지만 GPU 부하 증가")]
        public int devicePixelRatio = -1;

        [Header("IL2CPP/Stripping 설정")]
        public bool stripEngineCode = true;

        [Tooltip("-1 = 자동 (Release). WebGL에서 Master는 emscripten 최적화/LTO에 영향을 주지 않음(no-op)")]
        public int il2cppConfiguration = -1;

        [Tooltip("-1 = 자동 (Disk Size with LTO 적용 — Coatsink/Meta 로드타임 스택의 실제 LTO 레버). 0 = 미적용(Unity 설정 유지), 1 = 적용")]
        public int webGLCodeOptimization = -1;
        [Tooltip("-1 = 자동 (OptimizeSize, Unity 6+). 0 = OptimizeSpeed, 1 = OptimizeSize — 제네릭 인스턴스 공유로 wasm 코드 크기 축소")]
        public int il2cppCodeGeneration = -1;

        [Header("Unity 6 전용 설정")]
        [Tooltip("-1 = 자동 (HighPerformance)")]
        public int powerPreference = -1;

        public bool wasmStreaming = true;

        [Tooltip("-1 = 자동 (활성화, Unity 6+). WebAssembly 2023 기능셋(native exception/SIMD/BigInt/Table). 미지원 브라우저에서는 로드 실패")]
        public int wasm2023 = -1;

        [Header("고급 설정 (주의: 변경 시 호환성 문제 발생 가능)")]
        [Tooltip("-1 = 자동 (FullWithStacktrace, Sentry 경고 방지)")]
        public int exceptionSupport = -1;

        [Tooltip("-1 = 자동 (false), Unity Pro 라이선스 필요")]
        public int showUnityLogo = -1;

        [Tooltip("-1 = 자동 (false). 끄면 JS Brotli 디컴프레서가 번들에서 제거됨 — 플랫폼 CDN의 Content-Encoding: br 의존")]
        public int decompressionFallback = -1;

        [Tooltip("-1 = 자동 (false)")]
        public int runInBackground = -1;

        [Tooltip("-1 = 자동 (false, Unity 6+)")]
        public int webAssemblyArithmeticExceptions = -1;

        [Header("콘텐츠 축소 (빌드 산출물 .data/.wasm 실감축, 빌드 후 원복)")]
        [Tooltip("-1 = 자동 (true). 사용하지 않는 텍스처 밉맵 레벨을 빌드 산출물에서 제거 — .data 축소. 설정된 품질의 출력은 불변(미사용 밉만 제거).")]
        public int mipStripping = -1;
        [Tooltip("-1 = 자동 (true). 어떤 머티리얼도 쓰지 않는 메시 정점 채널(노멀/탄젠트/UV 등)을 제거 — .data 축소. 주의: 런타임에 머티리얼을 교체해 제거된 채널을 요구하면 시각 오류 가능.")]
        public int stripUnusedMeshComponents = -1;

        [Header("빌드 전 검사 설정")]
        [Tooltip("빌드 전 에셋 최적화 검사를 활성화합니다")]
        public bool enableBuildOptimizationCheck = true;

        [Header("계측 설정")]
        [Tooltip("first-interactive 계측: -1 = 자동 (활성), 0 = 비활성, 1 = 활성")]
        public int firstInteractiveLog = -1;
        [Header("콘텐츠 최적화 — ASTC 블록 에스컬레이션")]
        [Tooltip("-1 = 자동 (true), 0 = 비활성, 1 = 활성. " +
                 "ASTC 서브타겟 WebGL 빌드에서 텍스처를 더 큰 ASTC 블록(기본 12x12)으로 reimport 하여 " +
                 ".data on-wire 크기를 줄입니다. lossy(화질 저하 있음). 빌드 후 원본 임포트 설정으로 자동 복원. " +
                 "ASTC 서브타겟 전용 — DXT(기본) 서브타겟 프로젝트에서는 빌드 시 자동 skip됩니다.")]
        public int astcBlockEscalation = -1;

        [Tooltip("ASTC 블록 크기(4/5/6/8/10/12). 클수록 파일이 작아지고 화질이 낮아집니다. 기본값 12.")]
        public int astcBlockSize = 12;

        [Tooltip("WebGL 플랫폼 오버라이드 maxTextureSize 캡. 0=캡 안 함(원본 크기 유지).")]
        public int astcBlockMaxSize = 0;

        [Tooltip("SpriteAtlas 도 포함하여 WebGL 플랫폼 설정을 오버라이드하고 repack합니다.")]
        public bool astcBlockAtlas = true;

        [Tooltip("ASTC 블록 에스컬레이션을 적용할 폴더(쉼표 구분). 비우면 Assets 전체가 대상입니다.")]
        public string astcBlockDirs = "";

        [Tooltip("ASTC 블록 에스컬레이션에서 제외할 폴더(쉼표 구분). " +
                 "폰트/SDF/TextMeshPro 경로는 이 필드와 무관하게 항상 내장 휴리스틱으로 추가 제외됩니다.")]
        public string astcBlockExcludeDirs = "";

        [Header("콘텐츠 최적화 — 오디오 스트리밍")]
        [Tooltip("-1 = 자동 (true), 0 = 비활성, 1 = 활성. " +
                 "대용량 오디오를 초기 .data 에서 분리해 StreamingAssets 로 외부화하고, 런타임에 비동기 스트리밍으로 복원합니다. " +
                 "초기 다운로드/TTI 를 크게 줄입니다. 빌드 시 오디오 에셋을 일시적으로 무음 스텁으로 치환했다가 빌드 후 원상 복원합니다.")]
        public int audioStreaming = -1;

        [Tooltip("이 바이트 수보다 큰 AudioClip 만 외부화 대상입니다 (기본 256KB).")]
        public int audioStreamingMinBytes = 262144;

        [Tooltip("외부화 대상 폴더(쉼표 구분, Assets/ 기준 경로). 비우면 프로젝트 전체의 큰 오디오가 대상입니다. 예) Assets/Sounds/BGM,Assets/Music")]
        public string audioStreamingDirs = "";

        [Header("콘텐츠 최적화 — 오디오 재인코딩 (lossy, 기본 ON)")]
        [Tooltip("-1 = 자동(활성), 0 = 비활성, 1 = 활성. " +
                 "대상 AudioClip 의 WebGL 임포터 설정을 빌드 시 일시적으로 compressionFormat=Vorbis + quality 로 override 해 " +
                 "reimport 하여 .data/CDN 오디오 용량을 줄입니다(빌드 후 원본 임포트 설정으로 복원). " +
                 "자동 모드는 '이미 Vorbis 인 클립은 건드리지 않고' 비압축(PCM)/ADPCM 만 Vorbis 로 변환하므로 세대손실 없이 near-transparent 합니다. " +
                 "SDK 가 이미 lossy 텍스처 최적화(crunch/ASTC)를 기본 ON 으로 두는 것과 동일 posture 로 기본 활성입니다. " +
                 "audioStreaming 으로 외부화된 클립은 대상에서 제외됩니다(무음 스텁 재인코딩 방지).")]
        public int audioReencode = -1;

        [Tooltip("Vorbis quality(0.0~1.0). 기본 0.7 = near-transparent 헤드룸. 낮출수록 더 작지만 아티팩트 위험이 커집니다. " +
                 "explicit 활성(1)에서는 이미 Vorbis 인 클립도 이 값을 초과하면 이 값으로 낮춥니다(자동 모드는 비압축만 변환).")]
        [Range(0f, 1f)]
        public float audioReencodeQuality = 0.7f;

        [Tooltip("소스 파일 크기 필터(바이트). 이 크기 미만 오디오는 제외(짧은 SFX 보호). 0 = 필터 없음")]
        public long audioReencodeMinBytes = 0;

        [Tooltip("대상 폴더(쉼표 구분, Assets/ 기준). 비우면 프로젝트 전체 오디오가 대상입니다. 예) Assets/Audio,Assets/Sounds")]
        public string audioReencodeDirs = "";

        [Tooltip("제외 폴더(쉼표 구분, Assets/ 기준). 특정 폴더를 재인코딩에서 제외(원본 품질 보존 escape hatch).")]
        public string audioReencodeExcludeDirs = "";

        [Header("콘텐츠 최적화 — 텍스처 crunch")]
        [Tooltip("-1 = 자동 (true), 0 = 비활성, 1 = 활성. " +
                 "대상 텍스처/SpriteAtlas 를 빌드 시 일시적으로 crunch(DXT 위 4~8x) 압축 + maxTextureSize 캡으로 reimport 하여 " +
                 "다운로드/.data 를 줄입니다. 빌드 후 원본 임포트 설정으로 복원합니다. " +
                 "crunch reimport 는 무겁습니다(에셋 수에 비례).")]
        public int textureCrunch = -1;

        [Tooltip("텍스처 maxTextureSize 상한(0=캡 안 함). 이 값보다 큰 텍스처만 축소합니다. 예) 512, 1024")]
        public int textureCrunchMaxSize = 0;

        [Range(0, 100)]
        [Tooltip("crunch 압축 품질(0~100). 낮을수록 작고 화질↓. 기본 50.")]
        public int textureCrunchQuality = 50;

        [Tooltip("SpriteAtlas 도 함께 crunch + WebGL repack 합니다(기본 true).")]
        public bool textureCrunchAtlas = true;

        [Tooltip("SpriteAtlas maxTextureSize 상한(0=캡 안 함). 예) 1024, 2048")]
        public int textureCrunchAtlasMaxSize = 0;

        [Tooltip("대상 폴더(쉼표 구분, Assets/ 기준). 비우면 프로젝트 전체 텍스처가 대상입니다. 예) Assets/Art/Textures")]
        public string textureCrunchDirs = "";
        [Header("콘텐츠 최적화 — 텍스처 크기 클램프 (lossy, 기본 ON)")]
        [Tooltip("-1 = 자동(활성), 0 = 비활성, 1 = 활성. " +
                 "대상 텍스처의 maxTextureSize 만 빌드 시 일시적으로 캡(상한)으로 낮춰 reimport 하여 텍셀 수를 줄입니다 " +
                 "(format/compression/crunch 불변). 예) 4096→2048 은 텍셀 1/4 → 압축 payload/on-wire 도 ~1/4. " +
                 "SDK 가 이미 lossy 텍스처 최적화(crunch/ASTC)를 기본 ON 으로 두는 것과 동일 posture 로 기본 활성이며, " +
                 "안전한 기본 캡 2048 초과분(사실상 4096)만 축소합니다. 의도적 고해상도는 값 0(비활성)·캡 상향·폴더 " +
                 "제외로 opt-out 가능하고, 빌드 후 원본 임포트 설정으로 복원합니다.")]
        public int textureSizeClamp = -1;

        [Tooltip("텍스처 maxTextureSize 상한(이 값보다 큰 텍스처만 축소). 16 미만은 무시. 예) 1536, 2048, 3072.\n\n" +
                 "기본 2048 = HiDPI 헤드룸. 미니앱은 devicePixelRatio(모바일 웹뷰 실질 2~3)로 렌더하며 SDK 는 고사양 기기에 " +
                 "native DPR(iPhone Pro=3, 플래그십 Android=3+)을 그대로 줍니다. 화면 일부를 점유하는 스프라이트/UI/아이콘은 " +
                 "2048 로 충분히 선명(예: 200 CSS px @DPR3 = 600px ≪ 2048)하고, full-bleed 배경만 DPR3 최대폰에서 세로가 " +
                 "약간 소프트해집니다. 1024 로 낮추면 DPR2 풀스크린에서도 뭉개질 수 있어 HiDPI 에 과합니다.")]
        public int textureClampMaxSize = 2048;

        [Tooltip("소스 파일 크기 필터(바이트). 이 크기 미만 텍스처는 제외(작은 아이콘 보호). 0 = 필터 없음")]
        public long textureClampMinBytes = 0;

        [Tooltip("대상 폴더(쉼표 구분, Assets/ 기준). 비우면 프로젝트 전체 텍스처가 대상입니다. 예) Assets/Art/Backgrounds")]
        public string textureClampDirs = "";

        [Tooltip("제외할 폴더(쉼표 구분, Assets/ 기준). 사용자 escape hatch.")]
        public string textureClampExcludeDirs = "";
        [Header("콘텐츠 최적화 — 폰트 CJK subset")]
        [Tooltip("크고(≥1MB) 빌드에 포함될 가능성이 있는 .ttf/.otf 를 자동 탐지해, 프로젝트에 실제 등장하는 " +
                 "문자체계의 유니코드 블록 전체를 보존하도록 subset 합니다(.data 폰트 데이터 급감, CJK 풀 폰트 5~15MB → ~0.1MB). " +
                 "빌드 후 원본 폰트로 복원합니다. zero-config: -1=자동(권장), 0=비활성, 1=자동(명시). " +
                 "수동 제어가 필요하면 fontSubsetTargetPaths/fontSubsetUnicodeRanges 로 override 합니다.\n\n" +
                 "⚠ 동적 텍스트 리스크: subset 은 보존 범위 밖 글자를 제거합니다(lossy). 스캐너가 프로젝트에 " +
                 "'실제 등장하는' 문자체계는 블록 전체를 보존하지만, 서버/외부에서 '전혀 다른 언어'의 텍스트를 " +
                 "동적으로 받아 표시하는데 그 문자체계가 프로젝트 어디에도 없으면 □(tofu)가 될 수 있습니다. " +
                 "그런 경우 fontSubsetExtraRanges 에 해당 문자체계 범위를 추가하거나, 해당 폰트를 " +
                 "fontSubsetExcludeTargetPaths 로 제외하세요(TMP fallback/Dynamic atlas 폰트는 자동 제외/경고).")]
        public int fontSubset = -1; // -1=자동, 0=비활성, 1=자동(명시적 ON)

        [Tooltip("(override) 보존할 유니코드 범위를 직접 지정(쉼표 구분, fontTools 표기). 비우면 Auto 스캔이 범위를 결정합니다. " +
                 "값을 넣으면 그 범위만 보존하는 수동 모드가 됩니다(스캔 생략).")]
        public string fontSubsetUnicodeRanges = "";

        [Tooltip("(override) subset 대상 폰트 에셋 경로(쉼표 구분, Assets/ 기준의 .ttf/.otf). 비우면 Auto 탐지가 대상을 정합니다. " +
                 "값을 넣으면 그 폰트만 대상이 되는 수동 모드가 됩니다. 예) Assets/Fonts/NotoSansKR.ttf")]
        public string fontSubsetTargetPaths = "";

        [Tooltip("(additive) Auto 스캔 결과에 '추가로' 항상 보존할 유니코드 범위(쉼표 구분, fontTools 표기). " +
                 "fontSubsetUnicodeRanges 와 달리 override 가 아니라 합집합(union)입니다. 스캔이 놓칠 수 있는 " +
                 "'외부에서 동적 로드하는 다른 언어'를 보강하는 안전 필드입니다. 예) 일본어 UGC 지원 → U+3040-30FF,U+FF66-FF9F. " +
                 "수동 범위(fontSubsetUnicodeRanges) 사용 시에도 함께 union 됩니다.")]
        public string fontSubsetExtraRanges = "";

        [Tooltip("(escape hatch) Auto 탐지 대상에서 '제외'할 폰트 경로(쉼표 구분, Assets/ 기준의 .ttf/.otf). " +
                 "임의 언어 UGC 를 렌더하는 폰트 등 subset 하면 안 되는 폰트를 명시 보호합니다. " +
                 "TMP fallback 소스/Dynamic atlas 소스 폰트는 이 목록과 무관하게 자동 제외/경고됩니다. 예) Assets/Fonts/UGC_Universal.ttf")]
        public string fontSubsetExcludeTargetPaths = "";
        [Header("콘텐츠 최적화 — 대형 텍스처 스트리밍")]
        [Tooltip("비-부팅 대형 Texture2D 를 초기 .data 에서 분리해 StreamingAssets 로 외부화하고, 소스를 '동일 차원 단색 스텁'으로 치환합니다. " +
                 "런타임(AITStreamingTexture)이 first-frame 이후 실 텍스처를 비동기 스트리밍 로드하여 동일 Texture2D 객체에 픽셀을 제자리 복원하므로 " +
                 "이를 참조하는 Sprite/Material 이 참조 재할당 없이 갱신됩니다. 초기 다운로드/TTFF 를 크게 줄입니다. " +
                 "빌드 후 원본/임포터 설정을 원상 복원합니다. -1 = 자동 (활성화), 0 = 비활성화, 1 = 활성화.")]
        public int textureStreaming = -1;

        [Tooltip("이 바이트 수보다 큰 텍스처 소스만 외부화 대상입니다 (기본 512KB). 소형 아이콘은 동적 Resources.Load 로 부팅에 끌려올 수 있어 보호합니다.")]
        public int textureStreamingMinBytes = 524288;

        [Tooltip("외부화 대상 폴더(쉼표 구분, Assets/ 기준 경로). 비우면 프로젝트 전체의 큰 텍스처가 대상입니다. 예) Assets/Art/BG,Assets/Textures")]
        public string textureStreamingDirs = "";

        [Tooltip("외부화에서 제외할 폴더(쉼표 구분, Assets/ 기준). 부팅에 필요한 텍스처를 사용자가 명시 보호하는 escape hatch. 예) Assets/UI/Always")]
        public string textureStreamingExcludeDirs = "";

        [Range(1, 8)]
        [Tooltip("런타임 동시 스트리밍 다운로드/디코드 상한(기본 3). LoadImage 가 RGBA32 로 강제하므로 VRAM/메인스레드 hitch 를 이 값으로 제한합니다.")]
        public int textureStreamingMaxConcurrent = 3;

        [Tooltip("(lossy, 기본 ON) 외부화된 스트림 사본(StreamingAssets, CDN 배포본)을 max-size 캡보다 크면 축소해 CDN 무압축 총량을 실감축합니다. " +
                 "프로젝트 원본은 빌드 후 그대로 복원되고, 축소는 '배포/런타임에 보이는 텍스처'에만 적용됩니다(스텁은 원본 차원 유지 → Sprite rect 정합). " +
                 "균일 배율(캡÷최대변)로 축소해 스프라이트시트 서브-rect UV 도 비율 보존됩니다. 스트림은 비-부팅이라 로딩속도엔 무영향, CDN 캡만 감소. " +
                 "클램프와 동일 posture 로 기본 활성이며 오히려 더 안전합니다(CDN 전용·원본 불변). -1 = 자동(활성), 0 = 비활성, 1 = 활성.")]
        public int textureStreamDownscale = -1;

        [Tooltip("스트림 사본 다운스케일 max-size 캡(이 값보다 큰 스트림 텍스처만 축소). 16 미만은 무시. 기본 2048 = HiDPI(DPR2~3) 헤드룸. " +
                 "예) 1536, 2048, 3072. textureClampMaxSize 와 같은 HiDPI 캡 개념(스트림 대상).")]
        public int textureStreamDownscaleMaxSize = 2048;

        [Header("콘텐츠 최적화 — 대형 폰트 deferral")]
        [Tooltip("-1 = 자동(1MB 이상·부팅 씬 미포함 TMP_FontAsset 자동 스캔 후 외부화), " +
                 "0 = 비활성화, 1 = 수동(fontStreamingTargetPaths 에 명시한 경로만 외부화). " +
                 "자동 모드: 비-부팅 대형 폰트를 초기 .data 에서 분리해 WebGL AssetBundle 로 StreamingAssets 에 외부화하고, " +
                 "소스 폰트를 최소 스텁 .ttf 로 치환해 .data 에서 .ttf 바이트를 제외합니다. " +
                 "런타임(AITStreamingFont)이 first-frame 이후 번들을 로드하여 그 안의 TMP_FontAsset 을 TMP fallback 체인에 주입해 재수화합니다. " +
                 "⚠ 동적 텍스트 리스크: 재수화 전(또는 TMP 부재 시) 대상 폰트의 글자는 □ 로 렌더됩니다.")]
        public int fontStreaming = -1;

        [Tooltip("수동 모드(fontStreaming=1)일 때 외부화 대상 TMP_FontAsset 경로(쉼표 구분, Assets/ 기준의 .asset). " +
                 "각 대상의 소스 .ttf/.otf 는 의존성에서 자동 해석됩니다. 예) Assets/Fonts/NotoSansSC SDF.asset,Assets/Fonts/NotoSansJP SDF.asset")]
        public string fontStreamingTargetPaths = "";

        [Range(1, 4)]
        [Tooltip("런타임 동시 번들 다운로드/로드 상한(기본 2). 재수화는 post-first-frame 배경 작업이라 TTFF 무관하나, 메인스레드 hitch 를 이 값으로 제한합니다.")]
        public int fontStreamingMaxConcurrent = 2;

        [Header("권한 설정")]
        public AITPermissionConfig permissionConfig = new AITPermissionConfig();

        /// <summary>
        /// 아이콘 URL 유효성 검사
        /// </summary>
        public bool IsIconUrlValid()
        {
            return !string.IsNullOrWhiteSpace(iconUrl) &&
                   (iconUrl.StartsWith("http://") || iconUrl.StartsWith("https://"));
        }

        /// <summary>
        /// 앱 ID 유효성 검사
        /// </summary>
        public bool IsAppNameValid()
        {
            if (string.IsNullOrWhiteSpace(appName))
                return false;

            // 영문, 숫자, 하이픈만 허용
            foreach (char c in appName)
            {
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 버전 형식 검사 (x.y.z)
        /// </summary>
        public bool IsVersionValid()
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            string[] parts = version.Split('.');
            if (parts.Length != 3)
                return false;

            foreach (string part in parts)
            {
                if (!int.TryParse(part, out _))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 배포 준비 완료 여부 (기본 설정만 체크, deploymentKey는 AITCredentials에서 별도 확인)
        /// </summary>
        public bool IsReadyForDeploy()
        {
            return IsIconUrlValid() &&
                   IsAppNameValid() &&
                   IsVersionValid();
        }

        /// <summary>
        /// bridgeColorMode 문자열 반환
        /// </summary>
        public string GetBridgeColorModeString()
        {
            return bridgeColorMode == 0 ? "inverted" : "basic";
        }

        /// <summary>
        /// webViewProps.type 문자열 반환
        /// </summary>
        public string GetWebViewTypeString()
        {
            switch (webViewType)
            {
                case 0: return "game";
                case 1: return "partner";
                default: return "game";
            }
        }

        /// <summary>
        /// navigationBar 옵션을 granite.config.ts/apps-in-toss.config.ts 형식의 TS 객체 리터럴로 반환.
        /// webViewType=game일 때만 transparentBackground/theme를 emit하고(하위호환), partner면 빈 객체를
        /// 반환해 기존 동작과 USER_CONFIG의 navigationBar를 보존한다.
        /// 형식 예: { transparentBackground: true, theme: 'dark' }
        /// </summary>
        public string GetNavigationBarJson()
        {
            // partner(비게임)는 동작 불변 — 빈 객체로 emit (USER_CONFIG의 navigationBar 보존)
            if (webViewType != 0)
                return "{}";

            var parts = new System.Collections.Generic.List<string>
            {
                "transparentBackground: " + navigationBarTransparentBackground.ToString().ToLower()
            };
            if (navigationBarTheme == 1)
                parts.Add("theme: 'light'");
            else if (navigationBarTheme == 2)
                parts.Add("theme: 'dark'");

            return "{ " + string.Join(", ", parts) + " }";
        }

        /// <summary>
        /// permissionConfig를 granite.config.ts 형식의 JSON 배열로 변환
        /// 형식: [{ name: 'geolocation', access: 'access' }, ...]
        /// </summary>
        public string GetPermissionsJson()
        {
            if (permissionConfig == null)
                return "[]";

            var objects = new System.Collections.Generic.List<string>();

            // Clipboard
            if (permissionConfig.clipboardRead)
                objects.Add("{ name: 'clipboard', access: 'read' }");
            if (permissionConfig.clipboardWrite)
                objects.Add("{ name: 'clipboard', access: 'write' }");

            // Contacts (read only)
            if (permissionConfig.contacts)
                objects.Add("{ name: 'contacts', access: 'read' }");

            // Photos (read only)
            if (permissionConfig.photos)
                objects.Add("{ name: 'photos', access: 'read' }");

            // Camera (access only)
            if (permissionConfig.camera)
                objects.Add("{ name: 'camera', access: 'access' }");

            // Geolocation (access only)
            if (permissionConfig.geolocation)
                objects.Add("{ name: 'geolocation', access: 'access' }");

            return "[" + string.Join(", ", objects) + "]";
        }
    }

    /// <summary>
    /// Unity 버전별 기본 설정값을 제공하는 클래스
    /// 출처: apps-in-toss-unity-docs/Design/UnityVersion.md
    /// </summary>
    public static class AITDefaultSettings
    {
        /// <summary>
        /// 버전별 기본 메모리 크기 (MB)
        /// - Unity 2021.3: 256MB (UnityVersion.md:439)
        /// - Unity 2022.3: 512MB (UnityVersion.md:430)
        /// - Unity 6/2023.3: 1024MB (UnityVersion.md:392)
        /// - Unity 2024.2: 1536MB (UnityVersion.md:415)
        /// </summary>
        public static int GetDefaultMemorySize()
        {
#if UNITY_2024_2_OR_NEWER
            return 1536;  // AI 모델용 대용량 메모리
#elif UNITY_2023_3_OR_NEWER
            return 1024;  // Unity 6
#elif UNITY_2022_3_OR_NEWER
            return 512;   // Unity 2022.3
#else
            return 256;   // Unity 2021.3 (호환성 우선)
#endif
        }

        /// <summary>
        /// 버전별 기본 스레딩 지원 여부
        /// - Unity 2021.3/2022.3: false (UnityVersion.md:432 "브라우저 호환성")
        /// - Unity 6+: true (UnityVersion.md:394 "향상된 멀티스레딩")
        /// </summary>
        public static bool GetDefaultThreadsSupport()
        {
            // WebGL 멀티스레딩은 COOP/COEP 헤더가 필요하여 배포 환경에 따라 문제 발생 가능
            // 필요한 경우 사용자가 직접 활성화하도록 기본값은 false
            return false;
        }

        /// <summary>
        /// 기본 페이지 캐시 활성화 여부 (재방문 CacheStorage 서빙)
        /// 모든 Unity 버전에서 기본 활성화: 미지원 환경에서 무해 통과가 보장됨.
        /// </summary>
        public static bool GetDefaultPageCache()
        {
            return true;
        }

        /// <summary>
        /// 기본 warm manifest 산출 여부.
        /// pageCache 와 쌍으로 기본 ON: 호스트 warm 연동 zero-config 제공.
        /// pageCache 실효값이 OFF 이면 게이팅으로 no-op 처리되므로 독립적으로 ON 해도 안전.
        /// </summary>
        public static bool GetDefaultWarmManifest()
        {
            return true;
        }

        /// <summary>
        /// 기본 warm 페이지(ait-warm.html) 산출 여부.
        /// pageCache·warmManifest 실효값이 모두 ON 일 때만 실제 산출되는 AND 게이트이며,
        /// 산출물은 정적 파일 1개 추가일 뿐 게임 런타임 동작에 영향이 없어 기본 ON.
        /// </summary>
        public static bool GetDefaultWarmPage()
        {
            return true;
        }

        /// <summary>
        /// 기본 네이티브 에셋 소스 우선 사용 여부.
        /// pageCache 실효값이 ON 일 때만 인터셉터에 신호가 주입되는 AND 게이트이며,
        /// 호스트가 리졸버를 주입하지 않으면 신호만 노출되고 캐시-퍼스트로 자동 폴백되어 기본 ON.
        /// </summary>
        public static bool GetDefaultNativeAssetSource()
        {
            return true;
        }

        /// <summary>
        /// 버전별 기본 데이터 캐싱 여부
        /// - Unity 2021.3/2022.3: false
        /// - Unity 6+: true (UnityVersion.md:401)
        /// </summary>
        public static bool GetDefaultDataCaching()
        {
#if UNITY_2023_3_OR_NEWER
            return true;  // Unity 6에서만 활성화 권장
#else
            return false;
#endif
        }

        /// <summary>
        /// 기본 압축 포맷: Brotli
        /// decompressionFallback=false이므로 브라우저/CDN이 Content-Encoding: br로 네이티브 해제 (모든 Unity 버전 Brotli)
        /// </summary>
        public static WebGLCompressionFormat GetDefaultCompressionFormat()
        {
            return WebGLCompressionFormat.Brotli;
        }

        /// <summary>
        /// 기본 Managed Stripping Level
        /// 출처: StartupOptimization.md:89
        /// </summary>
        public static ManagedStrippingLevel GetDefaultManagedStrippingLevel()
        {
            return ManagedStrippingLevel.High;
        }

        /// <summary>
        /// 기본 IL2CPP 컴파일러 설정: Release
        /// 주의: 과거 이 값을 Master로 두고 Coatsink "Disk Size with LTO"의 LTO 파트라 가정했으나,
        /// 실측 결과 WebGL에서 컴파일러 config(Master)는 emscripten 최적화/LTO에 영향을 주지 않아
        /// Release와 바이트 단위로 동일한 산출물을 냈다(no-op). 실제 LTO 레버는
        /// emscripten code optimization = "Disk Size with LTO"이며 GetDefaultWebGLCodeOptimization()이 담당한다.
        /// </summary>
        public static Il2CppCompilerConfiguration GetDefaultIl2CppConfiguration()
        {
            return Il2CppCompilerConfiguration.Release;
        }

        /// <summary>
        /// 기본 WebGL Code Optimization: "Disk Size with LTO"(DiskSizeLTO)
        /// Meta+Unity 로드타임 스택의 실제 LTO 레버. emscripten Link Time Optimization으로
        /// cross-module dead-code를 제거해 wasm 코드 크기를 추가로 축소한다(실측 기준 압축전 ~-21%).
        /// trade-off: 빌드 시간 증가(LTO 링크). API가 버전마다 다르고(2022.3/6: UserBuildSettings,
        /// 구버전: PlayerSettings.WebGL) 모듈 어셈블리 참조 보장이 없어 AITWebGLCodeOptimization이
        /// reflection으로 적용한다. 멤버가 없는 버전(예: 2021.3)에서는 fail-safe로 건너뛴다.
        /// 출처: Unity Manual web-optimization-c-sharp, Coatsink "Ready, Set, Cook!" 케이스 스터디
        /// </summary>
        public static string GetDefaultWebGLCodeOptimization()
        {
            return AppsInToss.Editor.AITWebGLCodeOptimization.DiskSizeLTO;
        }
#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// 기본 IL2CPP 코드 생성 방식 (Unity 6+): OptimizeSize
        /// Meta+Unity 로드타임 스택의 "Faster (smaller) builds" — 제네릭 인스턴스화를
        /// 공유해 제네릭 폭발(측정상 ~130k 함수)을 붕괴시켜 wasm 코드 크기를 축소한다.
        /// trade-off: 공유 제네릭의 미세한 런타임 디스패치 비용(정확성 변화 아님).
        /// 출처: Unity Manual web-optimization-player (IL2CPP Code Generation = Optimize for code size)
        /// </summary>
        public static UnityEditor.Build.Il2CppCodeGeneration GetDefaultIl2CppCodeGeneration()
        {
            return UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize;
        }
#endif
        /// 기본 WebAssembly 2023 타겟 여부 (Unity 6+): 활성화
        /// Meta+Unity 로드타임 최적화: native exception/SIMD/BigInt/WebAssembly.Table 등
        /// 2023 기능셋을 번들해 코드 크기·다운로드·시작 시간을 단축한다.
        /// 주의: 미지원 브라우저(대략 Chrome&lt;91 / Safari&lt;16.4)에서는 graceful
        /// degradation이 아니라 로드 자체가 실패한다. Apps in Toss는 Toss 앱 WebView
        /// 전용이라 플랫폼 min-spec이 이를 충족하는 전제에서만 기본 활성.
        /// </summary>
        public static bool GetDefaultWasm2023()
        {
#if UNITY_6000_0_OR_NEWER
            return true;
#else
            return false;
#endif
        }

#if UNITY_2023_3_OR_NEWER
        /// <summary>
        /// Unity 6 전용: 기본 전력 설정
        /// 출처: UnityVersion.md:396
        /// </summary>
        public static WebGLPowerPreference GetDefaultPowerPreference()
        {
            return WebGLPowerPreference.HighPerformance;
        }
#endif

        /// <summary>
        /// 기본 예외 처리 모드
        /// 출처: UnityVersion.md:393, 431
        /// </summary>
        public static WebGLExceptionSupport GetDefaultExceptionSupport()
        {
            // Sentry/에러 추적 SDK가 stack trace를 캡처하려면 FullWithStacktrace 필요.
            // Unity 기본값(ExplicitlyThrownExceptionsOnly)을 올려서 Sentry의 런타임 경고 제거.
            return WebGLExceptionSupport.FullWithStacktrace;
        }

        /// <summary>
        /// 기본 Unity 로고 표시 여부
        /// Unity Pro 라이선스가 있으면 false, 없으면 true (필수)
        /// </summary>
        public static bool GetDefaultShowUnityLogo()
        {
            // Unity Pro가 아니면 로고 표시 필수
            return !UnityEditorInternal.InternalEditorUtility.HasPro();
        }

        /// <summary>
        /// 기본 Decompression Fallback: 비활성화(false)
        /// 끄면 Unity가 JS Brotli 디컴프레서를 프레임워크 번들에서 제외 → 다운로드/파싱 바이트 감소.
        /// 대신 브라우저/CDN이 Content-Encoding: br로 .unityweb를 네이티브 해제하도록 의존한다.
        /// Apps in Toss 플랫폼 CDN이 br 인코딩을 서빙하는 전제에서만 안전(자체 호스팅 시 헤더 필수).
        /// </summary>
        public static bool GetDefaultDecompressionFallback()
        {
            return false;
        }

        /// <summary>
        /// 기본 Run In Background
        /// 모바일 환경에서는 false 권장
        /// </summary>
        public static bool GetDefaultRunInBackground()
        {
            return false;
        }

        /// <summary>
        /// 기본 Mip Stripping: 활성화(true)
        /// 빌드 산출물에서 실제로 참조되지 않는 텍스처 밉맵 레벨을 제거해 .data 크기를 줄인다.
        /// 설정된 품질의 출력은 불변(쓰이지 않는 밉만 제거되므로 시각적 변화 없음).
        /// </summary>
        public static bool GetDefaultMipStripping()
        {
            return true;
        }

        /// <summary>
        /// 기본 Optimize Mesh Data(Strip Unused Mesh Components): 활성화(true)
        /// 어떤 머티리얼도 참조하지 않는 메시 정점 채널(노멀/탄젠트/UV 등)을 빌드 산출물에서 제거해 .data를 줄인다.
        /// 주의: 런타임에 머티리얼을 교체해 제거된 채널을 요구하면 시각 오류가 발생할 수 있다.
        /// </summary>
        public static bool GetDefaultStripUnusedMeshComponents()
        {
            return true;
        }

        /// <summary>
        /// 기본 폰트 CJK subset 활성화 여부.
        /// zero-config 철학: 기본 ON + 자동 안전장치(스캔/블록 완성/보수적 베이스라인/빌드 리포트/opt-out).
        /// 스캔이 등장 문자체계의 블록 전체를 보존하므로 동적 텍스트(닉네임/채팅)도 정확성이 보존된다.
        /// </summary>
        public static bool GetDefaultFontSubset()
        {
            return true;
        }

        /// <summary>
        /// 폰트 스트리밍 기본값: 자동 모드(-1 → true)
        /// 1MB 이상·부팅 씬 미포함 TMP_FontAsset 을 자동 스캔하여 외부화합니다.
        /// </summary>
        public static bool GetDefaultFontStreaming()
        {
            return true;
        }

#if UNITY_2023_3_OR_NEWER && !UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Unity 2023.3 전용: 기본 WASM 산술 예외 처리
        /// Unity 6000에서는 이 설정이 제거됨
        /// 출처: UnityVersion.md:397
        /// </summary>
        public static bool GetDefaultWebAssemblyArithmeticExceptions()
        {
            return false;
        }
#endif

        /// <summary>
        /// first-interactive 계측 기본 활성 여부.
        /// 픽셀 불변·세션당 1회 단일 이벤트·호스트 로딩 지표 표준화에 해당하므로 기본 ON.
        /// </summary>
        public static bool GetDefaultFirstInteractiveLog()
        {
            return true;
        }

        /// <summary>
        /// 기본 오디오 스트리밍 활성화 여부.
        /// 256KB 초과 AudioClip 을 초기 .data 에서 분리해 StreamingAssets 로 외부화하고
        /// 런타임에 비동기로 복원 — TTI 단축 효과가 크므로 기본 ON.
        /// </summary>
        public static bool GetDefaultAudioStreaming()
        {
            return true;
        }

        /// <summary>
        /// 기본 오디오 재인코딩 활성화 여부.
        /// 표시/청취 품질을 낮추는 lossy 변경이지만, 자동 모드는 비압축(PCM)/ADPCM 만 Vorbis(q≈0.7)로 변환하고
        /// 이미 압축된(Vorbis) 클립은 건드리지 않아 세대손실이 없고 near-transparent 하다. WebGL 에서 PCM 오디오는
        /// 사실상 오설정이므로 이를 Vorbis 로 정규화하는 것은 crunch/ASTC 를 기본 ON 으로 두는 것과 동일 posture.
        /// 미니앱 플랫폼(다운로드/.data 민감)에서 기본 ON(opt-out)이 효익을 실현한다. 빌드 후 임포터 설정은 항상 원상 복원.
        /// </summary>
        public static bool GetDefaultAudioReencode()
        {
            return true;
        }

        /// <summary>
        /// 기본 텍스처 crunch 활성화 여부.
        /// crunch(DXT 위 4~8x)는 q=50 기준 시각 저하가 통상 미미한 반면 다운로드/.data 절감이 커 기본 ON.
        /// ASTC 서브타겟에서는 빌드 시 자동으로 건너뛰고(no-op), 빌드 후 임포터 설정은 항상 원상 복원된다.
        /// </summary>
        public static bool GetDefaultTextureCrunch()
        {
            return true;
        }

        /// <summary>
        /// 텍스처 크기 클램프(maxTextureSize 캡) 기본 활성 여부.
        /// 표시 해상도를 낮추는 lossy 변경이지만, SDK 는 이미 lossy 텍스처 최적화를 기본 ON 으로 둔다
        /// (crunch=DXT 압축, ASTC 블록 에스컬레이션). 미니앱 플랫폼(200MB 캡·모바일 다운로드 민감)에서
        /// 그게 SDK 의 존재 이유이므로, 클램프도 동일 posture 로 기본 ON(opt-out)이 일관적이다.
        /// 기본 캡 2048 은 안전한 HiDPI 헤드룸 — 2048 초과(사실상 4096) 텍스처만 축소되고, 이는 DPR3
        /// 모바일에서 대개 과하다. 의도적 4096 은 캡 상향/폴더 제외/값 0 으로 opt-out 가능하고,
        /// 빌드 후 임포터 설정은 항상 원상 복원된다.
        /// </summary>
        public static bool GetDefaultTextureSizeClamp()
        {
            return true;
        }

        /// <summary>
        /// 기본 ASTC 블록 에스컬레이션 활성화 여부.
        /// 블록 확대(예: 12x12)는 시각 저하가 통상 미미한 반면 다운로드/.data 절감이 커 기본 ON.
        /// 비-ASTC 서브타겟에서는 빌드 시 자동으로 건너뛰고(no-op), 빌드 후 임포터 설정은 항상 원상 복원된다.
        /// </summary>
        public static bool GetDefaultAstcBlockEscalation()
        {
            return true;
        }

        /// <summary>
        /// 기본 텍스처 스트리밍 활성 여부.
        /// 비-부팅 대형 텍스처를 자동으로 외부화해 초기 다운로드/TTFF 를 줄인다(zero-config).
        /// </summary>
        public static bool GetDefaultTextureStreaming()
        {
            return true;
        }

        /// <summary>
        /// 기본 스트림 사본 다운스케일 활성 여부.
        /// 외부화된 스트림 사본을 HiDPI 캡으로 축소해 CDN 무압축 총량을 줄인다. 클램프와 동일 posture 로
        /// 기본 ON(opt-out)이며, 클램프보다 오히려 안전하다 — 스트림 사본은 비-부팅(CDN 전용)이라 로딩
        /// 속도엔 영향이 없고, 프로젝트 원본은 항상 불변이며, 기본 캡 2048 초과분만 축소된다.
        /// </summary>
        public static bool GetDefaultTextureStreamDownscale()
        {
            return true;
        }

        /// <summary>
        /// 현재 Unity 버전의 버전 그룹 이름 반환
        /// </summary>
        public static string GetUnityVersionGroup()
        {
#if UNITY_2024_2_OR_NEWER
            return "Unity 2024.2+";
#elif UNITY_2023_3_OR_NEWER
            return "Unity 6 (2023.3+)";
#elif UNITY_2022_3_OR_NEWER
            return "Unity 2022.3 LTS";
#else
            return "Unity 2021.3 LTS";
#endif
        }
    }
}
