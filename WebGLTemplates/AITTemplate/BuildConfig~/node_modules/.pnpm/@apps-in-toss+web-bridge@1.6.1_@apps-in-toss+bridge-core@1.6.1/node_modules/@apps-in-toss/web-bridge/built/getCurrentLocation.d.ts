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
export interface GetCurrentLocationOptions {
	/**
	 * 위치 정보를 가져올 정확도 수준이에요.
	 */
	accuracy: Accuracy;
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
export type GetCurrentLocation = (options: GetCurrentLocationOptions) => Promise<Location$1>;
type PermissionStatus$1 = "notDetermined" | "denied" | "allowed";
export type PermissionDialogFunction = () => Promise<Exclude<PermissionStatus$1, "notDetermined">>;
export type GetPermissionFunction = () => Promise<PermissionStatus$1>;
export type PermissionFunctionWithDialog<T extends (...args: any[]) => any> = T & {
	getPermission: GetPermissionFunction;
	openPermissionDialog: PermissionDialogFunction;
};
/**
 * @public
 * @category 위치 정보
 * @name getCurrentLocation
 * @description 디바이스의 현재 위치 정보를 가져오는 함수예요.
 * 위치 기반 서비스를 구현할 때 사용되고, 한 번만 호출되어 현재 위치를 즉시 반환해요.
 * 예를 들어 지도 앱에서 사용자의 현재 위치를 한 번만 가져올 때, 날씨 앱에서 사용자의 위치를 기반으로 기상 정보를 제공할 때, 매장 찾기 기능에서 사용자의 위치를 기준으로 가까운 매장을 검색할 때 사용하면 유용해요.
 *
 * @param {GetCurrentLocationOptions} options 위치 정보를 가져올 때 사용하는 옵션 객체예요.
 * @param {Accuracy} [options.accuracy] 위치 정보의 정확도 수준이에요. 정확도는 `Accuracy` 타입으로 설정돼요.
 * @property [openPermissionDialog] - 위치 정보 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 위치 정보 권한의 현재 상태를 반환해요. `allowed`는 사용자가 위치 정보 권한을 허용한 상태예요. `denied`는 사용자가 위치 정보 권한을 거부한 상태예요. `notDetermined`는 위치 정보 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<Location>} 디바이스의 위치 정보가 담긴 객체를 반환해요. 자세한 내용은 [Location](/react-native/reference/native-modules/Types/Location.html)을 참고해주세요.
 *
 * @signature
 * ```typescript
 * function getCurrentLocation(options: {
 *   accuracy: Accuracy;
 * }): Promise<Location>;
 * ```
 *
 * @example
 * ### 디바이스의 현재 위치 정보 가져오기
 *
 * "권한 확인하기"버튼을 눌러서 현재 위치정보 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`GetCurrentLocationPermissionError`](/react-native/reference/types/권한/GetCurrentLocationPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 위치정보 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { Accuracy, getCurrentLocation, Location } from '@apps-in-toss/web-framework';
 * import { useState } from 'react';
 * 
 *
 * // 현재 위치 정보를 가져와 화면에 표시하는 컴포넌트
 * function CurrentPosition() {
 *   const [position, setPosition] = useState<Location | null>(null);
 *
 *   const handlePress = async () => {
 *     try {
 *       const response = await getCurrentLocation({ accuracy: Accuracy.Balanced });
 *       setPosition(response);
 *     } catch (error) {
 *       console.error('위치 정보를 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <div>
 *       {position ? (
 *         <span>
 *           위치: {position.coords.latitude}, {position.coords.longitude}
 *         </span>
 *       ) : (
 *         <span>위치 정보를 아직 가져오지 않았어요</span>
 *       )}
 *       <input type="button" value="현재 위치 정보 가져오기" onClick={handlePress} />
 *       <input type="button"
 *         value="권한 확인하기"
 *         onClick={async () => {
 *           Alert.alert(await getCurrentLocation.getPermission());
 *         }}
 *       />
 *       <input type="button"
 *         value="권한 요청하기"
 *         onClick={async () => {
 *           Alert.alert(await getCurrentLocation.openPermissionDialog());
 *         }}
 *       />
 *     </div>
 *   );
 * }
 *
 * ```
 */
export declare const getCurrentLocation: PermissionFunctionWithDialog<GetCurrentLocation>;

export {};
