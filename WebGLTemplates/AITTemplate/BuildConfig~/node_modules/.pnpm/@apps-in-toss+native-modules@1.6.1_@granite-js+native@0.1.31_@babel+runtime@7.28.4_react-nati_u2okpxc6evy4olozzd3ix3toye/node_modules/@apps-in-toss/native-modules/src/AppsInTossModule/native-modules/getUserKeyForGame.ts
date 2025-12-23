import { AppsInTossModule } from './AppsInTossModule';
import { isMinVersionSupported } from './isMinVersionSupported';
import { GAME_USER_KEY_MIN_VERSION } from '../constants';

export interface GetUserKeyForGameSuccessResponse {
  hash: string;
  type: 'HASH';
}

export interface GetUserKeyForGameErrorResponse {
  type: 'NOT_AVAILABLE';
}

export type GetUserKeyForGameResponse = GetUserKeyForGameSuccessResponse | GetUserKeyForGameErrorResponse;

/**
 * @public
 * @category 게임
 * @name getUserKeyForGame
 * @description
 * 게임 카테고리 미니앱에서 사용자의 고유 키를 가져와요. 이 키를 사용해서 사용자를 식별하고 게임 데이터를 관리할 수 있어요.
 * 게임 카테고리가 아닌 미니앱에서 호출하면 `'INVALID_CATEGORY'`를 반환해요.
 * @returns {Promise<GetUserKeyForGameSuccessResponse | 'INVALID_CATEGORY' | 'ERROR' | undefined>}
 * 사용자 키 조회 결과를 반환해요.
 * - `GetUserKeyForGameSuccessResponse`: 사용자 키 조회에 성공했어요. `{ type: 'HASH', hash: string }` 형태로 반환돼요.
 * - `'INVALID_CATEGORY'`: 게임 카테고리가 아닌 미니앱에서 호출했어요.
 * - `'ERROR'`: 알 수 없는 오류가 발생했어요.
 * - `undefined`: 앱 버전이 최소 지원 버전보다 낮아요.
 *
 * @example
 * ```tsx
 * // react-native
 * import { Button } from 'react-native';
 * import { getUserKeyForGame } from '@apps-in-toss/framework';
 *
 * function GameUserKeyButton() {
 *   async function handlePress() {
 *       const result = await getUserKeyForGame();
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'INVALID_CATEGORY') {
 *         console.error('게임 카테고리가 아닌 미니앱이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('사용자 키 조회 중 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if (result.type === 'HASH') {
 *         console.log('사용자 키:', result.hash);
 *         // 여기에서 사용자 키를 사용해 게임 데이터를 관리할 수 있어요.
 *       }
 *   }
 *
 *   return (
 *     <Button onPress={handlePress} title="유저 키 가져오기" />
 *   );
 * }
 * ```
 *
 * @example
 * ```tsx
 * // webview
 * import { getUserKeyForGame } from '@apps-in-toss/web-framework';
 *
 * function GameUserKeyButton() {
 *   async function handleClick() {
 *       const result = await getUserKeyForGame();
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'INVALID_CATEGORY') {
 *         console.error('게임 카테고리가 아닌 미니앱이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('사용자 키 조회 중 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if (result.type === 'HASH') {
 *         console.log('사용자 키:', result.hash);
 *         // 여기에서 사용자 키를 사용해 게임 데이터를 관리할 수 있어요.
 *       }
 *   }
 *
 *   return (
 *     <button onClick={handleClick}>유저 키 가져오기</button>
 *   );
 * }
 * ```
 */
export async function getUserKeyForGame(): Promise<
  GetUserKeyForGameSuccessResponse | 'INVALID_CATEGORY' | 'ERROR' | undefined
> {
  const isSupported = isMinVersionSupported(GAME_USER_KEY_MIN_VERSION);

  if (!isSupported) {
    return;
  }

  try {
    const response = await AppsInTossModule.getUserKeyForGame({});
    if (response.type === 'HASH') {
      return response;
    }
    if (response.type === 'NOT_AVAILABLE') {
      return 'INVALID_CATEGORY';
    }
    return 'ERROR';
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
  } catch (_) {
    return 'ERROR';
  }
}
