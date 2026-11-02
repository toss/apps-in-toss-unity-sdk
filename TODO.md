# TODO: Repository 개선 항목

> 우선순위 P1(높음) ~ P3(낮음).
> 항목은 요지·심볼 포인터로 쓰고 줄번호와 조사 경위는 적지 않는다.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개라 Configuration UI 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`AITEditorScriptObject.dataCaching`)·빌드 적용(`AITBuildInitializer`)은 유지돼 UI 복원만 필요.

## devtools

- **P3 — devtools tunnel(실기기 프리뷰) 재검토**: `AIT_DEVTOOLS_TUNNEL`은 `aitc.dev`+`cloudflared` 의존으로 사람 수동 전용(Editor/CI 미설정). 호스트 운영 안정화 후 Editor 자동 설정·메뉴 노출 재검토.

## Deploy

- **P3 — 배포 URL 추출의 알려진 한계**: 박스 폭을 정확히 채운 URL 마지막 줄과 래핑 줄을 구분할 수 없다. 선결 조건: `ait deploy` stdout 전체를 한 번 캡처. 피해 범위: PR 코멘트·릴리스 노트 링크(배포 산출물 무관). 로직은 `.github/scripts/deploy/extract-deploy-url.sh`(테스트: `extract-deploy-url.test.sh`)로 통합돼 있다.

- **P3 — Deploy Release Candidate 성공 창의 콘솔 딥링크**: `DeploySuccessWindow`의 "콘솔 열기" 버튼은 콘솔 베이스 URL(`ConsoleBaseUrl`)만 연다. deploymentId 딥링크 라우트 존재 여부가 미확인이라 못 적용. 플랫폼 팀 확인 후 교체.

## 후속 검증

- **P2 — Unity 2021.3 순정 IDBFS 세션 노화 결함 실기기 확인**: E2E CI에서 Unity 2021.3 빌드가 일정 시간 후 순정 IDBFS 저장이 죽는다(ENOENT → `IDBFS.syncfs` 조용히 실패 → reload 시 유실). 레이어 무관은 통제군 검증, Storage는 전 버전 green(9-4는 2021.3 skip 중). `collectScoped`도 함께 죽는 문제는 폴백(`status().collectFallbackCount`)으로 해소. 후속: 실기기 재현 → 재현 시 2021.3 사용자에게 PlayerPrefs 영속화 opt-out 비권장 안내(사용자 허락 후) → 상류 리포트 판단. 절차: `internal/playerprefs-device-verification.md`.

## 코드 결함

- **P3 — stale 디렉터리에 남은 PlayerPrefs를 `collectScoped`가 매니페스트에 올린다**: 위 마이그레이션은 stale 디렉터리에 남은 PlayerPrefs는 다루지 않는다. `collectScoped()`가 `SCOPE_RE`에 맞는 경로를 전부 긁어 좌초된 값도 매니페스트에 올린다. 현재 앱 디렉터리는 `appDirWatch`가 무장된 이관 창에서만 관측되고(warm boot의 lookup 히트는 훅을 안 지난다) 상시 상태가 없어, 수집 범위를 좁히려면 상시 관측(또는 해시 유도)을 먼저 설계해야 한다. 관측 전에 좁히면 정상 디렉터리까지 빠져 빈 매니페스트가 push될 수 있다.


## 의존성

- **P3 — emnapi 2.x 안정판 출시 시 캡 override 해제**: `sdk-runtime-generator~/pnpm-workspace.yaml`의 `'@emnapi/core'`/`'@emnapi/runtime'` `'>=1.11.3 <2'` 캡(#1035)은 프리릴리스 유입을 막는 한시적 조치다(`@napi-rs/wasm-runtime`의 peer가 `^2.0.0-alpha.3` 요구, 2.x 안정판 없어 alpha만 매치. 유입 경로 vite(rolldown) → rolldown·oxc-transform의 optional wasm 바인딩, 미설치). 해제 조건: `npm view @emnapi/core versions`에 2.x 안정판 등장. 확인: `cd sdk-runtime-generator~ && pnpm why @emnapi/core`에 alpha 0건 + `--validate`.

## 문서

