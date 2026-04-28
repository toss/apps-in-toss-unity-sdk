// 빌드 커스터마이징 튜토리얼 #1, #2 결합 데모
//
// - 튜토리얼 #1: canvas-confetti — Unity와 무관한 외부 라이브러리 번들링 예시
// - 튜토리얼 #2: Firebase Analytics — 환경변수 기반 초기화 예시
//
// 자세한 내용: Docs~/BuildCustomization.md

import confetti from 'canvas-confetti';
import { initializeApp } from 'firebase/app';
import { getAnalytics, isSupported as isAnalyticsSupported } from 'firebase/analytics';

// E2E 테스트가 결과를 검증할 수 있도록 window에 상태를 노출
declare global {
  interface Window {
    __TUTORIAL_CONFETTI_FIRED__?: boolean;
    __TUTORIAL_FIREBASE_INITIALIZED__?: boolean;
    __TUTORIAL_FIREBASE_ANALYTICS_READY__?: boolean;
    __TUTORIAL_FIREBASE_ERROR__?: string;
  }
}

// 튜토리얼 #1: confetti 발사 (페이지 로드 후)
window.addEventListener('load', () => {
  confetti({
    particleCount: 100,
    spread: 70,
    origin: { y: 0.6 },
  });
  window.__TUTORIAL_CONFETTI_FIRED__ = true;
});

// 튜토리얼 #2: Firebase 초기화
const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID,
  measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID,
};

// 키가 비어 있어도 빌드 자체는 성공해야 하므로 try/catch 로 보호
try {
  if (firebaseConfig.apiKey && firebaseConfig.appId) {
    const app = initializeApp(firebaseConfig);
    window.__TUTORIAL_FIREBASE_INITIALIZED__ = true;

    // measurementId 가 있고 브라우저가 지원할 때만 Analytics 활성화
    isAnalyticsSupported()
      .then((supported) => {
        if (supported && firebaseConfig.measurementId) {
          getAnalytics(app);
          window.__TUTORIAL_FIREBASE_ANALYTICS_READY__ = true;
        }
      })
      .catch((err) => {
        window.__TUTORIAL_FIREBASE_ERROR__ = String(err);
      });
  } else {
    window.__TUTORIAL_FIREBASE_ERROR__ = 'VITE_FIREBASE_* 환경변수가 비어 있습니다.';
  }
} catch (err) {
  window.__TUTORIAL_FIREBASE_ERROR__ = String(err);
}
