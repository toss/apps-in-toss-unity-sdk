import { describe, it, expect, vi, beforeEach } from 'vitest';

describe('isMinVersionSupported per-test mocks with dynamic import', () => {
  beforeEach(() => {
    // 각 테스트 전 모듈 캐시 초기화
    vi.resetModules();
  });

  it('iOS 환경 테스트: 현재 버전이 최소 버전보다 낮을 때', async () => {
    // doMock을 사용하여 모킹이 해당 테스트에 국한되도록 함
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'ios' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: '1.0.0',
      ios: '2.0.0',
    });
    expect(result).toBe(false);
  });

  it('Android 환경 테스트: 현재 버전이 최소 버전보다 높을 때', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '3.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'android' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: '2.0.0',
      ios: '1.0.0',
    });
    expect(result).toBe(true);
  });

  it('iOS 환경 테스트: 현재 버전이 최소 버전과 같을 때', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '2.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'ios' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: '1.0.0',
      ios: '2.0.0',
    });
    expect(result).toBe(true);
  });

  it('iOS 환경 테스트: minVersion이 "always"일 때', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'ios' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: '1.0.0',
      ios: 'always',
    });
    expect(result).toBe(true);
  });

  it('Android 환경 테스트: minVersion이 "always"일 때', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'android' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: 'always',
      ios: '1.0.0',
    });
    expect(result).toBe(true);
  });

  it('iOS 환경 테스트: minVersion이 "never"일 때', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'ios' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: '1.0.0',
      ios: 'never',
    });
    expect(result).toBe(false);
  });

  it('Android 환경 테스트: minVersion이 "never"일 때', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'android' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: 'never',
      ios: '1.0.0',
    });
    expect(result).toBe(false);
  });

  it('minVersion이 없을 때 false를 반환', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'toss',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'ios' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    // eslint-disable-next-line @typescript-eslint/ban-ts-comment
    // @ts-expect-error: ios 값 누락 의도
    const result = isMinVersionSupported({
      android: '1.0.0',
    });
    expect(result).toBe(false);
  });

  it('sandbox 환경 테스트: 버전에 상관없이 항상 true를 반환', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '1.0.0',
        operationalEnvironment: 'sandbox',
      },
    }));

    vi.doMock('react-native', () => ({
      Platform: { OS: 'ios' },
    }));

    const { isMinVersionSupported } = await import('./isMinVersionSupported.js');
    const result = isMinVersionSupported({
      android: '5.0.0',
      ios: '5.0.0',
    });
    expect(result).toBe(true);
  });
});
