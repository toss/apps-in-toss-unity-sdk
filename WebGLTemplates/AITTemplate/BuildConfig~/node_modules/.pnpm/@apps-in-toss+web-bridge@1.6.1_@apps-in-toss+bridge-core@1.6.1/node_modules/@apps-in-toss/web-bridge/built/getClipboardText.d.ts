export type GetClipboardText = () => Promise<string>;
type PermissionStatus$1 = "notDetermined" | "denied" | "allowed";
export type PermissionDialogFunction = () => Promise<Exclude<PermissionStatus$1, "notDetermined">>;
export type GetPermissionFunction = () => Promise<PermissionStatus$1>;
export type PermissionFunctionWithDialog<T extends (...args: any[]) => any> = T & {
	getPermission: GetPermissionFunction;
	openPermissionDialog: PermissionDialogFunction;
};
/**
 * @public
 * @category 클립보드
 * @name getClipboardText
 * @description 클립보드에 저장된 텍스트를 가져오는 함수예요. 복사된 텍스트를 읽어서 다른 작업에 활용할 수 있어요.
 * @property [openPermissionDialog] - 클립보드 읽기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 클립보드 읽기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 클립보드 읽기 권한을 허용한 상태예요. `denied`는 사용자가 클립보드 읽기 권한을 거부한 상태예요. `notDetermined`는 클립보드 읽기 권한 요청을 한 번도 하지 않은 상태예요.
 * @returns {Promise<string>} - 클립보드에 저장된 텍스트를 반환해요. 클립보드에 텍스트가 없으면 빈 문자열을 반환해요.
 *
 * @signature
 * ```typescript
 * function getClipboardText(): Promise<string>;
 * ```
 *
 * @example
 * ### 클립보드의 텍스트 가져오기
 *
 * 클립보드의 텍스트를 가져오는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 클립보드 읽기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`GetClipboardTextPermissionError`](/react-native/reference/types/권한/GetClipboardTextPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 클립보드 읽기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import {
 *   getClipboardText,
 *   GetClipboardTextPermissionError,
 *   SetClipboardTextPermissionError,
 * } from '@apps-in-toss/web-framework';
 * import { useState } from 'react';
 *  *

 * // '붙여넣기' 버튼을 누르면 클립보드에 저장된 텍스트를 가져와 화면에 표시해요.
 * function PasteButton() {
 *   const [text, setText] = useState(''); *

 *   const handlePress = async () => {
 *     try {
 *       const clipboardText = await getClipboardText();
 *       setText(clipboardText || '클립보드에 텍스트가 없어요.');
 *     } catch (error) {
 *       if (error instanceof GetClipboardTextPermissionError) {
 *         // 클립보드 읽기 권한 없음
 *       } *

 *       if (error instanceof SetClipboardTextPermissionError) {
 *         // 클립보드 쓰기 권한 없음
 *       }
 *     }
 *   }; *

 *   return (
 *     <div>
 *       <span>{text}</span>
 *       <input type="button" value="붙여넣기" onClick={handlePress} />
 *       <input type="button"
 *         value="권한 확인하기"
 *         onClick={async () => {
 *           const permission = await getClipboardText.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <input type="button"
 *         value="권한 요청하기"
 *         onClick={async () => {
 *           const permission = await getClipboardText.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </div>
 *   );
 * }
 * ```
 */
export declare const getClipboardText: PermissionFunctionWithDialog<GetClipboardText>;

export {};
