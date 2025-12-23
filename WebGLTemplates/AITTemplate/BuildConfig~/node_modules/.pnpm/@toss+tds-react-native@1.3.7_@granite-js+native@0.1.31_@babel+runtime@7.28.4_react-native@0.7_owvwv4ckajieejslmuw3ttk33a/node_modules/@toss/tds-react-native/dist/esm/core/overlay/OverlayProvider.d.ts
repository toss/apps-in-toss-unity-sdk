import type { ReactElement, ReactNode } from 'react';
interface Props {
    children: ReactNode;
}
/**
 * @category Providers
 * @kind function
 * @name OverlayProvider
 * @description
 * OverlayContext를 하위 컴포넌트에 제공하는 Provider 컴포넌트예요.
 * 이 컴포넌트를 사용하려면 사용하는 화면의 `_app.tsx`에 `<OverlayProvider />`를 추가해야 해요.
 * 모달, 토스트 등 화면 위의 오버레이 상태를 선언적으로 관리할 수 있고, 여러 오버레이를 독립적으로 제어할 수 있어요.
 *
 * @interface
 * ```tsx
 * function OverlayProvider({ children }: Props): ReactElement
 * ```
 *
 * @param {ReactNode} children - OverlayContext를 바라보는 하위 컴포넌트예요.
 * @returns {ReactElement} - OverlayContext를 제공하는 Provider가 감싸는 컴포넌트예요.
 *
 * @example
 * ```tsx
 * export default function App({ Component, ...props }: AppProps) {
 *   return (
 *     <OverlayProvider>
 *       <Component {...props} />
 *     </OverlayProvider>
 *   )
 * }
 * ```
 */
export declare function OverlayProvider({ children }: Props): ReactElement;
export {};
