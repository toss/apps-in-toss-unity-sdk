# 주요 구현 세부사항

## 개발 명령어

SDK는 Unity 패키지입니다 - 전통적인 빌드/테스트 명령어가 없습니다. 개발은 Unity Editor 내에서 진행:

1. **Unity에서 열기**: Package Manager를 통해 Unity 프로젝트에 패키지 로드
2. **SDK 설정**: Unity 메뉴 `AIT > Configuration`
3. **빌드 실행**: Unity 메뉴 `AIT > Build & Package`

## 빌드 파이프라인 테스트

Unity 없이 빌드 파이프라인 테스트:

```bash
# 빌드 출력으로 이동
cd ait-build/

# 의존성 설치 (최초 1회만)
npm install

# 개발 서버 실행
npm run dev

# 프로덕션 빌드
npm run build

# Apps in Toss에 배포
npm run deploy
```

## WebGL 템플릿 시스템

Unity WebGL 템플릿은 `WebGLTemplates/AITTemplate/`에 있습니다. 템플릿은 자동으로:
1. 최초 사용 시 Unity 프로젝트의 `Assets/WebGLTemplates/`로 복사
2. PlayerSettings에서 `PROJECT:AITTemplate`으로 선택
3. WebGL 빌드 시 Apps in Toss 브릿지 코드 주입에 사용

## 빌드 설정 자동 구성

`AITConvertCore.Init()`이 최적 설정을 자동 구성 (내부적으로 AITBuildInitializer에 위임):
- **압축**: 비활성화 (Apps in Toss 요구사항)
- **스레딩**: 비활성화 (모바일 브라우저 호환성)
- **메모리**: Unity 2021.3은 256MB, 2022.3은 512MB, 6000.0은 1024MB, 6000.2+는 1536MB
- **데이터 캐싱**: 비활성화
- **링커 타겟**: Wasm

## 내장 Node.js 시스템

SDK는 자동 다운로드 기능이 있는 내장 Node.js 사용:
1. 내장 Node.js를 강제 사용 (시스템 설치 무시)
2. `~/.ait-unity-sdk/nodejs/v{VERSION}/{platform}/`에 자동 다운로드
3. SHA256 체크섬 검증 후 설치

**다운로드 소스 (폴백 순서):**
1. https://nodejs.org (공식)
2. https://cdn.npmmirror.com
3. https://repo.huaweicloud.com

**Node.js 버전**: v24.13.0 (체크섬은 `Editor/AITNodeJSDownloader.cs`에 있음)

## 설정 저장소

설정은 `Assets/AppsInToss/Editor/AITConfig.asset` (ScriptableObject)에 저장:
- 앱 메타데이터 (이름, 버전, 설명, 아이콘 URL)
- 빌드 설정 (프로덕션 모드, 최적화 플래그)
- 브랜딩 (기본 색상, 아이콘 URL)
- 배포 키 (`ait deploy`용)

**중요**: 아이콘 URL (`config.iconUrl`)은 빌드에 필수입니다.
