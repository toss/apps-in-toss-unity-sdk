/**
 * @public
 * @category 언어
 * @kind function
 * @name getLocale
 * @description
 * 사용자의 로케일(locale) 정보를 반환해요. 네이티브 모듈에서 로케일 정보를 가져올 수 없을 때는 기본값으로 'ko-KR'을 반환합니다. 앱의 현지화 및 언어 설정과 관련된 기능을 구현할 때 사용하세요.
 *
 * @returns {string} 사용자의 로케일 정보를 반환해요.
 *
 * @example
 * ### 현재 사용자의 로케일 정보 가져오기
 *
 * ```tsx
 * import { getLocale } from '@apps-in-toss/native-modules';
 * 
 *
 * function MyPage() {
 *  const locale = getLocale();
 *
 *  return (
 *    <span>사용자의 로케일 정보: {locale}</span>
 *  )
 * }
 *
 * ```
 */
export declare function getLocale(): string;

export {};
