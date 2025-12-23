/**
 * @public
 * @name setSecureScreen
 * @category 화면 제어
 * @kind function
 * @description
 * 화면 캡쳐를 차단해서 민감한 정보가 유출되지 않도록 보호하거나, 필요할 경우 캡쳐를 허용하도록 설정해요. 예를 들어 보안이 중요한 화면에서 사용할 수 있어요.
 *
 * @param {object} options 화면 캡쳐 설정 옵션이에요.
 * @param {boolean} options.enabled 화면 캡쳐를 차단할지 여부를 설정해요. `true`면 캡쳐를 차단하고, `false`면 허용해요.
 * @returns {enabled: boolean} 현재 설정된 캡쳐 차단 상태를 반환해요.
 *
 * @example
 * ### 캡쳐 허용 상태 변경하기
 *
 * ```tsx
 * 
 * import { setSecureScreen } from '@apps-in-toss/native-modules';
 *
 * function SetSecureScreen() {
 *  return <input type="button" value="캡쳐 막기" onClick={async () => {
 *    await setSecureScreen({ enabled: true });
 *  }} />;
 * }
 * ```
 */
export declare function setSecureScreen(options: {
	enabled: boolean;
}): Promise<{
	enabled: boolean;
}>;

export {};
