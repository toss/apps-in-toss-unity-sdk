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

// 모듈 전체를 window.AppsInToss에 노출
window.AppsInToss = WebFramework as typeof WebFramework & {
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

// 네임스페이스 API 명시적 노출 (ES module export 누락 방지)
window.AppsInToss.GoogleAdMob = GoogleAdMob;
window.AppsInToss.IAP = IAP;
window.AppsInToss.SafeAreaInsets = SafeAreaInsets;
window.AppsInToss.Storage = Storage;
window.AppsInToss.TossAds = TossAds;
window.AppsInToss.env = env;
window.AppsInToss.graniteEvent = graniteEvent;
window.AppsInToss.partner = partner;
window.AppsInToss.tdsEvent = tdsEvent;

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(WebFramework).length, 'exports');
console.log('[Unity Bridge] Available:', Object.keys(WebFramework).join(', '));
console.log('[Unity Bridge] Namespaces: GoogleAdMob, IAP, SafeAreaInsets, Storage, TossAds, env, graniteEvent, partner, tdsEvent');

export default WebFramework;
