import type { ComponentProps, ReactNode } from 'react';
import { Button } from '../../button';
export interface SingleCTAProps extends ComponentProps<typeof Button> {
    hasPaddingBottom?: boolean;
    background?: 'default' | 'none';
    topAccessory?: ReactNode;
    bottomAccessory?: ReactNode;
    ctaContentGap?: number;
}
export declare const BottomSheetSingleCTA: import("react").NamedExoticComponent<SingleCTAProps>;
