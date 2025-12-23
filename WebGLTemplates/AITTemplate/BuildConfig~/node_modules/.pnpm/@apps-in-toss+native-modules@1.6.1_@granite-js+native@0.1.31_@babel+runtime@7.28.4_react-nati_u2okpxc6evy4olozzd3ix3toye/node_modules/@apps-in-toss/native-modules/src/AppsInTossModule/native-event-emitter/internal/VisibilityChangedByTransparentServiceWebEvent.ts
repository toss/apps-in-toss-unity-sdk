import { GraniteEventDefinition } from '@granite-js/react-native';
import type { EmitterSubscription } from 'react-native';
import { nativeEventEmitter } from '../nativeEventEmitter';

export interface VisibilityChangedByTransparentServiceWebOptions {
  callbackId: string;
}

export interface VisibilityChangedByTransparentServiceWebResult {
  callbackId: string;
  isVisible: boolean;
}

export class VisibilityChangedByTransparentServiceWebEvent extends GraniteEventDefinition<
  VisibilityChangedByTransparentServiceWebOptions,
  boolean
> {
  name = 'onVisibilityChangedByTransparentServiceWeb' as const;

  subscription: EmitterSubscription | null = null;

  remove() {
    this.subscription?.remove();
    this.subscription = null;
  }

  listener(
    options: VisibilityChangedByTransparentServiceWebOptions,
    onEvent: (isVisible: boolean) => void,
    onError: (error: unknown) => void
  ) {
    const subscription = nativeEventEmitter.addListener('visibilityChangedByTransparentServiceWeb', (params) => {
      if (this.isVisibilityChangedByTransparentServiceWebResult(params)) {
        if (params.callbackId === options.callbackId) {
          onEvent(params.isVisible);
        }
      } else {
        onError(new Error('Invalid visibility changed by transparent service web result'));
      }
    });

    this.subscription = subscription;
  }

  private isVisibilityChangedByTransparentServiceWebResult(
    params: any
  ): params is VisibilityChangedByTransparentServiceWebResult {
    return typeof params === 'object' && typeof params.callbackId === 'string' && typeof params.isVisible === 'boolean';
  }
}
