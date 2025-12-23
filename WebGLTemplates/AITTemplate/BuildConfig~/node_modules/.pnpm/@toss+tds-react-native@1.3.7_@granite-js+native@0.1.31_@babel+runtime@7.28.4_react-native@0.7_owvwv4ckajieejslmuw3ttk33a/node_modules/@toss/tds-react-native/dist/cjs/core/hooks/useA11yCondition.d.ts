import type { MobileTypography } from '@toss/tds-typography';
export type A11yCondition = {
    biggerThan?: number;
    biggerThanOrEqualTo?: number;
    smallerThan?: number;
    smallerThanOrEqualTo?: number;
};
export declare function useFontScaleCondition({ biggerThan, biggerThanOrEqualTo, smallerThan, smallerThanOrEqualTo, }: A11yCondition): boolean;
export declare function useFontSizeCondition(typography: MobileTypography, { biggerThan, biggerThanOrEqualTo, smallerThan, smallerThanOrEqualTo }: A11yCondition): boolean;
