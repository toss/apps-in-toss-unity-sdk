import { AppsInTossModule } from './AppsInTossModule';
import { isMinVersionSupported } from './isMinVersionSupported';
import { GAME_CENTER_MIN_VERSION } from '../constants';

/**
 * @category 게임센터
 * @name GameCenterGameProfileResponse
 * @description 토스게임센터 프로필을 가져온 결과 타입이에요.
 * 앱에 프로필이 없는 경우, `statusCode`가 `'PROFILE_NOT_FOUND'`이고 다른 정보는 없어요.
 * 프로필이 있는 경우 `statusCode`가 `'SUCCESS'`이고, 닉네임과 프로필 이미지 주소가 함께 제공돼요.
 * @property {string} statusCode 프로필 조회 결과 상태예요. `'SUCCESS'` 또는 `'PROFILE_NOT_FOUND'` 중 하나예요.
 * @property {string} [nickname] 프로필 닉네임이에요. `statusCode`가 `'SUCCESS'`일 때만 있어요.
 * @property {string} [profileImageUri] 프로필 이미지 URL이에요. `statusCode`가 `'SUCCESS'`일 때만 있어요.
 */
export type GameCenterGameProfileResponse =
  | { statusCode: 'PROFILE_NOT_FOUND' }
  | {
      statusCode: 'SUCCESS';
      nickname: string;
      profileImageUri: string;
    };

/**
 * @category 게임센터
 * @name getGameCenterGameProfile
 * @description 토스게임센터 프로필 정보를 가져와요.
 * 사용자가 프로필을 만들지 않았다면 `statusCode`가 `'PROFILE_NOT_FOUND'`인 응답이 반환돼요.
 * 앱 버전이 최소 지원 버전(안드로이드 5.221.0, iOS 5.221.0)보다 낮으면 `undefined`를 반환해요.
 * @returns {Promise<GameCenterGameProfileResponse | undefined>} 프로필 정보 또는 `undefined`를 반환해요.
 *
 * @example
 * ### 게임센터 프로필 가져오기
 * ```tsx
 * import { getGameCenterGameProfile } from './getGameCenterGameProfile';
 * import { useState } from 'react';
 * import { View, Button } from 'react-native';
 *
 * function GameProfile() {
 *   const [profile, setProfile] = useState<GameCenterGameProfileResponse | null>(null);
 *
 *   const handlePress = async () => {
 *     try {
 *       const result = await getGameCenterGameProfile();
 *       if (result) {
 *         setProfile(result);
 *       }
 *     } catch (error) {
 *       console.error('게임센터 프로필 가져오기에 실패했어요.', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       <Button title="게임센터 프로필 가져오기" onPress={handlePress} />
 *     </View>
 *   );
 * }
 * ```
 */
export async function getGameCenterGameProfile() {
  const isSupported = isMinVersionSupported(GAME_CENTER_MIN_VERSION);

  if (!isSupported) {
    return;
  }

  return AppsInTossModule.getGameCenterGameProfile({});
}
