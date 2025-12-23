export interface AppsInTossSignTossCertParams {
	txId: string;
	skipConfirmDoc?: boolean;
}
/**
 * @public
 * @category 토스인증
 * @name appsInTossSignTossCert
 * @description 토스 인증서를 사용해 서명하는 기능을 제공해요. 이 함수를 사용하면 앱인토스에서 제공하는 인증서를 활용해 서명을 할 수 있어요.
 *
 * @param {AppsInTossSignTossCertParams} params - 서명에 필요한 파라미터를 포함하는 객체예요.
 * @param {string} params.txId - 토스인증서를 사용한 본인확인이나 간편인증, 전자서명에서 사용하는 Transaction Id예요.
 * @param {boolean} params.skipConfirmDoc - (선택) 토스 원터치 인증방식을 사용하기 위한 옵션이예요. true 설정 시 최소 버전: Android 5.236.0, iOS 5.236.0 (default: false)
 *
 * @example
 * ```tsx
 * import { appsInTossSignTossCert } from '@apps-in-toss/web-framework';
 *
 *  // 서명에 필요한 파라미터를 정의해요.
 *  const params = {
 *    txId: "f2e1a6df..."
 *  };
 *
 * appsInTossSignTossCert(params)
 *   .then(() => {
 *     console.log('서명 작업이 성공적으로 완료되었어요.');
 *   })
 *   .catch((error) => {
 *     console.error('서명 작업 중 에러가 발생했어요:', error);
 *   });
 * ```
 */
export declare function appsInTossSignTossCert(params: AppsInTossSignTossCertParams): Promise<void>;

export {};
