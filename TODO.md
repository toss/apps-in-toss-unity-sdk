# TODO: Repository 개선 항목

> 우선순위 P1(높음) ~ P3(낮음).
> 항목은 요지·심볼 포인터로 쓰고 줄번호와 조사 경위는 적지 않는다.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개라 Configuration UI 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`AITEditorScriptObject.dataCaching`)·빌드 적용(`AITBuildInitializer`)은 유지돼 UI 복원만 필요.

## devtools

- **P3 — devtools tunnel(실기기 프리뷰) 재검토**: `AIT_DEVTOOLS_TUNNEL`은 `aitc.dev`+`cloudflared` 의존으로 사람 수동 전용(Editor/CI 미설정). 호스트 운영 안정화 후 Editor 자동 설정·메뉴 노출 재검토.

## Deploy

- **P3 — 배포 URL 추출 로직이 네 벌로 복제돼 있고 자동 검증은 C# 한 벌에만 걸린다**: `ait deploy` 박스 출력의 URL 복원 로직이 `AITDeployManager.ExtractDeployUrl`(C#)과 preview/beta-release/release awk 세 벌에 있다(현재 동작 등가). awk는 shellcheck/actionlint 대상이 아니다. 고치면 awk를 스크립트로 추출, `DeployUrlTests.cs` 입력을 `--validate`에 등록.

- **P3 — 배포 URL 추출의 알려진 한계**: 박스 폭을 정확히 채운 URL 마지막 줄과 래핑 줄을 구분할 수 없다. 선결 조건: `ait deploy` stdout 전체를 한 번 캡처. 피해 범위: PR 코멘트·릴리스 노트 링크(배포 산출물 무관).

- **P3 — Deploy Release Candidate 성공 창의 콘솔 딥링크**: `DeploySuccessWindow`의 "콘솔 열기" 버튼은 콘솔 베이스 URL(`ConsoleBaseUrl`)만 연다. deploymentId 딥링크 라우트 존재 여부가 미확인이라 못 적용. 플랫폼 팀 확인 후 교체.

## 후속 검증

- **P2 — Unity 2021.3 순정 IDBFS 세션 노화 결함 실기기 확인**: E2E CI에서 Unity 2021.3 빌드가 일정 시간 후 순정 IDBFS 저장이 죽는다(ENOENT → `IDBFS.syncfs` 조용히 실패 → reload 시 유실). 레이어 무관은 통제군 검증, Storage는 전 버전 green(9-4는 2021.3 skip 중). `collectScoped`도 함께 죽는 문제는 폴백(`status().collectFallbackCount`)으로 해소. 후속: 실기기 재현 → 재현 시 2021.3 사용자에게 PlayerPrefs 영속화 opt-out 비권장 안내(사용자 허락 후) → 상류 리포트 판단. 절차: `internal/playerprefs-device-verification.md`.

- **P2 — 이전 origin 저장소 조회 수단 확보 시 어댑터 연결**: SDK 3.x로 서빙 origin이 바뀌어 origin 격리된 IDBFS의 이전 PlayerPrefs에 접근 불가. 요구 형태: `/idbfs` DB `FILE_DATA` 덤프(플랫폼에 전달됨). 마이그레이션 검토 중, 방법·일정 미정. mock 주입 seam(`getPlatformLegacySource()` stub) 있어 확정 시 stub만 채움. 심을 위치는 추측 없이 부팅 중 관측 시점에 `pickLegacyTarget()`이 리매핑해 심는다. 상세: `implementation-details.md` "PlayerPrefs 레거시 origin 마이그레이션" 표 행.

  선결 과제: (1) 플랫폼 API가 앱 단위 스코프인지 — 아니면 다른 게임 세이브를 옮길 소지(방어선 `LEGACY_MAX_CANDIDATES`, 걸리면 `skip-ambiguous`로 미심음). (2) 'empty' 응답을 종결로 볼지 — lazy-backfill 가능성으로 지금은 창을 연다.

## 코드 결함

- **P3 — stale 디렉터리에 남은 PlayerPrefs를 `collectScoped`가 매니페스트에 올린다**: 위 마이그레이션은 stale 디렉터리에 남은 PlayerPrefs는 다루지 않는다. `collectScoped()`가 `SCOPE_RE`에 맞는 경로를 전부 긁어 좌초된 값도 매니페스트에 올린다.

- **P3 — `onFlush()`가 IndexedDB 미러를 재시도하지 않는다(비대칭)**: `ait-playerprefs.js`의 `onFlush()`. visibilitychange/pagehide 훅은 `pushScoped(activeMount)`만 재시도, 순정 IDBFS 미러는 안 한다. 타이밍 레이스로 미러 실패가 관측됐다. write 없으면 사본이 빠질 수 있으나("백업의 백업") 다음 write에서 자가 치유, 회귀 아님. 고치면 `callOrig` 호출 한 줄 추가.

- **P3 — `AITEditorScriptObject.IsReadyForDeploy()`가 죽은 코드**: `IsIconUrlValid`/`IsAppNameValid`/`IsVersionValid`를 묶지만 호출되지 않는다(`AITCredentials.IsReadyForDeploy()`는 별개). Configuration 창은 `IsAppNameValid()`를 직접 호출해 게이팅해 기능 공백은 없다. 제거하거나 실제 게이트로 승격할지 결정.

- **P3 — 생성기가 파라미터 이름을 `args_0`/`args_1`로 내보냄**: `.d.ts`의 `@param` 이름을 못 살려 XML 주석·IntelliSense가 무의미해진다. `sdk-runtime-generator~/src/parser/`에서 이름 보존 필요(생성기 이슈, 문서 이슈 아님).

## 의존성

- **P3 — emnapi 2.x 안정판 출시 시 캡 override 해제**: `sdk-runtime-generator~/pnpm-workspace.yaml`의 `'@emnapi/core'`/`'@emnapi/runtime'` `'>=1.11.3 <2'` 캡(#1035)은 프리릴리스 유입을 막는 한시적 조치다(`@napi-rs/wasm-runtime`의 peer가 `^2.0.0-alpha.3` 요구, 2.x 안정판 없어 alpha만 매치. 유입 경로 vite(rolldown) → rolldown·oxc-transform의 optional wasm 바인딩, 미설치). 해제 조건: `npm view @emnapi/core versions`에 2.x 안정판 등장. 확인: `cd sdk-runtime-generator~ && pnpm why @emnapi/core`에 alpha 0건 + `--validate`.

## 파일 위생

- **P3 — 고아 `.meta` 제거**: `Tests~/E2E/tests/package-lock.json.meta`가 추적되고 있으나 짝이 되는 `package-lock.json`은 없다(해당 디렉터리는 `pnpm-lock.yaml`을 쓴다). `Tests~/`는 틸드 폴더라 Unity가 임포트하지 않으므로 이 디렉터리의 `.meta`는 전부 무의미하다. 최소한 고아 하나는 제거.

## 문서

- **P3 — 미문서 public API 약 65개**: 문서 통합 정리에서 의도적으로 범위 제외. API 설명은 상위 `@apps-in-toss/web-framework` JSDoc이 생성기로 XML 주석에 자동 이관되므로, 마크다운 레퍼런스를 두면 상위의 수기 포크가 돼 드리프트한다. 현재 완화책은 API 사용 패턴 문서의 "API 원문은 어디에 있나" 절. 정책 충분성은 사용자 피드백으로 재검토.

- **P3 — `PAYMENT_COMPLETED` 주문 상태 미검증**: 이전 문서가 인용한 값인데 저장소 C# 타입에 없다. 플랫폼 측 상태값 추정, 미확인이라 리라이트에서 제거했다. 실재 확인 후 IAP 문서에 반영.
