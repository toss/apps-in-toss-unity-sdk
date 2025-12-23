import { Animated } from 'react-native';
export type SpringAnimationConfig = Animated.SpringAnimationConfig & {
    /**
     * 애니메이션이 끝났을 때 도달할 목표 값을 설정합니다
     * @remarks
     * react-native의 Animated.spring과 다르게 RgbaValue와 AnimatedColor 타입의 toValue를 지원하지 않습니다.
     */
    toValue: Animated.TimingAnimationConfig['toValue'];
    /**
     * 스프링의 강성.
     * 값이 높을수록 더 갑작스러운 움직임을 만듭니다.
     */
    stiffness?: number;
    /**
     * 반대 힘의 강도.
     * 0으로 설정하면 스프링이 무한히 진동합니다.
     */
    damping?: number;
    /**
     * 스프링의 초기 속도.
     */
    velocity?: number;
    /**
     * 스프링의 허용 오차.
     */
    tolerance?: number;
};
/**
 *
 * `@toss/tds-easings`의 `spring` 토큰을 사용하여 스프링 애니메이션을 생성합니다.
 *
 * @example
 * ```ts
 * import { spring } from '@toss/tds-easings';
 * import { Animated } from '';
 *
 * const value = new Animated.Value(0);
 * Animated.spring(value, {
 *  toValue: 1,
 *  useNativeDriver: false,
 *  ...spring.basic,
 * }).start();
 * ```
 */
export declare const spring: (value: Animated.Value | Animated.ValueXY, config: SpringAnimationConfig) => Animated.CompositeAnimation;
