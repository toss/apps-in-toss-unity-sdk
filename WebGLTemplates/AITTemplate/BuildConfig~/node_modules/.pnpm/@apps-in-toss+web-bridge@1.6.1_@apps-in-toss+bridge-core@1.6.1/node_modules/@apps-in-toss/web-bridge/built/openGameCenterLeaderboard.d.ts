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
 * 
 * import { openGameCenterLeaderboard } from '@apps-in-toss/web-framework';
 *
 * // '리더보드' 버튼을 누르면 게임센터 리더보드 웹뷰가 열려요.
 * function LeaderboardButton() {
 *   const onClick = () => {
 *     openGameCenterLeaderboard();
 *   };
 *
 *   return <input type="button" value="리더보드 웹뷰 호출" onClick={onClick} />;
 * }
 */
export declare function openGameCenterLeaderboard(): Promise<void>;

export {};
