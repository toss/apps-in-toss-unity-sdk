# 문서

Apps in Toss Unity SDK 문서 목록입니다. 하려는 일부터 찾으세요.

## 처음이라면

| 문서 | 내용 |
|------|------|
| [시작하기](GettingStarted.md) | 설치, 설치 ref 관리, 설정, 첫 번째 빌드 |
| [API 사용 패턴](APIUsagePatterns.md) | async/await, 에러 처리, Mock 브릿지 |
| [문제 해결](Troubleshooting.md) | 자주 막히는 지점과 해결 방법 |

## 기능 붙이기

| 문서 | 내용 |
|------|------|
| [광고 연동](Advertising.md) | 전면·배너·AdMob 광고, `adGroupId` 사용 |
| [SDK 이벤트 로깅](Metrics.md) | SDK가 자동으로 남기는 이벤트와 파라미터 |
| [Sentry 연동](SentryIntegration.md) | 런타임 에러 수집, AIT 컨텍스트 자동 주입 |

## 빌드 다루기

| 문서 | 내용 |
|------|------|
| [빌드 프로필](BuildProfiles.md) | Dev Server, Production Server, Build & Package, Publish |
| [빌드 커스터마이징](BuildCustomization.md) | 웹 진입점 수정, 외부 라이브러리 추가, 마커 영역 |
| [로딩 화면 커스터마이징](LoadingScreenCustomization.md) | 로딩 화면 교체와 `AITLoading` API |
| [빌드 파이프라인](BuildProcess.md) | Unity WebGL 빌드부터 패키징까지의 내부 동작 |
| [수동 연동](ManualIntegration.md) | 권장하지 않는 예외 경로 — SDK 없이 수동으로 WebGL 빌드까지만 만드는 방법 (완결된 배포 절차는 제공하지 않음) |

## 파일럿 채널

사전 협의된 파일럿 제휴사 전용입니다. 일반 서비스 배포에는 stable 릴리즈 태그를 사용하세요.

| 문서 | 내용 |
|------|------|
| [베타 채널](BetaChannel.md) | `web-framework` 메이저 업그레이드 옵트인 |
| [perf 베타 채널](PerfBetaChannel.md) | WebGL 콜드 로드 최적화 레버 옵트인 |

## 기여하기

| 문서 | 내용 |
|------|------|
| [기여 가이드](Contributing.md) | 개발 환경 설정, `.meta` 규칙, 커밋·PR 규칙 |

`internal/`은 이 저장소를 운영하는 데 쓰는 런북입니다. SDK를 사용하는 데는 필요하지 않습니다.

| 문서 | 내용 |
|------|------|
| [프로젝트 구조](internal/project-structure.md) | 디렉터리 지도 |
| [구현 지점 색인](internal/implementation-details.md) | "이 동작은 어느 파일에 있나" |
| [SDK 런타임 생성기](internal/sdk-generator.md) | `Runtime/SDK/`가 만들어지는 방식 |
| [테스트 전략](internal/testing.md) | 3-Level 구조와 CI 실행 |
| [GitHub Actions 워크플로](internal/github-actions.md) | 워크플로 목록과 트리거 |
| [Sentry 알려진 이슈](internal/sentry-known-issues.md) | 무시 가능 이슈와 노이즈 차단 |
| [빌드 중 도메인 리로드 수동 재현](internal/build-session-recovery.md) | 자동화할 수 없는 검증 절차 |

## API 변경 이력

SDK가 노출하는 C# 표면이 `@apps-in-toss/web-framework` 버전에 따라 어떻게 달라지는지 담은 리포트입니다. `main`에 푸시될 때마다 CI가 재생성해 [changelog/index.html](changelog/index.html)에 커밋합니다.

Unity SDK 패키지 버전은 `SDK Update` 워크플로가 `@apps-in-toss/web-framework` 버전을 그대로 따라 올리므로 두 번호는 동일합니다(예: 패키지 `3.0.1` = web-framework `3.0.1` = 태그 `release/v3.0.1`). 릴리즈 노트와 설치 태그 목록은 [GitHub Releases](https://github.com/toss/apps-in-toss-unity-sdk/releases)에서 확인하세요.
