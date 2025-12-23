import { GetCurrentLocation, GetCurrentLocationPermissionError } from '@apps-in-toss/types';
import { AppsInTossModule } from '../../AppsInTossModule';
import { createPermissionFunction } from '../createPermissionFunction';

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
 * import { Accuracy, getCurrentLocation, Location } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Text, View } from 'react-native';
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
 *     <View>
 *       {position ? (
 *         <Text>
 *           위치: {position.coords.latitude}, {position.coords.longitude}
 *         </Text>
 *       ) : (
 *         <Text>위치 정보를 아직 가져오지 않았어요</Text>
 *       )}
 *       <Button title="현재 위치 정보 가져오기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           Alert.alert(await getCurrentLocation.getPermission());
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           Alert.alert(await getCurrentLocation.openPermissionDialog());
 *         }}
 *       />
 *     </View>
 *   );
 * }
 *
 * ```
 */
export const getCurrentLocation = createPermissionFunction<GetCurrentLocation>({
  handler: async (options) => {
    return AppsInTossModule.getCurrentLocation(options);
  },
  permission: {
    name: 'geolocation',
    access: 'access',
  },
  error: GetCurrentLocationPermissionError,
});
