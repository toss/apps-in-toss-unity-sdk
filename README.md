# Apps in Toss Unity SDK

Apps in Toss 플랫폼을 위한 Unity SDK입니다. Unity 프로젝트를 Apps in Toss 미니앱으로 변환하고 배포할 수 있습니다.

## 주요 기능

- **환경 최적화**: Apps in Toss 환경에 최적화된 WebGL 빌드
- **자동 변환 및 배포**: Unity 프로젝트를 미니앱으로 자동 변환하고 손쉽게 배포
- **Apps in Toss API**: 결제, 인증, 기기 정보, 위치, 권한 등 모든 Apps in Toss API 지원

Unity 2021.3 이상을 지원하며, Unity 6 이상을 권장합니다.

## 문서

| 문서 | 내용 |
|------|------|
| [시작하기](Documentation~/GettingStarted.md) | 설치, 설치 ref 관리, 설정, 첫 번째 빌드 |
| [API 사용 패턴](Documentation~/APIUsagePatterns.md) | async/await, 에러 처리, Mock 브릿지 |
| [빌드 프로필](Documentation~/BuildProfiles.md) | Dev Server, Production Server, Build & Package, Publish |
| [빌드 커스터마이징](Documentation~/BuildCustomization.md) | 웹 진입점 수정, 외부 라이브러리 추가 |
| [문제 해결](Documentation~/Troubleshooting.md) | 자주 막히는 지점과 해결 방법 |

광고 연동, 로딩 화면 커스터마이징, Sentry 연동, 기여 가이드를 포함한 전체 목록은 [문서 인덱스](Documentation~/README.md)에 있습니다.

## 베타 채널 (파일럿 전용)

`web-framework` 3.0.0 기반 SDK를 미리 테스트하려는 **선택된 파일럿 제휴사**를 위한 옵트인 베타 채널이 있습니다 (`...git#beta`). 일반 서비스 배포에는 stable(`#release/vX.Y.Z`)을 사용하세요. 설치·수동 업데이트·stable 복귀 절차는 [베타 채널 가이드](Documentation~/BetaChannel.md)를 참고하세요.

## perf 베타 채널 (파일럿 전용)

WebGL 콜드 로드 최적화를 미리 테스트하려는 파일럿 제휴사를 위한 옵트인 채널이 있습니다 (`...git#beta-perf`). 일반 서비스 배포에는 stable을 사용하고, 상세 절차는 [perf 베타 채널 가이드](Documentation~/PerfBetaChannel.md)를 참고하세요.
