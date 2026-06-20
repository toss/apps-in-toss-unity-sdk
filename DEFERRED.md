# DEFERRED — sdk-build-failure 묶음 미처리 항목

본 PR(`fix/auto-resolve-20260620-sdk-build-failure-repro`)에서 처리하지 않은 항목.

---

## APPS-IN-TOSS-UNITY-SDK-12F

**제목**: `UnityError: Library\PackageCache\im.toss.apps-in-toss-unity-sdk@4c5a03dc8a04\Editor\ErrorTracker\AITEditorErrorTracker.cs(1416,47): error CS0103: The name 'AITVersion' does not exist in the current context`

**조사 결과**:
- 에러가 보고된 SDK 커밋 `4c5a03dc8a04`(2026-06-17)에서 해당 코드를 분석함.
- `Runtime/Helpers/AITVersion.cs`는 `PackageAssetPath`, `PackageName` 등 관련 상수를 정의하고 있음.
- `Editor/AppsInTossSDKEditor.asmdef`가 `AppsInToss.Helpers` asmdef(GUID `3ef695279b5a46718ce875b0a45fa890`)를 세 번째 참조로 포함하고 있음.
- `Runtime/Helpers/AppsInToss.Helpers.asmdef.meta`의 GUID가 `3ef695279b5a46718ce875b0a45fa890`로 Editor asmdef 참조와 일치함.
- 따라서 해당 커밋에서 `AITVersion`은 `AITEditorErrorTracker.cs`에서 정상적으로 접근 가능해야 함.

**재현 불가 판단 근거**:
- 현재 main HEAD(`a3594a6`, 2026-06-20) 기준 코드가 올바름.
- CS0103 에러가 보고된 커밋에서도 코드 구조는 동일하게 올바름.
- 가능성 있는 원인: Unity PackageCache 부분 손상(partial cache update) 또는 이전 커밋 빌드 캐시 유지 상태에서 발생한 일시적 오류.

**추가 조사 필요 사항**:
- Sentry에서 해당 이슈의 발생 시점, 발생 Unity 버전, OS 정보 확인.
- 동일 사용자에게서 반복 발생 여부 확인.
- Unity PackageCache 클린 후 재현 시도 가이드라인 필요.

**조치 안 함 이유**: 재현 불가 상태에서 추측 수정 금지 원칙(runbook §절대금지) 준수.
