# SDK 런타임 생성기

`Runtime/SDK/`의 C# 코드와 jslib 브릿지가 어떻게 만들어지는지에 대한 내부 메모입니다.

> **대상**: SDK 기여자. 생성기를 돌리는 명령과 검증 순서는 [기여 가이드](../Contributing.md)에 있습니다.

## 입력과 출력

입력은 `@apps-in-toss/web-framework` 3.x가 배포하는 번들 `dist/index.d.ts` 하나뿐입니다. 번들이 export하는 값은 전부 `src/parser/bundle-parser.ts`가 분류합니다. 해석된 버전에 번들이 없으면 `src/discovery.ts`가 생성을 중단하고, 분류되지 않은 export가 있어도 중단합니다(새 export가 조용히 빠지는 일을 막기 위해서입니다).

| 산출물 | 위치 |
|--------|------|
| API partial class | `Runtime/SDK/AIT.<카테고리>.cs` |
| 타입 정의 | `Runtime/SDK/AIT.Types.<카테고리>.cs` |
| jslib 브릿지 | `Runtime/SDK/Plugins/` |
| 인프라 | `Runtime/SDK/AITCore.cs` |
| WebGL 쪽 호출 연결 | `WebGLTemplates/AITTemplate/BuildConfig~/unity-bridge.ts` |

카테고리 목록은 `sdk-runtime-generator~/src/categories.ts`가 단일 출처입니다.

## API 분류와 이름

| 번들의 모양 | C# API |
|-------------|--------|
| 호출 가능한 최상위 export | 함수 하나 (`getClipboardText` -> `GetClipboardText`) |
| 호출 서명이 없는 객체 export | 네임스페이스. 멤버마다 API (`Storage.getItem` -> `StorageGetItem`) |
| 네임스페이스의 값 멤버 | 읽기 API (`Device.locale` -> `DeviceLocale`) |
| `addEventListener` 하나만 가진 객체 | 이벤트별 구독 API |
| 함수에 붙은 `isSupported`/`getPermission`/`openPermissionDialog` | `<기반 API>IsSupported` 등 별도 API |

이름은 항상 네임스페이스 접두사에 멤버의 PascalCase를 붙인 평면 구조입니다. 이름이 겹치면 결정적인 규칙으로 구분합니다. deprecated 최상위 함수도 그대로 노출하고, `@deprecated` 문구가 대체 API를 가리키면 `[Obsolete("Use X")]`를 붙입니다.

C#으로 노출하지 않는 export와 이유는 번들 파서의 인벤토리에 남습니다. 기술적으로 불가능한 경우만 여기에 속합니다. 고차 팩토리(`createAsyncBridge` 등), 호출 결과가 `AITException`으로 변환되는 에러 클래스, 빈 이벤트 맵, 타입 수준에만 있는 제네릭 헬퍼가 해당합니다.

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

`src/parser/jsdoc-extractor.ts`가 번들 `.d.ts`의 JSDoc을 C# XML 주석으로 옮깁니다. 그래서 개별 API 설명은 마크다운에 옮겨 적지 않아도 IntelliSense에 그대로 뜨고, 상위 문서가 갱신되면 다음 `pnpm generate`에서 자동으로 따라옵니다.

> **중요**: API 설명을 마크다운 문서에 복사하지 마세요. 상위는 SDK 업데이트마다 재생성되지만 마크다운은 아니라서 확정적으로 어긋납니다. 공개 문서는 상위에 없는 것만 다룹니다 — [API 사용 패턴](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/api-usage-patterns) 참조.

3.x JSDoc은 `@param`/`@returns`/요약이 빠진 API가 있습니다. 이전 SDK에 있던 설명이 얇아지지 않도록 `src/doc-fallbacks.ts`의 `DOC_FALLBACKS`(키는 C# API 이름)가 번들에 없는 항목만 채웁니다. 번들에 값이 있으면 번들이 우선이고, 상위가 문서를 채우면 해당 항목은 표에서 지웁니다.

## 공개 타입 이름 고정

C# 타입에는 별칭을 export하는 방법이 없어서, 3.x가 타입 이름을 바꾸면 사용자 코드가 깨집니다. 그래서 생성기가 이름을 고정합니다.

- `src/generators/csharp/type-name-pins.ts`의 `TYPE_NAME_PINS`는 생성된 이름을 이전 공개 이름으로 되돌립니다(예: `AppLoginResponse` -> `AppLoginResult`). 카테고리 파일, `AITCore`, 타입 단위에 식별자 단위로 적용합니다. 되돌린 이름이 이미 있는 타입과 같으면 `///` 주석을 뺀 본문이 같을 때만 합치고, 다르면 생성을 중단합니다.
- `REMOVED_PUBLIC_TYPE_NAMES`는 3.x에서 의도적으로 사라진 이전 이름입니다.
- `public-type-names.ts`의 `PUBLIC_TYPE_NAMES`는 이전 공개 이름 전체이고, `tests/unit/type-name-pins.test.ts`가 "생성됐거나 제거 목록에 있을 것"을 검사합니다.
- `prune-synthetic-types.ts`는 생성기가 만들어 낸 클래스 중 아무도 참조하지 않는 것을 지웁니다. d.ts에 선언된 타입과 `PUBLIC_TYPE_NAMES`는 남깁니다.

새 타입의 이름은 3.x d.ts를 따릅니다. 문자열 리터럴 유니온은 enum이 되고, 값 집합이 같은 인라인 enum은 `enum-dedup.ts`가 합칩니다(핀 대상 이름은 합치지 않습니다). enum 멤버는 `[EnumMember(Value = ...)]`로 원래 문자열 값을 유지하므로 직렬화 형식은 바뀌지 않습니다.

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
- [API 사용 패턴](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/api-usage-patterns) — 생성된 API를 쓰는 쪽 관점
- [프로젝트 구조](project-structure.md) — 생성물이 놓이는 위치
