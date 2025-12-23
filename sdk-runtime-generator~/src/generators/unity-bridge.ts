/**
 * Unity Bridge 생성기
 *
 * jslib에서 사용하는 네임스페이스 API를 window.AppsInToss에 자동으로 노출하는
 * unity-bridge.ts 파일을 생성합니다.
 */

import { ParsedAPI } from '../types.js';

/**
 * Unity Bridge TypeScript 코드 생성
 * @param apis 파싱된 API 목록
 * @returns 생성된 unity-bridge.ts 코드
 */
export function generateUnityBridge(apis: ParsedAPI[]): string {
  // 사용되는 네임스페이스 수집 (중복 제거)
  const namespaces = new Set<string>();
  for (const api of apis) {
    if (api.namespace) {
      namespaces.add(api.namespace);
    }
  }

  const sortedNamespaces = Array.from(namespaces).sort();

  // 네임스페이스 import 문 생성
  const namespaceImports = sortedNamespaces
    .map(ns => `import { ${ns} } from '@apps-in-toss/web-framework';`)
    .join('\n');

  // 네임스페이스 타입 정의 생성
  const namespaceTypeProps = sortedNamespaces
    .map(ns => `      ${ns}: typeof ${ns};`)
    .join('\n');

  // 네임스페이스 노출 코드 생성 (Unity 6000.3+ Module 읽기 전용 속성 호환)
  const namespaceList_code = sortedNamespaces.join(', ');
  const namespaceExposures = `// 네임스페이스 API 안전한 노출 (Unity 6000.3+ Module 읽기 전용 속성 호환)
const _aitNamespaces = { ${namespaceList_code} };
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
${namespaceImports}

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
