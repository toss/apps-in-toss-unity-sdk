import type { CreateOverlayElement } from './types';
interface Options {
    exitOnUnmount?: boolean;
}
/**
 * @public
 * @tag UI
 * @category Hooks
 * @kind function
 * @name useOverlayBase
 * @description
 * useOverlayBase는 Dialog처럼 별도의 UI 레이어를 띄우는 Overlay를 선언적으로 다루기 위한 hook이에요.
 * 이 hook을 사용하려면 사용하는 화면의 _app.tsx에 <OverlayProvider />를 추가해야 해요.
 * Promise와 함께 사용할 수 있어서 비동기 작업과 결합해 Overlay를 제어할 때 유용해요.
 * 여러 개의 Overlay가 필요할 때는 useOverlayBase를 여러 번 호출하면 돼요.
 *
 * @param {boolean} [exitOnUnmount=true]
 * useOverlayBase를 호출한 컴포넌트가 unmount될 때 overlay도 자동으로 unmount(=exit)되도록 설정하는 옵션이예요.
 * 만약 exitOnUnmount를 false로 설정하면, useOverlayBase를 호출한 컴포넌트가 unmount되어도 overlay는 자동으로 unmount되지 않고 등록된 overlay가 메모리에 계속 남아 있어요.
 * 이럴 때는 원하는 시점에 직접 exit 함수를 호출해서 overlay를 수동으로 unmount 해주세요.
 *
 * @return {{
 *  open: (overlayElement: CreateOverlayElement) => void;
 *  close: () => void;
 * }}
 * `open` 함수는 `overlayElement`를 받아 Overlay를 표시하고, `close` 함수는 등록된 Overlay를 닫아요.
 *
 * @example
 * ```tsx
 * // _app.tsx
 * import { OverlayProvider } from '@toss/tds-react-native/private';
 *
 * export default function App({ Component, pageProps }: AppProps) {
 *   return (
 *     <OverlayProvider>
 *       <Component {...pageProps} />
 *     </OverlayProvider>
 *   )
 * }
 * ```
 *
 * ```tsx
 * // Page.tsx
 * const overlay = useOverlayBase();
 * const openFooConfirmDialog = () => {
 *   return new Promise<boolean>(resolve => {
 *     overlay.open(({ isOpen, close }) => (
 *       <FooConfirmDialog
 *         open={isOpen}
 *         onClose={() => {
 *           resolve(false);
 *           close();
 *         }}
 *         onConfirm={() => {
 *           resolve(true);
 *           close();
 *         }}
 *       />
 *     ));
 *   });
 * };
 *
 * await openFooConfirmDialog();
 * // ConfirmDialog의 confirmButton을 누르거나 onClose가 호출된 후
 * console.log('dialog closed');
 * ```
 */
export declare function useOverlayBase({ exitOnUnmount }?: Options): {
    open: (overlayElement: CreateOverlayElement) => void;
    close: () => void;
};
export {};
