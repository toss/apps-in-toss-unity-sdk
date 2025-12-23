/**
 * @public
 * @category 화면 제어
 * @name setIosSwipeGestureEnabled
 * @description
 * `setIosSwipeGestureEnabled` 함수는 iOS에서 화면을 스와이프하여 뒤로가기 기능을 활성화하거나 비활성화할 수 있어요.
 *
 * @param {object} options 스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요.
 * @param {boolean} options.isEnabled 화면을 스와이프하여 뒤로가기 기능을 활성화하거나 비활성화할 수 있어요. `true`를 설정하면 스와이프로 뒤로갈 수 있고, `false`를 설정하면 스와이프 뒤로가기 기능이 비활성화돼요.
 *
 * @returns {void}
 *
 * @example
 *
 * ### iOS에서 화면 스와이프로 뒤로가기 기능을 활성화하거나 비활성화하기
 *
 * **스와이프 끄기** 버튼을 눌러 화면 스와이프로 뒤로가기 기능을 비활성화하거나, **스와이프 켜기** 버튼을 눌러 화면 스와이프로 뒤로가기 기능을 활성화할 수 있어요.
 *
 *
 * ```tsx
 * import { setIosSwipeGestureEnabled } from '@apps-in-toss/native-modules';
 * 
 *
 * function Page() {
 *  return (
 *    <>
 *     <input type="button" value="스와이프 끄기" onClick={() => setIosSwipeGestureEnabled({ isEnabled: false })} />
 *     <input type="button" value="스와이프 켜기" onClick={() => setIosSwipeGestureEnabled({ isEnabled: true })} />
 *    </>
 *  );
 * }
 * ```
 *
 */
export declare function setIosSwipeGestureEnabled(options: {
	isEnabled: boolean;
}): Promise<void>;

export {};
