import type { ReactNode } from 'react';
export interface DoubleCTAProps {
    leftButton: ReactNode;
    rightButton: ReactNode;
    hasPaddingBottom?: boolean;
    background?: 'default' | 'none';
    topAccessory?: ReactNode;
    bottomAccessory?: ReactNode;
    ctaContentGap?: number;
}
export declare const BottomSheetDoubleCTA: import("react").NamedExoticComponent<DoubleCTAProps>;
