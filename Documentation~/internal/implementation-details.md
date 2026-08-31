# 구현 지점 색인

"이 동작은 어느 파일에 있나"를 빠르게 찾기 위한 색인입니다. 동작 자체의 설명은 공개 문서에 있고, 여기에는 코드 위치만 둡니다.

> **대상**: SDK 기여자. 빌드가 무엇을 하는지는 [빌드 파이프라인](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-process)을 보세요.

## 빌드

| 관심사 | 구현 지점 |
|--------|-----------|
| 빌드 진입점 | `Editor/AITConvertCore.cs` — `Init`, `DoExport` |
| PlayerSettings 자동 구성과 복원 | `Editor/AITBuildInitializer.cs` |
| 빌드 전 설정 검증 | `Editor/AITBuildValidator.cs` |
| 에러 코드와 사용자 안내 문구 | `Editor/AITExportErrorCatalog.cs` |
| WebGL 산출물 복사 | `Editor/Package/WebGLBuildCopier.cs` |
| PlayerPrefs 영속화 플레이스홀더 치환·실효값 계산 | `Editor/Package/WebGLBuildCopier.cs` — `%AIT_PLAYERPREFS_PERSISTENCE%` 치환, `EffectivePlayerPrefsPersistence()`. 동작 설명은 [PlayerPrefs 영속화](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/playerprefs) |
| granite 실행 | `Editor/Package/GraniteBuildRunner.cs` |
| 플레이스홀더 치환과 사용자 파일 병합 | `Editor/Package/BuildConfigMerger.cs` |
| 설치 스킵 마커 | `Editor/Package/PnpmInstallStateMarker.cs` |
| 도메인 리로드 후 복원 | `Editor/AITBuildSession.cs`, `Editor/AITBuildSessionRecovery.cs` |

## WebGL 템플릿

SDK의 `WebGLTemplates/AITTemplate/`이 원본이고, `Editor/AITTemplateManager.cs`가 프로젝트의 `Assets/WebGLTemplates/`로 복사합니다. `AITBuildInitializer`가 `PlayerSettings.WebGL.template`을 `PROJECT:AITTemplate`으로 지정합니다.

사용자가 편집하는 영역과 마커 계약은 [빌드 커스터마이징](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization)에 있습니다.

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

> **참고**: 필수 입력은 앱 ID 하나입니다. 검증 규칙은 `Editor/AITBuildValidator.cs`가 단일 출처이며, 사용자 관점 설명은 [FAQ](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/faq)에 있습니다.

devtools(`@apps-in-toss/devtools`) 설정은 빌드 프로필이 아니라 `AITConfig.asset`의 `AITDevtoolsSettings` 필드입니다. 활성화 게이트와 Vite 환경 변수 주입은 `Editor/Menu/DevtoolsSupport.cs`(`ShouldEnable`, `AddEnvVars`)가 단일 출처입니다.

## 배포

`Editor/Menu/AITDeployManager.cs`가 `AIT/Deploy for Online Test` / `AIT/Deploy Release Candidate` 두 메뉴를 공통 메서드 `RunDeploy(DeployKind kind)`로 처리합니다. `DeployKind` enum(`Test`/`Production`)이 `cleanBuild` 여부와 `-m/--memo` 접두사(`[Test]`/`[Production]`)를 결정합니다 — 그 외 빌드 프로필(`productionProfile`)과 `ait deploy` 호출 자체는 두 종류가 동일합니다.

`ait deploy`는 kind와 무관하게 항상 콘솔 QR 테스트 환경(`intoss-private://`)에 배포합니다. 실제 심사·출시는 이 CLI가 아니라 배포 완료 후 콘솔 UI에서 이뤄지는 별도 액션입니다.

배포 성공 시 뜨는 `DeploySuccessWindow`는 두 kind 모두 QR + URL을 보여주고, `Production`일 때만 "콘솔 열기" 버튼(콘솔 베이스 URL로 이동 — deploymentId 딥링크는 콘솔 라우트 미확인 상태라 후속 과제)을 추가로 표시합니다.

과거 있었던 `AIT/Production Server`(로컬 서버 + 샌드박스 앱 연동)는 3.0.0부터 샌드박스 앱 연동이 불가능해지면서 제거되었습니다. 로컬 서버는 이제 Local Debug(devtools mock, 내부적으로는 여전히 `ServerType.Dev` 단일 값) 하나뿐이며, `Editor/AITServerStateManager.cs`의 서버 상태 관리도 단일 서버 기준으로 단순화되어 있습니다.

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
| PlayerPrefs 영속화(IDBFS syncfs 미러링) | `WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js`. 동작 설명은 [PlayerPrefs 영속화](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/playerprefs) |
| PlayerPrefs 레거시 origin 마이그레이션 어댑터 | `WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js` — `resolveLegacySource()`, `getPlatformLegacySource()`, `normalizeLegacyCandidates()`, `applyLegacyFiles()`, `tryLegacyImport()`, `__AIT_PP_LEGACY_SOURCE__` 오버라이드 훅, `state.legacyImport`/`legacyBackend`/`legacyBytes`/`legacyMs`. 마이그레이션 창 판정은 `snapshotHasScopedFile()`(매니페스트 부재 + "PlayerPrefs 0건 매니페스트"), 창 유지는 `pushScoped()`의 `remoteHasScoped` 가드(빈 매니페스트 생성 금지), 임포트 크기 상한은 `LEGACY_MAX_BYTES`/`LEGACY_MAX_B64_CHARS`. 심을 위치(앱 디렉터리)는 **추측하지 않는다** — Unity는 IDBFS를 `/idbfs` **자체**에 마운트하므로(`prejs/IdbFs.js`의 `FS.mount(IDBFS, ..., '/idbfs')`) `mount.mountpoint`는 앱 디렉터리가 아니며, `/idbfs/<hash>`는 네이티브가 `main()` 안에서 만든다. `<hash>`는 빌드가 서비스되는 URL에서 유도돼 origin이 바뀌면 값이 달라지므로 옛 경로를 그대로 심어도, 로컬에 남은 유일한 `/idbfs/<hash>` 후보를 현재 앱 디렉터리로 간주해도(옛 `resolveAppDir()`, 삭제됨) 좌초 경로에 심게 된다. 지금은 `tryLegacyImport()`가 후보를 park만 하고(`armAppDirWatch()` → `legacyImport: 'deferred'`), 실제 심기는 관측 시점의 `tryPlantAt()`이 `pickLegacyTarget()`으로 리매핑해 수행한다. 감시 해제는 `disarmAppDirWatch()` 단일 진입점이며 세 곳에서 불린다 — `tryPlantAt()` 진입 즉시(성패 무관 1회성), 훅 예외 시, `pushScoped()` 성공 후 scoped 파일이 실렸을 때. 관측 공급자는 `installNodeOpsHook()` — 마운트 루트 `node_ops`를 **클론**해 `lookup`/`mknod`를 감싼다. `lookup`은 미스에서만 불리므로(`FS.lookupNode`가 nameTable 히트를 먼저 소비 — `library_fs.js:225-244`, `:614-616` / `MEMFS.node_ops.lookup`은 무조건 ENOENT throw — `library_memfs.js:183-185`) 라이브 데이터 덮어쓰기가 구조적으로 불가능하다. 계약 3가지: ① 훅 설치는 `armAppDirWatch()` 안에서만(레거시 소스가 없는 부팅에서는 엔진 객체를 참조 동일성까지 무변경 — 테스트 W10) ② `node_ops`는 절대 in-place 수정 금지(전역 공유 테이블 오염 — W7) ③ `armAppDirWatch()`는 반드시 부트 게이트 `finish()` **이전**(W9). warm boot 대비 depth-1 디렉터리 backfill(디렉터리 단위로 실패 격리 — 한 곳의 실패가 앱 디렉터리 관측을 죽이지 않게, 테스트 W18), 자기 FS 트래픽 재진입 가드(`inSelfFs`), 후보 mode는 `isFileMode()`로 **정규 파일만** 통과(`storeLocalEntry`가 그 외 타입을 `node type not supported`로 거부하는데 그 실패는 심는 시점에야 드러나 관측 기회를 태운다 — W17), 심기 성공 뒤의 뒤처리(로그/승격 push 예약)는 별도 try로 감싸 **노드 반환을 보장**(예외가 새면 호출부 catch가 원본 ENOENT로 위임해 Unity의 다음 `FS.mknod`가 EEXIST로 죽는다 — `library_fs.js:618-634`, W16), 감시 창 상한 `LEGACY_WATCH_MS`(`window.__AIT_PLAYERPREFS.legacyWatchMs`로 조정, 만료 시 `legacyImport: 'expired'`), 심은 경로는 `status().legacyAppDir`로 관측. `REQUIRED_IDBFS_FNS`에는 `lookup`을 **넣지 않는다**(그 셀프체크 실패는 레이어 전체를 vanilla로 떨군다 — 훅 셀프체크 실패는 `skip-no-watcher`로만 후퇴). 참고 실측: `FS.mkdir`는 `node_ops.mkdir`가 아니라 `FS.mknod` → `parent.node_ops.mknod`만 거치고(`library_fs.js:641-648`, `:618-634`), `MEMFS.ops_table.dir.node`에는 `mkdir` 키가 없어 `IdbFs.js:75`의 `mkdir` 오버라이드는 호출부 0건의 죽은 코드다 — 디렉터리 생성 단독으로는 persist도 발생하지 않는다. 플랫폼 seam(`getPlatformLegacySource()`)은 옛 origin 저장소 조회 API 스펙 미확정으로 아직 구현 없음(stub) |

## 관련 문서

- [빌드 파이프라인](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-process) — 빌드 단계와 에러 코드
- [빌드 프로필](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-profiles) — 프로필별 설정과 환경 변수 오버라이드
- [프로젝트 구조](project-structure.md) — 디렉터리 전체 지도
- [기여 가이드](../Contributing.md) — 개발 환경 설정
