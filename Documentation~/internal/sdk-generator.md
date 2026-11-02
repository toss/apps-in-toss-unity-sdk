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

C#으로 노출하지 않는 export와 이유는 번들 파서의 인벤토리에 남습니다. 고차 팩토리(`createAsyncBridge`, `createConstantBridge`, `createEventBridge`)는 JS 함수를 만들어 넘기는 탈출구라서 의도적으로 제외합니다(마샬링이 불가능해서가 아닙니다). 그 밖에 호출 결과가 `AITException`으로 변환되는 에러 클래스, 빈 이벤트 맵, 타입 수준에만 있는 제네릭 헬퍼도 제외합니다.

`options.callbacks` 묶음을 받는 API(`TossAds.initialize`, `attach`, `attachBanner`)는 콜백 이름별 이벤트(`AITCallbackBagEvent`)로 풀어서 C# `Action`/`Action<T>` 필드에 전달합니다. 결과에 `destroy`가 있으면 `Destroy` 핸들로 돌려줍니다.

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
| `ArrayBuffer`, `Uint8Array` | `byte[]` (반환값에 들어 있으면 jslib가 base64 문자열로 바꿔 보내고 C#이 `byte[]`로 읽습니다) |
| `Error` | `Exception` |
| `T \| U` | discriminated union 클래스 |

익명 객체 배열은 `<부모 타입><단수형 필드 이름>[]` 클래스로 생성합니다(`OriginStorageDump.opfs.files` -> `OriginStorageDumpOpfsFile[]`).

null 인자: C#이 `null`을 보내면 직렬화 결과는 문자열 `"null"`입니다. jslib는 선택 인자일 때 `null`/빈 값을 `undefined`로 바꿔 웹 API에 넘깁니다(`UTF8ToString(0)`은 `undefined`가 아니라 빈 문자열을 돌려주기 때문입니다).

`Promise<T>`는 Unity 버전에 따라 갈립니다. `UNITY_6000_0_OR_NEWER`에서는 `Awaitable<T>`, 그 이하에서는 `Task<T>`로 생성되고 한 파일 안에 `#if`로 두 벌이 들어갑니다.

## JSDoc 이관

`src/parser/jsdoc-extractor.ts`가 번들 `.d.ts`의 JSDoc을 C# XML 주석으로 옮깁니다. 그래서 개별 API 설명은 마크다운에 옮겨 적지 않아도 IntelliSense에 그대로 뜨고, 상위 문서가 갱신되면 다음 `pnpm generate`에서 자동으로 따라옵니다.

> **중요**: API 설명을 마크다운 문서에 복사하지 마세요. 상위는 SDK 업데이트마다 재생성되지만 마크다운은 아니라서 확정적으로 어긋납니다. 공개 문서는 상위에 없는 것만 다룹니다 — [API 사용 패턴](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/api-usage-patterns) 참조.

타입 멤버 설명이 빠진 경우는 `TYPE_MEMBER_DOC_FALLBACKS`(키는 `C# 타입.C# 멤버`)가 같은 방식으로 채웁니다. `tests/fixtures/documented-type-members.json`은 이전 SDK에서 설명이 있던 멤버 목록이고, `type-member-docs.test.ts`가 설명이 사라지면 실패합니다.

3.x JSDoc은 `@param`/`@returns`/요약이 빠진 API가 있습니다. 이전 SDK에 있던 설명이 얇아지지 않도록 `src/doc-fallbacks.ts`의 `DOC_FALLBACKS`(키는 C# API 이름)가 번들에 없는 항목만 채웁니다. 번들에 값이 있으면 번들이 우선이고, 상위가 문서를 채우면 해당 항목은 표에서 지웁니다.

## 공개 타입 이름 고정

C#의 `using X = Y;` 별칭은 선언한 파일 안에서만 보입니다. 라이브러리가 이름을 바꿔도 사용자 코드의 `X`는 그대로 컴파일되지 않고, 메서드 반환 타입도 별칭으로 이을 수 없습니다. 그래서 3.x가 타입 이름을 바꾸면 생성기가 두 가지 방법으로 이전 이름을 이어 줍니다.

- `src/generators/csharp/type-name-pins.ts`의 `TYPE_NAME_PINS`는 생성된 이름을 이전 공개 이름으로 되돌립니다(예: `AppLoginResponse` -> `AppLoginResult`). 카테고리 파일, `AITCore`, 타입 단위에 식별자 단위로 적용합니다. 되돌린 이름이 이미 있는 타입과 같으면 `///` 주석을 뺀 본문이 같을 때만 합치고, 다르면 생성을 중단합니다.
- `DEPRECATED_TYPE_SHIMS`는 3.x가 같은 모양의 새 타입으로 대체한 이전 이름을 `[Obsolete]` 하위 클래스로 남깁니다(`GrantPromotionRewardForGameParams : GrantPromotionRewardParams` 등). `new`, 필드 초기화, 새 API 인자로 넘기기는 그대로 되고, 반환값을 받는 쪽만 새 타입으로 받으면 됩니다. 기반 타입이 생성되지 않으면 생성을 중단합니다.
- `REMOVED_PUBLIC_TYPE_NAMES`는 어떤 방법으로도 이을 수 없어 의도적으로 사라진 이전 이름 목록이고, 지금은 비어 있습니다. `SafeAreaInsetsSubscribe__0`과 `number`는 `LEGACY_TYPES`(`compat.ts`)가 `[Obsolete]` 타입으로 남깁니다.
- `public-type-names.ts`의 `PUBLIC_TYPE_NAMES`는 이전 공개 이름 전체이고, `tests/unit/type-name-pins.test.ts`가 "생성됐거나 제거 목록에 있을 것", "하위 클래스의 기반이 있을 것", "System/UnityEngine 타입과 이름이 겹치지 않을 것", "참조되지 않는 공개 타입이 없을 것"을 검사합니다.
- `prune-synthetic-types.ts`는 생성기가 만들어 낸 클래스와 d.ts에 선언만 되고 아무도 참조하지 않는 타입(`Environment` enum은 `System.Environment`와 이름이 충돌합니다)을 지웁니다. `PUBLIC_TYPE_NAMES`는 남깁니다.
- 예전에 `(int timeoutMs)`만 받던 API가 선택 파라미터를 앞에 얻으면 `COMPAT_TIMEOUT_ONLY_OVERLOADS`(`api-data-preparer.ts`)에 올려 `Api(int timeoutMs)` 오버로드를 함께 생성합니다(`IAPGetCompletedOrRefundedOrders`).

새 타입의 이름은 3.x d.ts를 따릅니다. 문자열 리터럴 유니온은 enum이 되고, 값 집합이 같은 인라인 enum은 `enum-dedup.ts`가 합칩니다(핀 대상 이름은 합치지 않습니다). enum 멤버는 `[EnumMember(Value = ...)]`로 원래 문자열 값을 유지하므로 직렬화 형식은 바뀌지 않습니다.

## origin/main 대비 C# 호환성

깨지는 변경은 없습니다. origin/main의 공개 타입과 멤버는 이름과 시그니처가 그대로 있고, 3.x가 더 좋은 타입을 주는 곳은 이전 멤버를 `[Obsolete("Use <X>Typed")]`로 남긴 채 `Typed` 접미사를 붙인 새 멤버를 나란히 둡니다. 선언은 `src/generators/csharp/compat.ts` 한 곳에 있고, 생성이 끝난 뒤 대상이 없으면 생성을 중단합니다. 단위 테스트는 `tests/unit/compat.test.ts`입니다.

- 반환 타입이 바뀐 API(`RETURN_TYPE_COMPAT`): `OpenPermissionDialog`와 `GetUserKeyForGame`은 이전 이름이 `string`을 돌려주는 `[Obsolete]` 메서드이고(Awaitable과 Task 둘 다), 새 반환 타입은 `OpenPermissionDialogTyped`, `GetUserKeyForGameTyped`로 받습니다. 옛 메서드는 Typed 결과를 다시 해석하지 않고 이전 SDK와 같은 경로(같은 extern을 typeName `"string"`으로 호출)로 값을 받습니다. jslib는 typeName이 `"string"`이면 이전 인코딩(`data: JSON.stringify(result)`)으로 보내고 AITCore의 string 처리가 따옴표를 벗겨 문자열을 만들므로, 값은 이전 SDK와 같습니다(`allowed`, HASH 객체는 JSON 원문, 문자열 코드는 따옴표 없는 코드, `undefined`는 빈 문자열). `tests/unit/legacy-string-parity.test.ts`가 이를 검사합니다.
- `SafeAreaInsetsSubscribe(SafeAreaInsetsSubscribe__0 __0, int timeoutMs = 0)`와 `SafeAreaInsetsSubscribe__0` 타입은 `[Obsolete]` 오버로드로 남고 런타임에도 동작합니다. 새 `(Action<SafeAreaInsets> onEvent, Action<AITException> onError = null)` 오버로드가 함께 있습니다. `SafeAreaInsetsSubscribe(null)`처럼 인자 하나만 넘기는 옛 호출이 새 오버로드와 모호해지지 않도록, 기본값 없는 `(SafeAreaInsetsSubscribe__0)` 오버로드를 하나 더 둡니다.
- 문자열 필드가 enum이 된 곳(`STRING_ENUM_COMPAT`)은 이전 이름의 `string` 필드가 직렬화를 맡고(알 수 없는 값도 잃지 않음), `<Name>Typed`는 `[JsonIgnore]` nullable enum 계산 프로퍼티입니다. 대상은 `EventLogParams.Log_type`, `DeclaredAgeRange.Status`, `Location.AccessLocation`, `SubmitGameCenterLeaderBoardScoreResponse.StatusCode`, `TossAdsAttachBannerOptions.Theme`/`Tone`/`Variant`, `TossAdsAttachOptions.Theme`입니다.
- `ContactsViralEventData.RewardAmount`는 `double`(`[Obsolete]`)이고, `RewardAmountSpecified`로 존재 여부를 추적하며 `double? RewardAmountTyped`가 짝입니다.
- `CheckoutPaymentOptions.PayToken`과 `RequestTossPayPaysBillingOptions.WrappedToken`은 `[JsonIgnore] [Obsolete("Use Params.X")]` 프로퍼티로 `Params`(없으면 생성)에 전달합니다. 필드에서 프로퍼티가 되므로 `ref`/`out`으로 넘기는 코드만 바이너리·소스 호환이 아닙니다.
- 이전 생성기가 붙이던 `error` 필드(`AppLoginResult`, `AppsInTossGlobals`, `AttachBannerResult`, `CompletedOrRefundedOrdersResult`, `IAPGetPendingOrdersResult`, `IAPGetProductItemListResult`, `IapSubscriptionInfoResponse`, `Location`, `SafeAreaInsets`, `SetScreenAwakeModeResult`, `SetSecureScreenResult`)와 `GrantPromotionRewardResult.Code`는 이전 타입과 직렬화 그대로 `[Obsolete]`로 남습니다. 실제 실패는 `AITException`으로 옵니다.
- `PermissionName`, `ConsentedUserDataKey` enum은 멤버 순서를 이전 SDK에 맞추고 새 멤버를 뒤에 붙입니다. 정수 값이 바뀌지 않습니다.
- `IAPGetCompletedOrRefundedOrders(int timeoutMs = 0)`는 기본값까지 그대로이고, 새 시그니처는 첫 인자를 필수로 둬 모호하지 않습니다. `GrantPromotionRewardForGame`은 옛 매개변수 타입을 받는 `[Obsolete]` 오버로드가 있습니다.
- 검증은 main과 새 빌드의 DLL을 리플렉션으로 비교해 제거와 변경이 0건인지 보는 방식으로 합니다(추가와 `[Obsolete]` 추가만 허용). 알려진 예외는 위 필드에서 프로퍼티가 된 2건입니다.
- `APICategory`는 예전에 `Other`였던 API 9개가 도메인 카테고리(Media, Notification, Payment, Promotion, Review, SystemInfo, User)로 옮겨졌습니다. 카테고리를 읽는 코드는 `Tests~/E2E`의 `APIParameterInspector`뿐이고 매핑 가드 테스트가 있습니다.
- API 143개가 추가됐고, 3.x에서 deprecated인 API 90여 개는 `[Obsolete]`입니다. 경고일 뿐 컴파일을 막지 않습니다.

## 문서 링크가 생성물로 들어가는 지점

`src/generators/csharp/field-docs.ts`가 공개 문서의 경로와 헤딩을 리터럴로 인용해 `Runtime/SDK/AIT.Types.IAP.cs`의 XML 주석으로 내보냅니다. 그 헤딩을 바꾸면 생성물의 앵커가 깨지므로 공개 문서와 생성기 소스를 항상 함께 고쳐야 합니다.

## 에러 처리

생성된 API는 실패 시 `AITException`을 던집니다. 코드는 `ErrorCode`에 있습니다. JS 에러의 `name`(예: `PermissionError`)은 `ErrorCode`로 전달됩니다. 기본 `Error`이면 `ErrorCode`는 빈 문자열입니다.

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
