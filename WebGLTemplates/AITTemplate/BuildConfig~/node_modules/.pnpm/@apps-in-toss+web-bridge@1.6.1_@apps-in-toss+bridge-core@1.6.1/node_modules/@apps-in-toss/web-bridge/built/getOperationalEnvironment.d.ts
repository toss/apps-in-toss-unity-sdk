/**
 * @public
 * @category 환경 확인
 * @kind function
 * @name getOperationalEnvironment
 * @description
 * 현재 실행 중인 앱의 운영 환경을 확인해요.
 * 토스 앱에서 실행 중이라면 `'toss'`, 샌드박스 환경에서 실행 중이라면 `'sandbox'`를 반환해요.
 *
 * 운영 환경은 앱이 실행되는 컨텍스트를 의미하며, 특정 기능의 사용 가능 여부를 판단하는 데 활용할 수 있어요.
 *
 * @returns {'toss' | 'sandbox'}
 * 현재 운영 환경을 나타내는 문자열이에요.
 * - `'toss'`: 토스 앱에서 실행 중이에요.
 * - `'sandbox'`: 샌드박스 환경에서 실행 중이에요.
 *
 * @example
 * ### 현재 운영 환경 확인하기
 *
 * ```tsx
 * import { getOperationalEnvironment } from '@apps-in-toss/web-framework';
 * 
 *
 * function EnvironmentInfo() {
 *   const environment = getOperationalEnvironment();
 *
 *   return (
 *     <span>현재 운영 환경: {environment}</span>
 *   );
 * }
 * ```
 */
export declare function getOperationalEnvironment(): "toss" | "sandbox";

export {};
