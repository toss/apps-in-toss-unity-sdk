/**
 * @public
 * @category 게임센터
 * @name SubmitGameCenterLeaderBoardScoreResponse
 * @description
 * 토스게임센터 리더보드에 점수를 제출한 결과 정보를 담아서 반환해요. 반환된 정보를 사용해서 점수 제출 결과에 따라 적절한 에러 처리를 할 수 있어요.
 * @property {'SUCCESS' | 'LEADERBOARD_NOT_FOUND' | 'PROFILE_NOT_FOUND' | 'UNPARSABLE_SCORE'} statusCode
 * 점수 제출 결과를 나타내는 상태 코드예요.
 * - `'SUCCESS'`: 점수 제출이 성공했어요.
 * - `'LEADERBOARD_NOT_FOUND'`: `gameId`에 해당하는 리더보드를 찾을 수 없어요.
 * - `'PROFILE_NOT_FOUND'`: 사용자의 프로필이 없어요.
 * - `'UNPARSABLE_SCORE'`: 점수를 해석할 수 없어요. 점수는 실수(float) 형태의 문자열로 전달해야 해요.
 */
export interface SubmitGameCenterLeaderBoardScoreResponse {
	statusCode: "SUCCESS" | "LEADERBOARD_NOT_FOUND" | "PROFILE_NOT_FOUND" | "UNPARSABLE_SCORE";
}
/**
 * @public
 * @category 게임센터
 * @name submitGameCenterLeaderBoardScore
 * @description
 * 사용자의 게임 점수를 토스게임센터 리더보드에 제출해요. 이 기능으로 사용자의 점수를 공식 리더보드에 기록하고 다른 사용자와 비교할 수 있어요.
 * @param {string} params.score
 * 제출할 게임 점수예요. 실수 형태의 숫자를 문자열로 전달해야 해요. 예를들어 `"123.45"` 또는 `"9999"` 예요.
 * @returns {Promise<SubmitGameCenterLeaderBoardScoreResponse | undefined>}
 * 점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 `undefined`를 반환해요.
 *
 * @example
 * ### 게임 점수를 토스게임센터 리더보드에 제출하기
 * ```tsx
 * 
 * import { submitGameCenterLeaderBoardScore } from '@apps-in-toss/web-framework';
 *
 * function GameCenterLeaderBoardScoreSubmitButton() {
 *   async function handlePress() {
 *     try {
 *       const result = await submitGameCenterLeaderBoardScore({ score: '123.45' });
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result.statusCode === 'SUCCESS') {
 *         console.log('점수 제출 성공!');
 *       } else {
 *         console.error('점수 제출 실패:', result.statusCode);
 *       }
 *     } catch (error) {
 *       console.error('점수 제출 중 오류가 발생했어요.', error);
 *     }
 *   }
 *
 *   return (
 *     <input type="button" onClick={handlePress}>점수 제출하기</button>
 *   );
 * }
 * ```
 */
export declare function submitGameCenterLeaderBoardScore(params: {
	score: string;
}): Promise<SubmitGameCenterLeaderBoardScoreResponse | undefined>;

export {};
