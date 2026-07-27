# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).
> 2026-07-08 P2 잔여 항목 완료로 정리 · 2026-07-20 perf 채널 적대적 리뷰 후속 과제 등재 · 2026-07-24 P3 early-fetch 런타임 테스트 완료로 정리 · 2026-07-26 베타 기능 항목 추가.

## 베타 기능

- **P3 — 데이터 캐싱 베타 재노출**: 베타 미공개 상태라 Configuration UI에서 숨김 + 자동 기본값 전 버전 비활성화 처리(#1002). 플랫폼(WebView) 캐시 정책 검증(IndexedDB 캐시 무제한 증식 우려 해소) 후 UI 재노출 및 Unity 6+ 기본 활성화 재검토. 저장값(`config.dataCaching`)과 빌드 적용 로직은 유지되어 있어 재노출 시 UI 복원만 필요 — `Editor/AITConfigurationWindow.cs:468`, `Editor/AITEditorScriptObject.cs:400` 주석 참조.
