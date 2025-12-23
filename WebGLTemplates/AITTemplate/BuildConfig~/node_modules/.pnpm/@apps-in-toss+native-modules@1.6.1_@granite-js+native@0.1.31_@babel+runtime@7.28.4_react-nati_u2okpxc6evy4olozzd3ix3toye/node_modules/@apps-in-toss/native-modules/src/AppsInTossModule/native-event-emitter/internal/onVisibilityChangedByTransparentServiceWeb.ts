import type { EmitterSubscription } from 'react-native';
import { appsInTossEvent } from '../appsInTossEvent';
import type { EventEmitterSchema } from '../types';

export interface OnVisibilityChangedByTransparentServiceWebSubscription extends EmitterSubscription {
  remove: () => void;
}

export type OnVisibilityChangedByTransparentServiceWebEventEmitter = EventEmitterSchema<
  'visibilityChangedByTransparentServiceWeb',
  [boolean]
>;

export function onVisibilityChangedByTransparentServiceWeb(eventParams: {
  options: { callbackId: string };
  onEvent: (isVisible: boolean) => void;
  onError: (error: unknown) => void;
}): () => void {
  return appsInTossEvent.addEventListener('onVisibilityChangedByTransparentServiceWeb', eventParams);
}
