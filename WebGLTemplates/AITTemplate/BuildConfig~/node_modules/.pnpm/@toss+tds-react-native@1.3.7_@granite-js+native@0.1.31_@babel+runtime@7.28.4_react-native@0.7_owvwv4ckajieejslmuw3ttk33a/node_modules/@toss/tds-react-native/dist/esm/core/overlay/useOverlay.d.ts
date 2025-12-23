import type { CreateOverlayElement } from './types';
/**
 * @public
 * @tag UI
 * @category Hooks
 * @kind function
 * @name useOverlay
 * @description
 * `useOverlay`는 뒤로가기 버튼을 눌렀을 때 Overlay를 닫고 화면을 종료할 수 있도록 도와주는 hook이에요. 이 hook은 Overlay의 열림과 닫힘 상태를 간편하게 관리하고 싶을 때 유용해요.
 *
 * @returns {{
 *   open: (overlayElement: CreateOverlayElement) => void;
 *   close: () => void;
 * }}
 * `open` 함수는 `overlayElement`를 받아 Overlay를 표시하고, `close` 함수는 등록된 Overlay를 닫습니다.
 *
 * @example
 * ```tsx
 * import { useOverlay } from '';
 *
 *  const overlay = useOverlay();
 *
 *   const openBottomSheet = useCallback(() => {
 *     return new Promise<void>((resolve) => {
 *       overlay.open(({ isOpen, exit, close }) => {
 *         const handleClose = () => {
 *           close();
 *           resolve();
 *         };
 *
 *         return (
 *           <BottomSheet
 *             open={isOpen}
 *             onClose={handleClose}
 *             onExited={exit}
 *             header={<BottomSheet.Header>BottomSheet V1 Header</BottomSheet.Header>}
 *             cta={<BottomSheet.CTA onPress={handleClose}>확인</BottomSheet.CTA>}
 *           >
 *             <View>
 *               <TextField size="classic" label="name" value="FOCUS ME" />
 *             </View>
 *           </BottomSheet>
 *         );
 *       });
 *     });
 *   }, []);
 * ```
 */
export declare function useOverlay(): {
    close: () => void;
    open: (overlayElement: CreateOverlayElement) => void;
};
