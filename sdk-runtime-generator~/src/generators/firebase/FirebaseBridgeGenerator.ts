/**
 * Firebase Bridge TypeScript 생성기
 *
 * firebase-bridge.ts를 생성합니다.
 * - Firebase JS SDK를 import하고 초기화
 * - window.__AIT_Firebase 객체에 모든 API를 노출
 * - 빌드 시 플레이스홀더가 실제 값으로 치환됨
 */

import type { FirebaseAPIDefinition } from '../../parser/firebase-parser.js';

/**
 * firebase-bridge.ts 생성
 */
export function generateFirebaseBridge(apis: FirebaseAPIDefinition[]): string {
  // 서비스별 그룹핑
  const services = new Set(apis.map(a => a.service));

  // import 문 생성
  const imports = generateImports(apis, services);

  // 서비스 초기화 코드
  const serviceVars = generateServiceVariables(services);

  // API 메서드 구현
  const methods = apis.map(api => generateBridgeMethod(api)).join('\n\n');

  return `/**
 * Firebase Bridge for Apps in Toss Unity SDK
 *
 * Firebase JS SDK를 Unity WebGL jslib에서 사용할 수 있도록 래핑합니다.
 * 빌드 시 %AIT_FIREBASE_*% 플레이스홀더가 실제 값으로 치환됩니다.
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

${imports}

// Firebase 인스턴스
let app: FirebaseApp | null = null;
${serviceVars}

// Firebase 설정 (빌드 시 플레이스홀더 치환)
const firebaseConfig = {
  apiKey: '%AIT_FIREBASE_API_KEY%',
  authDomain: '%AIT_FIREBASE_AUTH_DOMAIN%',
  projectId: '%AIT_FIREBASE_PROJECT_ID%',
  storageBucket: '%AIT_FIREBASE_STORAGE_BUCKET%',
  messagingSenderId: '%AIT_FIREBASE_MESSAGING_SENDER_ID%',
  appId: '%AIT_FIREBASE_APP_ID%',
  measurementId: '%AIT_FIREBASE_MEASUREMENT_ID%',
  databaseURL: '%AIT_FIREBASE_DATABASE_URL%',
};

/**
 * 빈 플레이스홀더 값을 제거한 Firebase 설정 반환
 */
function getCleanConfig(): Record<string, string> {
  const clean: Record<string, string> = {};
  for (const [key, value] of Object.entries(firebaseConfig)) {
    // 치환되지 않은 플레이스홀더(%로 시작)나 빈 값은 제외
    if (value && !value.startsWith('%AIT_')) {
      clean[key] = value;
    }
  }
  return clean;
}

// window.__AIT_Firebase 타입 정의
declare global {
  interface Window {
    __AIT_Firebase: Record<string, (...args: any[]) => any>;
  }
}

// window.__AIT_Firebase에 API 노출
window.__AIT_Firebase = {
${methods}
};

/**
 * User 객체에서 필요한 필드만 추출
 */
function extractUser(user: any): Record<string, any> {
  return {
    uid: user.uid || '',
    email: user.email || null,
    displayName: user.displayName || null,
    photoURL: user.photoURL || null,
    phoneNumber: user.phoneNumber || null,
    isAnonymous: user.isAnonymous || false,
    emailVerified: user.emailVerified || false,
  };
}

console.log('[AIT Firebase] Bridge initialized');
`;
}

/**
 * Firebase import 문 생성
 */
function generateImports(apis: FirebaseAPIDefinition[], services: Set<string>): string {
  const lines: string[] = [];

  // firebase/app은 항상 필요
  lines.push("import { initializeApp, type FirebaseApp } from 'firebase/app';");

  if (services.has('analytics')) {
    const analyticsApis = apis.filter(a => a.service === 'analytics');
    const analyticsImports = analyticsApis.map(a => a.name).join(', ');
    lines.push(`import { getAnalytics, ${analyticsImports}, type Analytics } from 'firebase/analytics';`);
  }

  if (services.has('auth')) {
    const authApis = apis.filter(a => a.service === 'auth');
    const authImports = authApis.map(a => a.name).join(', ');
    lines.push(`import { getAuth, ${authImports}, type Auth, type User } from 'firebase/auth';`);
  }

  return lines.join('\n');
}

/**
 * 서비스 변수 선언 생성
 */
function generateServiceVariables(services: Set<string>): string {
  const vars: string[] = [];

  if (services.has('analytics')) {
    vars.push('let analytics: Analytics | null = null;');
  }
  if (services.has('auth')) {
    vars.push('let auth: Auth | null = null;');
  }

  return vars.join('\n');
}

/**
 * 개별 API의 bridge 메서드 생성
 */
function generateBridgeMethod(api: FirebaseAPIDefinition): string {
  // initializeApp은 특별 처리
  if (api.name === 'initializeApp') {
    return generateInitializeMethod();
  }

  if (api.isSubscription) {
    return generateSubscriptionBridgeMethod(api);
  }

  if (api.service === 'analytics') {
    return generateAnalyticsBridgeMethod(api);
  }

  if (api.service === 'auth') {
    return generateAuthBridgeMethod(api);
  }

  // 기본: 직접 호출
  const params = api.parameters.map(p => p.name).join(', ');
  return `  ${api.name}(${params}) {
    console.log('[AIT Firebase] ${api.name} called');
  },`;
}

/**
 * initializeApp 메서드 생성
 */
function generateInitializeMethod(): string {
  return `  initializeApp() {
    const config = getCleanConfig();

    if (!config.apiKey || !config.projectId || !config.appId) {
      throw new Error('[AIT Firebase] Firebase config is incomplete. Check AITEditorScriptObject settings.');
    }

    app = initializeApp(config);
    console.log('[AIT Firebase] App initialized:', config.projectId);

    // Analytics 초기화 (measurementId가 있는 경우)
    if (config.measurementId) {
      try {
        analytics = getAnalytics(app);
        console.log('[AIT Firebase] Analytics initialized');
      } catch (e) {
        console.warn('[AIT Firebase] Analytics init failed:', e);
      }
    }

    // Auth 초기화
    try {
      auth = getAuth(app);
      console.log('[AIT Firebase] Auth initialized');
    } catch (e) {
      console.warn('[AIT Firebase] Auth init failed:', e);
    }
  },`;
}

/**
 * Analytics API bridge 메서드 생성
 */
function generateAnalyticsBridgeMethod(api: FirebaseAPIDefinition): string {
  const params = api.parameters.map(p => p.name).join(', ');
  const callArgs = api.parameters.map(p => p.name).join(', ');

  return `  ${api.name}(${params}) {
    if (!analytics) {
      console.warn('[AIT Firebase] Analytics not initialized');
      return;
    }
    ${api.name}(analytics, ${callArgs});
  },`;
}

/**
 * Auth API bridge 메서드 생성 (비동기)
 */
function generateAuthBridgeMethod(api: FirebaseAPIDefinition): string {
  const params = api.parameters.map(p => p.name).join(', ');
  const callArgs = params ? `, ${params}` : '';

  if (api.name === 'signOut') {
    return `  async signOut() {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    await signOut(auth);
  },`;
  }

  if (api.name === 'signInAnonymously') {
    return `  async signInAnonymously() {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    const credential = await signInAnonymously(auth);
    return extractUser(credential.user);
  },`;
  }

  if (api.name === 'signInWithCustomToken') {
    return `  async signInWithCustomToken(token: string) {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    const credential = await signInWithCustomToken(auth, token);
    return extractUser(credential.user);
  },`;
  }

  // 기타 Auth API
  return `  async ${api.name}(${params}) {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    return await ${api.name}(auth${callArgs});
  },`;
}

/**
 * 구독 API bridge 메서드 생성
 */
function generateSubscriptionBridgeMethod(api: FirebaseAPIDefinition): string {
  if (api.name === 'onAuthStateChanged') {
    return `  onAuthStateChanged(callback: (data: any) => void) {
    if (!auth) {
      console.warn('[AIT Firebase] Auth not initialized');
      return () => {};
    }
    return onAuthStateChanged(auth, (user) => {
      callback(user ? extractUser(user) : null);
    });
  },`;
  }

  // 기타 구독 API (Phase 2/3용)
  return `  ${api.name}(callback: (data: any) => void) {
    console.warn('[AIT Firebase] ${api.name} not implemented');
    return () => {};
  },`;
}
