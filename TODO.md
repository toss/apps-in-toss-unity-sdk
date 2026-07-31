# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).
> 2026-07-08 P2 잔여 항목 완료로 정리 · 2026-07-26 베타 기능 항목 추가 · 2026-07-27 문서 통합 정리에서 발견한 항목 추가.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개 상태라 Configuration UI에서 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`config.dataCaching`)과 빌드 적용 로직은 유지되어 있어 재노출 시 UI 복원만 필요 — `Editor/AITConfigurationWindow.cs:468`, `Editor/AITEditorScriptObject.cs:400` 주석 참조.

## 코드 결함

- **P2 — `AITExportErrorCatalog`의 `INVALID_APP_CONFIG` 안내가 사실과 다름**: 두 곳 모두 존재하지 않는 메뉴와 잘못된 필수 조건을 안내한다. 사용자가 그대로 따라가면 막힌다.
  - `Editor/AITExportErrorCatalog.cs:41` — "Apps in Toss > Build & Deploy Window 열기". 그런 `[MenuItem]`은 없고 실제 메뉴는 `AIT > Configuration`이다.
  - `Editor/AITExportErrorCatalog.cs:42` — "아이콘 URL 입력 (필수)". 아이콘 URL은 선택 항목이다. `Editor/AITConfigurationWindow.cs:134` 주석이 "선택 사항"이라고 명시하고, 입력한 경우에만 `http://`/`https://` 형식을 검사한다.
  - `Editor/AITExportErrorCatalog.cs:150` — "필수 필드(App ID, 아이콘 URL 등)". 같은 오류 반복.
  - 실제 필수 항목은 앱 ID 하나. 공개 문서(`Documentation~/Troubleshooting.md`)는 이미 정정했으므로 콘솔 문구만 맞추면 된다.

- **P3 — 생성기가 파라미터 이름을 `args_0`/`args_1`로 내보냄**: `Runtime/SDK/AIT.Storage.cs:34` 등. 상위 `.d.ts`의 `@param` 이름을 살리지 못해 XML 주석과 IntelliSense가 무의미해진다. `sdk-runtime-generator~/src/parser/`에서 파라미터 이름을 보존하도록 수정 필요. 문서 이슈가 아니라 생성기 이슈.

## 파일 위생

- **P3 — 고아 `.meta` 제거**: `Tests~/E2E/tests/package-lock.json.meta`가 추적되고 있으나 짝이 되는 `package-lock.json`은 없다(해당 디렉터리는 `pnpm-lock.yaml`을 쓴다). `Tests~/`는 틸드 폴더라 Unity가 임포트하지 않으므로 이 디렉터리의 `.meta`는 전부 무의미하다. 최소한 고아 하나는 제거.

## 문서

- **P3 — 미문서 public API 약 65개**: 문서 통합 정리(2026-07)에서 의도적으로 범위 제외. 개별 API 설명은 상위 `@apps-in-toss/web-framework` JSDoc이 생성기를 통해 C# XML 주석으로 자동 이관되므로, 마크다운 레퍼런스를 만들면 상위의 수기 포크가 되어 확정적으로 드리프트한다. 현재는 `Documentation~/APIUsagePatterns.md`의 "API 원문은 어디에 있나" 절이 IntelliSense와 클라이언트 SDK 공식 문서로 안내한다. 이 정책이 충분한지 사용자 피드백으로 재검토.

- **P3 — `PAYMENT_COMPLETED` 주문 상태 미검증**: 이전 `Troubleshooting.md`가 인용하던 값인데 이 저장소의 C# 타입 어디에도 없다. 플랫폼 측 상태값으로 추정되나 확인되지 않아 리라이트에서 제거했다. 실재 여부를 확인하고, 실재한다면 IAP 문서에 정식으로 반영.
