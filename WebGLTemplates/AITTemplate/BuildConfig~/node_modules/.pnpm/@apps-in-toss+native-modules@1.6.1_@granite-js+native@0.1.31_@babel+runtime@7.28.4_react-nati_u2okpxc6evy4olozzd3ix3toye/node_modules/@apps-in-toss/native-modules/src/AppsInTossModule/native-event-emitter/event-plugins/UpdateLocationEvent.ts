import { Accuracy, GetCurrentLocationPermissionError, Location } from '@apps-in-toss/types';
import { GraniteEventDefinition } from '@granite-js/react-native';
import { AppsInTossModuleInstance } from '../../native-modules/AppsInTossModule';
import { requestPermission } from '../../native-modules/permissions/requestPermission';
import { nativeEventEmitter } from '../nativeEventEmitter';

export interface StartUpdateLocationOptions {
  /**
   * 위치 정확도를 설정해요.
   */
  accuracy: Accuracy;
  /**
   * 위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.
   */
  timeInterval: number;
  /**
   * 위치 변경 거리를 미터(m) 단위로 설정해요.
   */
  distanceInterval: number;
}

export class UpdateLocationEvent extends GraniteEventDefinition<StartUpdateLocationOptions, Location> {
  name = 'updateLocationEvent' as const;

  subscriptionCount = 0;

  ref = {
    remove: () => {},
  };
  remove() {
    if (--this.subscriptionCount === 0) {
      AppsInTossModuleInstance.stopUpdateLocation({});
    }
    this.ref.remove();
  }

  listener(
    options: StartUpdateLocationOptions,
    onEvent: (response: Location) => void,
    onError: (error: unknown) => void
  ): void {
    requestPermission({ name: 'geolocation', access: 'access' })
      .then((permissionStatus) => {
        if (permissionStatus === 'denied') {
          onError(new GetCurrentLocationPermissionError());
          return;
        }

        // @internal
        void AppsInTossModuleInstance.startUpdateLocation(options).catch(onError);
        const subscription = nativeEventEmitter.addListener('updateLocation', onEvent);

        this.ref = {
          remove: () => subscription?.remove(),
        };
        this.subscriptionCount++;
      })
      .catch(onError);
  }
}
