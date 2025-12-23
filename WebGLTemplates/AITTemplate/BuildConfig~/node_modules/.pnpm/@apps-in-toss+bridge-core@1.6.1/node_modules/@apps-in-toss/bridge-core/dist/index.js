// src/nativeWindow.ts
var NativeWindow = class {
  get _window() {
    if (typeof window !== "undefined") {
      return window;
    }
    return {
      ReactNativeWebView: {
        postMessage: () => {
        }
      },
      __GRANITE_NATIVE_EMITTER: {
        on: () => () => {
        }
      },
      __CONSTANT_HANDLER_MAP: {}
    };
  }
  postMessage(message) {
    const webView = this._window.ReactNativeWebView;
    if (!webView) {
      throw new Error("ReactNativeWebView is not available in browser environment");
    }
    webView.postMessage(JSON.stringify(message));
  }
  on(event, callback) {
    const emitter = this._window.__GRANITE_NATIVE_EMITTER;
    if (!emitter) {
      throw new Error("__GRANITE_NATIVE_EMITTER is not available");
    }
    return emitter.on(event, callback);
  }
  getConstant(method) {
    const constantHandlerMap = this._window.__CONSTANT_HANDLER_MAP;
    if (constantHandlerMap && method in constantHandlerMap) {
      return constantHandlerMap[method];
    }
    throw new Error(`${method} is not a constant handler`);
  }
};
var nativeWindow = new NativeWindow();
var createEventId = () => Math.random().toString(36).substring(2, 15);
var deserializeError = (value) => {
  if (value && value.__isError) {
    const err = new Error(value.message);
    for (const [key, val] of Object.entries(value)) {
      err[key] = val;
    }
    return err;
  }
  return value;
};

// src/createAsyncBridge.ts
function createAsyncBridge(method) {
  return (...args) => {
    const eventId = createEventId();
    const emitters = [];
    const unsubscribe = () => {
      for (const remove of emitters) {
        remove();
      }
    };
    return new Promise((resolve, reject) => {
      emitters.push(
        nativeWindow.on(`${method}/resolve/${eventId}`, (data) => {
          unsubscribe();
          resolve(data);
        })
      );
      emitters.push(
        nativeWindow.on(`${method}/reject/${eventId}`, (error) => {
          unsubscribe();
          reject(deserializeError(error));
        })
      );
      nativeWindow.postMessage({
        type: "method",
        functionName: method,
        eventId,
        args
      });
    });
  };
}

// src/createEventBridge.ts
function createEventBridge(method) {
  return (args) => {
    const eventId = createEventId();
    const removes = [
      nativeWindow.on(`${method}/onEvent/${eventId}`, (data) => {
        args.onEvent(data);
      }),
      nativeWindow.on(`${method}/onError/${eventId}`, (error) => {
        args.onError(deserializeError(error));
      })
    ];
    nativeWindow.postMessage({
      type: "addEventListener",
      functionName: method,
      eventId,
      args: args.options
    });
    return () => {
      nativeWindow.postMessage({
        type: "removeEventListener",
        functionName: method,
        eventId
      });
      removes.forEach((remove) => remove());
    };
  };
}

// src/createConstantBridge.ts
function createConstantBridge(method) {
  return () => {
    return nativeWindow.getConstant(method);
  };
}
export {
  createAsyncBridge,
  createConstantBridge,
  createEventBridge
};
