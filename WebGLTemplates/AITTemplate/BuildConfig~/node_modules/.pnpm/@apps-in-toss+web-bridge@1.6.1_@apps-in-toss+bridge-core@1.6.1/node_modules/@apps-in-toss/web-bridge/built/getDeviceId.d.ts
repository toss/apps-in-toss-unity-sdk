/**
 * @public
 * @category 환경 확인
 * @kind function
 * @name getDeviceId
 * @description
 * 사용 중인 기기의 고유 식별자를 문자열로 반환해요.
 *
 * 이 함수는 현재 사용 중인 기기의 고유 식별자를 문자열로 반환해요. 기기별로 설정이나 데이터를 저장하거나 사용자의 기기를 식별해서 로그를 기록하고 분석하는 데 사용할 수 있어요. 같은 사용자의 여러 기기를 구분하는 데도 유용해요.
 *
 * @returns {string} 기기의 고유 식별자를 나타내는 문자열이에요.
 *
 * @example
 * ### 기기 고유 식별자 가져오기
 *
 * ```tsx
 * import { getDeviceId } from '@apps-in-toss/web-framework';
 * 
 *
 * function MyPage() {
 *   const id = getDeviceId();
 *
 *   return (
 *     <span>사용자의 기기 고유 식별자: {id}</span>
 *   );
 * }
 * ```
 */
export declare function getDeviceId(): string;

export {};
