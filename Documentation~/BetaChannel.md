# 베타 채널

`@apps-in-toss/web-framework` 3.0.0 기반 SDK를 미리 써보는 옵트인 채널입니다. 사전 협의된 파일럿 제휴사에게만 안내됩니다.

> **주의**: 베타 채널은 production-ready가 아닙니다. 3.0.0은 빌드 시스템이 바뀌는 메이저 변경(Vite + Rolldown)입니다. 일반 서비스 배포에는 stable 릴리즈 태그(`#release/vX.Y.Z`)를 사용하세요.

## stable 과 무엇이 다른가

| 항목 | stable | 베타 채널 |
|------|--------|-----------|
| 설치 ref | `#release/vX.Y.Z` (불변 태그) | `#beta` (이동 브랜치) |
| web-framework | 현행 stable 라인 | 3.0.0-beta.x |
| 자동 업데이트 프롬프트 | 표시됨 | 표시 안 됨 (수동 관리) |
| GitHub Release 표시 | Latest | prerelease |
| 권장 용도 | 서비스 배포 | 파일럿 테스트 |

`beta` 브랜치 하나가 항상 최신 베타를 가리키는 **이동 ref**입니다. 새 베타가 나오면 이 브랜치가 force-push로 갱신됩니다.

## 옵트인

`Packages/manifest.json`의 fragment를 `#beta`로 바꿉니다.

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#beta"
  }
}
```

Package Manager의 `Add package from git URL...`에 같은 URL을 넣어도 됩니다. 설치 ref를 다루는 방법 전반은 [시작하기](GettingStarted.md)의 설치 ref 관리 절에 정리되어 있습니다.

특정 베타 스냅샷에 고정하려면 이동 브랜치 대신 스냅샷 태그(`#release/v3.0.0-beta.<해시>`)를 쓰세요. 베타 릴리즈 목록은 [GitHub Releases](https://github.com/toss/apps-in-toss-unity-sdk/releases)에서 `prerelease` 표시로 확인할 수 있습니다.

## 새 베타로 갱신

자동 업데이트 프롬프트가 뜨지 않으므로, 새 베타 안내를 받으면 직접 최신 `beta` HEAD를 다시 당겨와야 합니다. UPM이 커밋 해시를 `packages-lock.json`에 잠가 두기 때문에 Unity를 다시 여는 것만으로는 갱신되지 않습니다.

구체적인 두 가지 방법(패키지 제거 후 재추가 / lock 해제)은 [시작하기](GettingStarted.md)의 설치 ref 관리 절에 있습니다. 베타 채널이라서 다른 것은 없고, 이동 ref를 쓰는 모든 경우에 같은 절차가 적용됩니다.

## stable 로 복귀

fragment를 불변 stable 태그로 되돌립니다.

```json
"im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/vX.Y.Z"
```

stable 태그로 핀하면 자동 업데이터가 다시 해당 ref를 추적합니다. 3.0.0이 정식 stable로 graduation되면 그때는 `#release/v3.0.0` 태그를 쓰면 됩니다.

## 알아둘 점

- **자동 업데이트 없음**: 자동 업데이터는 ref 이름으로 prerelease 채널을 판정하고, 해당하면 프롬프트를 띄우지 않습니다. `AIT` > `Check for Updates...`를 수동 실행하면 베타 채널이라 수동 관리가 필요하다는 안내만 표시됩니다. 판정은 ref 이름만 보므로 `beta`, `rc`, `canary` 같은 토큰이 들어간 ref는 모두 같은 취급을 받습니다.
- **재현 가능한 빌드**: `beta`는 force-push로 갱신되는 이동 ref입니다. 같은 산출물을 다시 만들어야 한다면 스냅샷 태그로 핀하세요.
- **Latest 아님**: 베타 릴리즈는 항상 prerelease로 표시되며 stable의 Latest 표시에 영향을 주지 않습니다.
- **메이저 변경**: 빌드·배포 동작이 stable과 다를 수 있습니다. 파일럿 중 발견한 이슈는 안내받은 채널로 즉시 공유해 주세요.
- **Sentry 분리**: 베타 빌드의 에러는 `environment:beta`로 분리 수집되어 stable triage를 오염시키지 않습니다. 자세한 내용은 [Sentry 연동](SentryIntegration.md)을 참고하세요.

## 관련 문서

- [시작하기](GettingStarted.md) — 설치 ref 관리 공통 절차
- [perf 베타 채널](PerfBetaChannel.md) — 콜드 로드 최적화 파일럿 채널
- [문제 해결](Troubleshooting.md) — 문제가 생겼을 때
