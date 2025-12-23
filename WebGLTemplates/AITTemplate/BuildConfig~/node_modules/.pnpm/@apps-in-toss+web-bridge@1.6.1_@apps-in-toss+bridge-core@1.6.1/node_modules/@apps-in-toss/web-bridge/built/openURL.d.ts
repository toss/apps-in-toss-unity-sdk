/**
 * @public
 * @kind function
 * @category 화면 이동
 *
 * @name openURL
 * @signature
 * ```typescript
 * function openURL(url: string): Promise<any>;
 * ```
 *
 * @description
 * 지정된 URL을 기기의 기본 브라우저나 관련 앱에서 열어요.
 * 이 함수는 `react-native`의 [`Linking.openURL`](https://reactnative.dev/docs/0.72/linking#openurl) 메서드를 사용하여 URL을 열어요.
 *
 * @param {string} url 열고자 하는 URL 주소
 * @returns {Promise<any>} URL이 성공적으로 열렸을 때 해결되는 Promise
 *
 * @example
 *
 * ### 외부 URL 열기
 *
 * ```tsx
 * import { openURL } from '@apps-in-toss/native-modules';
 * 
 *
 * function Page() {
 *   const handlePress = () => {
 *     openURL('https://google.com');
 *   };
 *
 *   return <input type="button" value="구글 웹사이트 열기" onClick={handlePress} />;
 * }
 * ```
 */
export declare function openURL(url: string): Promise<any>;

export {};
