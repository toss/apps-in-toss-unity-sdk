/**
 * @name useIsFocusedSafely
 * @category Hooks
 * @kind function
 * @link https://github.com/react-navigation/react-navigation/blob/%40react-navigation/native%406.1.18/packages/core/src/useIsFocused.tsx
 * @description
 * Returns whether the current screen is in focus.
 *
 * A Hook that safely uses `useIsFocused` provided by `@react-navigation/native`.
 * This Hook is based on `useIsFocused` from `@react-navigation/native`, but modified to not throw errors when `navigation` or `root` objects are `null` or `undefined`.
 * It ensures that users don't see errors even when the code is used in environments where `@react-navigation/native` is not used.
 *
 * @returns {boolean} - Returns the focus state of the current screen.
 * @example
 * ```typescript
 *  const isFocused = useIsFocusedSafely();
 *  console.log(isFocused); // true or false
 * ```
 */
export declare function useIsFocusedSafely(): boolean;
