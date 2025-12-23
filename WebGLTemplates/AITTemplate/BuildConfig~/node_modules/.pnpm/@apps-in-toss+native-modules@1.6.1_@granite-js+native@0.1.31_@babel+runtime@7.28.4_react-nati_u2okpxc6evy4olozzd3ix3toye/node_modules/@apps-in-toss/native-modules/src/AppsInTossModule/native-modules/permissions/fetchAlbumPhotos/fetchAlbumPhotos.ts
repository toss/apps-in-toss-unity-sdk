import { FetchAlbumPhotos, FetchAlbumPhotosPermissionError } from '@apps-in-toss/types';
import { AppsInTossModule } from '../../AppsInTossModule';
import { createPermissionFunction } from '../createPermissionFunction';

const DEFAULT_MAX_COUNT = 10;
const DEFAULT_MAX_WIDTH = 1024;

/**
 * @public
 * @category 사진
 * @name fetchAlbumPhotos
 * @description
 * 사용자의 앨범에서 사진 목록을 불러오는 함수예요.
 * 최대 개수와 해상도를 설정할 수 있고 갤러리 미리보기, 이미지 선택 기능 등에 활용할 수 있어요.
 *
 * @param options - 조회 옵션을 담은 객체예요.
 * @param {number} [options.maxCount=10] 가져올 사진의 최대 개수를 설정해요. 숫자로 입력하며 기본값은 10이에요.
 * @param {number} [options.maxWidth=1024] 사진의 최대 폭을 제한해요. 단위는 픽셀이며 기본값은 `1024`이에요.
 * @param {boolean} [options.base64=false] 이미지를 base64 형식으로 반환할지 설정해요. 기본값은 `false`예요.
 * @property [openPermissionDialog] - 사진첩 읽기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 사진첩 읽기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 사진첩 읽기 권한을 허용한 상태예요. `denied`는 사용자가 사진첩 읽기 권한을 거부한 상태예요. `notDetermined`는 사진첩 읽기 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<ImageResponse[]>}
 * 앨범 사진의 고유 ID와 데이터 URI를 포함한 배열을 반환해요.
 *
 * @signature
 * ```typescript
 * function fetchAlbumPhotos(options: {
 *   maxCount: number;
 *   maxWidth: number;
 *   base64: boolean;
 * }): Promise<ImageResponse[]>;
 * ```
 *
 * @example
 * ### 사진의 최대 폭을 360px로 제한하여 가져오기
 *
 * 사진을 가져오는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 사진첩 읽기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`FetchAlbumPhotosPermissionError`](/react-native/reference/types/권한/FetchAlbumPhotosPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 사진첩 읽기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { fetchAlbumPhotos, FetchAlbumPhotosPermissionError, ImageResponse } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Image, View } from 'react-native';
 *
 * const base64 = true;
 *
 * // 앨범 사진 목록을 가져와 화면에 표시하는 컴포넌트
 * function AlbumPhotoList() {
 *   const [albumPhotos, setAlbumPhotos] = useState<ImageResponse[]>([]);
 *
 *   const handlePress = async () => {
 *     try {
 *       const response = await fetchAlbumPhotos({
 *         base64,
 *         maxWidth: 360,
 *       });
 *       setAlbumPhotos((prev) => [...prev, ...response]);
 *     } catch (error) {
 *       if (error instanceof FetchAlbumPhotosPermissionError) {
 *         // 앨범 읽기 권한 없음
 *       }
 *       console.error('앨범을 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       {albumPhotos.map((image) => {
 *         // base64 형식으로 반환된 이미지를 표시하려면 데이터 URL 스키마 Prefix를 붙여야해요.
 *         const imageUri = base64 ? 'data:image/jpeg;base64,' + image.dataUri : image.dataUri;
 *
 *         return <Image source={{ uri: imageUri }} key={image.id} />;
 *       })}
 *       <Button title="앨범 가져오기" onPress={handlePress} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await fetchAlbumPhotos.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await fetchAlbumPhotos.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */
export const fetchAlbumPhotos = createPermissionFunction<FetchAlbumPhotos>({
  handler: async (options) => {
    return AppsInTossModule.fetchAlbumPhotos({
      ...options,
      maxCount: options?.maxCount ?? DEFAULT_MAX_COUNT,
      maxWidth: options?.maxWidth ?? DEFAULT_MAX_WIDTH,
    });
  },
  permission: {
    name: 'photos',
    access: 'read',
  },
  error: FetchAlbumPhotosPermissionError,
});
