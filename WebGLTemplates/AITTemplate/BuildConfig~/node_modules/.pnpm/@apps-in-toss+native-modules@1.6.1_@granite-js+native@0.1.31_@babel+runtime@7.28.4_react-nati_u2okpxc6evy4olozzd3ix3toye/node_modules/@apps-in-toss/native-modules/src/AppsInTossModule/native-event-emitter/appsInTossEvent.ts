import { GraniteEvent } from '@granite-js/react-native';
import { UpdateLocationEvent } from './event-plugins/UpdateLocationEvent';
import { AppBridgeCallbackEvent } from './internal/AppBridgeCallbackEvent';
import { VisibilityChangedByTransparentServiceWebEvent } from './internal/VisibilityChangedByTransparentServiceWebEvent';

export const appsInTossEvent = new GraniteEvent([
  new UpdateLocationEvent(),

  // Internal events
  new AppBridgeCallbackEvent(),
  new VisibilityChangedByTransparentServiceWebEvent(),
]);
