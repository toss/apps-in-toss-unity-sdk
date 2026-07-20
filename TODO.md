# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).
> 2026-07-08 P2 잔여 항목 완료로 정리 · 2026-07-20 perf 채널 적대적 리뷰 후속 과제 등재 · 2026-07-26 베타 기능 항목 추가 · 2026-07-27 문서 통합 정리에서 발견한 항목 추가 · 2026-08-01 의존성 항목 추가.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개 상태라 Configuration UI에서 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`config.dataCaching`)과 빌드 적용 로직은 유지되어 있어 재노출 시 UI 복원만 필요 — `Editor/AITConfigurationWindow.cs:468`, `Editor/AITEditorScriptObject.cs:400` 주석 참조.

## P3 (낮음)

- **레거시 early-fetch 킥오프 런타임 실행 기반 테스트 보강** — 현재 `AITEarlyFetchScriptTests`는 생성된 JS의 토큰 존재만 `StringAssert`로 검증해, 런타임 동작 회귀(로더 fetch의 pending 합류, `bodyUsed` 응답 재사용 방지 폴백, 저메모리 분기의 실제 fetch 선택, `init.signal` 우회)는 잡지 못한다. Node `vm`/`child_process`로 생성 스크립트를 `fetch`/`caches`/`sessionStorage` mock과 함께 실제 실행해 이 동작들을 assert하는 테스트를 추가하거나, `Tests~/E2E/tests/e2e-ce-serving.test.js`에 `cache: early-kick`/`early-join` 로그 존재 + Build 리소스 단일 다운로드(이중 다운로드 미발생) 검증 케이스를 추가한다. (근거: 2026-07 early-fetch 킥오프 적대적 리뷰 confirmed finding — `Editor/Package/WebGLBuildCopier.cs` `GenerateEarlyFetchScriptLegacyCaching`)

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

- **P3 — 미문서 public API 약 65개**: 문서 통합 정리(2026-07)에서 의도적으로 범위 제외. 개별 API 설명은 상위 `@apps-in-toss/web-framework` JSDoc이 생성기를 통해 C# XML 주석으로 자동 이관되므로, 마크다운 레퍼런스를 만들면 상위의 수기 포크가 되어 확정적으로 드리프트한다. 현재는 `Documentation~/APIUsagePatterns.md`의 "API 원문은 어디에 있나" 절이 IntelliSense와 클라이언트 SDK 공식 문서로 안내한다. 이 정책이 충분한지 사용자 피드백으로 재검토.

- **P3 — `PAYMENT_COMPLETED` 주문 상태 미검증**: 이전 `Troubleshooting.md`가 인용하던 값인데 이 저장소의 C# 타입 어디에도 없다. 플랫폼 측 상태값으로 추정되나 확인되지 않아 리라이트에서 제거했다. 실재 여부를 확인하고, 실재한다면 IAP 문서에 정식으로 반영.
