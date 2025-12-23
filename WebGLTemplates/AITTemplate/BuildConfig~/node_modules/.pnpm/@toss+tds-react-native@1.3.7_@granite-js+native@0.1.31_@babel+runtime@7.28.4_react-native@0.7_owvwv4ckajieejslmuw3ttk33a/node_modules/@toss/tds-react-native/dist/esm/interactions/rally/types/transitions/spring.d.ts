export interface Spring {
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
     * Mass of the moving object.
     * Higher values will result in more lethargic movement.
     * @default 1
     */
    mass?: number;
    /**
     * The initial velocity of the spring.
     * @default 0.0
     */
    velocity?: number;
}
