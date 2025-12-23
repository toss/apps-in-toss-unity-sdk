export type HapticFeedbackType = 'tickWeak' | 'tap' | 'tickMedium' | 'softMedium' | 'basicWeak' | 'basicMedium' | 'success' | 'error' | 'wiggle' | 'confetti';
export interface HapticFeedbackOptions {
    type: HapticFeedbackType;
}
export declare function generateHapticFeedback(options: HapticFeedbackOptions): void;
