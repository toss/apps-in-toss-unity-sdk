export declare function getSpringEasing({ stiffness, damping, mass, velocity }: Options): {
    duration: number;
    easeFunction: (t: number) => number;
};

/**
 * @description tension는 stiffness로, friction은 damping으로 대체한다.
 * @description 엄밀하게는 다른 값이지만, 편의상 같은 값으로 취급한다.
 */
export declare interface Options {
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

export declare type Spring = Options;

export { }
