import { OpenCameraPermissionError, type OpenCamera } from '@apps-in-toss/types';
import { AppsInTossModule } from '../../AppsInTossModule';
import { createPermissionFunction } from '../createPermissionFunction';

/**
 * @public
 * @category 카메라
 * @name openCamera
 * @description 카메라를 실행해서 촬영된 이미지를 반환하는 함수예요.
 * @param {OpenCameraOptions} options - 카메라 실행 시 사용되는 옵션 객체예요.
 * @param {boolean} [options.base64=false] - 이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값은 `false`예요. `true`로 설정하면 `dataUri` 대신 Base64 인코딩된 문자열을 반환해요.
 * @param {number} [options.maxWidth=1024] - 이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값은 `1024`예요.
 * @property [openPermissionDialog] - 카메라 접근 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 카메라 접근 권한의 현재 상태를 반환해요. `allowed`는 사용자가 카메라 접근 권한을 허용한 상태예요. `denied`는 사용자가 카메라 접근 권한을 거부한 상태예요. `notDetermined`는 카메라 접근 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<ImageResponse>}
 * 촬영된 이미지 정보를 포함한 객체를 반환해요. 반환 객체의 구성은 다음과 같아요:
 * - `id`: 이미지의 고유 식별자예요.
 * - `dataUri`: 이미지 데이터를 표현하는 데이터 URI예요.
 *
 * @signature
 * ```typescript
 * function openCamera(options: {
 *   base64: boolean;
 *   maxWidth: number;
 * }): Promise<ImageResponse>;
 * ```
 *
 * @example
 * ### 카메라 실행 후 촬영된 사진 가져오기
 *
 * 카메라로 사진을 찍고 결과를 가져오는 예제예요.
 * 이 과정에서 현재 카메라 권한 상태를 확인할 수 있고, 권한이 없으면 권한을 요청해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`OpenCameraPermissionError`](/react-native/reference/types/권한/OpenCameraPermissionError)를 반환해요.
 *
 * ```tsx
 * import { ImageResponse, openCamera, OpenCameraPermissionError } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Image, Text, View } from 'react-native';
 *
 * const base64 = true;
 *
 * // 카메라를 실행하고 촬영된 이미지를 화면에 표시하는 컴포넌트
 * function Camera() {
 *   const [image, setImage] = useState<ImageResponse | null>(null);
 *
 *   const handlePress = async () => {
 *     try {
 *       const response = await openCamera({ base64 });
 *       setImage(response);
 *     } catch (error) {
 *       if (error instanceof OpenCameraPermissionError) {
 *         console.log('권한 에러');
 *       }
 *
 *       console.error('사진을 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   // base64 형식으로 반환된 이미지를 표시하려면 데이터 URL 스키마 Prefix를 붙여야해요.
 *   const imageUri = base64 ? 'data:image/jpeg;base64,' + image?.dataUri : image?.dataUri;
 *
 *   return (
 *     <View>
 *       {image ? <Image source={{ uri: imageUri }} style={{ width: 200, height: 200 }} /> : <Text>사진이 없어요</Text>}
 *       <Button title="사진 촬영하기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await openCamera.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const currentPermission = await openCamera.openPermissionDialog();
 *           Alert.alert(currentPermission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
export const openCamera = createPermissionFunction<OpenCamera>({
  handler: (options) => {
    return AppsInTossModule.openCamera({
      base64: false,
      maxWidth: 1024,
      ...options,
    });
  },
  permission: {
    name: 'camera',
    access: 'access',
  },
  error: OpenCameraPermissionError,
});
