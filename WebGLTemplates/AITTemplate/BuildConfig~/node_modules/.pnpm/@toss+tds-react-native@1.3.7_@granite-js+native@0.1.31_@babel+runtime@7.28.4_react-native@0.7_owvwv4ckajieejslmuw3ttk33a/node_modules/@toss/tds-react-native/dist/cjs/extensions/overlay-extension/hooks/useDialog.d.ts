import type { ComponentProps, ReactElement } from 'react';
import { AlertDialog, ConfirmDialog } from '../../../components/dialog';
type AlertDialogProps = ComponentProps<typeof AlertDialog>;
interface AlertOptions extends Omit<AlertDialogProps, 'open' | 'onClose' | 'onExited' | 'content'> {
}
type ConfirmDialogProps = ComponentProps<typeof ConfirmDialog>;
interface ConfirmOptions extends Omit<ConfirmDialogProps, 'open' | 'leftButton' | 'rightButton' | 'onClose' | 'onExited' | 'content'> {
    rightButton?: ReactElement | string;
    leftButton?: ReactElement | string;
}
interface UseDialogOptions {
    closeOnDestroy?: boolean;
}
/**
 * @public
 * @category Hooks
 * @kind function
 * @name useDialog
 * @description
 * `useDialog`는 `@toss/tds-react-native`의 `AlertDialog`와 `ConfirmDialog`를 쉽게 제어할 수 있도록 도와주는 Hook이에요. `AlertDialog`는 사용자가 닫는 시점을 Promise 로 전달받을 수 있어요. `ConfirmDialog`는 사용자가 어떤 버튼으로 닫았는지 그 결과를 Promise 로 전달받을 수 있어요. 예를 들어, `ConfirmDialog` 에서 "확인" 버튼과 "취소" 버튼에 따라 이후 처리를 다르게 할 수 있어요.
 *
 * @param {UseDialogOptions} [options] - Dialog를 제어하기 위한 옵션 객체에요.
 * @param {boolean} [options.closeOnDestroy=true] - 컴포넌트가 언마운트될 때 Dialog를 자동으로 닫을지 여부를 결정해요.
 *
 * @returns {{
 *   close: () => void;
 *   openAlert: (options: AlertOptions) => Promise<void>;
 *   openConfirm: (options: ConfirmOptions) => Promise<boolean>;
 * }}
 * - `close` 함수는 현재 열려있는 Dialog를 닫아요.
 * - `openAlert` 함수는 TDS 의 `AlertDialog`를 열어요. `AlertDialog`가 닫히면 `Promise` 가 `resolve` 돼요.
 * - `openConfirm` 함수는 `ConfirmDialog`를 열고, 사용자의 선택 결과를 `Promise`로 반환해요.
 *
 * @example
 *
 * ### `openAlert`으로 `AlertDialog`를 여는 예제
 * ```tsx
 * import { useDialog } from '../../../overlay-extension';
 *
 * const dialog = useDialog();
 *
 * const showAlert = async () => {
 *   const result = await dialog.openAlert({
 *     title: '알림',
 *     message: '이것은 AlertDialog입니다.',
 *   });
 * };
 * ```
 *
 * ### `openConfirm`으로 `ConfirmDialog`를 여는 예제
 * ```tsx
 * const showConfirm = async () => {
 *   const result = await dialog.openConfirm({
 *     title: '확인',
 *     message: '정말로 진행하시겠습니까?',
 *   });
 *   if (result) {
 *     // 사용자가 "확인"을 눌렀을 때의 처리
 *   } else {
 *     // 사용자가 "취소"를 눌렀을 때의 처리
 *   }
 * };
 * ```
 */
export declare function useDialog(options?: UseDialogOptions): {
    close: () => void;
    openAlert: ({ closeOnDimmerClick, onEntered, onButtonPress, ...otherOptions }: AlertOptions) => Promise<void>;
    openConfirm: ({ rightButton, leftButton, closeOnDimmerClick, onEntered, ...otherOptions }: ConfirmOptions) => Promise<boolean>;
};
export {};
