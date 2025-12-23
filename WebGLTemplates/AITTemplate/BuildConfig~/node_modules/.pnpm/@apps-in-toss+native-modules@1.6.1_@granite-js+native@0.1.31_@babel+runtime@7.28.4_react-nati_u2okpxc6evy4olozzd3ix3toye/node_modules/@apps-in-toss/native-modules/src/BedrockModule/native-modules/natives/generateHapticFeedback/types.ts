export type HapticFeedbackType =
  | 'tickWeak'
  | 'tap'
  | 'tickMedium'
  | 'softMedium'
  | 'basicWeak'
  | 'basicMedium'
  | 'success'
  | 'error'
  | 'wiggle'
  | 'confetti';

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
