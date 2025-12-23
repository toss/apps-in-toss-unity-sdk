/* eslint-disable @typescript-eslint/naming-convention */
import { GraniteEventDefinition } from '@granite-js/react-native';
import { EmitterSubscription, NativeEventEmitter } from 'react-native';
import { INTERNAL__appBridgeHandler } from './appBridge';
import { nativeEventEmitter } from '../nativeEventEmitter';

export interface AppBridgeCallbackResult {
  name: string;
  params?: any;
}

const UNSAFE__nativeEventEmitter = nativeEventEmitter as unknown as NativeEventEmitter;

export class AppBridgeCallbackEvent extends GraniteEventDefinition<void, AppBridgeCallbackResult> {
  private static INTERNAL__appBridgeSubscription?: EmitterSubscription;

  name = 'appBridgeCallbackEvent' as const;

  constructor() {
    super();
    this.registerAppBridgeCallbackEventListener();
  }

  remove() {}
  listener() {}

  private registerAppBridgeCallbackEventListener() {
    if (AppBridgeCallbackEvent.INTERNAL__appBridgeSubscription != null) {
      return;
    }

    AppBridgeCallbackEvent.INTERNAL__appBridgeSubscription = UNSAFE__nativeEventEmitter.addListener(
      'appBridgeCallback',
      this.ensureInvokeAppBridgeCallback
    );
  }

  private ensureInvokeAppBridgeCallback(result: AppBridgeCallbackResult) {
    if (typeof result === 'object' && typeof result.name === 'string') {
      INTERNAL__appBridgeHandler.invokeAppBridgeCallback(result.name, result.params);
    } else {
      console.warn('Invalid app bridge callback result:', result);
    }
  }
}
