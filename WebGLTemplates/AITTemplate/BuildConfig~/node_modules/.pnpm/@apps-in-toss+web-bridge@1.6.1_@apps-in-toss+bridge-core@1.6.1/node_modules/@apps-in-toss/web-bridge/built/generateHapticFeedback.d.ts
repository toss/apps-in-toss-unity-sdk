export type HapticFeedbackType = "tickWeak" | "tap" | "tickMedium" | "softMedium" | "basicWeak" | "basicMedium" | "success" | "error" | "wiggle" | "confetti";
/**
 * @public
 * @category 인터렉션
 * @name HapticFeedbackOptions
 * @description
 * generateHapticFeedback 함수에 전달할 햅틱진동의 타입을 나타내요. 진동타입의 종류는 다음과 같아요.
 * ```typescript
 * type HapticFeedbackType =
 * | "tickWeak"
 * | "tap"
 * | "tickMedium"
 * | "softMedium"
 * | "basicWeak"
 * | "basicMedium"
 * | "success"
 * | "error"
 * | "wiggle"
 * | "confetti";
 * ```
 * @typedef { type: HapticFeedbackType } HapticFeedbackOptions
 * @typedef { "tickWeak" | "tap" | "tickMedium" | "softMedium" | "basicWeak" | "basicMedium" | "success" | "error" | "wiggle" | "confetti" } HapticFeedbackType
 *
 */
export interface HapticFeedbackOptions {
	type: HapticFeedbackType;
}
/**
 * @public
 * @category 인터렉션
 * @name generateHapticFeedback
 * @description 디바이스에 햅틱 진동을 일으키는 함수예요. 예를 들어, 버튼 터치나 화면전환에 드라마틱한 효과를 주고 싶을 때 사용할 수 있어요. [HapticFeedbackOptions](/react-native/reference/native-modules/인터렉션/HapticFeedbackOptions.html)에서 진동타입을 확인해 보세요.
 * @returns {void}
 *
 * @example
 * ### 버튼을 눌러 햅틱 일으키기
 *
 * ```tsx
 * 
 * import { generateHapticFeedback } from '@apps-in-toss/native-modules';
 *
 * function GenerateHapticFeedback() {
 *  return <input type="button" value="햅틱" onClick={() => { generateHapticFeedback( { type: "tickWeak"}) }} />;
 * }
 * ```
 */
export declare function generateHapticFeedback(options: HapticFeedbackOptions): Promise<void>;

export {};
