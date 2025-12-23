import { AppsInTossModule } from './AppsInTossModule';
import { isMinVersionSupported } from './isMinVersionSupported';
import { GAME_PROMOTION_REWARD_MIN_VERSION } from '../constants';

export interface GrantPromotionRewardForGameSuccessResponse {
  key: string;
}

export interface GrantPromotionRewardForGameErrorResponse {
  code: string;
  [key: string]: any;
}

export interface GrantPromotionRewardForGameErrorResult {
  errorCode: string;
  message: string;
}

export type GrantPromotionRewardForGameResponse =
  | GrantPromotionRewardForGameSuccessResponse
  | GrantPromotionRewardForGameErrorResponse;

type GrantPromotionRewardForGameResult =
  | GrantPromotionRewardForGameResponse
  | GrantPromotionRewardForGameErrorResult
  | 'ERROR'
  | undefined;

function isGrantPromotionRewardForGameError(error: unknown): error is { code: string; message: string } {
  return (
    typeof error === 'object' &&
    error !== null &&
    'code' in error &&
    typeof (error as any).code === 'string' &&
    'message' in error &&
    typeof (error as any).message === 'string'
  );
}

/**
 * @public
 * @category 게임
 * @name grantPromotionRewardForGame
 * @description
 * 이 함수를 사용하면 게임 카테고리 미니앱에서 프로모션 코드를 사용해서 유저에게 리워드를 지급할 수 있어요.
 * 게임 카테고리가 아닌 미니앱에서 호출할 수 없어요.
 * @param {{ params: { promotionCode: string; amount: number } }} params - 포인트를 지급하기 위해 필요한 정보예요.
 * @param {string} params.promotionCode - 프로모션 코드예요.
 * @param {number} params.amount - 지급할 포인트 금액이에요.
 * @returns {Promise<{ key: string } | { errorCode: string; message: string } | 'ERROR' | undefined>}
 * 포인트 지급 결과를 반환해요.
 * - `{ key: string }`: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요.
 * - `{ errorCode: string, message: string }`: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요.
 *   - `"40000"`: 게임이 아닌 미니앱에서 호출했을 때
 *   - `"4100"`: 프로모션 정보를 찾을 수 없을 때
 *   - `"4104"`: 프로모션이 중지되었을 때
 *   - `"4105"`: 프로모션이 종료되었을 때
 *   - `"4108"`: 프로모션이 승인되지 않았을 때
 *   - `"4109"`: 프로모션이 실행중이 아닐 때
 *   - `"4110"`: 리워드를 지급/회수할 수 없을 때
 *   - `"4112"`: 프로모션 머니가 부족할 때
 *   - `"4113"`: 이미 지급/회수된 내역일 때
 *   - `"4114"`: 프로모션에 설정된 1회 지급 금액을 초과할 때
 * - `'ERROR'`: 알 수 없는 오류가 발생했어요.
 * - `undefined`: 앱 버전이 최소 지원 버전보다 낮아요.
 * @example
 * ```tsx
 * // react-native
 * import { Button } from 'react-native';
 * import { grantPromotionRewardForGame } from '@apps-in-toss/framework';
 *
 * function GrantRewardButton() {
 *   async function handlePress() {
 *       const result = await grantPromotionRewardForGame({
 *         params: {
 *           promotionCode: 'GAME_EVENT_2024',
 *           amount: 1000,
 *         },
 *       });
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('포인트 지급 중 알 수 없는 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if ('key' in result) {
 *         console.log('포인트 지급 성공!', result.key);
 *       } else if ('errorCode' in result) {
 *         console.error('포인트 지급 실패:', result.errorCode, result.message);
 *       }
 *   }
 *
 *   return <Button onPress={handlePress} title="포인트 지급하기" />;
 * }
 * ```
 *
 * @example
 * ```tsx
 * // webview
 * import { grantPromotionRewardForGame } from '@apps-in-toss/web-framework';
 *
 * function GrantRewardButton() {
 *   async function handleClick() {
 *       const result = await grantPromotionRewardForGame({
 *         params: {
 *           promotionCode: 'GAME_EVENT_2024',
 *           amount: 1000,
 *         },
 *       });
 *
 *       if (!result) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *
 *       if (result === 'ERROR') {
 *         console.error('포인트 지급 중 알 수 없는 오류가 발생했어요.');
 *         return;
 *       }
 *
 *       if ('key' in result) {
 *         console.log('포인트 지급 성공!', result.key);
 *       } else if ('errorCode' in result) {
 *         console.error('포인트 지급 실패:', result.errorCode, result.message);
 *       }
 *   }
 *
 *   return (
 *     <button onClick={handleClick}>포인트 지급하기</button>
 *   );
 * }
 * ```
 */
export async function grantPromotionRewardForGame({
  params,
}: {
  params: { promotionCode: string; amount: number };
}): Promise<GrantPromotionRewardForGameResult> {
  const isSupported = isMinVersionSupported(GAME_PROMOTION_REWARD_MIN_VERSION);

  if (!isSupported) {
    return;
  }

  try {
    const response = await AppsInTossModule.grantPromotionRewardForGame({ params });
    if (response.key) {
      return response;
    }
    return 'ERROR';
  } catch (error) {
    if (isGrantPromotionRewardForGameError(error)) {
      return {
        errorCode: error.code,
        message: error.message,
      };
    }
    return 'ERROR';
  }
}
