import type { Layout, Length, Percentage, RelativeLength, RelativePercentage } from '../../../types';
export declare function getHeight(_value: Length | Percentage | RelativeLength | RelativePercentage, { layout, beforeValue, }: {
    layout: Layout;
    beforeValue: number;
}): number;
