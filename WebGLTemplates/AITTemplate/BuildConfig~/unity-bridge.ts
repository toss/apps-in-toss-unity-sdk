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

// 필수 네임스페이스 (모든 버전에서 지원)
import { GoogleAdMob } from '@apps-in-toss/web-framework';
import { IAP } from '@apps-in-toss/web-framework';
import { SafeAreaInsets } from '@apps-in-toss/web-framework';
import { Storage } from '@apps-in-toss/web-framework';
import { env } from '@apps-in-toss/web-framework';
import { graniteEvent } from '@apps-in-toss/web-framework';
import { partner } from '@apps-in-toss/web-framework';
import { tdsEvent } from '@apps-in-toss/web-framework';

// 선택적 네임스페이스 (SDK 1.6.0+ 에서만 지원)
// rollup tree-shaking을 우회하기 위해 동적으로 접근
const TossAds = (WebFramework as any).TossAds;

// window.AppsInToss 타입 정의
declare global {
  interface Window {
    AppsInToss: typeof WebFramework & {
      GoogleAdMob: typeof GoogleAdMob;
      IAP: typeof IAP;
      SafeAreaInsets: typeof SafeAreaInsets;
      Storage: typeof Storage;
      TossAds?: any; // Optional - SDK 1.6.0+
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
  TossAds?: any;
  env: typeof env;
  graniteEvent: typeof graniteEvent;
  partner: typeof partner;
  tdsEvent: typeof tdsEvent;
};

// 필수 네임스페이스
const _requiredNamespaces: Record<string, any> = { GoogleAdMob, IAP, SafeAreaInsets, Storage, env, graniteEvent, partner, tdsEvent };

// 선택적 네임스페이스 (존재하는 경우에만 추가)
const _optionalNamespaces: Record<string, any> = {};
if (TossAds) _optionalNamespaces.TossAds = TossAds;

const _aitNamespaces = { ..._requiredNamespaces, ..._optionalNamespaces };

// 네임스페이스 API 안전한 노출 (Unity 6000.3+ Module 읽기 전용 속성 호환)
for (const [_name, _value] of Object.entries(_aitNamespaces)) {
  try {
    // 이미 존재하고 값이 같으면 건너뛰기
    if ((window.AppsInToss as any)[_name] === _value) continue;

    // Object.defineProperty로 안전하게 속성 설정
    Object.defineProperty(window.AppsInToss, _name, {
      value: _value,
      writable: true,
      configurable: true,
      enumerable: true
    });
  } catch (_err) {
    // Unity 6000.3+에서 Module 객체가 읽기 전용이면 무시
    console.warn(`[Unity Bridge] ${_name} is read-only, skipping`);
  }
}

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(WebFramework).length, 'exports');
console.log('[Unity Bridge] Available:', Object.keys(WebFramework).join(', '));
console.log('[Unity Bridge] Namespaces:', Object.keys(_aitNamespaces).join(', '));
if (!TossAds) console.log('[Unity Bridge] Note: TossAds not available in this SDK version');

export default WebFramework;
