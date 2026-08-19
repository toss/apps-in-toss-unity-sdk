# 빌드 프로필

`AIT` 메뉴의 빌드 진입점마다 어떤 설정이 자동으로 달라지는지, 그리고 그 값을 어떻게 바꾸는지 설명합니다.

## 작업 메뉴

| 메뉴 | 용도 |
|------|------|
| `AIT > Dev Server > Start Server` | 로컬 개발 서버 실행 (devtools Mock SDK + 패널로 브라우저 테스트) |
| `AIT > Build & Package` | 배포용 패키지 생성 |
| `AIT > Deploy (Test)` | 증분 빌드 후 테스트 배포 (`ait deploy`) — QR/URL로 실기기 확인 |
| `AIT > Deploy (Production)` | 클린 빌드 후 테스트 배포 (`ait deploy`) — 배포 후 콘솔에서 심사/출시 신청 |

`ait deploy`는 두 메뉴 모두에서 **항상 콘솔 QR 테스트 환경**(`intoss-private://`)에 배포합니다. 실제 사용자에게 노출되는 출시는 이 CLI 명령이 아니라 배포 후 **콘솔에서 심사를 신청**해야 이뤄집니다. 자세한 흐름은 [시작하기](GettingStarted.md#실기기로-확인하기-deploy-test)를 참고하세요.

## 프로필 매트릭스

| 작업 | 디버그 콘솔 | Development Build | WebGL 압축 | Stripping Level | LZ4 압축 | 디버그 심볼 |
|------|:-----------:|:-----------------:|:----------:|:---------------:|:--------:|:-----------:|
| **Dev Server** | ✅ 활성화 | ✅ 활성화 | Disabled | Minimal | ✅ 활성화 | Embedded |
| **Build & Package** | ❌ 비활성화 | ❌ 비활성화 | 자동 (Brotli) | 자동 (High) | ✅ 활성화 | External |
| **Deploy (Test)** | ❌ 비활성화 | ❌ 비활성화 | Gzip (오버라이드) | Minimal (오버라이드) | ✅ 활성화 | External |
| **Deploy (Production)** | ❌ 비활성화 | ❌ 비활성화 | 자동 (Brotli) | 자동 (High) | ✅ 활성화 | External |

Deploy (Test)는 production 프로필의 복제본에 압축(Gzip)·스트리핑(Minimal) 오버라이드를 적용해 배포 속도를 높입니다(`AITEditorScriptObject.CreateTestDeployProfile`) — Brotli 대비 다운로드 크기가 소폭 늘고 Minimal 스트리핑은 산출물 크기가 커질 수 있으니, 테스트 배포 결과를 성능·크기 측정 기준으로 삼지 마세요. 여기에 더해 증분 빌드 + IL2CPP Debug/OptimizeSize 빠른 빌드(fastBuild)도 함께 적용됩니다(`AITDeployManager.RunDeploy`). Deploy (Production)과 Build & Package는 이런 오버라이드 없이 `config.productionProfile` 원본 그대로 사용합니다. 둘의 나머지 차이는 이 표에 없는 **빌드 범위**(증분 vs 클린)와 **배포 memo 접두사**(`-m "[Test] …"` / `-m "[Production] …"`)입니다 — 콘솔에서 이 접두사로 두 배포를 구분합니다.

> `Mock 브릿지`는 빌드 프로필 항목이 아닙니다. `devtools`(`@apps-in-toss/devtools`)는 별도 절 참고.

## 각 설정의 의미

### devtools

Dev Server 전용으로, **빌드 프로필이 아니라 `AIT > Configuration`의 별도 설정**입니다. 켜져 있으면 브라우저에서 토스 앱 없이 devtools의 Mock SDK로 게임을 돌려볼 수 있고 화면에 상태 조작용 패널이 뜹니다. 빌드 프로필과 달리 빌드 산출물을 바꾸지 않으므로 설정을 바꾼 뒤 **서버 재시작만으로 반영**됩니다. 반환값과 한계는 [API 사용 패턴](APIUsagePatterns.md)의 devtools 절에 정리되어 있습니다.

### Development Build

Unity의 Development Build 옵션입니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 빌드 속도 향상, 디버깅 편의 (Profiler 연결 가능) |
| 비활성화 | 최적화된 릴리즈 빌드 |

### 디버그 심볼

디버그 심볼(소스맵)을 어디에 둘지 결정합니다. Unity 2022.3 이상에서 적용됩니다.

| 값 | 설명 |
|-----|------|
| Embedded | 빌드 파일에 심볼 포함 (파일 크기 증가, 디버깅 용이) |
| External | 별도 파일로 분리 (파일 크기 감소, 배포에 적합) |

### 디버그 콘솔

화면 좌측 하단에 디버그 버튼을 띄웁니다. 눌러서 로그와 [SDK 이벤트](Metrics.md)를 확인할 수 있습니다. 프로덕션 배포 시에는 비활성화하세요.

### WebGL 압축

최종 산출물의 압축 포맷입니다.

| 값 | 설명 |
|-----|------|
| 자동 | Brotli 사용 (기본값) |
| Disabled | 압축 없음 (빌드 속도 우선) |
| Gzip | Gzip 압축 |
| Brotli | Brotli 압축 (최고 압축률) |

Dev Server는 빌드 속도를 위해 Disabled, Deploy (Test)는 배포 가속을 위해 Gzip으로 오버라이드됩니다(Brotli 대비 다운로드 크기 소폭 증가 — 성능·크기 측정 기준으로 쓰지 마세요). 나머지(Build & Package, Deploy (Production))는 자동(Brotli)입니다.

### Stripping Level

사용하지 않는 관리 코드를 제거하는 수준입니다.

| 값 | 설명 |
|-----|------|
| 자동 | High 사용 (기본값) |
| Minimal | 최소한의 코드만 제거 |
| Low | 낮은 수준의 코드 제거 |
| Medium | 중간 수준의 코드 제거 |
| High | 적극적으로 코드 제거 (최소 빌드 크기) |

Dev Server와 Deploy (Test)는 빌드 속도를 위해 Minimal이고(Deploy (Test)는 오버라이드 — High 대비 산출물 크기가 커질 수 있습니다), 나머지(Build & Package, Deploy (Production))는 자동(High)입니다.

> **참고**: Disabled는 WebGL(IL2CPP)에서 지원되지 않아 옵션에서 제외되었으며, 이전 버전에서 Disabled로 저장된 값은 Minimal로 정규화됩니다. 리플렉션으로만 참조되는 타입이 High에서 제거될 수 있으니, 스트리핑 이후 동작이 달라지면 `link.xml`로 보존 대상을 지정하세요.

### LZ4 압축

Unity 빌드 프로세스의 LZ4 압축입니다. 모든 프로필에서 기본 활성화되어 있습니다.

## 프로필 커스터마이징

1. `AIT > Configuration` 메뉴 열기
2. "빌드 프로필" 섹션 확장
3. 원하는 프로필(Dev Server, Production 등) 펼치기
4. 각 옵션의 체크박스 변경
5. 변경 사항은 자동 저장됨

프로필 설정은 `Assets/AppsInToss/Editor/AITConfig.asset`에 저장됩니다.

## 환경 변수 오버라이드

CI/CD나 자동화 스크립트에서 프로필 값을 코드 수정 없이 덮어쓸 수 있습니다. 대부분은 `AITBuildInitializer.ApplyEnvironmentVariableOverrides(profile)`가 프로필 사본에 적용하고, `AIT_IL2CPP_CONFIGURATION`만 `Init` 단계에서 PlayerSettings에 직접 적용합니다.

| 환경 변수 | 설명 | 허용 값 |
|----------|------|-----|
| `AIT_DEBUG_CONSOLE` | 디버그 콘솔 활성화 | `true` / `false` |
| `AIT_COMPRESSION_FORMAT` | 압축 포맷 | `-1` (자동) / `0` (Disabled) / `1` (Gzip) / `2` (Brotli) |
| `AIT_DEVELOPMENT_BUILD` | Development Build | `true` / `false` |
| `AIT_IL2CPP_CONFIGURATION` | IL2CPP 컴파일러 최적화 수준 | `Debug` / `Release` / `Master` |

허용 값을 벗어나면 `Debug.LogWarning`으로 경고하고 그 변수만 무시합니다 — 빌드는 계속됩니다.

`AIT_DEVELOPMENT_BUILD`와 `AIT_IL2CPP_CONFIGURATION`은 둘 다 링크 시간을 줄이지만 서로 다른 레이어입니다. 전자는 Player 옵션이고 후자는 IL2CPP 컴파일러 옵티마이저 레벨이라, CI에서 빌드 시간을 줄이려면 두 개를 함께 지정해야 효과가 큽니다.

```bash
AIT_DEBUG_CONSOLE=true \
AIT_COMPRESSION_FORMAT=0 \
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath ./MyProject
```

SDK가 읽는 환경 변수가 이 넷이 전부는 아닙니다. 패키징 단계의 `AIT_DISABLE_INSTALL_SKIP`은 [빌드 파이프라인](BuildProcess.md)에, Sentry 관련 변수는 [Sentry 연동](SentryIntegration.md)에 있습니다.

## 빌드 로그

빌드 시작 시 적용된 프로필이 Unity Console에 출력됩니다. 어떤 값이 실제로 먹었는지는 이 로그가 최종 확인 수단입니다 — 환경 변수 오버라이드도 여기에 반영됩니다.

```text
[AIT] ========================================
[AIT] 빌드 프로필: Dev Server
[AIT] ========================================
[AIT]   디버그 콘솔: 활성화
[AIT]   Development Build: 활성화
[AIT]   LZ4 압축: 활성화
[AIT]   압축 포맷: Disabled
[AIT]   Stripping Level: Minimal
[AIT]   디버그 심볼: Embedded
[AIT] ========================================
```

압축 포맷은 프로필에 저장된 값 그대로(`-1`이면 `자동`) 찍히고, Stripping Level은 `-1`일 때 실제 적용될 값을 괄호로 함께 표시합니다.

## 관련 문서

- [시작하기](GettingStarted.md) — 설치 및 기본 설정
- [빌드 파이프라인](BuildProcess.md) — 프로필이 적용된 뒤 실제로 일어나는 일
- [빌드 커스터마이징](BuildCustomization.md) — 웹 진입점과 마커 영역
- [API 사용 패턴](APIUsagePatterns.md) — devtools 동작
- [문제 해결](Troubleshooting.md) — 빌드가 막혔을 때
