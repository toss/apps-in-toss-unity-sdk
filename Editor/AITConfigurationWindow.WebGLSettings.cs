using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    public partial class AITConfigurationWindow
    {
        private void DrawWebGLOptimizationSettings()
        {
            showWebGLSettings = EditorGUILayout.Foldout(showWebGLSettings, "WebGL 최적화 설정", true);

            if (!showWebGLSettings) return;

            EditorGUILayout.BeginVertical("box");

            // 현재 Unity 버전 정보
            EditorGUILayout.HelpBox(
                $"현재 Unity 버전: {AITDefaultSettings.GetUnityVersionGroup()}\n" +
                "각 설정은 Unity 버전에 맞게 자동으로 최적화됩니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 메모리 크기
            DrawMemorySizeSetting();

            // 스레딩 지원
            DrawThreadsSupportSetting();

            // 데이터 캐싱: 베타 기능 — 정식 공개 전까지 설정 UI에서 제외
            // (config.dataCaching 저장값과 빌드 적용 로직은 유지 — AITBuildInitializer 참조)

            // 파일 해싱
            config.nameFilesAsHashes = EditorGUILayout.Toggle("파일명 해싱", config.nameFilesAsHashes);

            GUILayout.Space(10);

            // first-interactive 계측
            DrawFirstInteractiveLogSetting();
            // 페이지 캐시 (재방문 서빙, opt-in)
            DrawPageCacheSetting();

            // 콘텐츠 최적화 — 오디오 스트리밍
            DrawAudioStreamingSetting();

            GUILayout.Space(10);

            // 콘텐츠 최적화 — 오디오 재인코딩 (Vorbis+quality)
            DrawAudioReencodeSetting();

            GUILayout.Space(10);

            // 콘텐츠 최적화 — 텍스처 crunch (빌드 산출물 .data 축소)
            DrawTextureCrunchSetting();

            GUILayout.Space(10);

            // 콘텐츠 최적화 — 텍스처 크기 클램프 (maxTextureSize 캡)
            DrawTextureSizeClampSetting();
            // 콘텐츠 최적화 — ASTC 블록 에스컬레이션
            DrawAstcBlockSetting();
            // 콘텐츠 최적화 — Mesh 압축 (정점 데이터 양자화)
            DrawMeshCompressionSetting();
            // 콘텐츠 최적화 — 폰트 CJK subset (.data 폰트 데이터 축소)
            DrawFontSubsetSetting();

            GUILayout.Space(10);

            // PlayerPrefs 영속화
            DrawPlayerPrefsPersistenceSetting();

            GUILayout.Space(10);

            // 변경된 설정 개수 표시
            int modifiedCount = CountModifiedWebGLSettings();
            if (modifiedCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{modifiedCount}개 설정이 기본값에서 변경됨",
                    MessageType.Warning
                );

                if (GUILayout.Button("모든 WebGL 설정 기본값으로 복원"))
                {
                    ResetWebGLSettings();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAudioStreamingSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultAudioStreaming();
            bool isModified = config.audioStreaming >= 0 && (config.audioStreaming == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 오디오 스트리밍", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.audioStreaming < 0
                ? $"오디오 스트리밍 (자동: {autoLabel})"
                : "오디오 스트리밍";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.audioStreaming < 0 ? 0 : config.audioStreaming + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대용량 오디오를 초기 .data에서 분리해 StreamingAssets로 외부화하고, 런타임에 비동기 스트리밍으로 복원합니다. " +
                    "초기 다운로드/TTI를 크게 줄입니다. 빌드 시 오디오를 무음 스텁으로 일시 치환했다가 빌드 후 원상 복원합니다."),
                currentIndex,
                options
            );
            config.audioStreaming = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.audioStreaming = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 오디오 스트리밍이 켜져 있으면 (자동 포함) 하위 옵션 + 지연 안내 항시 표시
            bool effectiveEnabled = config.audioStreaming >= 0 ? config.audioStreaming == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;

                config.audioStreamingMinBytes = EditorGUILayout.IntField(
                    new GUIContent("최소 크기(Bytes)", "이 바이트 수보다 큰 AudioClip만 외부화합니다 (기본 262144 = 256KB)."),
                    config.audioStreamingMinBytes);
                if (config.audioStreamingMinBytes <= 0)
                {
                    config.audioStreamingMinBytes = 262144;
                }

                config.audioStreamingDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체의 큰 오디오가 대상. 예) Assets/Sounds/BGM,Assets/Music"),
                    config.audioStreamingDirs);

                // 하위 레버: 외부화 사본 저비트레이트 재인코딩 (청취 검증 전까지 auto=OFF, 명시 활성 전용)
                bool transcodeDefault = AITDefaultSettings.GetDefaultAudioStreamTranscode();
                string transcodeAuto = transcodeDefault ? "활성화" : "비활성화";
                string[] transcodeOptions = { $"자동 ({transcodeAuto})", "비활성화", "활성화" };
                int transcodeIndex = config.audioStreamTranscode < 0 ? 0 : config.audioStreamTranscode + 1;
                int newTranscodeIndex = EditorGUILayout.Popup(
                    new GUIContent("스트림 재인코딩 (lossy)",
                        "외부화된 스트리밍 MP3 사본을 저비트레이트 MP3로 재인코딩해 .ait 번들 크기를 줄입니다(원본 비접촉, 런타임 불변). " +
                        "소스가 이미 lossy라 세대손실이 누적되고 루핑 BGM 이음새 갭 위험이 있어, 청취 검증 전까지 명시 활성에서만 동작합니다."),
                    transcodeIndex,
                    transcodeOptions);
                config.audioStreamTranscode = newTranscodeIndex == 0 ? -1 : newTranscodeIndex - 1;

                bool transcodeEnabled = config.audioStreamTranscode >= 0
                    ? config.audioStreamTranscode == 1
                    : transcodeDefault;
                if (transcodeEnabled)
                {
                    EditorGUI.indentLevel++;
                    config.audioStreamTranscodeBitrateKbps = EditorGUILayout.IntField(
                        new GUIContent("목표 비트레이트(kbps)", "기본 160 (CBR). 96~320 범위로 클램프됩니다."),
                        config.audioStreamTranscodeBitrateKbps);
                    if (config.audioStreamTranscodeBitrateKbps <= 0)
                    {
                        config.audioStreamTranscodeBitrateKbps = 160;
                    }

                    config.audioStreamTranscodeMinSourceKbps = EditorGUILayout.IntField(
                        new GUIContent("소스 최소 비트레이트(kbps)", "이 평균 비트레이트 이상인 소스만 재인코딩(기본 256)."),
                        config.audioStreamTranscodeMinSourceKbps);
                    if (config.audioStreamTranscodeMinSourceKbps <= 0)
                    {
                        config.audioStreamTranscodeMinSourceKbps = 256;
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;

                // 지연 안내 — 항시 표시 (active 상태인 경우)
                EditorGUILayout.HelpBox(
                    "interactive 이후 오디오가 비동기 복원되어 초기 BGM 시작이 수백 ms 지연될 수 있음. " +
                    "빌드 후 소스 오디오는 자동 복원됩니다.",
                    MessageType.Info);
            }
        }

        private void DrawAudioReencodeSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultAudioReencode();
            bool isModified = config.audioReencode >= 0 && (config.audioReencode == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 오디오 재인코딩", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.audioReencode < 0
                ? $"오디오 재인코딩 (자동: {autoLabel})"
                : "오디오 재인코딩";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.audioReencode < 0 ? 0 : config.audioReencode + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대상 AudioClip 의 WebGL compressionFormat/quality 만 빌드 시 Vorbis 로 override 해 reimport 하여 오디오 용량을 줄입니다 " +
                    "(loadType/sampleRate 불변). 자동 모드는 비압축(PCM)/ADPCM 만 변환하고 이미 Vorbis 인 클립은 건드리지 않아 near-transparent 합니다. " +
                    "빌드 후 원본 임포트 설정으로 복원합니다. audioStreaming 으로 외부화된 클립은 제외됩니다."),
                currentIndex,
                options
            );
            config.audioReencode = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.audioReencode = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.audioReencode >= 0 ? config.audioReencode == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;

                config.audioReencodeQuality = EditorGUILayout.Slider(
                    new GUIContent("Vorbis quality", "0.0~1.0. 기본 0.7 = near-transparent. 낮출수록 더 작지만 아티팩트 위험. explicit 활성(활성화) 시 이미 Vorbis 인 클립도 이 값 초과분을 낮춥니다."),
                    config.audioReencodeQuality, 0f, 1f);

                config.audioReencodeMinBytes = EditorGUILayout.LongField(
                    new GUIContent("최소 크기(Bytes)", "이 바이트 수보다 큰 오디오만 재인코딩(짧은 SFX 보호). 0 = 필터 없음."),
                    config.audioReencodeMinBytes);
                if (config.audioReencodeMinBytes < 0)
                {
                    config.audioReencodeMinBytes = 0;
                }

                config.audioReencodeDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체 오디오가 대상. 예) Assets/Audio,Assets/Sounds"),
                    config.audioReencodeDirs);

                config.audioReencodeExcludeDirs = EditorGUILayout.TextField(
                    new GUIContent("제외 폴더(쉼표 구분)", "특정 폴더를 재인코딩에서 제외(원본 품질 보존 escape hatch)."),
                    config.audioReencodeExcludeDirs);

                EditorGUI.indentLevel--;

                if (config.audioReencode == 1)
                {
                    EditorGUILayout.HelpBox(
                        "explicit 활성: 이미 Vorbis 인 클립도 quality 초과 시 낮춥니다(세대손실 가능). " +
                        "자동(권장)은 비압축만 변환해 세대손실이 없습니다.",
                        MessageType.Info);
                }
            }
        }

        private void DrawTextureCrunchSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultTextureCrunch();
            bool isModified = config.textureCrunch >= 0 && (config.textureCrunch == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 텍스처 crunch", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.textureCrunch < 0
                ? $"텍스처 crunch (자동: {autoLabel})"
                : "텍스처 crunch";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.textureCrunch < 0 ? 0 : config.textureCrunch + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대상 텍스처/SpriteAtlas를 빌드 시 crunch(DXT 위 4~8x) 압축 + maxTextureSize 캡으로 reimport하여 다운로드/.data를 줄입니다. " +
                    "빌드 후 원본 임포트 설정으로 복원합니다. crunch reimport는 무겁습니다(에셋 수에 비례)."),
                currentIndex,
                options
            );
            config.textureCrunch = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.textureCrunch = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.textureCrunch >= 0 ? config.textureCrunch == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;
                config.textureCrunchMaxSize = EditorGUILayout.IntField(
                    new GUIContent("최대 텍스처 크기(0=캡 안 함)", "이 값보다 큰 텍스처만 축소합니다. 예) 512, 1024"),
                    config.textureCrunchMaxSize);

                config.textureCrunchQuality = EditorGUILayout.IntSlider(
                    new GUIContent("crunch 품질(0~100)", "낮을수록 작고 화질↓. 기본 50."),
                    Mathf.Clamp(config.textureCrunchQuality, 0, 100), 0, 100);

                config.textureCrunchAtlas = EditorGUILayout.Toggle(
                    new GUIContent("SpriteAtlas 포함", "SpriteAtlas도 함께 crunch + WebGL repack 합니다."),
                    config.textureCrunchAtlas);

                if (config.textureCrunchAtlas)
                {
                    config.textureCrunchAtlasMaxSize = EditorGUILayout.IntField(
                        new GUIContent("아틀라스 최대 크기(0=캡 안 함)", "예) 1024, 2048"),
                        config.textureCrunchAtlasMaxSize);
                }

                config.textureCrunchDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체 텍스처가 대상. 예) Assets/Art/Textures"),
                    config.textureCrunchDirs);

                EditorGUILayout.HelpBox(
                    "crunch는 lossy 압축입니다. 특히 그라데이션/UI 텍스처에서 화질이 눈에 띄게 저하될 수 있으므로 " +
                    "빌드 후 결과를 반드시 확인하세요. 빌드 후 원본 임포트 설정은 자동 복원됩니다.\n\n" +
                    "⚠ WebGL Texture Compression을 ASTC로 설정한 경우 crunch(DXT 기반)가 동작하지 않으며 " +
                    "오히려 RGBA32 비압축으로 팽창할 수 있습니다. 이 경우 빌드 시 자동으로 건너뜁니다(DXT 서브타겟에서만 유효).",
                    MessageType.Warning);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTextureSizeClampSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultTextureSizeClamp();
            bool isModified = config.textureSizeClamp >= 0 && (config.textureSizeClamp == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — 텍스처 크기 클램프", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.textureSizeClamp < 0
                ? $"텍스처 크기 클램프 (자동: {autoLabel})"
                : "텍스처 크기 클램프";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.textureSizeClamp < 0 ? 0 : config.textureSizeClamp + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대상 텍스처의 maxTextureSize만 빌드 시 일시적으로 캡(상한)으로 낮춰 reimport하여 텍셀 수를 줄입니다 " +
                    "(format/compression/crunch 불변). 예) 2048→1024는 텍셀 1/4. 빌드 후 원본 임포트 설정으로 복원합니다."),
                currentIndex,
                options
            );
            config.textureSizeClamp = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.textureSizeClamp = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.textureSizeClamp >= 0 ? config.textureSizeClamp == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;
                if (config.textureSizeClamp == 1)
                {
                    config.textureClampMaxSize = EditorGUILayout.IntField(
                        new GUIContent("최대 텍스처 크기", "이 값보다 큰 텍스처만 축소합니다. 16 미만은 무시. 기본 2048 = HiDPI 헤드룸. 예) 1536, 2048, 3072"),
                        config.textureClampMaxSize);
                }
                else
                {
                    // 자동 모드는 SDK 안전 캡 고정(구버전 자산에 박제된 옛 캡이 의도 없이 적용되는 것 차단).
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.IntField(
                            new GUIContent("최대 텍스처 크기 (자동: SDK 관리)",
                                "자동 모드는 SDK 안전 캡(2048)을 사용합니다. 캡을 직접 튜닝하려면 '활성화'로 전환하세요."),
                            AITDefaultSettings.GetDefaultTextureClampMaxSize());
                    }
                }

                int minBytesKb = EditorGUILayout.IntField(
                    new GUIContent("소스 크기 필터(KB, 0=없음)", "소스 파일이 이 크기 미만이면 제외(작은 아이콘 보호)."),
                    (int)(config.textureClampMinBytes / 1024));
                config.textureClampMinBytes = (long)Mathf.Max(0, minBytesKb) * 1024;

                config.textureClampDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더(쉼표 구분)", "Assets/ 기준 경로. 비우면 프로젝트 전체 텍스처가 대상. 예) Assets/Art/Backgrounds"),
                    config.textureClampDirs);

                config.textureClampExcludeDirs = EditorGUILayout.TextField(
                    new GUIContent("제외 폴더(쉼표 구분)", "Assets/ 기준 경로. 클램프에서 제외할 폴더(escape hatch)."),
                    config.textureClampExcludeDirs);

                EditorGUILayout.HelpBox(
                    "크기 클램프는 표시 해상도를 낮추는 lossy 변경입니다(예: 2048→1024). UI/텍스트 텍스처에서 흐려짐이 " +
                    "눈에 띌 수 있으므로 빌드 후 결과를 반드시 확인하세요. crunch와 달리 format/압축은 건드리지 않아 " +
                    "ASTC로 굽는 프로젝트에서도 그대로 적용됩니다. 빌드 후 원본 임포트 설정은 자동 복원됩니다.",
                    MessageType.Warning);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFontSubsetSetting()
        {
            EditorGUILayout.LabelField("콘텐츠 최적화 — 폰트 CJK subset", EditorStyles.boldLabel);

            bool defaultValue = AITDefaultSettings.GetDefaultFontSubset();
            // 수동 설정 모드: targetPaths 또는 unicodeRanges 가 채워져 있으면 override(수동) 모드로 간주.
            bool hasManualOverride = !string.IsNullOrWhiteSpace(config.fontSubsetTargetPaths)
                || !string.IsNullOrWhiteSpace(config.fontSubsetUnicodeRanges);
            bool hasLanguageSelection = !string.IsNullOrWhiteSpace(config.fontSubsetLanguages);

            // S8: 선택 언어 lazy 확장(tri-state)도 이 섹션 소속 — audioStreamTranscode 와 동일하게
            // 선언 기본값과 실효값이 다를 때만 "수정됨"으로 집계(아래 lazyEnabled UI 계산과 동일 공식).
            bool lazyDefault = AITDefaultSettings.GetDefaultFontSubsetLazyLanguages();
            bool lazyModified = config.fontSubsetLazyLanguages >= 0 && (config.fontSubsetLazyLanguages == 1) != lazyDefault;

            // 4-state 매핑: 0=자동, 1=비활성화, 2=명시 활성(스캔 단독 실행), 3=수동 설정.
            // 우선순위: 비활성화(fontSubset==0) > 수동 override 존재 > 명시 활성(fontSubset==1) > 자동.
            //   fontSubset==1 이면서 수동 override 필드까지 채워져 있으면 "수동 설정"(3)이 실제 상태를
            //   정확히 표현한다 — "명시 활성"은 override 없는 스캔 단독 실행이라는 뜻이기 때문이다.
            int currentIndex;
            if (config.fontSubset == 0)
            {
                currentIndex = 1;
            }
            else if (hasManualOverride)
            {
                currentIndex = 3;
            }
            else if (config.fontSubset == 1)
            {
                currentIndex = 2;
            }
            else
            {
                currentIndex = 0;
            }

            EditorGUILayout.BeginHorizontal();

            bool isModified = config.fontSubset == 0 || config.fontSubset == 1 || hasManualOverride || hasLanguageSelection || lazyModified;
            DrawModifiedIndicator(isModified);

            string label = currentIndex == 0
                ? $"폰트 subset (자동: {(defaultValue ? "활성화" : "비활성화")})"
                : "폰트 subset";
            string[] options = { "자동 (언어 선택 시 실행)", "비활성화", "명시 활성 (스캔 단독 실행)", "수동 설정 (대상·범위 입력 시)" };
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "zero-config: 자동 모드는 동적 텍스트 언어를 하나 이상 선택해야 실행됩니다(선택 = 인지된 활성화). " +
                    "실행되면 크고(≥1MB) 빌드 포함 가능한 폰트를 탐지하고, 프로젝트에 등장하는 문자체계의 " +
                    "유니코드 블록 전체를 보존하도록 subset합니다(동적 텍스트도 □가 되지 않음). " +
                    "명시 활성은 언어 선택 없이도 스캔을 실행하고, 수동 설정은 대상/범위를 직접 지정합니다."),
                currentIndex, options);

            if (newIndex != currentIndex)
            {
                switch (newIndex)
                {
                    case 0: // 자동
                        config.fontSubset = -1;
                        config.fontSubsetTargetPaths = string.Empty;
                        config.fontSubsetUnicodeRanges = string.Empty;
                        break;
                    case 1: // 비활성화
                        config.fontSubset = 0;
                        break;
                    case 2: // 명시 활성(스캔 단독 실행) — 수동 override 는 이 상태의 정의에서 제외.
                        config.fontSubset = 1;
                        config.fontSubsetTargetPaths = string.Empty;
                        config.fontSubsetUnicodeRanges = string.Empty;
                        break;
                    case 3: // 수동 설정 — 대상/범위 필드는 사용자가 아래에서 채운다.
                        // 필드를 채우기 전까지는 다음 repaint에서 "명시 활성"(2)으로 표시된다
                        // (이 인덱스는 필드 기재 여부로 판별되는 파생 상태 — 옵션 라벨에 명시).
                        config.fontSubset = 1;
                        break;
                }

                currentIndex = newIndex;
            }

            if (isModified && DrawResetButton())
            {
                config.fontSubset = -1;
                config.fontSubsetTargetPaths = string.Empty;
                config.fontSubsetUnicodeRanges = string.Empty;
                config.fontSubsetLanguages = string.Empty;
                config.fontSubsetLazyLanguages = -1;
                currentIndex = 0;
            }

            EditorGUILayout.EndHorizontal();

            if (currentIndex == 0)
            {
                EditorGUILayout.HelpBox(
                    "자동 모드: 동적 텍스트 언어를 하나 이상 선택해야 실행됩니다. 실행되면 대상 폰트를 탐지하고, " +
                    "씬/프리팹/asset/스크립트/로컬라이제이션에 등장하는 문자체계의 유니코드 블록 전체를 보존하도록 subset합니다. " +
                    "한자는 KS X 1001 상용 한자(4,888자) + 감지된 한자를 보존하며, ASCII·한글 등 베이스라인은 항상 포함됩니다. " +
                    "빌드 로그에 드롭 리포트가 출력됩니다.",
                    MessageType.Info);
            }
            else if (currentIndex == 2 || currentIndex == 3)
            {
                EditorGUI.indentLevel++;
                config.fontSubsetTargetPaths = EditorGUILayout.TextField(
                    new GUIContent("대상 폰트 경로(쉼표 구분)", "Assets/ 기준의 .ttf/.otf. 비우면 자동 탐지로 폴백됩니다. 예) Assets/Fonts/NotoSansKR.ttf"),
                    config.fontSubsetTargetPaths);

                config.fontSubsetUnicodeRanges = EditorGUILayout.TextField(
                    new GUIContent("보존 유니코드 범위", "쉼표 구분(fontTools 표기). 비우면 Auto 스캔이 범위를 결정합니다. 예) U+0020-007E,U+AC00-D7A3"),
                    config.fontSubsetUnicodeRanges);
                EditorGUI.indentLevel--;

                if (currentIndex == 3)
                {
                    EditorGUILayout.HelpBox(
                        "⚠ 수동 보존 범위를 지정하면 그 범위만 남고 나머지 글자(희귀 한자/이모지/동적 텍스트)는 □로 렌더됩니다. " +
                        "범위를 비우면 Auto 스캔이 등장 문자체계를 보존하므로 더 안전합니다.",
                        MessageType.Warning);
                }
            }

            // ── 동적 텍스트 언어 선택 — 자동·명시 활성·수동 공통(비활성화 모드 제외). 선택 = 인지된 활성화. ──
            if (currentIndex != 1)
            {
                EditorGUILayout.LabelField("동적 텍스트(닉네임·채팅 등)에 나올 수 있는 언어", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                var selectedTags = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (var rawTag in (config.fontSubsetLanguages ?? string.Empty).Split(','))
                {
                    string trimmedTag = rawTag.Trim();
                    if (trimmedTag.Length > 0)
                    {
                        selectedTags.Add(trimmedTag);
                    }
                }

                // ── lazy 확장(실험적) tri-state — LazyEligible 태그가 하나라도 체크돼 있을 때만 노출
                //   (audioStreamTranscode 와 동일 opt-in UI 관용구: 자동은 항상 비활성). 이 값은 아래
                //   체크박스 라벨(lazy 대상 병기)에도 쓰이므로 체크박스 루프보다 앞에서 계산한다.
                bool anyLazyEligibleSelected = false;
                foreach (var tag in selectedTags)
                {
                    if (AITFontSubsetLanguages.TryFindEntry(tag, out var selectedEntry) && selectedEntry.LazyEligible)
                    {
                        anyLazyEligibleSelected = true;
                        break;
                    }
                }

                // lazyDefault 는 메서드 상단(isModified 계산)에서 이미 계산됨 — 재사용.
                bool lazyEnabled = config.fontSubsetLazyLanguages >= 0
                    ? config.fontSubsetLazyLanguages == 1
                    : lazyDefault;

                if (anyLazyEligibleSelected)
                {
                    string lazyAuto = lazyDefault ? "활성화" : "비활성화";
                    string[] lazyOptions = { $"자동 ({lazyAuto})", "비활성화", "활성화" };
                    int lazyIndex = config.fontSubsetLazyLanguages < 0 ? 0 : config.fontSubsetLazyLanguages + 1;
                    int newLazyIndex = EditorGUILayout.Popup(
                        new GUIContent("선택 언어 lazy 확장 (실험적)",
                            "ko/la(항상 보존)를 제외한 선택 언어를 부트 union 대신 lazy 확장으로 분리합니다: " +
                            "빌드 시 언어별 확장 서브셋 TTF → Dynamic TMP_FontAsset → AssetBundle 외부화, " +
                            "런타임에 해당 문자체계 텍스트가 실제로 등장할 때만 다운로드해 TMP 전역 fallback 에 " +
                            "주입합니다. 어떤 단계든 실패한 언어는 안전하게 부트 union 으로 되돌아갑니다 " +
                            "(fallback-to-boot — 1단계 대비 tofu 리스크 증가 없음)."),
                        lazyIndex, lazyOptions);
                    config.fontSubsetLazyLanguages = newLazyIndex == 0 ? -1 : newLazyIndex - 1;
                    lazyEnabled = config.fontSubsetLazyLanguages >= 0
                        ? config.fontSubsetLazyLanguages == 1
                        : lazyDefault;
                }

                bool languageChanged = false;
                foreach (var entry in AITFontSubsetLanguages.Table)
                {
                    if (entry.AlwaysIncluded)
                    {
                        bool prevEnabled = GUI.enabled;
                        GUI.enabled = false;
                        if (!string.IsNullOrWhiteSpace(config.fontSubsetUnicodeRanges))
                        {
                            // 수동 범위(fontSubsetUnicodeRanges) 지정 시에만 스캔/베이스라인이 생략되므로
                            // 그 경우에 한해 "항상 보존"이 거짓 — 체크 해제+비활성으로 정직화.
                            // (대상 폰트만 수동 지정한 경우는 베이스라인이 여전히 union된다.)
                            EditorGUILayout.ToggleLeft($"{entry.Label} (수동 범위에 직접 포함 필요)", false);
                        }
                        else
                        {
                            EditorGUILayout.ToggleLeft($"{entry.Label} (항상 보존)", true);
                        }

                        GUI.enabled = prevEnabled;
                        continue;
                    }

                    string languageLabel;
                    bool isCjkHanBlock = entry.Tag == "ja" || entry.Tag == "zh-Hans" || entry.Tag == "zh-Hant";
                    if (isCjkHanBlock)
                    {
                        // lazy 활성 시 이 언어는 부트 union 에서 완전히 빠지므로 "한자 통블록 — 절감 효과
                        // 감소" 경고는 더 이상 사실이 아니다 — "부트 폰트 비영향"으로 대체.
                        languageLabel = lazyEnabled
                            ? $"{entry.Label} (lazy 확장으로 분리 — 부트 폰트 비영향)"
                            : $"{entry.Label} (한자 통블록 — 절감 효과 감소)";
                    }
                    else if (lazyEnabled && entry.LazyEligible)
                    {
                        languageLabel = $"{entry.Label} (lazy 확장으로 분리)";
                    }
                    else
                    {
                        languageLabel = entry.Label;
                    }

                    bool wasChecked = selectedTags.Contains(entry.Tag);
                    bool isChecked = EditorGUILayout.ToggleLeft(languageLabel, wasChecked);
                    if (isChecked != wasChecked)
                    {
                        languageChanged = true;
                        if (isChecked)
                        {
                            selectedTags.Add(entry.Tag);
                        }
                        else
                        {
                            selectedTags.Remove(entry.Tag);
                        }
                    }
                }

                if (languageChanged)
                {
                    // 테이블 순서 고정 → 결정적 직렬화.
                    var orderedTags = new System.Collections.Generic.List<string>();
                    foreach (var entry in AITFontSubsetLanguages.Table)
                    {
                        if (!entry.AlwaysIncluded && selectedTags.Contains(entry.Tag))
                        {
                            orderedTags.Add(entry.Tag);
                        }
                    }

                    config.fontSubsetLanguages = string.Join(",", orderedTags);
                }

                EditorGUI.indentLevel--;

                if (currentIndex == 0 && AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection(
                        config.fontSubset,
                        config.fontSubsetLanguages,
                        config.fontSubsetUnicodeRanges,
                        config.fontSubsetExtraRanges,
                        config.fontSubsetTargetPaths,
                        config.fontSubsetExcludeTargetPaths))
                {
                    EditorGUILayout.HelpBox(
                        "현재 설정에서는 서브셋이 실행되지 않습니다. 동적 텍스트 언어를 선택하거나, " +
                        "명시 활성 또는 수동 설정으로 전환하세요.",
                        MessageType.Info);
                }
            }

            // ── 안전 필드(additive/exclude) — 자동·명시 활성·수동 공통(비활성화 모드 제외) ──
            if (currentIndex != 1)
            {
                EditorGUI.indentLevel++;
                config.fontSubsetExtraRanges = EditorGUILayout.TextField(
                    new GUIContent("추가 보존 범위(union)",
                        "Auto 스캔/수동 범위에 '추가로' 항상 보존할 유니코드 범위(쉼표 구분, fontTools 표기). override 가 아니라 합집합입니다. " +
                        "외부에서 동적 로드하는 다른 언어를 보강하세요. 예) 일본어 UGC → U+3040-30FF,U+FF66-FF9F"),
                    config.fontSubsetExtraRanges);

                config.fontSubsetExcludeTargetPaths = EditorGUILayout.TextField(
                    new GUIContent("subset 제외 폰트(쉼표 구분)",
                        "이 폰트들은 subset 대상에서 제외합니다(Assets/ 기준 .ttf/.otf). 임의 언어 UGC 를 렌더하는 폰트 보호용. " +
                        "TMP fallback/Dynamic atlas 소스 폰트는 이 목록과 무관하게 자동 제외/경고됩니다."),
                    config.fontSubsetExcludeTargetPaths);
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    "동적 텍스트 안전: 프로젝트에 '등장하는' 문자체계는 블록 전체가 보존되어 □ 가 되지 않습니다. " +
                    "다만 서버/외부에서 '프로젝트에 전혀 없는 다른 언어'를 받아 표시하면 그 문자체계가 subset 에서 빠져 □ 가 될 수 있습니다. " +
                    "그런 경우 '추가 보존 범위'에 해당 범위를 넣거나 해당 폰트를 'subset 제외'에 지정하세요. " +
                    "TMP fallback 소스 폰트는 자동 제외되고, Dynamic atlas 소스 폰트는 빌드 로그에 ⚠ 로 표시됩니다.",
                    MessageType.Info);
            }
        }

        private void DrawMemorySizeSetting()
        {
            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            bool isModified = config.memorySize > 0 && config.memorySize != defaultMemory;

            EditorGUILayout.BeginHorizontal();

            // 하이라이트 표시
            DrawModifiedIndicator(isModified);

            string label = config.memorySize <= 0
                ? $"초기 메모리 크기 (자동: {defaultMemory}MB)"
                : "초기 메모리 크기";

            string[] options = { $"자동 ({defaultMemory}MB)", "256MB", "512MB", "768MB", "1024MB", "1536MB" };
            int currentIndex = GetMemorySizeIndex(config.memorySize, defaultMemory);
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.memorySize = IndexToMemorySize(newIndex);

            if (isModified && DrawResetButton())
            {
                config.memorySize = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawThreadsSupportSetting()
        {
            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            bool isModified = config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.threadsSupport < 0
                ? $"스레딩 지원 (자동: {(defaultThreads ? "활성화" : "비활성화")})"
                : "스레딩 지원";

            string[] options = { $"자동 ({(defaultThreads ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.threadsSupport < 0 ? 0 : config.threadsSupport + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.threadsSupport = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.threadsSupport = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPageCacheSetting()
        {
            bool defaultPageCache = AITDefaultSettings.GetDefaultPageCache();
            bool isModified = config.pageCache >= 0 && (config.pageCache == 1) != defaultPageCache;

            EditorGUILayout.LabelField("WebGL 로딩 — 페이지 캐시(재방문 서빙)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.pageCache < 0
                ? $"페이지 캐시 (자동: {(defaultPageCache ? "활성화" : "비활성화")})"
                : "페이지 캐시";

            string[] options = { $"자동 ({(defaultPageCache ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.pageCache < 0 ? 0 : config.pageCache + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "재방문 시 Build/* 자산을 CacheStorage 에서 직접 서빙합니다(ServiceWorker 불필요). " +
                    "첫 방문(콜드)에는 효과가 없고, 미지원/비보안 환경에서는 자동으로 원래 로드로 무해 통과합니다. " +
                    "기본 자동(활성화). -1=자동, 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.pageCache = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.pageCache = -1;
            }

            EditorGUILayout.EndHorizontal();

            // 활성(자동 또는 명시적 활성화) 시 캐시명 설정 표시
            bool pageCacheActive = config.pageCache < 0
                ? defaultPageCache
                : config.pageCache == 1;

            if (pageCacheActive)
            {
                EditorGUI.indentLevel++;

                // 자동 파생 캐시명 미리보기
                string derivedName = AppsInToss.Editor.Package.AITPageCacheEmitter.ResolveCacheName(config);
                bool isNameAuto = string.IsNullOrEmpty(config.pageCacheName);

                config.pageCacheName = EditorGUILayout.TextField(
                    new GUIContent(
                        isNameAuto ? $"캐시 이름 (자동: {derivedName})" : "캐시 이름",
                        "호스트 백그라운드 pre-fill 페이지와 동일한 이름을 써야 같은 캐시를 공유합니다. " +
                        "비우면 앱 ID(appName)에서 자동 파생합니다(멀티앱 오리진 공유 시 sweep 상호 간섭 방지). " +
                        "런타임 window.__AIT_CACHE_NAME 으로도 오버라이드 가능."),
                    config.pageCacheName
                );

                EditorGUILayout.HelpBox(
                    "재방문 전용 최적화입니다. 콘텐츠-해시 URL(파일명 해싱) 전제이며, " +
                    "부팅 시 현재 빌드에 없는 옛 캐시 엔트리는 자동 정리됩니다. " +
                    "저장 공간 초과 시 무해하게 건너뜁니다.\n" +
                    $"적용될 캐시명: {derivedName}{(isNameAuto ? " (appName 기반 자동 파생)" : "")}",
                    MessageType.Info
                );

                // appName 미설정 + pageCacheName 미설정 시 기본 폴백 경고
                if (isNameAuto && string.IsNullOrWhiteSpace(config.appName))
                {
                    EditorGUILayout.HelpBox(
                        "앱 ID(appName)가 비어 있어 기본 캐시명 'ait-page-cache' 을 사용합니다. " +
                        "멀티앱 오리진 공유 환경에서는 앱 ID를 설정하거나 캐시 이름을 직접 지정하세요.",
                        MessageType.Warning
                    );
                }

                EditorGUI.indentLevel--;
            }

            // warm manifest 산출 (tri-state) — pageCache 실효값이 OFF 이면 회색 비활성.
            // if (pageCacheActive) 블록 바깥에 배치해 항상 렌더링(숨김 없음).
            using (new EditorGUI.DisabledScope(!pageCacheActive))
            {
                DrawWarmManifestSetting();
            }

            // pageCache=OFF + warmManifest 실효값=ON 일 때 경고 HelpBox 표시.
            if (!pageCacheActive)
            {
                bool warmManifestEffective = config.warmManifest < 0
                    ? AITDefaultSettings.GetDefaultWarmManifest()
                    : config.warmManifest == 1;

                if (warmManifestEffective)
                {
                    EditorGUILayout.HelpBox(
                        "페이지 캐시가 비활성이므로 Warm Manifest 도 산출되지 않습니다. " +
                        "페이지 캐시를 활성화하거나 Warm Manifest 를 명시적으로 비활성화하세요.",
                        MessageType.Warning
                    );
                }
            }

            // warm 페이지 산출 (tri-state) — pageCache·warmManifest 실효값이 모두 true 일 때만 편집 가능.
            bool warmManifestEffectiveForPage = config.warmManifest < 0
                ? AITDefaultSettings.GetDefaultWarmManifest()
                : config.warmManifest == 1;
            using (new EditorGUI.DisabledScope(!(pageCacheActive && warmManifestEffectiveForPage)))
            {
                DrawWarmPageSetting();
            }

            // pageCache 또는 warmManifest 실효값=OFF + warmPage 실효값=ON 일 때 경고 HelpBox 표시.
            if (!(pageCacheActive && warmManifestEffectiveForPage))
            {
                bool warmPageEffective = config.warmPage < 0
                    ? AITDefaultSettings.GetDefaultWarmPage()
                    : config.warmPage == 1;

                if (warmPageEffective)
                {
                    EditorGUILayout.HelpBox(
                        "페이지 캐시 또는 Warm Manifest 가 비활성이므로 Warm 페이지도 산출되지 않습니다. " +
                        "페이지 캐시와 Warm Manifest 를 활성화하거나 Warm 페이지를 명시적으로 비활성화하세요.",
                        MessageType.Warning
                    );
                }
            }

            // 네이티브 에셋 소스 우선 (tri-state) — pageCache 실효값이 ON 일 때만 편집 가능(인터셉터 존재 전제).
            // warmManifest/warmPage 와 독립: 인터셉터만 있으면 신호를 노출하므로 pageCache 에만 AND 게이팅.
            using (new EditorGUI.DisabledScope(!pageCacheActive))
            {
                DrawNativeAssetSourceSetting();
            }

            // pageCache 실효값=OFF + nativeAssetSource 실효값=ON 일 때 경고 HelpBox 표시.
            if (!pageCacheActive)
            {
                bool nativeSourceEffective = config.nativeAssetSource < 0
                    ? AITDefaultSettings.GetDefaultNativeAssetSource()
                    : config.nativeAssetSource == 1;

                if (nativeSourceEffective)
                {
                    EditorGUILayout.HelpBox(
                        "페이지 캐시가 비활성이므로 네이티브 에셋 소스도 동작하지 않습니다(인터셉터 없음). " +
                        "페이지 캐시를 활성화하거나 네이티브 에셋 소스를 명시적으로 비활성화하세요.",
                        MessageType.Warning
                    );
                }
            }
        }

        private void DrawWarmManifestSetting()
        {
            bool defaultWarmManifest = AITDefaultSettings.GetDefaultWarmManifest();
            bool isModified = config.warmManifest >= 0 && (config.warmManifest == 1) != defaultWarmManifest;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.warmManifest < 0
                ? $"Warm Manifest 산출 (자동: {(defaultWarmManifest ? "활성화" : "비활성화")})"
                : "Warm Manifest 산출";

            string[] options = { $"자동 ({(defaultWarmManifest ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.warmManifest < 0 ? 0 : config.warmManifest + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "빌드 시 ait-warm-manifest.json 을 web 루트에 산출합니다. " +
                    "호스트(슈퍼앱)가 선다운로드(warm) diff 기준으로 사용합니다. " +
                    "페이지 캐시 실효값이 OFF 이면 회색 비활성화됩니다(AND 게이팅). " +
                    "-1=자동(활성화), 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.warmManifest = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.warmManifest = -1;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawWarmPageSetting()
        {
            bool defaultWarmPage = AITDefaultSettings.GetDefaultWarmPage();
            bool isModified = config.warmPage >= 0 && (config.warmPage == 1) != defaultWarmPage;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.warmPage < 0
                ? $"Warm 페이지 산출 (자동: {(defaultWarmPage ? "활성화" : "비활성화")})"
                : "Warm 페이지 산출";

            string[] options = { $"자동 ({(defaultWarmPage ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.warmPage < 0 ? 0 : config.warmPage + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "빌드 시 self-warming 페이지(ait-warm.html)를 함께 산출합니다. " +
                    "호스트가 숨김 WebView 로 열면 매니페스트 변경분을 미리 캐시에 적재합니다. " +
                    "페이지 캐시·Warm Manifest 실효값이 모두 OFF 이면 회색 비활성화됩니다(AND 게이팅). " +
                    "-1=자동(활성화), 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.warmPage = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.warmPage = -1;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawNativeAssetSourceSetting()
        {
            bool defaultNativeSource = AITDefaultSettings.GetDefaultNativeAssetSource();
            bool isModified = config.nativeAssetSource >= 0 && (config.nativeAssetSource == 1) != defaultNativeSource;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.nativeAssetSource < 0
                ? $"네이티브 에셋 소스 우선 (자동: {(defaultNativeSource ? "활성화" : "비활성화")})"
                : "네이티브 에셋 소스 우선";

            string[] options = { $"자동 ({(defaultNativeSource ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.nativeAssetSource < 0 ? 0 : config.nativeAssetSource + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "페이지 캐시 인터셉터가 Build/* 요청에 대해 호스트 네이티브 프리페치 결과를 우선 사용합니다. " +
                    "호스트가 window.__aitResolveAsset 리졸버를 주입하면 native→CacheStorage→network 순으로 해석합니다. " +
                    "리졸버 미주입 시 신호만 노출되고 기존 캐시-퍼스트 동작으로 자동 폴백됩니다. " +
                    "페이지 캐시 실효값이 OFF 이면 회색 비활성화됩니다(AND 게이팅). " +
                    "-1=자동(활성화), 0=비활성화, 1=활성화."),
                currentIndex,
                options
            );
            config.nativeAssetSource = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.nativeAssetSource = -1;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawTextureStreamingSettings()
        {
            showTextureStreamingSettings = EditorGUILayout.Foldout(showTextureStreamingSettings, "콘텐츠 최적화 — 텍스처 스트리밍", true);

            if (!showTextureStreamingSettings) return;

            EditorGUILayout.BeginVertical("box");

            // tri-state 드롭다운
            DrawTextureStreamingSetting();

            GUILayout.Space(5);

            // 스텁 노출 구간 설명 HelpBox
            EditorGUILayout.HelpBox(
                "스텁 노출 구간: 첫 프레임 직후 ~ 비동기 로드 완료 사이에 단색 스텁이 잠깐 보일 수 있습니다.\n" +
                "항상 원본 텍스처가 보여야 하는 경로는 '제외 폴더'에 지정하세요.\n\n" +
                "외부화 대상: 부팅 씬에 의존하지 않는 대형(기본 512KB 이상) Texture2D.\n" +
                "자동 제외: 부팅 씬 의존 / Resources / SpriteAtlas 패킹 대상 / 동명·동차원 충돌 / linear / NormalMap.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 최소 바이트 설정
            config.textureStreamingMinBytes = EditorGUILayout.IntField(
                new GUIContent("외부화 최소 크기 (바이트)", "이 크기보다 큰 텍스처 소스만 외부화합니다 (기본 524288 = 512KB)."),
                config.textureStreamingMinBytes
            );

            // 대상 폴더
            config.textureStreamingDirs = EditorGUILayout.TextField(
                new GUIContent("대상 폴더 (쉼표 구분)", "비우면 프로젝트 전체. 예) Assets/Art/BG,Assets/Textures"),
                config.textureStreamingDirs
            );

            // 제외 폴더
            config.textureStreamingExcludeDirs = EditorGUILayout.TextField(
                new GUIContent("제외 폴더 (쉼표 구분)", "항상 원본 텍스처를 사용해야 하는 경로. 예) Assets/UI/Always"),
                config.textureStreamingExcludeDirs
            );

            // 최대 동시 스트리밍
            config.textureStreamingMaxConcurrent = EditorGUILayout.IntSlider(
                new GUIContent("최대 동시 스트리밍", "런타임 동시 다운로드/디코드 상한 (기본 3). VRAM/메인스레드 hitch 제한."),
                config.textureStreamingMaxConcurrent, 1, 8
            );

            GUILayout.Space(5);

            // 스트림 사본 다운스케일(lossy, 기본 ON)
            bool dsDefault = AITDefaultSettings.GetDefaultTextureStreamDownscale();
            string dsAutoLabel = dsDefault ? "활성" : "비활성";
            int dsIndex = config.textureStreamDownscale < 0 ? 0 : config.textureStreamDownscale + 1;
            int dsNew = EditorGUILayout.Popup(
                new GUIContent("스트림 다운스케일 (lossy)",
                    "외부화된 스트림 사본(CDN 배포본)을 max-size 캡보다 크면 축소해 CDN 무압축 총량을 실감축합니다. " +
                    "프로젝트 원본은 불변, 스텁은 원본 차원 유지(Sprite rect 정합). 균일 배율이라 스프라이트시트 UV 도 보존. " +
                    "스트림은 비-부팅이라 로딩속도엔 무영향, CDN 캡만 감소. 클램프와 동일 posture 로 기본 활성(CDN 전용·원본 불변이라 더 안전)."),
                dsIndex, new[] { new GUIContent($"자동 ({dsAutoLabel})"), new GUIContent("비활성"), new GUIContent("활성") });
            config.textureStreamDownscale = dsNew == 0 ? -1 : dsNew - 1;

            bool dsEffective = config.textureStreamDownscale >= 0 ? config.textureStreamDownscale == 1 : dsDefault;
            if (dsEffective)
            {
                EditorGUI.indentLevel++;
                if (config.textureStreamDownscale == 1)
                {
                    config.textureStreamDownscaleMaxSize = EditorGUILayout.IntField(
                        new GUIContent("다운스케일 max-size 캡",
                            "이 값보다 큰 스트림 텍스처만 축소(균일 배율). 기본 2048 = HiDPI(DPR2~3) 헤드룸. 16 미만 무시. 예) 1536, 2048, 3072"),
                        config.textureStreamDownscaleMaxSize);
                }
                else
                {
                    // 자동 모드는 SDK 안전 캡 고정(클램프 캡과 동일 규칙 — ResolveDownscaleCap 참조).
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.IntField(
                            new GUIContent("다운스케일 max-size 캡 (자동: SDK 관리)",
                                "자동 모드는 SDK 안전 캡(2048)을 사용합니다. 캡을 직접 튜닝하려면 '활성'으로 전환하세요."),
                            AITDefaultSettings.GetDefaultTextureStreamDownscaleMaxSize());
                    }
                }
                EditorGUILayout.HelpBox(
                    "미니앱은 devicePixelRatio(모바일 2~3)로 렌더하며 고사양 기기엔 native DPR(iPhone Pro=3 등)을 줍니다. " +
                    "2048 은 화면 일부 스프라이트/UI 는 DPR3 에서도 선명하고 full-bleed 배경만 최대폰에서 세로가 살짝 소프트. " +
                    "1024 로 낮추면 DPR2 풀스크린도 뭉개질 수 있어 HiDPI 에 과합니다.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // 스트림 PNG 무손실 재압축(기본 ON)
            bool rcDefault = AITDefaultSettings.GetDefaultTextureStreamRecompress();
            string rcAutoLabel = rcDefault ? "활성" : "비활성";
            int rcIndex = config.textureStreamRecompress < 0 ? 0 : config.textureStreamRecompress + 1;
            int rcNew = EditorGUILayout.Popup(
                new GUIContent("스트림 PNG 재압축 (무손실)",
                    "외부화된 스트림 PNG 사본을 oxipng(WASM)로 무손실 재압축합니다. 픽셀 데이터 불변(필터/deflate 재탐색만) — " +
                    "런타임 LoadImage 결과 동일, 품질 트레이드오프 없음. 다운스케일이 다시 쓴 PNG(실측 −32%)와 " +
                    "원본 소스 PNG(실측 −7~16%)를 함께 눌러 CDN 무압축 총량을 실감축합니다."),
                rcIndex, new[] { new GUIContent($"자동 ({rcAutoLabel})"), new GUIContent("비활성"), new GUIContent("활성") });
            config.textureStreamRecompress = rcNew == 0 ? -1 : rcNew - 1;

            // 불투명 스트림 PNG → JPEG 전환(lossy, 시각 검증 전 기본 OFF)
            bool jtDefault = AITDefaultSettings.GetDefaultTextureStreamJpeg();
            string jtAutoLabel = jtDefault ? "활성" : "비활성";
            int jtIndex = config.textureStreamJpeg < 0 ? 0 : config.textureStreamJpeg + 1;
            int jtNew = EditorGUILayout.Popup(
                new GUIContent("스트림 JPEG 전환 (lossy)",
                    "알파 없는(불투명 RGB) 스트림 PNG 사본을 JPEG 로 전환합니다(불투명 사진류 실측 −77%). " +
                    "프로젝트 원본은 불변(스트림 사본만 교체), 런타임 LoadImage 는 PNG/JPG 를 매직 바이트로 자동 감지. " +
                    "⚠ DCT 아티팩트(플랫 아트 ringing 등) 위험이 있는 lossy 전환이라 시각 검증 전까지 자동 모드는 비활성입니다."),
                jtIndex, new[] { new GUIContent($"자동 ({jtAutoLabel})"), new GUIContent("비활성"), new GUIContent("활성") });
            config.textureStreamJpeg = jtNew == 0 ? -1 : jtNew - 1;

            if (config.textureStreamJpeg == 1)
            {
                EditorGUI.indentLevel++;
                config.textureStreamJpegQuality = EditorGUILayout.IntSlider(
                    new GUIContent("JPEG 품질",
                        "50~100(기본 90). 실측상 q85 의 추가 이득은 q90 대비 ~1.5%p 에 불과해 품질 보수적인 90 을 권장합니다."),
                    config.textureStreamJpegQuality <= 0 ? 90 : config.textureStreamJpegQuality, 50, 100);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTextureStreamingSetting()
        {
            bool defaultEnabled = AITDefaultSettings.GetDefaultTextureStreaming();
            bool isModified = config.textureStreaming >= 0 && (config.textureStreaming == 1) != defaultEnabled;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.textureStreaming < 0
                ? $"텍스처 스트리밍 (자동: {(defaultEnabled ? "활성화" : "비활성화")})"
                : "텍스처 스트리밍";

            string[] options = { $"자동 ({(defaultEnabled ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.textureStreaming < 0 ? 0 : config.textureStreaming + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.textureStreaming = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.textureStreaming = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private int GetMemorySizeIndex(int memorySize, int defaultMemory)
        {
            if (memorySize <= 0) return 0;

            switch (memorySize)
            {
                case 256: return 1;
                case 512: return 2;
                case 768: return 3;
                case 1024: return 4;
                case 1536: return 5;
                default: return 0;
            }
        }

        private int IndexToMemorySize(int index)
        {
            switch (index)
            {
                case 0: return -1;  // 자동
                case 1: return 256;
                case 2: return 512;
                case 3: return 768;
                case 4: return 1024;
                case 5: return 1536;
                default: return -1;
            }
        }

        private void DrawFirstInteractiveLogSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultFirstInteractiveLog();
            bool isModified = config.firstInteractiveLog >= 0 && (config.firstInteractiveLog == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.firstInteractiveLog < 0
                ? $"first-interactive 계측 (자동: {autoLabel})"
                : "first-interactive 계측";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.firstInteractiveLog < 0 ? 0 : config.firstInteractiveLog + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "원래 첫 씬 로드 완료 시점(time-to-original-scene)을 호스트에 전송합니다. " +
                    "픽셀 불변·세션당 1회 단일 이벤트이므로 기본 활성화됩니다."),
                currentIndex, options);
            config.firstInteractiveLog = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.firstInteractiveLog = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayerPrefsPersistenceSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultPlayerPrefsPersistence();
            bool isModified = config.playerPrefsPersistence >= 0 && (config.playerPrefsPersistence == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.playerPrefsPersistence < 0
                ? $"PlayerPrefs 영속화 (자동: {autoLabel})"
                : "PlayerPrefs 영속화";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.playerPrefsPersistence < 0 ? 0 : config.playerPrefsPersistence + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "PlayerPrefs 영속화 (앱인토스 Storage): WebGL의 IndexedDB 영속성이 보장되지 않는 웹뷰 환경에서 " +
                    "PlayerPrefs 데이터를 앱인토스 Storage에 투명하게 백업/복원합니다. 기본 활성화됩니다."),
                currentIndex, options);
            config.playerPrefsPersistence = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.playerPrefsPersistence = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private int CountModifiedWebGLSettings()
        {
            int count = 0;

            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            if (config.memorySize > 0 && config.memorySize != defaultMemory) count++;

            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            if (config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads) count++;

            bool defaultFirstInteractive = AITDefaultSettings.GetDefaultFirstInteractiveLog();
            if (config.firstInteractiveLog >= 0 && (config.firstInteractiveLog == 1) != defaultFirstInteractive) count++;

            // 파일명 해싱: 선언 기본 true. false 로 바뀐 경우만 변경으로 집계(reset 버튼 노출 조건에 포함).
            if (!config.nameFilesAsHashes) count++;
            // 페이지 캐시: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultPageCache = AITDefaultSettings.GetDefaultPageCache();
            if (config.pageCache >= 0 && (config.pageCache == 1) != defaultPageCache) count++;

            // warm manifest: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultWarmManifest = AITDefaultSettings.GetDefaultWarmManifest();
            if (config.warmManifest >= 0 && (config.warmManifest == 1) != defaultWarmManifest) count++;

            // warm 페이지: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultWarmPage = AITDefaultSettings.GetDefaultWarmPage();
            if (config.warmPage >= 0 && (config.warmPage == 1) != defaultWarmPage) count++;

            // 네이티브 에셋 소스: 기본 자동(true). 명시적으로 기본값과 다르게 설정된 경우만 변경으로 집계.
            bool defaultNativeSource = AITDefaultSettings.GetDefaultNativeAssetSource();
            if (config.nativeAssetSource >= 0 && (config.nativeAssetSource == 1) != defaultNativeSource) count++;

            bool defaultStreaming = AITDefaultSettings.GetDefaultAudioStreaming();
            if (config.audioStreaming >= 0 && (config.audioStreaming == 1) != defaultStreaming) count++;

            bool defaultStreamTranscode = AITDefaultSettings.GetDefaultAudioStreamTranscode();
            if (config.audioStreamTranscode >= 0 && (config.audioStreamTranscode == 1) != defaultStreamTranscode) count++;

            bool defaultAudioReencode = AITDefaultSettings.GetDefaultAudioReencode();
            if (config.audioReencode >= 0 && (config.audioReencode == 1) != defaultAudioReencode) count++;

            bool defaultCrunch = AITDefaultSettings.GetDefaultTextureCrunch();
            if (config.textureCrunch >= 0 && (config.textureCrunch == 1) != defaultCrunch) count++;

            bool defaultTextureClamp = AITDefaultSettings.GetDefaultTextureSizeClamp();
            if (config.textureSizeClamp >= 0 && (config.textureSizeClamp == 1) != defaultTextureClamp) count++;
            bool defaultStreamRecompress = AITDefaultSettings.GetDefaultTextureStreamRecompress();
            if (config.textureStreamRecompress >= 0 && (config.textureStreamRecompress == 1) != defaultStreamRecompress) count++;
            bool defaultStreamJpeg = AITDefaultSettings.GetDefaultTextureStreamJpeg();
            if (config.textureStreamJpeg >= 0 && (config.textureStreamJpeg == 1) != defaultStreamJpeg) count++;
            bool defaultAstcBlock = AITDefaultSettings.GetDefaultAstcBlockEscalation();
            if (config.astcBlockEscalation >= 0 && (config.astcBlockEscalation == 1) != defaultAstcBlock) count++;
            bool defaultMeshCompression = AITDefaultSettings.GetDefaultMeshCompression();
            if (config.meshCompression >= 0 && (config.meshCompression == 1) != defaultMeshCompression) count++;
            // 폰트 subset: 비활성(0)·명시 활성(1)이거나 수동 override(target/range/추가범위/제외경로/언어 지정)면 변경으로 집계.
            if (config.fontSubset == 0
                || config.fontSubset == 1
                || !string.IsNullOrEmpty(config.fontSubsetTargetPaths)
                || !string.IsNullOrEmpty(config.fontSubsetUnicodeRanges)
                || !string.IsNullOrEmpty(config.fontSubsetExtraRanges)
                || !string.IsNullOrEmpty(config.fontSubsetExcludeTargetPaths)
                || !string.IsNullOrEmpty(config.fontSubsetLanguages))
            {
                count++;
            }

            bool defaultPlayerPrefsPersistence = AITDefaultSettings.GetDefaultPlayerPrefsPersistence();
            if (config.playerPrefsPersistence >= 0 && (config.playerPrefsPersistence == 1) != defaultPlayerPrefsPersistence) count++;

            // 폰트 subset lazy 언어 확장: audioStreamTranscode 와 동일하게 tri-state 가 선언 기본값과
            // 다를 때만(자동 기준 실효값 비교) 변경으로 집계.
            bool defaultFontSubsetLazy = AITDefaultSettings.GetDefaultFontSubsetLazyLanguages();
            if (config.fontSubsetLazyLanguages >= 0 && (config.fontSubsetLazyLanguages == 1) != defaultFontSubsetLazy) count++;

            return count;
        }

        private void DrawAstcBlockSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultAstcBlockEscalation();
            bool isModified = config.astcBlockEscalation >= 0 && (config.astcBlockEscalation == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — ASTC 블록 에스컬레이션", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.astcBlockEscalation < 0
                ? $"ASTC 블록 에스컬레이션 (자동: {autoLabel})"
                : "ASTC 블록 에스컬레이션";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.astcBlockEscalation < 0 ? 0 : config.astcBlockEscalation + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "ASTC 서브타겟 전용: 텍스처를 더 큰 ASTC 블록으로 reimport 하여 .data 크기를 줄입니다. " +
                    "lossy. 빌드 후 원본 임포트 설정으로 복원합니다."),
                currentIndex,
                options
            );
            config.astcBlockEscalation = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.astcBlockEscalation = -1;
            }

            EditorGUILayout.EndHorizontal();

            bool effectiveEnabled = config.astcBlockEscalation >= 0 ? config.astcBlockEscalation == 1 : defaultValue;
            if (effectiveEnabled)
            {
                EditorGUI.indentLevel++;

                // 블록 크기 팝업
                int[] blockSizes = { 4, 5, 6, 8, 10, 12 };
                string[] blockSizeLabels = { "4x4 (최고화질, 최대용량)", "5x5", "6x6", "8x8", "10x10", "12x12 (최소용량, 최저화질)" };
                int currentBlockIndex = Array.IndexOf(blockSizes, config.astcBlockSize);
                if (currentBlockIndex < 0) currentBlockIndex = 5; // 기본값 12x12
                int newBlockIndex = EditorGUILayout.IntPopup(
                    "블록 크기",
                    currentBlockIndex,
                    blockSizeLabels,
                    new int[] { 0, 1, 2, 3, 4, 5 }
                );
                config.astcBlockSize = blockSizes[newBlockIndex];

                // maxTextureSize 캡
                config.astcBlockMaxSize = EditorGUILayout.IntField(
                    new GUIContent("maxTextureSize 캡", "0=캡 안 함(원본 크기 유지). 양수 입력 시 해당 크기로 제한."),
                    config.astcBlockMaxSize
                );

                // SpriteAtlas 포함
                config.astcBlockAtlas = EditorGUILayout.Toggle(
                    new GUIContent("SpriteAtlas 포함", "SpriteAtlas 의 WebGL 플랫폼 설정도 오버라이드하고 repack합니다."),
                    config.astcBlockAtlas
                );

                // 대상 폴더
                config.astcBlockDirs = EditorGUILayout.TextField(
                    new GUIContent("대상 폴더 (쉼표 구분)", "비우면 Assets 전체. 예: Assets/Textures,Assets/UI"),
                    config.astcBlockDirs
                );

                // 제외 폴더
                config.astcBlockExcludeDirs = EditorGUILayout.TextField(
                    new GUIContent("제외 폴더 (쉼표 구분)", "폰트/SDF/TextMeshPro 는 항상 자동 제외됩니다."),
                    config.astcBlockExcludeDirs
                );

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox(
                "⚠ lossy: 원본보다 화질이 낮아집니다.\n" +
                "ASTC 서브타겟 전용 — DXT(기본) 서브타겟 프로젝트에서는 빌드 시 자동 skip됩니다.\n" +
                "빌드 완료 후 원본 임포트 설정이 자동으로 복원됩니다(비파괴).",
                MessageType.Warning
            );
        }

        private void DrawMeshCompressionSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultMeshCompression();
            bool isModified = config.meshCompression >= 0 && (config.meshCompression == 1) != defaultValue;

            EditorGUILayout.LabelField("콘텐츠 최적화 — Mesh 압축", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.meshCompression < 0
                ? $"Mesh 압축 (자동: {autoLabel})"
                : "Mesh 압축";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.meshCompression < 0 ? 0 : config.meshCompression + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "대상 Mesh(모델 임포트 자산 및 직렬화 Mesh .asset)의 압축 설정을 빌드 시 일시적으로 Medium 으로 올려 " +
                    "정점 데이터(position/normal/uv/tangent)를 양자화합니다. lossy. 빌드 후 원본 압축 설정으로 복원합니다."),
                currentIndex,
                options
            );
            config.meshCompression = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.meshCompression = -1;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "⚠ 손실: 정점 데이터를 양자화합니다. 대형 지형/정밀 지오메트리에서 아티팩트가 보일 수 있으므로 " +
                "켠 뒤 빌드 결과를 반드시 시각 확인하세요. 시각 검증 전까지는 자동 모드가 비활성입니다.\n" +
                "빌드 완료 후 원본 압축 설정이 자동으로 복원됩니다(비파괴).",
                MessageType.Warning
            );
        }

        private void ResetWebGLSettings()
        {
            ResetWebGLOptimizationDefaults(config);
        }

        /// <summary>
        /// 모든 WebGL 최적화 레버를 AITEditorScriptObject 의 선언 기본값으로 되돌린다.
        /// 각 레버는 master 토글뿐 아니라 수치 파라미터·스코프 디렉터리/경로까지 전부 복원해야
        /// "기본값 복원"이 실제로 fresh 설정과 동일해진다(stale 스코프/임계값이 남으면 재활성 시 의도와 다르게 동작).
        /// 신규 레버 필드를 추가하면 이 메서드에도 반드시 그 기본값을 추가할 것
        /// (AITResetWebGLSettingsTests 가 fresh 인스턴스와 비교해 누락을 검출한다).
        /// </summary>
        internal static void ResetWebGLOptimizationDefaults(AITEditorScriptObject config)
        {
            if (config == null) return;

            // 엔진 / 전송
            config.memorySize = -1;
            config.threadsSupport = -1;
            // dataCaching은 UI에서 숨겨진 베타 설정이라 리셋 대상에서도 제외 —
            // 화면에 보이지 않는 값을 복원 버튼이 조용히 덮어쓰면 안 됨
            // (brotliRecompress 도 동일 — UI 미노출 숨김 스파이크 설정이라 리셋 대상에서 제외)
            config.nameFilesAsHashes = true; // [Header "WebGL 최적화 설정"] 파일명 해싱 토글 (선언 기본 true)
            config.firstInteractiveLog = -1;
            config.playerPrefsPersistence = -1;

            // 페이지 캐시 / warm / 네이티브 프리페치
            config.pageCache = -1;
            config.pageCacheName = "";
            config.warmManifest = -1;
            config.warmPage = -1;
            config.nativeAssetSource = -1;

            // 콘텐츠 최적화 — 오디오 스트리밍
            config.audioStreaming = -1;
            config.audioStreamingMinBytes = 262144;
            config.audioStreamingDirs = "";
            config.audioStreamTranscode = -1;
            config.audioStreamTranscodeBitrateKbps = 160;
            config.audioStreamTranscodeMinSourceKbps = 256;

            // 콘텐츠 최적화 — 오디오 재인코딩
            config.audioReencode = -1;
            config.audioReencodeQuality = 0.7f;
            config.audioReencodeMinBytes = 0;
            config.audioReencodeDirs = "";
            config.audioReencodeExcludeDirs = "";

            // 콘텐츠 최적화 — 텍스처 crunch
            config.textureCrunch = -1;
            config.textureCrunchMaxSize = 0;
            config.textureCrunchQuality = 50;
            config.textureCrunchAtlas = true;
            config.textureCrunchAtlasMaxSize = 0;
            config.textureCrunchDirs = "";

            // 콘텐츠 최적화 — 텍스처 크기 클램프
            config.textureSizeClamp = -1;
            config.textureClampMaxSize = 2048;
            config.textureClampMinBytes = 0;
            config.textureClampDirs = "";
            config.textureClampExcludeDirs = "";

            // 콘텐츠 최적화 — ASTC 블록 에스컬레이션
            config.astcBlockEscalation = -1;
            config.astcBlockSize = 12;
            config.astcBlockMaxSize = 0;
            config.astcBlockAtlas = true;
            config.astcBlockDirs = "";
            config.astcBlockExcludeDirs = "";

            // 콘텐츠 최적화 — Mesh 압축
            config.meshCompression = -1;

            // 콘텐츠 최적화 — 폰트 CJK subset
            config.fontSubset = -1;
            config.fontSubsetTargetPaths = string.Empty;
            config.fontSubsetUnicodeRanges = string.Empty;
            config.fontSubsetExtraRanges = string.Empty;
            config.fontSubsetExcludeTargetPaths = string.Empty;
            config.fontSubsetLanguages = string.Empty;
            config.fontSubsetLazyLanguages = -1;

            // 콘텐츠 최적화 — 대형 텍스처 스트리밍
            config.textureStreaming = -1;
            config.textureStreamingMinBytes = 524288;
            config.textureStreamingDirs = "";
            config.textureStreamingExcludeDirs = "";
            config.textureStreamingMaxConcurrent = 3;
            config.textureStreamDownscale = -1;
            config.textureStreamDownscaleMaxSize = 2048;
            config.textureStreamRecompress = -1;
            config.textureStreamJpeg = -1;
            config.textureStreamJpegQuality = 90;

            // 콘텐츠 최적화 — 대형 폰트 deferral
            config.fontStreaming = -1;
            config.fontStreamingTargetPaths = string.Empty;
            config.fontStreamingMaxConcurrent = 2;
        }

    }
}
