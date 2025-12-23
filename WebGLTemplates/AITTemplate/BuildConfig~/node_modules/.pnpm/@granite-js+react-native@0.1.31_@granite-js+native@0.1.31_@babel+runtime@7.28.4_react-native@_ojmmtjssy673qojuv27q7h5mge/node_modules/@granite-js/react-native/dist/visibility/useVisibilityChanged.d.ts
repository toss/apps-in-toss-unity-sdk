import { PropsWithChildren, ReactElement } from 'react';
/**
 * @name VisibilityChangedProvider
 * @kind function
 * @description
 * A Provider that manages whether a React Native screen is visible.
 * It subscribes to the app's `visibilityChanged` event to detect and manage screen visibility.
 *
 * @param {ReactNode | undefined} children - Child components that check screen visibility.
 * @param {boolean} isVisible - A boolean value indicating whether the screen is visible.
 * @returns {ReactElement} - A React Provider component wrapped with `VisibilityChangedContext.Provider`.
 * @example
 * ```typescript
 * export function VisibilityProvider({ isVisible, children }: Props) {
 *   return (
 *     <VisibilityChangedProvider isVisible={isVisible}>
 *       {children}
 *     </VisibilityChangedProvider>
 *   );
 * }
 * ```
 */
export declare function VisibilityChangedProvider({ children, isVisible, }: PropsWithChildren<{
    isVisible: boolean;
}>): ReactElement;
/**
 * @name useVisibilityChanged
 * @category Hooks
 * @kind function
 * @description
 * A Hook that returns whether a React Native screen is visible.
 * @returns {boolean} - Returns whether the screen is visible.
 * @throws {Error} Throws an error when not used within a `VisibilityChangedProvider`.
 * @example
 * ```typescript
 * const isVisible = useVisibilityChanged();
 * console.log(isVisible);
 * // true or false
 * ```
 */
export declare function useVisibilityChanged(): boolean;
