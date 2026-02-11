/**
 * Firebase Bridge for Apps in Toss Unity SDK
 *
 * Firebase JS SDK를 Unity WebGL jslib에서 사용할 수 있도록 래핑합니다.
 * 빌드 시 %AIT_FIREBASE_*% 플레이스홀더가 실제 값으로 치환됩니다.
 *
 * This file is auto-generated. Do not modify directly.
 * 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
 */

import { initializeApp, type FirebaseApp } from 'firebase/app';
import { getAnalytics, logEvent, setUserId, setUserProperties, setAnalyticsCollectionEnabled, type Analytics } from 'firebase/analytics';
import { getAuth, signInAnonymously, signInWithCustomToken, signOut, onAuthStateChanged, type Auth, type User } from 'firebase/auth';

// Firebase 인스턴스
let app: FirebaseApp | null = null;
let analytics: Analytics | null = null;
let auth: Auth | null = null;

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
  initializeApp() {
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
  },

  logEvent(eventName, eventParams) {
    if (!analytics) {
      console.warn('[AIT Firebase] Analytics not initialized');
      return;
    }
    logEvent(analytics, eventName, eventParams);
  },

  setUserId(userId) {
    if (!analytics) {
      console.warn('[AIT Firebase] Analytics not initialized');
      return;
    }
    setUserId(analytics, userId);
  },

  setUserProperties(properties) {
    if (!analytics) {
      console.warn('[AIT Firebase] Analytics not initialized');
      return;
    }
    setUserProperties(analytics, properties);
  },

  setAnalyticsCollectionEnabled(enabled) {
    if (!analytics) {
      console.warn('[AIT Firebase] Analytics not initialized');
      return;
    }
    setAnalyticsCollectionEnabled(analytics, enabled);
  },

  async signInAnonymously() {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    const credential = await signInAnonymously(auth);
    return extractUser(credential.user);
  },

  async signInWithCustomToken(token: string) {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    const credential = await signInWithCustomToken(auth, token);
    return extractUser(credential.user);
  },

  async signOut() {
    if (!auth) {
      throw new Error('[AIT Firebase] Auth not initialized');
    }
    await signOut(auth);
  },

  onAuthStateChanged(callback: (data: any) => void) {
    if (!auth) {
      console.warn('[AIT Firebase] Auth not initialized');
      return () => {};
    }
    return onAuthStateChanged(auth, (user) => {
      callback(user ? extractUser(user) : null);
    });
  },
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
