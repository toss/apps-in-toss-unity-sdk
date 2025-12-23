/**
 * @public
 * @name getSchemeUri
 * @category 환경 확인
 * @kind function
 * @description 처음에 화면에 진입한 스킴 값이에요. 페이지 이동으로 인한 URI 변경은 반영되지 않아요.
 * @returns {string} 처음에 화면에 진입한 스킴 값을 반환해요.
 *
 * @example
 * ### 처음 진입한 스킴 값 가져오기
 *
 * ```tsx
 * import { getSchemeUri } from '@apps-in-toss/native-modules';
 * 
 *
 * function MyPage() {
 *  const schemeUri = getSchemeUri();
 *
 *  return <span>처음에 화면에 진입한 스킴 값: {schemeUri}</span>
 * }
 * ```
 */
export declare function getSchemeUri(): string;

export {};
