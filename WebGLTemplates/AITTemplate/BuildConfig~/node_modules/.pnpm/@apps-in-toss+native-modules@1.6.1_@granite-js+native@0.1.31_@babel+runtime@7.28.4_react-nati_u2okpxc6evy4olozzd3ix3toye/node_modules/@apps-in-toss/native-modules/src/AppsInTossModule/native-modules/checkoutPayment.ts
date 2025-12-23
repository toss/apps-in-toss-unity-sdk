import { AppsInTossModule } from './AppsInTossModule';

/**
 * @public
 * @category 토스페이
 * @name CheckoutPaymentOptions
 * @description 토스페이 결제창을 띄울 때 필요한 옵션이에요.
 * @property {string} payToken 결제 토큰이에요.
 */
export interface CheckoutPaymentOptions {
  /**
   * 결제 토큰이에요.
   */
  payToken: string;
}

/**
 * @public
 * @category 토스페이
 * @name CheckoutPaymentResult
 * @description 토스페이 결제창에서 사용자가 인증에 성공했는지 여부예요.
 * @property {boolean} success 인증이 성공했는지 여부예요.
 * @property {string} [reason] 인증이 실패했을 경우의 이유예요.
 */
export interface CheckoutPaymentResult {
  /**
   * 인증이 성공했는지 여부예요.
   */
  success: boolean;

  /**
   * 인증이 실패했을 경우의 이유예요.
   */
  reason?: string;
}

/**
 * @public
 * @category 토스페이
 * @name checkoutPayment
 * @description 토스페이 결제창을 띄우고, 사용자 인증을 수행해요. 인증이 완료되면 성공 여부를 반환해요.
 *
 * 이 함수는 결제창을 통해 사용자 인증만 해요. 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요.
 *
 * @param {CheckoutPaymentOptions} options 결제창을 띄울 때 필요한 옵션이에요.
 * @returns {Promise<CheckoutPaymentResult>} 인증 성공 여부를 포함한 결과를 반환해요.
 *
 * @example
 *
 * ### 토스페이 결제창 띄우고 인증 처리하기
 *
 * ```tsx
 * import { TossPay } from '@apps-in-toss/framework';
 *
 * async function handlePayment() {
 *   try {
 *     // 실제 구현 시 결제 생성 역할을 하는 API 엔드포인트로 대체해주세요.
 *     const { payToken } = await fetch('/my-api/payment/create').then(res => res.json());
 *
 *     const { success, reason } = await TossPay.checkoutPayment({ payToken });
 *
 *     if (success) {
 *       // 실제 구현 시 결제를 실행하는 API 엔드포인트로 대체해주세요.
 *       await fetch('/my-api/payment/execute', {
 *         method: 'POST',
 *         body: JSON.stringify({ payToken }),
 *         headers: { 'Content-Type': 'application/json' },
 *       });
 *     } else {
 *       console.log('인증 실패:', reason);
 *     }
 *   } catch (error) {
 *     console.error('결제 인증 중 오류가 발생했어요:', error);
 *   }
 * }
 * ```
 */
export async function checkoutPayment(options: CheckoutPaymentOptions): Promise<CheckoutPaymentResult> {
  return AppsInTossModule.checkoutPayment({ params: options });
}
