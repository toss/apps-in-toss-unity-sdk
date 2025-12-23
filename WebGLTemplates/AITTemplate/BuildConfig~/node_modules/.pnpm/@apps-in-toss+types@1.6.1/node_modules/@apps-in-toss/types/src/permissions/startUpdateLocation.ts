import { Accuracy, Location } from './getCurrentLocation';

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

export type StartUpdateLocationEventParams = {
  onEvent: (response: Location) => void;
  onError: (error: unknown) => void;
  options: StartUpdateLocationOptions;
};

export type StartUpdateLocation = (eventParams: StartUpdateLocationEventParams) => () => void;
