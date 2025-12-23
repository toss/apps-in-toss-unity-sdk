/**
 * @public
 * @category 로그인
 * @name appLogin
 * @description 토스 인증으로 로그인해요. 로그인이 완료되면 다시 토스 앱으로 이동해요.
 * @example
 *
 * ### 토스 인증을 통해 로그인을 하는 예제
 *
 * ```tsx
 * 
 * import { appLogin } from '@apps-in-toss/web-framework';
 *
 * function Page() {
 *   const handleLogin = async () => {
 *     const { authorizationCode, referrer } = await appLogin();
 *
 *     // 획득한 인가 코드(`authorizationCode`)와 `referrer`를 서버로 전달해요.
 *   }
 *
 *   return <input type="button" value="로그인" onClick={handleLogin} />;
 * }
 * ```
 */
export declare function appLogin(): Promise<{
	authorizationCode: string;
	referrer: "DEFAULT" | "SANDBOX";
}>;

export {};
