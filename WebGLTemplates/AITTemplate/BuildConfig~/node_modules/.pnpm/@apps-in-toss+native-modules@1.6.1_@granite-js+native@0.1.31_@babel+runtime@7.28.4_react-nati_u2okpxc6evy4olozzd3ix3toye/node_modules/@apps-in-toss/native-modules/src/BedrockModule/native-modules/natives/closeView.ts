import { BedrockModule } from './BedrockModule';

/**
 * @public
 * @category 화면 제어
 * @kind function
 * @name closeView
 * @description 현재 화면을 닫는 함수에요. 예를 들어, "닫기" 버튼을 눌러서 서비스를 종료할 때 사용할 수 있어요.
 * @returns {Promise<void>}
 *
 * @example
 * ### 닫기 버튼을 눌러 화면 닫기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { closeView } from '@apps-in-toss/native-modules';
 *
 * function CloseButton() {
 *  return <Button title="닫기" onPress={closeView} />;
 * }
 * ```
 */
export async function closeView() {
  return BedrockModule.closeView();
}
