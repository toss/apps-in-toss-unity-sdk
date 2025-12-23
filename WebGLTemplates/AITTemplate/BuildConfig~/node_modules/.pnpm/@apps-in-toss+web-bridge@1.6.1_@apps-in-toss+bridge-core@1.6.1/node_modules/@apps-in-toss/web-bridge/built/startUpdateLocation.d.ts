declare enum Accuracy {
	/**
	 * 오차범위 3KM 이내
	 */
	Lowest = 1,
	/**
	 * 오차범위 1KM 이내
	 */
	Low = 2,
	/**
	 * 오차범위 몇 백미터 이내
	 */
	Balanced = 3,
	/**
	 * 오차범위 10M 이내
	 */
	High = 4,
	/**
	 * 가장 높은 정확도
	 */
	Highest = 5,
	/**
	 * 네비게이션을 위한 최고 정확도
	 */
	BestForNavigation = 6
}
interface Location$1 {
	/**
	 * Android에서만 지원하는 옵션이에요.
	 *
	 * - `FINE`: 정확한 위치
	 * - `COARSE`: 대략적인 위치
	 *
	 * @see https://developer.android.com/codelabs/approximate-location
	 */
	accessLocation?: "FINE" | "COARSE";
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
	onEvent: (response: Location$1) => void;
	onError: (error: unknown) => void;
	options: StartUpdateLocationOptions;
};
type PermissionStatus$1 = "notDetermined" | "denied" | "allowed";
export type PermissionDialogFunction = () => Promise<Exclude<PermissionStatus$1, "notDetermined">>;
export type GetPermissionFunction = () => Promise<PermissionStatus$1>;
export interface EventEmitterSchema<K extends string, P extends unknown[]> {
	name: K;
	params: P;
}
/**
 * @name UpdateLocationEventEmitter
 * @kind typedef
 * @description
 * 디바이스의 위치 정보 변경을 감지해요
 */
export type UpdateLocationEventEmitter = EventEmitterSchema<"updateLocation", [
	Location$1
]>;
/**
 * @public
 * @category 위치 정보
 * @name startUpdateLocation
 * @description  디바이스의 위치 정보를 지속적으로 감지하고, 위치가 변경되면 콜백을 실행하는 함수예요. 콜백 함수를 등록하면 위치가 변경될 때마다 자동으로 호출돼요.
 * 실시간 위치 추적이 필요한 기능을 구현할 때 사용할 수 있어요. 예를 들어 지도 앱에서 사용자의 현재 위치를 실시간으로 업데이트할 때, 운동 앱에서 사용자의 이동 거리를 기록할 때 등이에요.
 * 위치 업데이트 주기와 정확도를 조정해 배터리 소모를 최소화하면서도 필요한 정보를 얻을 수 있어요.
 *
 *
 * @param {(error: unknown) => void} onError 위치 정보 감지에 실패했을 때 호출되는 콜백 함수예요.
 * @param {(location: Location) => void} onEvent 위치 정보가 변경될 때 호출되는 콜백 함수예요. 자세한 내용은 [Location](/react-native/reference/native-modules/Types/Location.html)을 참고해주세요.
 * @param {StartUpdateLocationOptions} options - 위치 정보 감지에 필요한 설정 객체에요.
 * @param {number} [options.accuracy] 위치 정확도를 설정해요.
 * @param {number} [options.timeInterval] 위치 정보를 업데이트하는 최소 주기로, 단위는 밀리초(ms)예요. 이 값은 위치 업데이트가 발생하는 가장 짧은 간격을 설정하지만, 시스템이나 환경의 영향을 받아 지정한 주기보다 더 긴 간격으로 업데이트될 수 있어요.
 * @param {number} [options.distanceInterval] 위치 변경 거리를 미터(m) 단위로 설정해요.
 *
 * @property [openPermissionDialog] - 위치 정보 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 위치 정보 권한의 현재 상태를 반환해요. `allowed`는 사용자가 위치 정보 권한을 허용한 상태예요. `denied`는 사용자가 위치 정보 권한을 거부한 상태예요. `notDetermined`는 위치 정보 권한 요청을 한 번도 하지 않은 상태예요.
 *
 * @signature
 * ```typescript
 * function startUpdateLocation(options: {
 *   onError: (error: unknown) => void;
 *   onEvent: (location: Location) => void;
 *   options: StartUpdateLocationOptions;
 * }): () => void;
 * ```
 *
 * @example
 * ### 위치 정보 변경 감지하기
 *
 * 위치 정보가 변경되는것을 감지하는 예제예요. "위치 정보 변경 감지하기"를 눌러서 감지할 수 있어요.
 *
 * "권한 확인하기"버튼을 눌러서 현재 위치 정보 변경 감지 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`StartUpdateLocationPermissionError`](/react-native/reference/types/권한/StartUpdateLocationPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 위치 정보 변경 감지 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { Accuracy, Location, startUpdateLocation, StartUpdateLocationPermissionError } from '@apps-in-toss/web-framework';
 * import { useCallback, useState } from 'react';
 * 
 *
 * // 위치 정보 변경 감지하기
 * function LocationWatcher() {
 *   const [location, setLocation] = useState<Location | null>(null);
 *
 *   const handlePress = useCallback(() => {
 *     startUpdateLocation({
 *       options: {
 *         accuracy: Accuracy.Balanced,
 *         timeInterval: 3000,
 *         distanceInterval: 10,
 *       },
 *       onEvent: (location) => {
 *         setLocation(location);
 *       },
 *       onError: (error) => {
 *         if (error instanceof StartUpdateLocationPermissionError) {
 *           // 위치 정보 변경 감지 권한 없음
 *         }
 *         console.error('위치 정보를 가져오는데 실패했어요:', error);
 *       },
 *     });
 *   }, []);
 *
 *   return (
 *     <div>
 *       {location != null && (
 *         <>
 *           <span>위도: {location.coords.latitude}</span>
 *           <span>경도: {location.coords.longitude}</span>
 *           <span>위치 정확도: {location.coords.accuracy}m</span>
 *           <span>높이: {location.coords.altitude}m</span>
 *           <span>고도 정확도: {location.coords.altitudeAccuracy}m</span>
 *           <span>방향: {location.coords.heading}°</span>
 *         </>
 *       )}
 *
 *       <input type="button" value="위치 정보 변경 감지하기" onClick={handlePress} />
 *
 *       <input type="button"
 *         value="권한 확인하기"
 *         onClick={async () => {
 *           const permission = await startUpdateLocation.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <input type="button"
 *         value="권한 요청하기"
 *         onClick={async () => {
 *           const permission = await startUpdateLocation.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </div>
 *   );
 * }
 * ```
 */
export declare function startUpdateLocation(eventParams: StartUpdateLocationEventParams): () => void;
export declare namespace startUpdateLocation {
	var openPermissionDialog: PermissionDialogFunction;
	var getPermission: GetPermissionFunction;
}

export {};
