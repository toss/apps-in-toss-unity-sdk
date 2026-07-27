# SDK 런타임 생성기

`@apps-in-toss/web-framework`의 TypeScript 정의에서 C# SDK 코드를 자동 생성합니다.

## 워크플로우
```bash
cd sdk-runtime-generator~
pnpm install
pnpm generate   # TypeScript → C# + JavaScript 브릿지 (~1.5초)
pnpm format     # CSharpier 포맷팅 (선택, dotnet 필요)
pnpm validate   # vitest로 생성 코드 속성 검증
pnpm test       # 유닛 테스트 실행
```

## 타입 매핑
| TypeScript | C# |
|------------|-----|
| `string` | `string` |
| `number` | `double` |
| `boolean` | `bool` |
| `Promise<T>` | `Task<T>` (async/await 패턴) |
| `T \| U` | Discriminated union class |

## API 사용 패턴
SDK API는 async/await 패턴을 사용하며, 에러 발생 시 `AITException`을 throw합니다:

```csharp
try
{
    string deviceId = await AIT.GetDeviceId();
    PlatformOS os = await AIT.GetPlatformOS();
}
catch (AITException ex)
{
    Debug.LogError($"API 호출 실패: {ex.Message} (code: {ex.Code})");
}
```
