import type { DoubleCTAProps, SingleCTAProps } from './CTA';
import { BottomSheetCTA } from './CTA';
import type { HeaderProps } from './Header';
import { BottomSheetHeader } from './Header';
import type { HeaderDescriptionProps } from './HeaderDescription';
import { BottomSheetHeaderDescription } from './HeaderDescription';
import type { RootProps } from './Root';
import { BottomSheetRoot } from './Root';
import type { SelectProps } from './Select';
import { BottomSheetSelect } from './Select';
import type * as BottomSheetTypes from './types';
interface BottomSheetType {
    Root: typeof BottomSheetRoot;
    Header: typeof BottomSheetHeader;
    HeaderDescription: typeof BottomSheetHeaderDescription;
    CTA: typeof BottomSheetCTA;
    Select: typeof BottomSheetSelect;
}
export declare const BottomSheet: BottomSheetType;
export type { BottomSheetTypes, RootProps as BottomSheetRootProps, HeaderProps as BottomSheetHeaderProps, HeaderDescriptionProps as BottomSheetHeaderDescriptionProps, SingleCTAProps as BottomSheetSingleCTAProps, DoubleCTAProps as BottomSheetDoubleCTAProps, SelectProps as BottomSheetSelectProps, };
