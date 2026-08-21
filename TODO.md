# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).
> 2026-07-08 P2 잔여 항목 완료로 정리 · 2026-07-26 베타 기능 항목 추가 · 2026-07-27 문서 통합 정리에서 발견한 항목 추가 · 2026-08-01 의존성 항목 추가 · 2026-08-10 Deploy 항목 추가 · 2026-08-21 후속 검증에 옛 origin 조회 API 통합 대기 항목 추가.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개 상태라 Configuration UI에서 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`config.dataCaching`)과 빌드 적용 로직은 유지되어 있어 재노출 시 UI 복원만 필요 — `Editor/AITConfigurationWindow.cs:468`, `Editor/AITEditorScriptObject.cs:400` 주석 참조.

## devtools

- **P3 — devtools tunnel(실기기 프리뷰) 재검토**: `AIT_DEVTOOLS_TUNNEL`은 `aitc.dev` 호스트 + `cloudflared` 다운로드에 의존해 사람이 수동으로만 켜도록 막아뒀다(Editor/CI는 절대 설정하지 않음). `aitc.dev` 호스트 운영 상황이 안정화되면 Editor 통합(자동 설정, 메뉴 노출) 여부를 재검토.

## Deploy

- **P3 — Deploy Release Candidate 성공 창의 콘솔 딥링크**: `Editor/Menu/DeploySuccessWindow.cs`의 "콘솔 열기" 버튼은 현재 콘솔 베이스 URL(`https://apps-in-toss.toss.im/console`)만 여는데, deploymentId로 배포 상세 화면에 바로 이동하는 딥링크 라우트가 있는지 콘솔 라우트가 미확인이라 적용하지 못했다. 플랫폼 팀 확인 후 딥링크로 교체.

## 후속 검증

- **P2 — Unity 2021.3 순정 IDBFS 세션 노화 결함 실기기 확인**: E2E CI(Chromium, macOS/Windows)에서 Unity 2021.3(Emscripten 2.0.19) 빌드가 세션 시작 약 60초 후부터 순정 IDBFS 저장이 통째로 죽는 현상을 재현(4 run × 2 attempt × 2 OS, 16/16). 시그니처: `IDBFS.getLocalSet`의 MEMFS 트리 순회가 `errno=44`(ENOENT)로 실패 → `IDBFS.syncfs` 양방향 전부 조용히 실패(Unity가 에러를 삼킴) → 이후 저장된 값은 reload 시 유실, `indexedDB.open('/idbfs')` 직접 프로브도 응답 없음. 레이어를 완전히 끈 순정 페이지에서는 노화 후 reload하면 `page.evaluate`가 무기한 hang되는 페이지 wedge까지 관찰됨(run 31577487933, 양 OS). SDK PlayerPrefs 레이어와 무관함은 E2E 9-6 통제군(레이어 완전 비활성)으로 검증하며, 9-4의 IDBFS 폴백 값 단언은 2021.3에서만 skip 처리(`Tests~/E2E/tests/e2e-full-pipeline.test.js`). 같은 조건에서 앱인토스 Storage 경로(9-1/9-2)는 2021.3 포함 전 버전 green — 이 결함이 본 기능의 필요성을 강화한다. 후속: (1) 실기기(토스 앱 WebView) 2021.3 빌드에서 동일 결함 재현 여부 확인, (2) 재현 시 2021.3 사용자에게 PlayerPrefs 영속화 opt-out 비권장 안내 문서화(사용자 허락 후), (3) Unity 상류 리포트 여부 판단. (실측 절차: Documentation~/internal/playerprefs-device-verification.md)

- **P2 — 이전 origin 저장소 조회 수단 확보 시 어댑터 연결**: SDK 3.x로 오면서 서빙 origin이 변경됐다(플랫폼 공지 기준). 브라우저 저장소는 origin 단위로 격리되므로, 순정 경로의 PlayerPrefs가 놓이는 IDBFS(IndexedDB)는 origin이 바뀌면 이전 데이터에 접근할 수 없다. 플랫폼이 마이그레이션 지원 방안을 검토 중이나 **구체적인 방법과 일정은 미정**이다. 우리가 필요로 하는 형태는 IndexedDB DB명 `/idbfs`, 오브젝트스토어 `FILE_DATA`의 덤프(키=파일 경로, 값=contents/mode/timestamp)이며, 이 요구는 플랫폼 측에 전달돼 있다. 이번 변경으로 레이어에 mock 주입 가능한 seam(`__AIT_PP_LEGACY_SOURCE__` 오버라이드 훅 + `getPlatformLegacySource()` stub)이 들어가 있어, 수단이 확정되면 stub 하나를 채우는 작은 통합만 남는다. 어댑터를 의도적으로 얇게 유지하는 근거: IndexedDB는 웹 표준상 best-effort 저장소라 이미 좌초된 데이터를 구조하는 일의 기대값이 낮고, 가치의 본체는 앞으로의 쓰기를 IndexedDB에서 걷어내는 쪽에 있다.

## 코드 결함

- **P3 — `AITEditorScriptObject.IsReadyForDeploy()`가 죽은 코드**: `Editor/AITEditorScriptObject.cs:273`. `IsIconUrlValid`/`IsAppNameValid`/`IsVersionValid` 셋을 묶지만 어디서도 호출되지 않는다(`Editor/AITCredentials.cs:82`의 동명 static은 별개 메서드이고 이쪽도 호출처가 없다). Configuration 창은 `IsAppNameValid()`를 직접 호출해 빌드 버튼을 게이팅하므로(`Editor/AITConfigurationWindow.cs:1139`) 기능 공백은 없다. 제거하거나, 빌드 진입 경로의 실제 게이트로 승격할지 결정 필요.

- **P3 — 생성기가 파라미터 이름을 `args_0`/`args_1`로 내보냄**: `Runtime/SDK/AIT.Storage.cs:34` 등. 상위 `.d.ts`의 `@param` 이름을 살리지 못해 XML 주석과 IntelliSense가 무의미해진다. `sdk-runtime-generator~/src/parser/`에서 파라미터 이름을 보존하도록 수정 필요. 문서 이슈가 아니라 생성기 이슈.

## 의존성

- **P3 — minimatch 10.x 이관 시 brace-expansion 취약점 재검토**: Dependabot #109(`WebGLTemplates/AITTemplate/BuildConfig~`)·#110(`sdk-runtime-generator~`)을 2026-08-01에 `tolerable_risk`로 dismiss했다. **dismiss한 알림은 같은 어드바이저리로 재알림이 오지 않으므로 아래 조건이 충족되면 수동으로 reopen해야 한다.**
  - 대상: GHSA-mh99-v99m-4gvg (brace-expansion, 영향 범위 `<=5.0.7`, 유일 패치 `5.0.8`).
  - dismiss 근거: dev 전이 의존이라 SDK/WebGL 산출물에 포함되지 않고, 공격 성립에 로컬 빌드의 glob 패턴 통제가 필요하다.
  - 고칠 수 없었던 이유 두 가지 — (1) `brace-expansion: '>=5.0.8'` override는 5.x가 `"type": "module"` + `exports` 맵으로 재작성돼 `minimatch@3.1.5`/`5.1.9`/`9.0.9`의 CJS interop을 깨뜨린다(브레이스 패턴에서 `TypeError: expand is not a function`, 실측 확인). (2) 취약 버전을 끌어오는 부모 `jest@29.7.0`·`archiver@7.0.1`·`glob@7.2.3`·`test-exclude@6.0.0`은 전부 `@apps-in-toss/web-framework` 픽스처와 granite 툴체인의 전이 의존이라 우리가 bump할 수 없다(직계 devDependency는 `glob: 13.0.6` 하나뿐이고 이미 minimatch 10.x를 쓴다).
  - 재검토 조건: 위 부모들이 minimatch 10.x 계열로 이관되어 `brace-expansion@1.1.16`·`2.1.x`가 락파일에서 사라지는 시점. 확인 방법은 `grep -oE 'brace-expansion@[0-9.]+' <lockfile> | sort -u`.

- **P3 — emnapi 2.x 안정판 출시 시 캡 override 해제**: `sdk-runtime-generator~/pnpm-workspace.yaml`의 `'@emnapi/core': '>=1.11.3 <2'`·`'@emnapi/runtime': '>=1.11.3 <2'`(#1035)는 **프리릴리스가 트리에 들어오는 것을 막기 위한 한시적 조치**다. 안정판이 나오면 걷어내고 상류 선언을 따르는 게 맞다.
  - 배경: `@napi-rs/wasm-runtime@1.2.0`이 peerDependencies로 `^2.0.0-alpha.3`을 명시 요구하는데, 레지스트리에 emnapi 2.x 안정판이 없어 alpha.3가 그 범위의 유일한 매치였다. 유입 경로는 `vite`(rolldown 백엔드) → `rolldown`·`oxc-transform`의 `wasm32-wasi` optional 바인딩.
  - 이 override는 상류 peer 선언을 의도적으로 무시한다. 해당 바인딩이 cpu/os 필터로 실제 설치되지 않는 optional 경로라 성립하는 예외이므로, **오래 유지할수록 위험이 커진다**. 특히 wasm 경로를 실제로 타는 환경이 생기면 검증되지 않은 조합이 된다(#1035 검증은 darwin-arm64에서 수행돼 wasm 바인딩 실행 확인을 하지 못했다).
  - 해제 조건: `npm view @emnapi/core versions`에 프리릴리스가 아닌 2.x가 등장하는 시점. 해제 후 `grep -c 'emnapi' <lockfile>`로 알파 잔존 0건과 `./run-local-tests.sh --validate`를 확인.

## 파일 위생

- **P3 — 고아 `.meta` 제거**: `Tests~/E2E/tests/package-lock.json.meta`가 추적되고 있으나 짝이 되는 `package-lock.json`은 없다(해당 디렉터리는 `pnpm-lock.yaml`을 쓴다). `Tests~/`는 틸드 폴더라 Unity가 임포트하지 않으므로 이 디렉터리의 `.meta`는 전부 무의미하다. 최소한 고아 하나는 제거.

## 문서

- **P3 — 미문서 public API 약 65개**: 문서 통합 정리(2026-07)에서 의도적으로 범위 제외. 개별 API 설명은 상위 `@apps-in-toss/web-framework` JSDoc이 생성기를 통해 C# XML 주석으로 자동 이관되므로, 마크다운 레퍼런스를 만들면 상위의 수기 포크가 되어 확정적으로 드리프트한다. 현재는 [API 사용 패턴](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/api-usage-patterns)의 "API 원문은 어디에 있나" 절이 IntelliSense와 클라이언트 SDK 공식 문서로 안내한다. 이 정책이 충분한지 사용자 피드백으로 재검토.

- **P3 — `PAYMENT_COMPLETED` 주문 상태 미검증**: 이전 `Troubleshooting.md`가 인용하던 값인데 이 저장소의 C# 타입 어디에도 없다. 플랫폼 측 상태값으로 추정되나 확인되지 않아 리라이트에서 제거했다. 실재 여부를 확인하고, 실재한다면 IAP 문서에 정식으로 반영.
