import { SetClipboardTextPermissionError, type SetClipboardText } from '@apps-in-toss/types';
import { AppsInTossModule } from '../../AppsInTossModule';
import { createPermissionFunction } from '../createPermissionFunction';

/**
 * @public
 * @category 클립보드
 * @name setClipboardText
 * @description 텍스트를 클립보드에 복사해서 사용자가 다른 곳에 붙여 넣기 할 수 있어요.
 * @param {Promise<void>} text - 클립보드에 복사할 텍스트예요. 문자열 형식으로 입력해요.
 * @property [openPermissionDialog] - 클립보드 쓰기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 클립보드 쓰기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 클립보드 쓰기 권한을 허용한 상태예요. `denied`는 사용자가 클립보드 쓰기 권한을 거부한 상태예요. `notDetermined`는 클립보드 쓰기 권한 요청을 한 번도 하지 않은 상태예요.
 *
 * @signature
 * ```typescript
 * function setClipboardText(text: string): Promise<void>;
 * ```
 *
 * @example
 * ### 텍스트를 클립보드에 복사하기
 *
 * 텍스트를 클립보드에 복사하는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 클립보드 쓰기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`SetClipboardTextPermissionError`](/react-native/reference/types/권한/SetClipboardTextPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 클립보드 쓰기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { setClipboardText, SetClipboardTextPermissionError } from '@apps-in-toss/framework';
 * import { Alert, Button } from 'react-native';
 *
 * // '복사' 버튼을 누르면 "복사할 텍스트"가 클립보드에 복사돼요.
 * function CopyButton() {
 *   const handleCopy = async () => {
 *     try {
 *       await setClipboardText('복사할 텍스트');
 *       console.log('텍스트가 복사됐어요!');
 *     } catch (error) {
 *       if (error instanceof SetClipboardTextPermissionError) {
 *         // 텍스트 쓰기 권한 거부됨
 *       }
 *     }
 *   };
 *
 *   return (
 *     <>
 *       <Button title="복사" onPress={handleCopy} />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await setClipboardText.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await setClipboardText.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </>
 *   );
 * }
 * ```
 */
export const setClipboardText = createPermissionFunction<SetClipboardText>({
  handler: (text) => {
    return AppsInTossModule.setClipboardText({ text });
  },
  permission: {
    name: 'clipboard',
    access: 'write',
  },
  error: SetClipboardTextPermissionError,
});
