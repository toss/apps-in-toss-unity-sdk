# Apps in Toss Unity SDK

Apps in Toss 플랫폼을 위한 Unity SDK입니다. Unity 프로젝트를 Apps in Toss 미니앱으로 변환하고 배포할 수 있습니다.

## 주요 기능

- **환경 최적화**: Apps in Toss 환경에 최적화된 WebGL 빌드
- **자동 변환 및 배포**: Unity 프로젝트를 미니앱으로 자동 변환하고 손쉽게 배포
- **Apps in Toss API**: 결제, 인증, 기기 정보, 위치, 권한 등 모든 Apps in Toss API 지원

Unity 2021.3 이상을 지원하고 Unity 6 LTS 이상을 권장합니다.

집중 관리 버전은 아래 LTS 패치입니다. CI E2E 테스트를 매번 이 버전으로 macOS와 Windows에서 돌리고 문제가 생기면 이 버전 기준으로 먼저 고칩니다. 새 프로젝트라면 (권장)으로 표시한 버전을 쓰세요.

| Unity 버전 | 구분 |
|------|------|
| 6000.3.27f1 | Unity 6.3 LTS (권장) |
| 6000.0.85f1 | Unity 6 LTS (권장) |
| 2022.3.62f3 | Unity 2022 LTS |
| 2021.3.45f2 | Unity 2021 LTS, 최소 지원 버전 |

그 밖에 2021.3 이후의 버전 계열(예: `6000.2.x`, `6000.6.x`)과 위 LTS의 다른 패치(예: `6000.3.x`)도 지원합니다. 이 버전들도 SDK가 컴파일되도록 관리하지만 패치마다 E2E를 돌리지는 않습니다. 앞으로 나올 버전과의 호환을 미리 보려고 최신 Supported 버전(지금은 6000.6.7f1)도 E2E에 함께 넣어 둡니다.

## 문서

전체 문서는 [Apps in Toss 개발자 문서 포털](https://developers-apps-in-toss.toss.im/documentation/unity/overview)에 있습니다.

| 문서 | 내용 |
|------|------|
| [시작하기](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/getting-started) | 설치, 설치 ref 관리, 설정, 첫 번째 빌드 |
| [API 사용 패턴](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/api-usage-patterns) | async/await, 에러 처리, Mock 브릿지 |
| [빌드 프로필](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-profiles) | Local Debug, Advanced > Build & Package, Deploy for Online Test, Deploy Release Candidate |
| [빌드 커스터마이징](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization) | 웹 진입점 수정, 외부 라이브러리 추가 |
| [FAQ](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/faq) | 자주 막히는 지점과 해결 방법 |

광고 연동, 로딩 화면 커스터마이징, Sentry 연동은 포털에서 확인할 수 있고, 기여 가이드는 [저장소 문서](Documentation~/Contributing.md)에 있습니다.

## 베타 채널 (파일럿 전용)

`web-framework` 3.0.0 기반 SDK를 미리 테스트하려는 **선택된 파일럿 제휴사**를 위한 옵트인 베타 채널이 있습니다 (`...git#beta`). 일반 서비스 배포에는 stable(`#release/vX.Y.Z`)을 사용하세요. 설치·수동 업데이트·stable 복귀 절차는 [베타 채널 가이드](Documentation~/BetaChannel.md)를 참고하세요.

## perf 베타 채널 (파일럿 전용)

WebGL 콜드 로드 최적화를 미리 테스트하려는 파일럿 제휴사를 위한 옵트인 채널이 있습니다 (`...git#beta-perf`). 일반 서비스 배포에는 stable을 사용하고, 상세 절차는 [perf 베타 채널 가이드](Documentation~/PerfBetaChannel.md)를 참고하세요.
