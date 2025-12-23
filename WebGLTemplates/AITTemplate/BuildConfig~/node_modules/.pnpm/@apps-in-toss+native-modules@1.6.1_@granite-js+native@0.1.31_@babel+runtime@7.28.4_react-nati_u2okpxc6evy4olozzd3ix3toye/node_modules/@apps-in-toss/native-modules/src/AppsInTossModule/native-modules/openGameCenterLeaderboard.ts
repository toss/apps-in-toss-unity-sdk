import { openURL } from '@granite-js/react-native';
import { isMinVersionSupported } from './isMinVersionSupported';
import { GAME_CENTER_MIN_VERSION } from '../constants';

/**
 * @public
 * @category 게임센터
 * @name openGameCenterLeaderboard
 * @description 게임센터 리더보드 웹뷰를 열어요.
 * 앱 버전이 최소 지원 버전(`5.221.0`)보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
 * 게임센터를 사용하는 사용자는 반드시 최소 지원 버전 이상이어야 게임을 실행할 수 있어요.
 * @returns 리더보드 웹뷰를 호출해요. 앱 버전이 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
 *
 * @example
 * import { Button } from 'react-native';
 * import { openGameCenterLeaderboard } from '@apps-in-toss/framework';
 *
 * // '리더보드' 버튼을 누르면 게임센터 리더보드 웹뷰가 열려요.
 * function LeaderboardButton() {
 *   const onPress = () => {
 *     openGameCenterLeaderboard();
 *   };
 *
 *   return <Button title="리더보드 웹뷰 호출" onPress={onPress} />;
 * }
 */
export async function openGameCenterLeaderboard(): Promise<void> {
  if (!isMinVersionSupported(GAME_CENTER_MIN_VERSION)) {
    return;
  }

  const appName = global.__granite?.app?.name;

  if (appName == null) {
    throw new Error('Cannot get app name');
  }

  const url = new URL('servicetoss://game-center/leaderboard?_navbar=hide');
  url.searchParams.set('appName', appName);
  url.searchParams.set('referrer', `appsintoss.${appName}`);
  return openURL(url.toString());
}

declare const global: { __granite: any };
