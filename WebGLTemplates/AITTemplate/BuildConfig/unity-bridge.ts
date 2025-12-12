/**
 * Unity Bridge for Apps in Toss
 *
 * @apps-in-toss/web-framework의 모든 export를 window.AppsInToss에 노출합니다.
 * Unity jslib에서 window.AppsInToss.functionName()으로 호출할 수 있습니다.
 */

import * as WebFramework from '@apps-in-toss/web-framework';

// window.AppsInToss 타입 정의
declare global {
  interface Window {
    AppsInToss: typeof WebFramework;
  }
}

// 모듈 전체를 window.AppsInToss에 노출
window.AppsInToss = WebFramework;

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(WebFramework).length, 'exports');
console.log('[Unity Bridge] Available:', Object.keys(WebFramework).join(', '));

export default WebFramework;
