/**
 * @public
 * @category 토스 로그인
 * @name getIsTossLoginIntegratedService
 * @description
 * 유저가 토스 로그인을 연동했는지 여부를 확인해요.
 * 기존 토스 로그인 사용자를 찾아서 게임 식별자로 전환하는 마이그레이션을 할 때 사용할 수 있어요.
 * @returns {Promise<boolean | undefined>}
 * 토스 로그인이 연동된 유저인지 여부를 반환해요.
 * - `true`: 토스 로그인이 연동된 유저에요.
 * - `false`: 토스 로그인이 연동되지 않은 유저에요.
 * - `undefined`: 앱 버전이 최소 지원 버전보다 낮아요.
 * @throw {message: "oauth2ClientId 설정이 필요합니다."} - 토스 로그인을 사용하지 않는 미니앱에서 호출했을 때 발생해요.
 * @example
 * ```tsx
 * // react-native
 * 
 * import { getIsTossLoginIntegratedService } from '@apps-in-toss/web-framework';
 *
 * function GetIsTossLoginIntegratedServiceButton() {
 *   async function handlePress() {
 *     try {
 *       const result = await getIsTossLoginIntegratedService();
 *
 *       if (result === undefined) {
 *         console.warn('지원하지 않는 앱 버전이에요.');
 *         return;
 *       }
 *       if (result === true) {
 *         console.log('토스 로그인이 연동된 유저에요.');
 *         // 여기에서 토스 로그인 연동 유저에 대한 처리를 할 수 있어요.
 *       }
 *       if (result === false) {
 *         console.log('토스 로그인이 연동되지 않은 유저에요.');
 *         // 여기에서 토스 로그인 연동 유저가 아닌 경우에 대한 처리를 할 수 있어요.
 *       }
 *     } catch (error) {
 *       console.error(error);
 *     }
 *   }
 *
 *   return (
 *     <input type="button" onClick={handlePress} value="토스 로그인 통합 서비스 여부 확인" />
 *   );
 * }
 * ```
 */
export declare function getIsTossLoginIntegratedService(): Promise<boolean | undefined>;

export {};
