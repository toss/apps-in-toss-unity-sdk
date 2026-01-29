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
| `AIT > Production Server > Start Server` | 프로덕션 환경 로컬 확인 |
| `AIT > Build & Package` | 배포용 패키지 생성 |
| `AIT > Publish` | Apps in Toss에 배포 |

---

## 프로필 매트릭스

각 프로필의 기본 설정입니다:

| 작업 | Mock 브릿지 | 디버그 심볼 | 디버그 콘솔 | LZ4 압축 |
|------|:-----------:|:-----------:|:-----------:|:--------:|
| **Dev Server** | ✅ 활성화 | Embedded | ✅ 활성화 | ✅ 활성화 |
| **Production Server** | ❌ 비활성화 | External | ❌ 비활성화 | ✅ 활성화 |
| **Build & Package** | ❌ 비활성화 | External | ❌ 비활성화 | ✅ 활성화 |
| **Publish** | ❌ 비활성화 | External | ❌ 비활성화 | ✅ 활성화 |

---

## 각 설정의 의미

### Mock 브릿지

네이티브 API가 없는 환경(로컬 브라우저)에서 SDK API를 테스트할 수 있도록 하는 시뮬레이션 레이어입니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 로컬 브라우저에서 테스트 가능 (Mock 데이터 반환) |
| 비활성화 | 실제 Apps in Toss 앱 환경에서만 동작 |

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

### LZ4 압축

Unity WebGL 빌드 시 LZ4 압축을 사용합니다.

| 값 | 설명 |
|-----|------|
| 활성화 | 빌드 속도 향상 (압축률은 낮음) |
| 비활성화 | 기본 압축 사용 |

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

### 사용 예시

#### 로컬 테스트

```bash
AIT_DEBUG_CONSOLE=true ./run-local-tests.sh --all
```

#### Unity 직접 실행

```bash
AIT_DEBUG_CONSOLE=true /Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath ./MyProject \
  -executeMethod AITConvertCore.CommandLineBuild
```

#### GitHub Actions

```yaml
- name: Build with Debug Console
  env:
    AIT_DEBUG_CONSOLE: "true"
  run: |
    unity -executeMethod E2EBuildRunner.CommandLineBuild ...
```

---

## 빌드 로그

빌드 시작 시 적용된 프로필이 Unity Console에 출력됩니다:

```
[AIT] ========================================
[AIT] 빌드 프로필: Dev Server
[AIT] ========================================
[AIT]   Mock 브릿지: 활성화
[AIT]   디버그 심볼: Embedded
[AIT]   디버그 콘솔: 활성화
[AIT]   LZ4 압축: 활성화
[AIT] ========================================
```

이 로그를 통해 현재 빌드에 어떤 설정이 적용되었는지 확인할 수 있습니다.

---

## 다음 단계

- [시작하기](GettingStarted.md) - 설치 및 기본 설정
- [API 사용 패턴](APIUsagePatterns.md) - async/await 패턴, 에러 처리
- [WebGL 커스터마이징](WebGLCustomization.md) - 템플릿 커스터마이징
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
