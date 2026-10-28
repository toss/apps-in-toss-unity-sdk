/**
 * 공개 C# 타입 이름 고정(pin).
 *
 * 3.x d.ts는 같은 개념의 타입 이름을 바꾼 곳이 있다 (예: AppLoginResult -> AppLoginResponse).
 * C#의 `using` 별칭은 선언한 파일 안에서만 보이므로 이름을 새로 바꾸는 데는 쓸 수 없다. 그래서 생성기가
 * 만든 새 이름을 기존 공개 이름으로 바꿔 쓴다. 이름만 바뀌고 새 타입이 따로 있는 경우는 아래
 * DEPRECATED_TYPE_SHIMS 의 `[Obsolete]` 하위 클래스로 옛 이름을 이어 간다. 키는 생성기가 만드는 이름, 값은 SDK에 이미 공개돼 있던 이름이다.
 *
 * - 새 이름이 3.x에서 따로 선언된 구 이름 타입(예: AdMobLoadResult)과 겹치면, 두 타입의 필드가
 *   같다는 것을 확인한 뒤 하나로 합친다. 필드가 다르면 생성을 중단한다.
 * - 값 이름은 SDK에 공개된 적이 있는 이름이어야 한다. tests/fixtures/public-type-names.json 의
 *   목록과 `type-name-pins.test.ts` 가 모든 공개 타입 이름이 살아 있는지 확인한다.
 */
export const TYPE_NAME_PINS: Readonly<Record<string, string>> = {
  // 로그인
  AppLoginResponse: 'AppLoginResult',
  AppLoginResponseReferrer: 'AppLoginResultReferrer',

  // 네비게이션 바 액세서리 버튼
  AddAccessoryButtonParams: 'AddAccessoryButtonOptions',
  AddAccessoryButtonParamsIcon: 'AddAccessoryButtonOptionsIcon',

  // AdMob
  LoadAppsInTossAdmobParams: 'LoadAdMobOptions',
  ShowAppsInTossAdmobParams: 'ShowAdMobOptions',
  GetCachedStatusAppsInTossAdmobParams: 'IsAdMobLoadedOptions',
  ShowAppsInTossAdmobEvent: 'ShowAdMobEvent',
  ShowAppsInTossAdmobEventData: 'ShowAdMobEventData',
  LoadAppsInTossAdmobResult: 'AdMobLoadResult',
  LoadAppsInTossAdmobResponseInfo: 'ResponseInfo',
  LoadAppsInTossAdmobAdNetworkInfo: 'AdNetworkResponseInfo',
  GoogleAdMobLoadAppsInTossAdMobParams: 'GoogleAdMobLoadAppsInTossAdMobArgs',
  GoogleAdMobShowAppsInTossAdMobParams: 'GoogleAdMobShowAppsInTossAdMobArgs',

  // 인앱 결제
  IAPCompleteProductGrantArgs: 'IAPCompleteProductGrantArgs_0',
  IAPCompleteProductGrantArgsParams: 'IAPCompleteProductGrantArgs_0Params',
  IAPGetSubscriptionInfoArgs: 'IAPGetSubscriptionInfoArgs_0',
  IAPGetSubscriptionInfoArgsParams: 'IAPGetSubscriptionInfoArgs_0Params',
  IapCreateOneTimePurchaseOrderOptionsOnEventParam: 'SuccessEvent',
  CreateSubscriptionPurchaseOrderOptionsOnEventParam: 'SubscriptionSuccessEvent',
  GetPendingOrdersResult: 'IAPGetPendingOrdersResult',
  PendingOrder: 'IAPGetPendingOrdersResultOrder',
  IapGetProductItemListResult: 'IAPGetProductItemListResult',
  CompletedOrRefundedOrder: 'CompletedOrRefundedOrdersResultOrder',
  CompletedOrRefundedOrderStatus: 'CompletedOrRefundedOrdersResultOrderStatus',

  // 알림 동의, 화면 방향
  RequestNotificationAgreementParams: 'RequestNotificationAgreementOptionsOptions',
  NotificationAgreementResult: 'RequestNotificationAgreementOptionsOnEventParamType',
  DeviceOrientation: 'SetDeviceOrientationOptionsType',

  // 익명 키, 연락처
  GetAnonymousKeyResponse: 'GetAnonymousKeySuccessResponse',
  FetchContactsResult: 'ContactResult',
  FetchContactsParams: 'FetchContactsOptions',
  FetchContactsParamsQuery: 'FetchContactsOptionsQuery',

  // 연락처 바이럴
  ContactsViralOption: 'ContactsViralParamsOptions',
  ContactsViralRewardData: 'RewardFromContactsViralEventData',
  ContactsViralCloseData: 'ContactsViralSuccessEventData',
  ContactsViralCloseDataCloseReason: 'ContactsViralSuccessEventDataCloseReason',
  ContactsViralRewardEvent: 'RewardFromContactsViralEvent',
  ContactsViralCloseEvent: 'ContactsViralSuccessEvent',
};

/**
 * 3.x에서 사라졌지만 기존 SDK가 공개하던 이름 중 어떤 3.x 타입으로도 이어 줄 수 없는 것.
 * 의도적으로 제거한다 (`tests/fixtures/public-type-names.json` 검증에서 이 목록은 건너뛴다).
 */
export const REMOVED_PUBLIC_TYPE_NAMES: readonly string[] = [
  // 비어 있다: 공개 타입은 하나도 지우지 않는다. 3.x 에서 사라진 타입도 compat.ts 의 LEGACY_TYPES 가 `[Obsolete]` 로 남긴다.
];

/**
 * 3.x가 같은 모양의 새 타입으로 대체한 옛 공개 이름. 새 타입을 상속하는 `[Obsolete]` 클래스로 남겨,
 * `new GrantPromotionRewardForGameParams { ... }`처럼 옛 이름을 쓰는 호출이 새 시그니처에 그대로 들어가게 한다.
 * (타입 별칭이 아니라 하위 클래스라서 `new`와 필드 초기화, 새 API 인자로 넘기기가 모두 된다.
 *  반환 타입으로 받는 쪽은 새 타입으로 받아야 한다.)
 */
export interface DeprecatedTypeShim {
  name: string;
  base: string;
  /** 옛 타입에만 있던 필드 (C# 선언 그대로) */
  extraMembers?: string;
}

export const DEPRECATED_TYPE_SHIMS: readonly DeprecatedTypeShim[] = [
  {
    name: 'GrantPromotionRewardForGameParams',
    base: 'GrantPromotionRewardParams',
    // 옛 Params 필드의 타입(옛 이름 하위 클래스)을 그대로 유지한다. 기반 클래스의 같은 이름 필드를 가린다.
    extraMembers: `        [Preserve]
        [JsonProperty("params")]
        public new GrantPromotionRewardForGameParamsParams Params;
`,
  },
  { name: 'GrantPromotionRewardForGameParamsParams', base: 'GrantPromotionRewardParamsParams' },
  { name: 'GrantPromotionRewardForGameSuccessResponse', base: 'GrantPromotionRewardResponse' },
  { name: 'GrantPromotionRewardForGameErrorResponse', base: 'GrantPromotionRewardErrorResponse' },
  { name: 'GrantPromotionRewardForGameErrorResult', base: 'GrantPromotionRewardErrorResult' },
  {
    name: 'GrantPromotionRewardForGameResponse',
    base: 'GrantPromotionRewardResponse',
    extraMembers: `        [Preserve]
        [JsonProperty("code")]
        public string Code; // optional
`,
  },
];

export interface ShimmableUnit extends PinnableUnit {
  category: string;
}

/** 옛 이름 하위 클래스 유닛을 만든다. 기반 타입이 생성되지 않았으면 중단한다. */
export function buildDeprecatedShimUnits<T extends ShimmableUnit>(units: readonly T[]): T[] {
  const byName = new Map(units.map(unit => [unit.name, unit] as const));
  return DEPRECATED_TYPE_SHIMS.map(shim => {
    const base = byName.get(shim.base);
    if (!base) {
      throw new Error(
        `옛 이름 '${shim.name}'의 기반 타입 '${shim.base}'이(가) 생성되지 않았습니다. ` +
        `type-name-pins.ts 의 DEPRECATED_TYPE_SHIMS 를 확인하세요.`,
      );
    }
    if (byName.has(shim.name)) {
      throw new Error(`옛 이름 '${shim.name}'이(가) 이미 생성돼 있어 하위 클래스를 만들 수 없습니다.`);
    }
    const body = shim.extraMembers ? `\n    {\n${shim.extraMembers}    }` : `\n    {\n    }`;
    const code = `    /// <summary>
    /// 이전 SDK에서 쓰던 이름이다. <see cref="${shim.base}"/>를 쓴다.
    /// </summary>
    [Serializable]
    [Preserve]
    [Obsolete("${shim.base}를 사용하세요.")]
    public class ${shim.name} : ${shim.base}${body}`;
    return { ...base, name: shim.name, code };
  });
}

const WORD = /[A-Za-z_][A-Za-z0-9_]*/g;

/** 코드 텍스트의 식별자 단위로 pin 이름을 공개 이름으로 바꾼다 (부분 일치는 건드리지 않는다) */
export function applyTypeNamePins(text: string): string {
  return text.replace(WORD, word => TYPE_NAME_PINS[word] ?? word);
}

export interface PinnableUnit {
  name: string;
  code: string;
}

function normalizeForCompare(code: string): string {
  return code
    .split('\n')
    .filter(line => !line.trim().startsWith('///'))
    .join('\n')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * 타입 유닛에 pin을 적용한다. pin 결과 이름이 이미 다른 유닛으로 있으면(3.x가 구 이름 타입을 따로
 * 선언한 경우) 본문이 같은지 확인하고 하나만 남긴다.
 */
export function applyTypeNamePinsToUnits<T extends PinnableUnit>(units: readonly T[]): T[] {
  const renamed = units.map(unit => ({
    ...unit,
    name: applyTypeNamePins(unit.name),
    code: applyTypeNamePins(unit.code),
  }));

  const byName = new Map<string, T>();
  const result: T[] = [];
  for (const unit of renamed) {
    const existing = byName.get(unit.name);
    if (!existing) {
      byName.set(unit.name, unit);
      result.push(unit);
      continue;
    }
    if (normalizeForCompare(existing.code) !== normalizeForCompare(unit.code)) {
      throw new Error(
        `타입 이름 pin 충돌: '${unit.name}' 이(가) 서로 다른 두 타입으로 생성됐습니다. ` +
        `TYPE_NAME_PINS 를 확인하세요.`
      );
    }
    // 같은 본문이면 먼저 나온 유닛만 둔다
  }
  return result;
}
