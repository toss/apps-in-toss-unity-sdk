import { Animated } from 'react-native';
/**
 * @category Hooks
 * @name useKeyboardAnimatedHeight
 * @description
 * A Hook that returns an animatable value (`Animated.Value`) representing the keyboard height changes when the keyboard appears or disappears. You can smoothly animate UI elements according to the keyboard height as it rises or falls.
 *
 * This Hook is primarily used on iOS. On Android, it does not detect keyboard height changes and always returns an `Animated.Value` with an initial value of `0`. In other words, animations are not applied in the Android environment.
 *
 * @returns {Animated.Value} - An animation value representing the keyboard height.
 * @example
 * ```typescript
 * const keyboardHeight = useKeyboardAnimatedHeight();
 *
 * <Animated.View style={{ marginBottom: keyboardHeight }}>
 *  {children}
 * </Animated.View>
 * ```
 */
export declare function useKeyboardAnimatedHeight(): Animated.Value;
