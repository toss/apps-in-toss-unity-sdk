import type { DoubleCTAProps } from './DoubleCTA';
import { BottomSheetDoubleCTA } from './DoubleCTA';
import type { SingleCTAProps } from './SingleCTA';
import { BottomSheetSingleCTA } from './SingleCTA';
type BottomSheetCTAType = typeof BottomSheetSingleCTA & {
    Double: typeof BottomSheetDoubleCTA;
};
export declare const BottomSheetCTA: BottomSheetCTAType;
export type { SingleCTAProps, DoubleCTAProps };
