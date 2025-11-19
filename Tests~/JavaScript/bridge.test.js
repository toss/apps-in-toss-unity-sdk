import { describe, it, expect, beforeEach, vi } from 'vitest';

// 브라우저 환경 모킹
beforeEach(() => {
  global.window = {
    navigator: {
      userAgent: ''
    },
    ReactNativeWebView: undefined,
  };
});

describe('Browser Detection', () => {
  it('should detect Chrome browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36';

    const browser = detectBrowser();
    expect(browser.name).toBe('Chrome');
  });

  it('should detect Safari browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15';

    const browser = detectBrowser();
    expect(browser.name).toBe('Safari');
  });

  it('should detect Firefox browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0';

    const browser = detectBrowser();
    expect(browser.name).toBe('Firefox');
  });

  it('should detect Edge browser', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0';

    const browser = detectBrowser();
    expect(browser.name).toBe('Edge');
  });

  it('should return Unknown for unrecognized browser', () => {
    global.window.navigator.userAgent = 'Some unknown browser';

    const browser = detectBrowser();
    expect(browser.name).toBe('Unknown');
  });
});

describe('OS Detection', () => {
  it('should detect iOS', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15';

    const os = detectOS();
    expect(os).toBe('iOS');
  });

  it('should detect iPad as iOS', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15';

    const os = detectOS();
    expect(os).toBe('iOS');
  });

  it('should detect Android', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36';

    const os = detectOS();
    expect(os).toBe('Android');
  });

  it('should detect Windows', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36';

    const os = detectOS();
    expect(os).toBe('Windows');
  });

  it('should detect macOS', () => {
    global.window.navigator.userAgent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15';

    const os = detectOS();
    expect(os).toBe('macOS');
  });

  it('should return Unknown for unrecognized OS', () => {
    global.window.navigator.userAgent = 'Some unknown OS';

    const os = detectOS();
    expect(os).toBe('Unknown');
  });
});

describe('ReactNativeWebView Detection', () => {
  it('should detect when in ReactNativeWebView', () => {
    global.window.ReactNativeWebView = { postMessage: vi.fn() };

    const inWebView = isReactNativeWebView();
    expect(inWebView).toBe(true);
  });

  it('should detect when not in ReactNativeWebView', () => {
    global.window.ReactNativeWebView = undefined;

    const inWebView = isReactNativeWebView();
    expect(inWebView).toBe(false);
  });

  it('should handle null ReactNativeWebView', () => {
    global.window.ReactNativeWebView = null;

    const inWebView = isReactNativeWebView();
    expect(inWebView).toBe(false);
  });
});

describe('Environment Detection', () => {
  it('should detect production mode from string "true"', () => {
    const isProduction = detectProduction('true');
    expect(isProduction).toBe(true);
  });

  it('should detect production mode from boolean true', () => {
    const isProduction = detectProduction(true);
    expect(isProduction).toBe(true);
  });

  it('should detect development mode from string "false"', () => {
    const isProduction = detectProduction('false');
    expect(isProduction).toBe(false);
  });

  it('should detect development mode from boolean false', () => {
    const isProduction = detectProduction(false);
    expect(isProduction).toBe(false);
  });

  it('should default to development for undefined', () => {
    const isProduction = detectProduction(undefined);
    expect(isProduction).toBe(false);
  });
});

describe('Unity SendMessage Mock', () => {
  it('should call Unity SendMessage with correct parameters', () => {
    const mockUnityInstance = {
      SendMessage: vi.fn()
    };

    sendMessageToUnity(mockUnityInstance, 'GameObject', 'MethodName', 'param');

    expect(mockUnityInstance.SendMessage).toHaveBeenCalledWith('GameObject', 'MethodName', 'param');
  });

  it('should handle Unity instance not ready', () => {
    const result = sendMessageToUnity(null, 'GameObject', 'MethodName', 'param');
    expect(result).toBe(false);
  });

  it('should handle missing SendMessage method', () => {
    const mockUnityInstance = {};
    const result = sendMessageToUnity(mockUnityInstance, 'GameObject', 'MethodName', 'param');
    expect(result).toBe(false);
  });
});

// Helper functions (실제 bridge.js에서 export되어야 함)
function detectBrowser() {
  const ua = window.navigator.userAgent;
  if (ua.includes('Edg/')) return { name: 'Edge' };
  if (ua.includes('Chrome')) return { name: 'Chrome' };
  if (ua.includes('Safari') && !ua.includes('Chrome')) return { name: 'Safari' };
  if (ua.includes('Firefox')) return { name: 'Firefox' };
  return { name: 'Unknown' };
}

function detectOS() {
  const ua = window.navigator.userAgent;
  if (ua.includes('iPhone') || ua.includes('iPad')) return 'iOS';
  if (ua.includes('Android')) return 'Android';
  if (ua.includes('Windows')) return 'Windows';
  if (ua.includes('Macintosh')) return 'macOS';
  if (ua.includes('Linux')) return 'Linux';
  return 'Unknown';
}

function isReactNativeWebView() {
  return !!window.ReactNativeWebView;
}

function detectProduction(value) {
  if (typeof value === 'boolean') return value;
  if (typeof value === 'string') return value === 'true';
  return false;
}

function sendMessageToUnity(unityInstance, objectName, methodName, value) {
  if (!unityInstance || typeof unityInstance.SendMessage !== 'function') {
    return false;
  }
  unityInstance.SendMessage(objectName, methodName, value);
  return true;
}
