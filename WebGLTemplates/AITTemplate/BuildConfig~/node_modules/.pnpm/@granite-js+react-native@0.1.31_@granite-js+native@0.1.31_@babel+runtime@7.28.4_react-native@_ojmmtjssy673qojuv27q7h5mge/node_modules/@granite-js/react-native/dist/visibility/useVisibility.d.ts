/**
 * @public
 * @category Screen Control
 * @name useVisibility
 * @description
 * Returns whether the screen is visible to the user.
 * Returns `true` if the app's screen is currently visible to the user, and `false` if it's not. However, the screen visibility state does not change when opening and closing the system share modal ([share](/reference/react-native/Share/share)).
 *
 * Usage examples:
 * - Returns `false` when switching to another app or pressing the home button.
 * - Returns `true` when returning to the granite app or when the screen becomes visible.
 * - Returns `false` when navigating to another service within the granite app.
 *
 * @returns {boolean} - Whether the current screen is visible to the user.
 * @example
 *
 * ### Example of checking screen visibility
 *
 * ```tsx
 * import { useEffect } from 'react';
 * import { Text } from 'react-native';
 * import { useVisibility } from '@granite-js/react-native';
 *
 * export function UseVisibilityExample() {
 *   const visibility = useVisibility();
 *
 *   useEffect(() => {
 *     console.log({ visibility });
 *   }, [visibility]);
 *
 *   return <Text>Logs are created when leaving and returning to the home screen.</Text>;
 * }
 * ```
 */
export declare function useVisibility(): boolean;
