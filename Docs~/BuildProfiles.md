# 빌드 프로필 시스템

이 문서는 Apps in Toss Unity SDK의 빌드 프로필 시스템을 설명합니다.

## 목차

- [개요](#개요)
- [프로필 매트릭스](#프로필-매트릭스)
- [각 설정의 의미](#각-설정의-의미)
- [프로필 커스터마이징](#프로필-커스터마이징)
- [환경 변수 오버라이드](#환경-변수-오버라이드)
- [빌드 로그](#빌드-로그)

---

## 개요

SDK는 각 작업 메뉴별로 다른 빌드 설정(프로필)을 자동 적용합니다. 이를 통해 개발, 테스트, 배포 각 단계에 최적화된 빌드를 생성할 수 있습니다.

### 작업 메뉴

| 메뉴 | 용도 |
|------|------|
| `AIT > Dev Server > Start Server` | 로컬 개발 서버 실행 |
| `AIT > Production Server > Start Server` | 프로덕션 설정으로 로컬 서버 실행 (샌드박스 앱 연동 가능) |
| `AIT > Build & Package` | 배포용 패키지 생성 |
| `AIT > Publish` | Apps in Toss에 배포 |

---

## 프로필 매트릭스

각 프로필의 기본 설정입니다:

| 작업 | Mock 브릿지 | 디버그 콘솔 | Development Build | WebGL 압축 | Stripping Level | LZ4 압축 | 디버그 심볼 |
|------|:-----------:|:-----------:|:-----------------:|:----------:|:---------------:|:--------:|:-----------:|
| **Dev Server** | ✅ 활성화 | ✅ 활성화 | ✅ 활성화 | Disabled | Minimal | ✅ 활성화 | Embedded |
| **Production Server** | ❌ 비활성화 | ❌ 비활성화 | ❌ 비활성화 | 자동 (Brotli) | 자동 (High) | ✅ 활성화 | External |
| **Build & Package** | ❌ 비활성화 | ❌ 비활성화 | ❌ 비활성화 | 자동 (Brotli) | 자동 (High) | ✅ 활성화 | External |
| **Publish** | ❌ 비활성화 | ❌ 비활성화 | ❌ 비활성화 | 자동 (Brotli) | 자동 (High) | ✅ 활성화 | External |

---

## 각 설정의 의미

### Mock 브릿지

네이티브 API가 없는 환경(로컬 브라우저)에서 SDK API를 테스트할 수 있도록 하는 시뮬레이션 레이어입니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 로컬 브라우저에서 테스트 가능 (Mock 데이터 반환) |
| 비활성화 | 실제 Apps in Toss 앱 환경에서만 동작 |

> **권장**: 개발 중에는 활성화, 배포 시에는 비활성화

### Development Build

Unity의 Development Build 옵션을 활성화합니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 빌드 속도 향상, 디버깅 편의 (Profiler 연결 가능) |
| 비활성화 | 최적화된 릴리즈 빌드 |

> **권장**: 개발 중에는 활성화, 배포 시에는 비활성화

### 디버그 심볼

디버그 심볼(소스맵) 포함 방식을 결정합니다.

| 값 | 설명 |
|-----|------|
| Embedded | 빌드 파일에 심볼 포함 (파일 크기 증가, 디버깅 용이) |
| External | 별도 파일로 분리 (파일 크기 감소, 배포에 적합) |

> **권장**: 개발 중에는 Embedded, 배포 시에는 External

### 디버그 콘솔

화면에 디버그 콘솔 UI를 표시합니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 좌측 하단에 디버그 버튼 표시 (메트릭, 로그 확인 가능) |
| 비활성화 | 디버그 UI 숨김 |

> **권장**: 개발/테스트 중에는 활성화, 프로덕션 배포 시에는 비활성화

### WebGL 압축

최종 빌드 결과물의 압축 포맷을 결정합니다.

| 값 | 설명 |
|-----|------|
| 자동 | Brotli 사용 (기본값) |
| Disabled | 압축 없음 (빌드 속도 우선) |
| Gzip | Gzip 압축 |
| Brotli | Brotli 압축 (최고 압축률) |

> **참고**: Dev Server는 빌드 속도를 위해 Disabled, 나머지 프로필은 자동(Brotli)이 기본값입니다.

### Stripping Level

사용하지 않는 코드를 제거하여 빌드 크기를 줄이는 수준을 결정합니다.

| 값 | 설명 |
|-----|------|
| 자동 | High 사용 (기본값) |
| Disabled | 코드 제거 없음 |
| Minimal | 최소한의 코드만 제거 |
| Low | 낮은 수준의 코드 제거 |
| Medium | 중간 수준의 코드 제거 |
| High | 적극적으로 코드 제거 (최소 빌드 크기) |

> **참고**: Dev Server는 빌드 속도를 위해 Minimal, 나머지 프로필은 자동(High)이 기본값입니다.

### LZ4 압축

Unity 빌드 프로세스에서 LZ4 압축을 사용하여 빌드 속도를 향상시킵니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 빌드 속도 향상 |
| 비활성화 | LZ4 미사용 |

> **참고**: 모든 프로필에서 기본적으로 활성화되어 있습니다.

---

## 프로필 커스터마이징

각 프로필의 설정을 개별적으로 변경할 수 있습니다.

### 설정 방법

1. `AIT > Configuration` 메뉴 열기
2. "빌드 프로필" 섹션 확장
3. 원하는 프로필(Dev Server, Production Server 등) 펼치기
4. 각 옵션의 체크박스 변경
5. 변경 사항은 자동 저장됨

### 설정 저장 위치

프로필 설정은 `Assets/AppsInToss/Editor/AITConfig.asset`에 저장됩니다.

---

## 환경 변수 오버라이드

CI/CD 환경이나 자동화 스크립트에서 환경 변수를 통해 빌드 프로필 설정을 오버라이드할 수 있습니다.

### 지원 환경 변수

| 환경 변수 | 설명 | 값 |
|----------|------|-----|
| `AIT_DEBUG_CONSOLE` | 디버그 콘솔 활성화 | `true` / `false` |
| `AIT_COMPRESSION_FORMAT` | 압축 포맷 오버라이드 | `-1` (자동) / `0` (Disabled) / `1` (Gzip) / `2` (Brotli) |

### 사용 예시

Unity Editor를 커맨드라인으로 실행할 때 환경 변수를 설정할 수 있습니다:

```bash
AIT_DEBUG_CONSOLE=true /Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath ./MyProject
```

---

## 빌드 로그

빌드 시작 시 적용된 프로필이 Unity Console에 출력됩니다:

```
[AIT] ========================================
[AIT] 빌드 프로필: Dev Server
[AIT] ========================================
[AIT]   Mock 브릿지: 활성화
[AIT]   디버그 콘솔: 활성화
[AIT]   Development Build: 활성화
[AIT]   LZ4 압축: 활성화
[AIT]   압축 포맷: Disabled
[AIT]   Stripping Level: Minimal
[AIT]   디버그 심볼: Embedded
[AIT] ========================================
```

이 로그를 통해 현재 빌드에 어떤 설정이 적용되었는지 확인할 수 있습니다.

---

## 다음 단계

- [시작하기](GettingStarted.md) - 설치 및 기본 설정
- [API 사용 패턴](APIUsagePatterns.md) - async/await 패턴, 에러 처리
- [빌드 커스터마이징](BuildCustomization.md) - 템플릿 커스터마이징
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
