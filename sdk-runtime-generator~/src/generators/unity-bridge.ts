/**
 * Unity Bridge 생성기
 *
 * jslib에서 사용하는 네임스페이스 API를 window.AppsInToss에 자동으로 노출하는
 * unity-bridge.ts 파일을 생성합니다.
 */

import { ParsedAPI, isFromWebAnalytics } from '../types.js';
import { BUNDLED_NAMESPACE_ALLOWLIST } from '../categories.js';

/**
 * Unity Bridge TypeScript 코드 생성
 * @param apis 파싱된 API 목록
 * @returns 생성된 unity-bridge.ts 코드
 */
export function generateUnityBridge(apis: ParsedAPI[]): string {
  // 사용되는 네임스페이스 수집 (중복 제거) + 패키지별 분류
  const frameworkNamespaces = new Set<string>();
  const analyticsNamespaces = new Set<string>();

  for (const api of apis) {
    if (api.namespace) {
      if (isFromWebAnalytics(api)) {
        analyticsNamespaces.add(api.namespace);
      } else {
        frameworkNamespaces.add(api.namespace);
      }
    }
  }

  const sortedFrameworkNs = Array.from(frameworkNamespaces).sort();
  const sortedAnalyticsNs = Array.from(analyticsNamespaces).sort();
  const sortedNamespaces = [...sortedFrameworkNs, ...sortedAnalyticsNs].sort();

  // 번들(dist/index.d.ts) 전용 네임스페이스는 named import 대상에서 제외한다.
  // 구버전 web-framework나 devtools mock에는 해당 export가 없을 수 있는데,
  // named import는 export 누락 시 link-time 에러로 unity-bridge 모듈 그래프 전체를 죽인다.
  const bundledSet = new Set(BUNDLED_NAMESPACE_ALLOWLIST);
  const staticFrameworkNs = sortedFrameworkNs.filter(ns => !bundledSet.has(ns));
  const bundledFrameworkNs = sortedFrameworkNs.filter(ns => bundledSet.has(ns));

  // 네임스페이스 import 문 생성 (패키지별 분리)
  const frameworkImports = staticFrameworkNs
    .map(ns => `import { ${ns} } from '@apps-in-toss/web-framework';`)
    .join('\n');
  const analyticsImports = sortedAnalyticsNs
    .map(ns => `import { ${ns} } from '@apps-in-toss/web-analytics';`)
    .join('\n');
  const allImports = [frameworkImports, analyticsImports].filter(Boolean).join('\n');

  // 번들 전용 네임스페이스 방어적 접근자 (없으면 undefined — 모듈 로드는 계속된다)
  const bundledAccessors = bundledFrameworkNs
    .map(
      ns => `// ${ns}는 번들 전용 네임스페이스 — 구버전 web-framework/devtools mock에 없을 수 있어
// named import(누락 시 link-time 에러) 대신 방어적으로 접근한다.
const ${ns} = (WebFramework as Record<string, any>)['${ns}'];`,
    )
    .join('\n');
  const bundledAccessorBlock = bundledAccessors ? `\n\n${bundledAccessors}` : '';

  // 네임스페이스 타입 정의 생성
  const namespaceTypeProps = sortedNamespaces
    .map(ns => `      ${ns}: typeof ${ns};`)
    .join('\n');

  // 번들 전용 네임스페이스가 있을 때만 undefined 스킵 가드를 넣는다
  // (없을 때는 기존 생성물과 byte-identical 유지)
  const undefinedGuard =
    bundledFrameworkNs.length > 0
      ? `    // 번들 전용 네임스페이스가 설치된 web-framework에 없으면 노출을 건너뛴다
    if (_value === undefined) {
      console.warn(\`[Unity Bridge] \${_name} is not available in the installed @apps-in-toss/web-framework — skipping\`);
      continue;
    }

`
      : '';

  // 네임스페이스 노출 코드 생성 (Unity 6000.3+ Module 읽기 전용 속성 호환)
  const namespaceList_code = sortedNamespaces.join(', ');
  const namespaceExposures = `// 네임스페이스 API 안전한 노출 (Unity 6000.3+ Module 읽기 전용 속성 호환)
const _aitNamespaces = { ${namespaceList_code} };
for (const [_name, _value] of Object.entries(_aitNamespaces)) {
  try {
${undefinedGuard}    // 이미 존재하고 값이 같으면 건너뛰기
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
    console.warn(\`[Unity Bridge] \${_name} is read-only, skipping\`);
  }
}`;

  // 네임스페이스 목록 문자열 (로그용)
  const namespaceList = sortedNamespaces.join(', ');

  return `/**
 * Unity Bridge for Apps in Toss
 *
 * @apps-in-toss/web-framework의 모든 export를 window.AppsInToss에 노출합니다.
 * Unity jslib에서 window.AppsInToss.functionName()으로 호출할 수 있습니다.
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

import * as WebFramework from '@apps-in-toss/web-framework';
${allImports}${bundledAccessorBlock}

// window.AppsInToss 타입 정의
declare global {
  interface Window {
    AppsInToss: typeof WebFramework & {
${namespaceTypeProps}
    };
  }
}

// 모듈 전체를 window.AppsInToss에 노출
window.AppsInToss = WebFramework as typeof WebFramework & {
${namespaceTypeProps.replace(/      /g, '  ')}
};

${namespaceExposures}

console.log('[Unity Bridge] AppsInToss bridge initialized with', Object.keys(WebFramework).length, 'exports');
console.log('[Unity Bridge] Available:', Object.keys(WebFramework).join(', '));
console.log('[Unity Bridge] Namespaces: ${namespaceList}');

export default WebFramework;
`;
}
