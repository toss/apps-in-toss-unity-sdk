import { ReactElement, ReactNode } from 'react';
interface Props {
    children: ReactNode;
}
/**
 * @name AppStateProvider
 * @description
 * A Provider that manages the `AppState` of React Native screens. This Provider manages the app's state (active, background, etc.) and passes it to child components that subscribe to it.
 * @link https://reactnative.dev/docs/appstate
 * @param {Props} children - Child components to be wrapped by `AppStateProvider`. These components can detect and respond to changes in `AppState`.
 * @returns {ReactElement} - A React Provider component wrapped with `AppStateProvider`.
 * @example
 * ```tsx
 * export function App() {
 *   return (
 *     <AppStateProvider>
 *      <MyApp />
 *     </AppStateProvider>
 *   );
 * }
 * ```
 */
export declare function AppStateProvider({ children }: Props): ReactElement;
/**
 * @category Hooks
 * @name useIsAppForeground
 * @description
 * Returns whether the React Native app is in the foreground state.
 *
 * @see https://reactnative.dev/docs/0.72/appstate#app-states
 * @returns {boolean} - Returns whether the app is in the foreground state.
 * @throws {Error} Throws an error when the managed AppState is `null`.
 * @example
 * ```typescript
 *  const isForeground = useIsAppForeground();
 * ```
 */
export declare const useIsAppForeground: () => boolean;
export {};
