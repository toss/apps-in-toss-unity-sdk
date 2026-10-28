/**
 * 공개 C# 타입 이름 고정(pin).
 *
 * 3.x d.ts는 같은 개념의 타입 이름을 바꾼 곳이 있다 (예: AppLoginResult -> AppLoginResponse).
 * C#에는 외부로 내보낼 수 있는 타입 별칭이 없으므로, 생성기가 만든 새 이름을 기존 공개 이름으로
 * 바꿔 쓴다. 키는 생성기가 만드는 이름, 값은 SDK에 이미 공개돼 있던 이름이다.
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
 * 3.x에서 사라졌지만 기존 SDK가 공개하던 이름. 어떤 3.x 타입으로도 이어지지 않아 의도적으로 제거한다
 * (`tests/fixtures/public-type-names.json` 검증에서 이 목록은 건너뛴다).
 */
export const REMOVED_PUBLIC_TYPE_NAMES: readonly string[] = [
  // GrantPromotionRewardForGame 의 인자 묶음: 3.x는 GrantPromotionRewardParams 를 그대로 받는다
  'GrantPromotionRewardForGameParams',
  'GrantPromotionRewardForGameParamsParams',
  // 3.x d.ts 의 v2 호환 별칭(type X = Y)이라 별도 클래스가 없고, 어떤 API 시그니처도 쓰지 않았다
  'GrantPromotionRewardForGameSuccessResponse',
  'GrantPromotionRewardForGameErrorResponse',
  'GrantPromotionRewardForGameErrorResult',
  'GrantPromotionRewardForGameResponse',
  // 3.x 에서 구독이 직접 콜백을 받는 형태로 바뀌어 매개변수 묶음이 없어졌다
  'SafeAreaInsetsSubscribe__0',
  // 과거 생성기가 `number` 라는 이름으로 잘못 만든 빈 클래스
  'number',
];

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
