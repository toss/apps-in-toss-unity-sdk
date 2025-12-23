import { ReactElement, ReactNode } from 'react';
interface Props {
    isVisible: boolean;
    children: ReactNode;
}
/**
 * @name VisibilityProvider
 * @description
 * A Provider that manages whether a ReactNative view is currently in the foreground state.
 * @param {boolean} isVisible - Whether the app is in the foreground state.
 * @param {ReactNode | undefined} children - Child components that observe `AppState`.
 * @returns {ReactElement} - A React Provider component wrapped with `VisibilityChangedProvider`.
 * @example
 * ```typescript
 *
 * function App() {
 *  return (
 *   <VisibilityProvider isVisible={true}>
 *     <MyApp />
 *   </VisibilityProvider>
 *  );
 * }
 *
 * ```
 */
export declare function VisibilityProvider({ isVisible, children }: Props): ReactElement;
export {};
