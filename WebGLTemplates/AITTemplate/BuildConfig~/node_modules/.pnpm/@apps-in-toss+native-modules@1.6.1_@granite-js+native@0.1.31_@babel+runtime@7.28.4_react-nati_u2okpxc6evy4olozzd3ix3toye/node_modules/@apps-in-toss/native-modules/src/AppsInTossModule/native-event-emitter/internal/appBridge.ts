/* eslint-disable @typescript-eslint/naming-convention */
import { generateUUID } from '../../../utils/generateUUID';
import { AppsInTossModuleInstance } from '../../native-modules/AppsInTossModule';

export interface AppBridgeCompatCallbacks<Result> {
  onSuccess: (result: Result) => void;
  onError: (reason: unknown) => void;
}

type AppBridgeCallback = (...args: any[]) => void;
type AppBridgeCallbackId = string;

const INTERNAL__callbacks = new Map<AppBridgeCallbackId, AppBridgeCallback>();

function invokeAppBridgeCallback(id: string, ...args: any[]): boolean {
  const callback = INTERNAL__callbacks.get(id);

  callback?.call(null, ...args);

  return Boolean(callback);
}

function invokeAppBridgeMethod<Result = any, Params = any>(
  methodName: string,
  params: Params,
  callbacks: AppBridgeCompatCallbacks<Result> & Record<string, AppBridgeCallback>
) {
  const { onSuccess, onError, ...appBridgeCallbacks } = callbacks;
  const { callbackMap, unregisterAll } = registerCallbacks(appBridgeCallbacks);
  const method = AppsInTossModuleInstance[methodName];

  if (method == null) {
    onError(new Error(`'${methodName}' is not defined in AppsInTossModule`));
    return unregisterAll;
  }

  const promise = method({
    params,
    callbacks: callbackMap,
  }) as Promise<Result>;

  void promise.then(onSuccess).catch(onError);

  return unregisterAll;
}

function registerCallbacks(callbacks: Record<string, AppBridgeCallback>) {
  const callbackMap: Record<string, AppBridgeCallbackId> = {};

  for (const [callbackName, callback] of Object.entries(callbacks)) {
    const id = registerCallback(callback, callbackName);
    callbackMap[callbackName] = id;
  }

  const unregisterAll = () => {
    Object.values(callbackMap).forEach(unregisterCallback);
  };

  return { callbackMap, unregisterAll };
}

function registerCallback(callback: AppBridgeCallback, name = 'unnamed') {
  const uniqueId = generateUUID();
  const callbackId = `${uniqueId}__${name}`;

  INTERNAL__callbacks.set(callbackId, callback);

  return callbackId;
}

function unregisterCallback(id: string) {
  INTERNAL__callbacks.delete(id);
}

function getCallbackIds() {
  return Array.from(INTERNAL__callbacks.keys());
}

export const INTERNAL__appBridgeHandler = {
  invokeAppBridgeCallback,
  invokeAppBridgeMethod,
  registerCallback,
  unregisterCallback,
  getCallbackIds,
};
