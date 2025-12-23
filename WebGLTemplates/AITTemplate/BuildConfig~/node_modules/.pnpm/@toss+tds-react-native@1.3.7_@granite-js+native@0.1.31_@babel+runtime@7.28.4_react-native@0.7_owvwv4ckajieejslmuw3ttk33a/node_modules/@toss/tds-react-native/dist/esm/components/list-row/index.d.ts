import type { ListRowProps, ListRowRef } from './ListRow';
import { ListRowRightTexts } from './ListRowRightTexts';
import { ListRowTexts } from './ListRowTexts';
import { ListRowImageContainer } from './ListRowImageContainer';
import { ListRowIcon } from './ListRowIcon';
import { ListRowImage } from './ListRowImage';
import { ListRowLeftText } from './ListRowLeftText';
interface CompoundComposition {
    Icon: typeof ListRowIcon;
    Image: typeof ListRowImage;
    Texts: typeof ListRowTexts;
    LeftText: typeof ListRowLeftText;
    RightTexts: typeof ListRowRightTexts;
    ImageContainer: typeof ListRowImageContainer;
}
export declare const ListRow: import("react").ForwardRefExoticComponent<ListRowProps & import("react").RefAttributes<ListRowRef>> & CompoundComposition;
export type { ListRowProps, ListRowRef };
