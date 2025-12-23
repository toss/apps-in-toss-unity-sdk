export declare const bezier: Record<BezierType, CubicBezier>;

/**
 * @deprecated
 * `beziers` 대신 `bezier` 를 사용하세요
 */
export declare const beziers: Record<BezierType, CubicBezier>;

/**
 * cubic bezier array를 css easing 형식으로 변환하는 함수
 * @param bezier
 * @returns css cubic-bezier string
 */
export declare const bezierToCss: (bezier: CubicBezier) => string;

export declare type BezierType = 'linear' | 'ease' | 'out' | 'expo' | 'back';

export declare type CubicBezier = [number, number, number, number];

/**
 * @deprecated
 * `easings` 대신 `bezier` 를 사용하세요
 */
export declare const easings: Record<BezierType, CubicBezier>;

/**
 * @description tension는 stiffness로, friction은 damping으로 대체한다.
 * @description 엄밀하게는 다른 값이지만, 편의상 같은 값으로 취급한다.
 */
declare interface Options {
    /**
     * Stiffness of the spring.
     * Higher values will create more sudden movement.
     * @default 100
     */
    stiffness?: number;
    /**
     * Strength of opposing force.
     * If set to 0, spring will oscillate indefinitely.
     * @default 10
     */
    damping?: number;
    /**
     * The initial velocity of the spring.
     * @default 0.0
     */
    velocity?: number;
    /**
     * The tolerance of the spring.
     * @default 0.01
     */
    tolerance?: number;
    /**
     * @deprecated 사용되지 않는 값입니다.
     */
    mass?: number;
}

declare type Spring = Options;

export declare const spring: Record<SpringType, Spring>;

/**
 * framer-motion, gsap 등 custom ease function을 지원하는 라이브러리에서 사용할 수 있는
 * ease function와 duration 세트
 */
export declare const springEaseWithDuration: Record<SpringType, {
    duration: number;
    easeFunction: (t: number) => number;
    ease: (t: number) => number;
}>;

export declare type SpringType = 'basic' | 'small' | 'medium' | 'large' | 'quick' | 'slow' | 'rapid' | 'bounce';

export { }
