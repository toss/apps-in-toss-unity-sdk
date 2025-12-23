import type { ReactNode } from 'react';
export type Left = {
    type: 'asset32' | 'asset40' | 'numberIcon';
};
export type Center = {
    type: 'A' | 'B' | 'C';
    hasDescription: boolean;
};
export type Right = {
    type: 'arrow' | 'button' | 'none';
};
interface ProviderProps {
    children: ReactNode;
}
export declare function StepperRowContextProvider({ children }: ProviderProps): import("react/jsx-runtime").JSX.Element;
export declare function useStepperRowContext(): {
    left: Left | undefined;
    center: Center | undefined;
    right: Right | undefined;
};
/**
 * NOTE(@hyeonji.shin):
 * object를 그대로 deps array에 넣으면 무한 렌더링이 발생해서 JSON.stringify를 사용합니다.
 */
export declare function useUpdateStepperRowLeft(left: Left): void;
export declare function useUpdateStepperRowCenter(center: Center): void;
export declare function useUpdateStepperRowRight(right: Right): void;
export {};
