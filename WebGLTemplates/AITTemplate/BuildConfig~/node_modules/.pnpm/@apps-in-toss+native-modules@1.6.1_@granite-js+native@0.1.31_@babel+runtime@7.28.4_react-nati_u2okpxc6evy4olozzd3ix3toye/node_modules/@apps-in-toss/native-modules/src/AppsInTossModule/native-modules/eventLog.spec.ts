import { describe, beforeEach, it, expect, vi, afterEach } from 'vitest';

describe('eventLog', () => {
  const mockEventLog = vi.fn();
  let originalConsoleLog: typeof console.log;

  // 테스트 전에 모킹 설정
  beforeEach(() => {
    // 각 테스트 전 모듈 캐시 초기화
    vi.resetModules();

    // 기본 모킹 설정
    vi.doMock('react-native', () => ({
      NativeModules: {
        AppsInTossModule: {
          eventLog: mockEventLog,
          tossAppVersion: '5.208.0',
          operationalEnvironment: 'toss',
        },
      },
      Platform: {
        OS: 'ios',
      },
    }));

    originalConsoleLog = console.log;
    console.log = vi.fn();
  });

  afterEach(() => {
    console.log = originalConsoleLog;
    vi.restoreAllMocks();
    mockEventLog.mockReset();
  });

  it('샌드박스 환경에서는 콘솔에 로그를 출력하고 네이티브 모듈을 호출하지 않아야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'sandbox',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'info' as const,
      params: {
        key1: 'value1',
        key2: 123,
        key3: true,
        key4: undefined,
      },
    };

    await eventLog(params);

    expect(console.log).toHaveBeenCalledWith('[eventLogDebug]', {
      log_name: 'test_log',
      log_type: 'info',
      params: {
        key1: 'value1',
        key2: '123',
        key3: 'true',
      },
    });
    expect(mockEventLog).not.toHaveBeenCalled();
  });

  it('토스 환경에서는 네이티브 모듈을 호출해야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'error' as const,
      params: {
        key1: 'value1',
        key2: 123,
        key3: true,
        key4: null,
        key5: undefined,
      },
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'error',
      params: {
        key1: 'value1',
        key2: '123',
        key3: 'true',
        key4: 'null',
      },
    });
    expect(console.log).not.toHaveBeenCalled();
  });

  it('undefined 값은 정규화된 파라미터에서 제외되어야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'debug' as const,
      params: {
        key1: 'value1',
        key2: undefined,
      },
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'debug',
      params: {
        key1: 'value1',
      },
    });
  });

  it('모든 값은 문자열로 변환되어야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'warn' as const,
      params: {
        number: 123,
        boolean: false,
        nullValue: null,
        symbol: Symbol('test').toString(),
      },
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'warn',
      params: {
        number: '123',
        boolean: 'false',
        nullValue: 'null',
        symbol: 'Symbol(test)',
      },
    });
  });

  it('빈 객체가 전달되면 빈 객체를 반환해야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'info' as const,
      params: {},
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'info',
      params: {},
    });
  });

  it('여러 개의 undefined 값이 있는 경우 모두 제외되어야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'debug' as const,
      params: {
        key1: undefined,
        key2: undefined,
        key3: 'value3',
        key4: undefined,
      },
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'debug',
      params: {
        key3: 'value3',
      },
    });
  });

  it('숫자 0과 빈 문자열은 정상적으로 문자열로 변환되어야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const params = {
      log_name: 'test_log',
      log_type: 'info' as const,
      params: {
        zeroNumber: 0,
        emptyString: '',
        falseValue: false,
      },
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'info',
      params: {
        zeroNumber: '0',
        emptyString: '',
        falseValue: 'false',
      },
    });
  });

  it('복잡한 심볼 값도 문자열로 적절히 변환되어야 한다', async () => {
    vi.doMock('./AppsInTossModule.js', () => ({
      AppsInTossModule: {
        eventLog: mockEventLog,
        tossAppVersion: '5.208.0',
        operationalEnvironment: 'toss',
      },
    }));

    const { eventLog } = await import('./eventLog.js');

    const testSymbol = Symbol('복잡한_테스트');

    const params = {
      log_name: 'test_log',
      log_type: 'debug' as const,
      params: {
        symbolValue: testSymbol.toString(),
      },
    };

    await eventLog(params);

    expect(mockEventLog).toHaveBeenCalledWith({
      log_name: 'test_log',
      log_type: 'debug',
      params: {
        symbolValue: 'Symbol(복잡한_테스트)',
      },
    });
  });
});
