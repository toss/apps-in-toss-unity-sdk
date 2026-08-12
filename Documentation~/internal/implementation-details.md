# 구현 지점 색인

"이 동작은 어느 파일에 있나"를 빠르게 찾기 위한 색인입니다. 동작 자체의 설명은 공개 문서에 있고, 여기에는 코드 위치만 둡니다.

> **대상**: SDK 기여자. 빌드가 무엇을 하는지는 [빌드 파이프라인](../BuildProcess.md)을 보세요.

## 빌드

| 관심사 | 구현 지점 |
|--------|-----------|
| 빌드 진입점 | `Editor/AITConvertCore.cs` — `Init`, `DoExport` |
| PlayerSettings 자동 구성과 복원 | `Editor/AITBuildInitializer.cs` |
| 빌드 전 설정 검증 | `Editor/AITBuildValidator.cs` |
| 에러 코드와 사용자 안내 문구 | `Editor/AITExportErrorCatalog.cs` |
| WebGL 산출물 복사 | `Editor/Package/WebGLBuildCopier.cs` |
| granite 실행 | `Editor/Package/GraniteBuildRunner.cs` |
| 플레이스홀더 치환과 사용자 파일 병합 | `Editor/Package/BuildConfigMerger.cs` |
| 설치 스킵 마커 | `Editor/Package/PnpmInstallStateMarker.cs` |
| 도메인 리로드 후 복원 | `Editor/AITBuildSession.cs`, `Editor/AITBuildSessionRecovery.cs` |

## WebGL 템플릿

SDK의 `WebGLTemplates/AITTemplate/`이 원본이고, `Editor/AITTemplateManager.cs`가 프로젝트의 `Assets/WebGLTemplates/`로 복사합니다. `AITBuildInitializer`가 `PlayerSettings.WebGL.template`을 `PROJECT:AITTemplate`으로 지정합니다.

사용자가 편집하는 영역과 마커 계약은 [빌드 커스터마이징](../BuildCustomization.md)에 있습니다.

## 내장 Node.js

`Editor/AITNodeJSDownloader.cs`가 단일 출처입니다. 시스템 설치를 쓰지 않고 항상 내장 바이너리를 내려받아 씁니다.

| 항목 | 위치 |
|------|------|
| 버전 | `NODE_VERSION` 상수 |
| SHA256 체크섬 | 같은 파일 상단, 출처 주석 포함 |
| 재시도 횟수 | `MAX_DOWNLOAD_RETRIES` 상수 |
| 설치 경로 | macOS·Linux `~/.ait-unity-sdk/nodejs/v<버전>/<플랫폼>/`, Windows `%LOCALAPPDATA%\ait-unity-sdk\nodejs\v<버전>\<플랫폼>\` |

다운로드 소스는 세 곳을 순서대로 시도합니다.

```text
1. https://nodejs.org           (공식)
2. https://cdn.npmmirror.com
3. https://repo.huaweicloud.com
```

패키지 매니저는 pnpm이고, 버전 핀은 `Editor/AITPackageManagerHelper.cs`의 `PNPM_VERSION`이 단일 출처입니다. 이 값과 세 `package.json`의 `packageManager` 필드는 항상 같아야 합니다 — [기여 가이드](../Contributing.md) 참조.

## 설정 저장소

설정은 `Assets/AppsInToss/Editor/AITConfig.asset`(ScriptableObject)에 저장됩니다. 필드 정의와 기본값은 `Editor/AITEditorScriptObject.cs`, 편집 UI는 `Editor/AITConfigurationWindow.cs`에 있습니다.

배포 자격증명은 별도 에셋(`Assets/AppsInToss/Editor/AITCredentials.asset`)에 분리되어 있고 `Editor/AITGitGuard.cs`가 커밋되지 않도록 감시합니다.

> **참고**: 필수 입력은 앱 ID 하나입니다. 검증 규칙은 `Editor/AITBuildValidator.cs`가 단일 출처이며, 사용자 관점 설명은 [문제 해결](../Troubleshooting.md)에 있습니다.

devtools(`@apps-in-toss/devtools`) 설정은 빌드 프로필이 아니라 `AITConfig.asset`의 `AITDevtoolsSettings` 필드입니다. 활성화 게이트와 Vite 환경 변수 주입은 `Editor/Menu/DevtoolsSupport.cs`(`ShouldEnable`, `AddEnvVars`)가 단일 출처입니다.

## 런타임

| 관심사 | 구현 지점 |
|--------|-----------|
| jslib 브릿지 인프라와 예외 | `Runtime/SDK/AITCore.cs` |
| SDK 이벤트 로깅 | `Runtime/Helpers/AIT.PerformanceLogger.cs` |
| 포그라운드·백그라운드 전환 | `Runtime/Helpers/AIT.VisibilityHelper.cs` |
| 배너 광고 | `Runtime/Helpers/AIT.BannerAd.cs`, `AITBannerAdView.cs` |
| Sentry 태그·컨텍스트 | `Runtime/Sentry/AITSentryIntegration.cs` |
| Sentry 화면·노출·클릭 추적 | `Runtime/Sentry/AITSentryAnalytics.cs` |
| IL2CPP 스트리핑 방지 | `Runtime/Sentry/link.xml` |

## 관련 문서

- [빌드 파이프라인](../BuildProcess.md) — 빌드 단계와 에러 코드
- [빌드 프로필](../BuildProfiles.md) — 프로필별 설정과 환경 변수 오버라이드
- [프로젝트 구조](project-structure.md) — 디렉터리 전체 지도
- [기여 가이드](../Contributing.md) — 개발 환경 설정
