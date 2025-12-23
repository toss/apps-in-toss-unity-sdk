import type { Layout, Length, Percentage, RelativeLength, RelativePercentage } from '../../../types';
export declare function getTranslateY(_value: Length | Percentage | RelativeLength | RelativePercentage, { layout, beforeValue, }: {
    layout: Layout;
    beforeValue: number;
}): number;
