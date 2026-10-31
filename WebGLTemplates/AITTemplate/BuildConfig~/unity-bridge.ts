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
import { Analytics } from '@apps-in-toss/web-framework';
import { Clipboard } from '@apps-in-toss/web-framework';
import { Device } from '@apps-in-toss/web-framework';
import { Environment } from '@apps-in-toss/web-framework';
import { File } from '@apps-in-toss/web-framework';
import { Game } from '@apps-in-toss/web-framework';
import { GoogleAdMob } from '@apps-in-toss/web-framework';
import { IAP } from '@apps-in-toss/web-framework';
import { Migration } from '@apps-in-toss/web-framework';
import { NavigationBar } from '@apps-in-toss/web-framework';
import { Notification } from '@apps-in-toss/web-framework';
import { Promotion } from '@apps-in-toss/web-framework';
import { Review } from '@apps-in-toss/web-framework';
import { SafeArea } from '@apps-in-toss/web-framework';
import { SafeAreaInsets } from '@apps-in-toss/web-framework';
import { Screen } from '@apps-in-toss/web-framework';
import { Share } from '@apps-in-toss/web-framework';
import { Storage } from '@apps-in-toss/web-framework';
import { TossAds } from '@apps-in-toss/web-framework';
import { TossAuth } from '@apps-in-toss/web-framework';
import { TossPay } from '@apps-in-toss/web-framework';
import { User } from '@apps-in-toss/web-framework';
import { Viral } from '@apps-in-toss/web-framework';
import { env } from '@apps-in-toss/web-framework';
import { graniteEvent } from '@apps-in-toss/web-framework';
import { partner } from '@apps-in-toss/web-framework';
import { tdsEvent } from '@apps-in-toss/web-framework';

// window.AppsInToss 타입 정의
declare global {
  interface Window {
    AppsInToss: typeof WebFramework & {
      Analytics: typeof Analytics;
      Clipboard: typeof Clipboard;
      Device: typeof Device;
      Environment: typeof Environment;
      File: typeof File;
      Game: typeof Game;
      GoogleAdMob: typeof GoogleAdMob;
      IAP: typeof IAP;
      Migration: typeof Migration;
      NavigationBar: typeof NavigationBar;
      Notification: typeof Notification;
      Promotion: typeof Promotion;
      Review: typeof Review;
      SafeArea: typeof SafeArea;
      SafeAreaInsets: typeof SafeAreaInsets;
      Screen: typeof Screen;
      Share: typeof Share;
      Storage: typeof Storage;
      TossAds: typeof TossAds;
      TossAuth: typeof TossAuth;
      TossPay: typeof TossPay;
      User: typeof User;
      Viral: typeof Viral;
      env: typeof env;
      graniteEvent: typeof graniteEvent;
      partner: typeof partner;
      tdsEvent: typeof tdsEvent;
    };
  }
}

// 모듈 전체를 window.AppsInToss에 노출
window.AppsInToss = WebFramework as typeof WebFramework & {
  Analytics: typeof Analytics;
  Clipboard: typeof Clipboard;
  Device: typeof Device;
  Environment: typeof Environment;
  File: typeof File;
  Game: typeof Game;
  GoogleAdMob: typeof GoogleAdMob;
  IAP: typeof IAP;
  Migration: typeof Migration;
  NavigationBar: typeof NavigationBar;
  Notification: typeof Notification;
  Promotion: typeof Promotion;
  Review: typeof Review;
  SafeArea: typeof SafeArea;
  SafeAreaInsets: typeof SafeAreaInsets;
  Screen: typeof Screen;
  Share: typeof Share;
  Storage: typeof Storage;
  TossAds: typeof TossAds;
  TossAuth: typeof TossAuth;
  TossPay: typeof TossPay;
  User: typeof User;
  Viral: typeof Viral;
  env: typeof env;
  graniteEvent: typeof graniteEvent;
  partner: typeof partner;
  tdsEvent: typeof tdsEvent;
};

// 네임스페이스 API 안전한 노출 (Unity 6000.3+ Module 읽기 전용 속성 호환)
const _aitNamespaces = { Analytics, Clipboard, Device, Environment, File, Game, GoogleAdMob, IAP, Migration, NavigationBar, Notification, Promotion, Review, SafeArea, SafeAreaInsets, Screen, Share, Storage, TossAds, TossAuth, TossPay, User, Viral, env, graniteEvent, partner, tdsEvent };
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

// window.AppsInToss 네임스페이스(특히 Storage) 설치 완료 신호.
// ait-playerprefs.js 가 50ms 폴링 대신 이 이벤트로 즉시 Storage 를 해석한다.
// 두 파일이 공유하는 계약이므로 이름('ait:bridge-ready')을 바꾸면 ait-playerprefs.js 도 함께 고친다.
// 구 브리지·이벤트 유실 시에는 ait-playerprefs.js 의 폴링이 폴백한다.
try { window.dispatchEvent(new Event('ait:bridge-ready')); } catch {}

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(WebFramework).length, 'exports');
console.log('[Unity Bridge] Available:', Object.keys(WebFramework).join(', '));
console.log('[Unity Bridge] Namespaces: Analytics, Clipboard, Device, Environment, File, Game, GoogleAdMob, IAP, Migration, NavigationBar, Notification, Promotion, Review, SafeArea, SafeAreaInsets, Screen, Share, Storage, TossAds, TossAuth, TossPay, User, Viral, env, graniteEvent, partner, tdsEvent');

export default WebFramework;
