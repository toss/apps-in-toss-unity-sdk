/**
 * Unity Bridge for Apps in Toss
 *
 * @apps-in-toss/web-framework의 모든 export를 window.AppsInToss에 노출합니다.
 * Unity jslib에서 window.AppsInToss.functionName()으로 호출할 수 있습니다.
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

import * as WebFramework from '@apps-in-toss/web-framework';
import { GoogleAdMob } from '@apps-in-toss/web-framework';
import { IAP } from '@apps-in-toss/web-framework';
import { SafeAreaInsets } from '@apps-in-toss/web-framework';
import { Storage } from '@apps-in-toss/web-framework';
import { TossAds } from '@apps-in-toss/web-framework';
import { env } from '@apps-in-toss/web-framework';
import { graniteEvent } from '@apps-in-toss/web-framework';
import { partner } from '@apps-in-toss/web-framework';
import { tdsEvent } from '@apps-in-toss/web-framework';

// window.AppsInToss 타입 정의
declare global {
  interface Window {
    AppsInToss: typeof WebFramework & {
      GoogleAdMob: typeof GoogleAdMob;
      IAP: typeof IAP;
      SafeAreaInsets: typeof SafeAreaInsets;
      Storage: typeof Storage;
      TossAds: typeof TossAds;
      env: typeof env;
      graniteEvent: typeof graniteEvent;
      partner: typeof partner;
      tdsEvent: typeof tdsEvent;
    };
  }
}

// ES Module 객체는 frozen이므로 새 객체로 복사하여 window.AppsInToss에 노출
// (직접 할당 시 "Cannot assign to read only property" 에러 발생)
window.AppsInToss = {
  ...WebFramework,
  GoogleAdMob,
  IAP,
  SafeAreaInsets,
  Storage,
  TossAds,
  env,
  graniteEvent,
  partner,
  tdsEvent,
} as typeof WebFramework & {
  GoogleAdMob: typeof GoogleAdMob;
  IAP: typeof IAP;
  SafeAreaInsets: typeof SafeAreaInsets;
  Storage: typeof Storage;
  TossAds: typeof TossAds;
  env: typeof env;
  graniteEvent: typeof graniteEvent;
  partner: typeof partner;
  tdsEvent: typeof tdsEvent;
};

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(WebFramework).length, 'exports');
console.log('[Unity Bridge] Available:', Object.keys(WebFramework).join(', '));
console.log('[Unity Bridge] Namespaces: GoogleAdMob, IAP, SafeAreaInsets, Storage, TossAds, env, graniteEvent, partner, tdsEvent');

export default WebFramework;
