/**
 * @public
 * @category 위치 정보
 * @name Accuracy
 * @description 위치 정확도 옵션이에요.
 */
export enum Accuracy {
  /**
   * 오차범위 3KM 이내
   */
  Lowest = 1,
  /**
   * 오차범위 1KM 이내
   */
  Low,
  /**
   * 오차범위 몇 백미터 이내
   */
  Balanced,
  /**
   * 오차범위 10M 이내
   */
  High,
  /**
   * 가장 높은 정확도
   */
  Highest,
  /**
   * 네비게이션을 위한 최고 정확도
   */
  BestForNavigation,
}

export interface GetCurrentLocationOptions {
  /**
   * 위치 정보를 가져올 정확도 수준이에요.
   */
  accuracy: Accuracy;
}

/**
 * @public
 * @category 위치 정보
 * @name Location
 * @description 위치 정보를 나타내는 객체예요.
 */
export interface Location {
  /**
   * Android에서만 지원하는 옵션이에요.
   *
   * - `FINE`: 정확한 위치
   * - `COARSE`: 대략적인 위치
   *
   * @see https://developer.android.com/codelabs/approximate-location
   */
  accessLocation?: 'FINE' | 'COARSE';
  /**
   * 위치가 업데이트된 시점의 유닉스 타임스탬프예요.
   */
  timestamp: number;
  /**
   * @description 위치 정보를 나타내는 객체예요. 자세한 내용은 [LocationCoords](/react-native/reference/native-modules/Types/LocationCoords.html)을 참고해주세요.
   */
  coords: LocationCoords;
}

/**
 * @public
 * @category 위치 정보
 * @name LocationCoords
 * @description 세부 위치 정보를 나타내는 객체예요.
 */
export interface LocationCoords {
  /**
   * 위도
   */
  latitude: number;
  /**
   * 경도
   */
  longitude: number;
  /**
   * 높이
   */
  altitude: number;
  /**
   * 위치 정확도
   */
  accuracy: number;
  /**
   * 고도 정확도
   */
  altitudeAccuracy: number;
  /**
   * 방향
   */
  heading: number;
}

export type GetCurrentLocation = (options: GetCurrentLocationOptions) => Promise<Location>;
