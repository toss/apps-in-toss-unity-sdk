# SDK 런타임 생성기

`Runtime/SDK/`의 C# 코드와 jslib 브릿지가 어떻게 만들어지는지에 대한 내부 메모입니다.

> **대상**: SDK 기여자. 생성기를 돌리는 명령과 검증 순서는 [기여 가이드](../Contributing.md)에 있습니다.

## 입력과 출력

입력은 `@apps-in-toss/web-framework`가 배포하는 `.d.ts`입니다. 생성기는 이를 파싱해 카테고리별 partial class와 jslib을 만듭니다.

| 산출물 | 위치 |
|--------|------|
| API partial class | `Runtime/SDK/AIT.<카테고리>.cs` |
| 타입 정의 | `Runtime/SDK/AIT.Types.<카테고리>.cs` |
| jslib 브릿지 | `Runtime/SDK/Plugins/` |
| 인프라 | `Runtime/SDK/AITCore.cs` |

카테고리 목록은 `sdk-runtime-generator~/src/categories.ts`가 단일 출처입니다.

## 타입 매핑

`sdk-runtime-generator~/src/validators/types.ts`의 `TYPE_MAPPING`이 단일 출처입니다.

| TypeScript | C# |
|------------|-----|
| `string` | `string` |
| `number` | `double` |
| `boolean` | `bool` |
| `void` | `void` |
| `any` | `void` |
| `unknown` | `object` |
| `symbol` | `object` |
| `Date` | `DateTime` |
| `ArrayBuffer`, `Uint8Array` | `byte[]` |
| `Error` | `Exception` |
| `T \| U` | discriminated union 클래스 |

`Promise<T>`는 Unity 버전에 따라 갈립니다. `UNITY_6000_0_OR_NEWER`에서는 `Awaitable<T>`, 그 이하에서는 `Task<T>`로 생성되고 한 파일 안에 `#if`로 두 벌이 들어갑니다.

## JSDoc 이관

`src/parser/jsdoc-extractor.ts`가 상위 `.d.ts`의 JSDoc을 C# XML 주석으로 옮깁니다. 그래서 개별 API 설명은 마크다운에 옮겨 적지 않아도 IntelliSense에 그대로 뜨고, 상위 문서가 갱신되면 다음 `pnpm generate`에서 자동으로 따라옵니다.

> **중요**: API 설명을 마크다운 문서에 복사하지 마세요. 상위는 SDK 업데이트마다 재생성되지만 마크다운은 아니라서 확정적으로 어긋납니다. 공개 문서는 상위에 없는 것만 다룹니다 — [API 사용 패턴](../APIUsagePatterns.md) 참조.

## 문서 링크가 생성물로 들어가는 지점

`src/generators/csharp/field-docs.ts`가 공개 문서의 경로와 헤딩을 리터럴로 인용해 `Runtime/SDK/AIT.Types.IAP.cs`의 XML 주석으로 내보냅니다. 그 헤딩을 바꾸면 생성물의 앵커가 깨지므로 공개 문서와 생성기 소스를 항상 함께 고쳐야 합니다.

## 에러 처리

생성된 API는 실패 시 `AITException`을 던집니다. 코드는 `ErrorCode`에 있습니다.

```csharp
try
{
    string deviceId = await AIT.GetDeviceId();
}
catch (AITException ex)
{
    Debug.LogError($"API 호출 실패: {ex.Message} (code: {ex.ErrorCode})");
}
```

## 관련 문서

- [기여 가이드](../Contributing.md) — 생성기 실행 명령과 검증 순서
- [API 사용 패턴](../APIUsagePatterns.md) — 생성된 API를 쓰는 쪽 관점
- [프로젝트 구조](project-structure.md) — 생성물이 놓이는 위치
