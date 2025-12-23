import { HapticFeedbackOptions, HapticFeedbackType } from './types';
import { BedrockModule } from '../BedrockModule';

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
 * import { Button } from 'react-native';
 * import { generateHapticFeedback } from '@apps-in-toss/native-modules';
 *
 * function GenerateHapticFeedback() {
 *  return <Button title="햅틱" onPress={() => { generateHapticFeedback( { type: "tickWeak"}) }} />;
 * }
 * ```
 */
export function generateHapticFeedback(options: HapticFeedbackOptions) {
  return BedrockModule.generateHapticFeedback(options);
}

export type { HapticFeedbackType };
