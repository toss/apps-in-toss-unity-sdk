import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { INTERNAL__appBridgeHandler } from './appBridge.js';

const mocks = vi.hoisted(() => {
  const mockedNativeMethodName = 'mockedNativeMethod';
  const mockedReturnValue = new Date().getTime();
  const mockedNativeMethod = vi.fn().mockImplementation(async () => ({ value: mockedReturnValue }));

  return { mockedNativeMethodName, mockedReturnValue, mockedNativeMethod };
});

vi.mock('react-native', () => {
  return {
    NativeModules: {
      AppsInTossModule: {
        [mocks.mockedNativeMethodName]: mocks.mockedNativeMethod,
      },
    },
  };
});

describe('appBridge', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('invokeAppBridgeMethod', () => {
    it('should register callbacks', async () => {
      await new Promise((resolve, reject) => {
        INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
          mocks.mockedNativeMethodName,
          {},
          {
            onSuccess: resolve,
            onError: reject,
            onFoo: () => {},
            onBar: () => {},
            onBaz: () => {},
          }
        );
      });

      // `onSuccess` and `onError` aren't registered as callbacks
      // because they are used as the resolve and reject of the promise.
      //
      // `onFoo`, `onBar`, `onBaz` are registered as callbacks (length = 3)
      expect(INTERNAL__appBridgeHandler.getCallbackIds()).toHaveLength(3);
    });

    it('should invoke the native method with the correct parameters', async () => {
      const task = new Promise((resolve, reject) => {
        INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
          mocks.mockedNativeMethodName,
          {
            value_1: 1,
            value_2: 2,
            value_3: 3,
          },
          {
            onSuccess: resolve,
            onError: reject,
            onFoo: () => {},
            onBar: () => {},
            onBaz: () => {},
          }
        );
      });

      await expect(task).resolves.toEqual({ value: mocks.mockedReturnValue });
      expect(mocks.mockedNativeMethod).toHaveBeenCalledTimes(1);
      expect(mocks.mockedNativeMethod).toHaveBeenCalledWith({
        params: {
          value_1: 1,
          value_2: 2,
          value_3: 3,
        },
        callbacks: {
          // Callbacks are replaced with their unique IDs
          onFoo: expect.any(String),
          onBar: expect.any(String),
          onBaz: expect.any(String),
        },
      });
    });

    it('when the native method is rejected, the error callback is called', async () => {
      mocks.mockedNativeMethod.mockImplementationOnce(() => Promise.reject(new Error('Mocked error')));

      const task = new Promise((resolve, reject) => {
        INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
          mocks.mockedNativeMethodName,
          {},
          {
            onSuccess: resolve,
            onError: reject,
          }
        );
      });

      await expect(task).rejects.toThrowErrorMatchingInlineSnapshot(`[Error: Mocked error]`);
    });
  });

  describe('invokeAppBridgeCallback', () => {
    const CALLBACK_NAME = 'onTest';
    let mockedCallback: Mock;

    // Register a callback before invoking the callback by its ID.
    beforeEach(() => {
      mockedCallback = vi.fn();

      return new Promise<void>((resolve, reject) => {
        INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
          mocks.mockedNativeMethodName,
          {},
          {
            onSuccess: resolve,
            onError: reject,
            [CALLBACK_NAME]: mockedCallback,
          }
        );
      });
    });

    it('should invoke the callback with the correct parameters', async () => {
      const callbackId = INTERNAL__appBridgeHandler.getCallbackIds().pop()!;
      const mockedValue = new Date().getTime();

      INTERNAL__appBridgeHandler.invokeAppBridgeCallback(callbackId, { value: mockedValue });

      expect(mockedCallback).toHaveBeenCalledTimes(1);
      expect(mockedCallback).toHaveBeenCalledWith({ value: mockedValue });
    });
  });
});
