# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).
> 2026-07-08 P2 잔여 항목 완료로 정리 · 2026-07-26 베타 기능 항목 추가 · 2026-07-27 문서 통합 정리에서 발견한 항목 추가 · 2026-08-01 의존성 항목 추가 · 2026-08-10 Deploy 항목 추가 · 2026-08-21 후속 검증에 옛 origin 조회 API 통합 대기 항목 추가 · 2026-08-25 머지 전 감사에서 나온 배포 URL 추출 항목 추가.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개 상태라 Configuration UI에서 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`config.dataCaching`)과 빌드 적용 로직은 유지되어 있어 재노출 시 UI 복원만 필요 — `Editor/AITConfigurationWindow.cs:468`, `Editor/AITEditorScriptObject.cs:400` 주석 참조.

## devtools

- **P3 — devtools tunnel(실기기 프리뷰) 재검토**: `AIT_DEVTOOLS_TUNNEL`은 `aitc.dev` 호스트 + `cloudflared` 다운로드에 의존해 사람이 수동으로만 켜도록 막아뒀다(Editor/CI는 절대 설정하지 않음). `aitc.dev` 호스트 운영 상황이 안정화되면 Editor 통합(자동 설정, 메뉴 노출) 여부를 재검토.

## Deploy

- **P3 — 배포 URL 추출 로직이 네 벌로 복제돼 있고 자동 검증은 C# 한 벌에만 걸린다**: 박스 래핑된 `ait deploy` 출력에서 URL을 복원하는 같은 로직이 `Editor/Menu/AITDeployManager.cs`의 `ExtractDeployUrl`(C#)과 `.github/workflows/{preview,beta-release,release}.yml`의 awk 블록 세 벌에 있다. 머지 전 감사에서 네 벌을 필드별로 대조해 **현재는 동작 등가**임을 확인했다(문자 클래스, 래핑 판정, `host=` 멱등 검사, `?`/`&` 분기가 1:1. C#은 비매치 줄에서 `break`, awk는 스캔을 계속하지만 `!url` 가드 때문에 두 번째 URL을 잡지 않아 결과가 같다). 문제는 검증 비대칭이다 — `Tests~/E2E/SharedScripts/Editor/EditModeTests/DeployUrlTests.cs`(6건)는 C#만 고정하고, awk 세 벌은 shellcheck/actionlint도 없어 드리프트하면 해당 채널의 배포 링크만 조용히 깨진다. 고친다면 awk를 `scripts/`의 셸 스크립트 한 벌로 추출해 세 워크플로가 호출하게 하고, `DeployUrlTests.cs`와 같은 입력을 먹이는 셸 테스트를 `run-local-tests.sh --validate`에 등록하는 형태.

  같은 감사에서 나온 **알려진 한계**도 함께 기록한다: 래핑 판정은 "매치가 줄 끝에 닿았는가"로만 하므로, URL의 마지막 조각이 박스 폭을 **정확히** 채우면 래핑이 끝났는데도 다음 줄을 이어붙인다. 다음 줄이 공백 없는 순수 ASCII 토큰(`SUCCESS` 등)이면 그대로 URL에 붙어 잘못된 링크가 된다. 하단 테두리·빈 줄·공백 포함 문장은 문자 클래스에 걸리지 않아 안전하고, URL이 줄 끝에 닿지 않는 경우는 `ExtractDeployUrl_UnwrappedUrlFollowedByText_DoesNotSwallowNextLine`이 고정하고 있다. 렌더된 박스 출력만 보고는 "폭을 정확히 채운 마지막 줄"과 "래핑 중인 줄"을 원리적으로 구분할 수 없어 지금은 방어를 넣지 않았다. 선결 조건은 **`ait deploy`의 실제 stdout 전체 포맷을 한 번 캡처하는 것** — 저장소·CI 로그 어디에도 전체 출력이 없어 현재 로직은 관측된 두 스니펫과 추론으로만 고정돼 있다. 캡처하면 박스 내부 폭과 URL 뒤에 오는 줄의 실제 형태가 확정되고, 그때 방어가 필요한지도 함께 결정된다. 피해 범위는 PR 코멘트·릴리스 노트의 링크이며 배포 산출물 자체는 무관하다.

- **P3 — Deploy Release Candidate 성공 창의 콘솔 딥링크**: `Editor/Menu/DeploySuccessWindow.cs`의 "콘솔 열기" 버튼은 현재 콘솔 베이스 URL(`https://apps-in-toss.toss.im/console`)만 여는데, deploymentId로 배포 상세 화면에 바로 이동하는 딥링크 라우트가 있는지 콘솔 라우트가 미확인이라 적용하지 못했다. 플랫폼 팀 확인 후 딥링크로 교체.

## 후속 검증

- **P2 — PlayerPrefs 영속화: 플랫폼 Storage 값 크기 상한 미확인**: 개발자센터 스토리지 문서에 `Storage.setItem` 값 크기 상한이 명시돼 있지 않아, `WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js`는 보수값 512KB(`MAX_MANIFEST_CHARS`) 초과 시 push를 건너뛰고 경고를 남긴다. 실기기(토스 앱 샌드박스)에서 16KB~1MB 구간 setItem/getItem 왕복을 실측해 상한을 확정하고 임계값을 조정할 것. 함께 실측: 백그라운드 전환 시 Unity의 PlayerPrefs 자동 flush 여부와 `visibilitychange` 발화 후 JS 실행 시간 확보 여부(미보장으로 확인되면 C#측 강제 `PlayerPrefs.Save()` 헬퍼를 후속 추가).

- **P2 — Unity 2021.3 순정 IDBFS 세션 노화 결함 실기기 확인**: E2E CI(Chromium, macOS/Windows)에서 Unity 2021.3(Emscripten 2.0.19) 빌드가 세션 시작 약 60초 후부터 순정 IDBFS 저장이 통째로 죽는 현상을 재현(4 run × 2 attempt × 2 OS, 16/16). 시그니처: `IDBFS.getLocalSet`의 MEMFS 트리 순회가 `errno=44`(ENOENT)로 실패 → `IDBFS.syncfs` 양방향 전부 조용히 실패(Unity가 에러를 삼킴) → 이후 저장된 값은 reload 시 유실, `indexedDB.open('/idbfs')` 직접 프로브도 응답 없음. 레이어를 완전히 끈 순정 페이지에서는 노화 후 reload하면 `page.evaluate`가 무기한 hang되는 페이지 wedge까지 관찰됨(run 31577487933, 양 OS). SDK PlayerPrefs 레이어와 무관함은 E2E 9-6 통제군(레이어 완전 비활성)으로 검증하며, 9-4의 IDBFS 폴백 값 단언은 2021.3에서만 skip 처리(`Tests~/E2E/tests/e2e-full-pipeline.test.js`). 같은 조건에서 앱인토스 Storage 경로(9-1/9-2)는 2021.3 포함 전 버전 green — 이 결함이 본 기능의 필요성을 강화한다. 후속: (1) 실기기(토스 앱 WebView) 2021.3 빌드에서 동일 결함 재현 여부 확인, (2) 재현 시 2021.3 사용자에게 PlayerPrefs 영속화 opt-out 비권장 안내 문서화(사용자 허락 후), (3) Unity 상류 리포트 여부 판단.

- **P2 — 이전 origin 저장소 조회 수단 확보 시 어댑터 연결**: SDK 3.x로 오면서 서빙 origin이 변경됐다(플랫폼 공지 기준). 브라우저 저장소는 origin 단위로 격리되므로, 순정 경로의 PlayerPrefs가 놓이는 IDBFS(IndexedDB)는 origin이 바뀌면 이전 데이터에 접근할 수 없다. 플랫폼이 마이그레이션 지원 방안을 검토 중이나 **구체적인 방법과 일정은 미정**이다. 우리가 필요로 하는 형태는 IndexedDB DB명 `/idbfs`, 오브젝트스토어 `FILE_DATA`의 덤프(키=파일 경로, 값=contents/mode/timestamp)이며, 이 요구는 플랫폼 측에 전달돼 있다. 이번 변경으로 레이어에 mock 주입 가능한 seam(`__AIT_PP_LEGACY_SOURCE__` 오버라이드 훅 + `getPlatformLegacySource()` stub)이 들어가 있어, 수단이 확정되면 stub 하나를 채우는 작은 통합만 남는다. **심을 위치 문제는 이 PR에서 해소됐다(2026-08-23).** 심을 경로 `/idbfs/<hash>/PlayerPrefs`의 `<hash>`는 빌드가 서비스되는 URL에서 유도돼 origin이 바뀌면 값이 달라지는데, 이 디렉터리는 Unity 네이티브가 `main()` 안에서 만들기 때문에 populate 시점(= 임포트 시점)에는 아직 없다. 옛 규칙(`resolveAppDir()`)은 로컬 엔트리 목록에서 `/idbfs/<한 세그먼트>` 후보를 찾아 **정확히 1개일 때만** 채택하는 추측이었고, 그 추측은 두 방향으로 모두 나빴다 — 후보 0개(신규 origin)면 이관이 영영 발화하지 않았고(A), 후보 1개가 현재 앱 디렉터리가 아니면(같은 origin에서 서빙 URL만 바뀐 경로 버저닝 `/app/v1` → `/app/v2` 등) 좌초 경로에 심고 그것이 매니페스트로 승격돼 창이 **영구히** 닫혔다(B).

  지금은 심을 위치를 추측하지 않고 **관측**한다. `resolveAppDir()`은 함수째 삭제했고, `tryLegacyImport()`는 후보를 park만 한 뒤(`legacyImport: 'deferred'`) 실제 심기는 `tryPlantAt()`으로 미룬다. 관측 앵커는 **`node_ops.lookup` 미스**다: `FS.lookupNode`(`library_fs.js:225-244`)는 nameTable을 먼저 뒤지고 **미스일 때만** `FS.lookup`(`:614-616`) → `parent.node_ops.lookup`을 부르며 그 반환값을 그대로 노드로 쓴다. `MEMFS.node_ops.lookup`(`library_memfs.js:183-185`)은 무조건 ENOENT를 throw하므로, 이 지점 도달은 곧 "지금 없는 이름을 누군가 찾는다"는 순수 이벤트다. 즉 Unity가 `<appDir>/PlayerPrefs`를 처음 열려는 순간 **엔진이 parent 노드(= 현재 앱 디렉터리)를 직접 건네준다** — 추측이 아니라 통보다. 미스에서만 발화하므로 라이브 데이터를 덮어쓰는 것이 구조적으로 불가능하다는 성질도 따라온다. (이전에 여기 적혀 있던 `node_ops.mkdir` 훅 안은 틀렸다: MEMFS `ops_table.dir.node`에는 `mkdir` 키가 아예 없고 `FS.mkdir`(`library_fs.js:641`)은 `FS.mknod`(`:618`)를 거쳐 `parent.node_ops.mknod`(`:632`)만 부르므로, `prejs/IdbFs.js:75`의 `mkdir` 오버라이드는 호출부 0건의 죽은 코드다. 디렉터리 생성 단독으로는 persist도 발생하지 않는다.)

  **남은 선결 과제 — 플랫폼 API의 스코프 확인.** 훅이 붙으면서 임포트가 "콜드 부트에서도 같은 세션에 반드시 심는다"로 공격적으로 바뀌었다. `pickLegacyTarget()`의 리매핑 규칙은 "경로 정확일치 우선, 없으면 후보가 1개일 때만"이라, 플랫폼 API가 여러 앱/origin의 데이터를 한 덤프에 섞어 주면 **다른 게임의 세이브를 이 게임의 앱 디렉터리로 옮길** 소지가 있다. `getPlatformLegacySource()` stub을 채우기 전에 그 API가 앱 단위로 스코프되어 있는지 반드시 확인할 것. (덤프가 우리가 이해하는 모양이 아닐 때의 방어선: 후보 수 `LEGACY_MAX_CANDIDATES`, 누적 크기 `LEGACY_MAX_B64_CHARS`, 그리고 위 규칙에 걸리면 `legacyImport: 'skip-ambiguous'`로 아무것도 심지 않는다.)

  **선결 과제 2 — 레거시 'empty' 응답의 창 처리 재결정.** 현재 구현은 소스가 존재하는데 빈 응답이면 `legacyChecked`를 기록하지 않는다(재시도군 — 매 부팅 재조회, 예산 ≤1초). 미출시 플랫폼 API의 빈 응답이 lazy-backfill(데이터가 나중에 채워짐)일 가능성을 배제할 수 없어 보수적으로 창을 열어 뒀다. stub을 채우는 PR에서 API 의미론(빈 응답이 종결인지)과 실지연을 확인해, 'empty'를 종결로 기록할지(재조회 비용 제거) 재결정할 것.

  나머지 남는 위험: 이 세션에서 PlayerPrefs를 한 번도 열지 않는 게임은 `LEGACY_WATCH_MS`(20초) 만료 후 `legacyImport: 'expired'`로 포기하고 다음 부팅에 재시도한다(오늘의 skip과 동급). stale 디렉터리에 PlayerPrefs가 **남아 있는** 경우는 이 설계의 대상이 아니다 — `collectScoped()`가 `SCOPE_RE`에 맞는 모든 경로를 긁어 좌초 PlayerPrefs가 매니페스트에 올라가는 별건 결함이다.

  어댑터를 의도적으로 얇게 유지하는 근거: IndexedDB는 웹 표준상 best-effort 저장소라 이미 좌초된 데이터를 구조하는 일의 기대값이 낮고, 가치의 본체는 앞으로의 쓰기를 IndexedDB에서 걷어내는 쪽에 있다.

## 코드 결함

- **P3 — `onFlush()`가 IndexedDB 미러를 재시도하지 않는다(비대칭)**: `WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js:1534-1537`. visibilitychange/pagehide 훅은 `pushScoped(activeMount)`만 다시 부르고 순정 IDBFS 미러(`callOrig(mount, false, ...)`)는 재시도하지 않는다. E2E run 32585243501에서 Windows leg 한정으로 `lastError: "IndexedDB 미러: No such file or directory"`가 관측됐는데(순정 `IDBFS.syncfs` 안의 `getLocalSet`/`loadLocalEntry`에서 나는 러너 부하 타이밍 레이스, 우리 코드가 호출하지도 않는 경로), 그 세션에서 이후 write가 없으면 미러 사본이 빠진 채 끝날 수 있다. 주 경로인 AIT Storage push는 무사하고 다음 write에서 diff가 재계산돼 자가 치유되므로 "백업의 백업"이 빠지는 수준이다. 이 비대칭은 #1066부터 있던 것이고 레이스도 순정 코드라 어느 쪽도 최근 변경의 회귀는 아니다. 고친다면 `onFlush()`에 기존 `callOrig` 헬퍼 호출을 한 줄 더하는 정도.

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
