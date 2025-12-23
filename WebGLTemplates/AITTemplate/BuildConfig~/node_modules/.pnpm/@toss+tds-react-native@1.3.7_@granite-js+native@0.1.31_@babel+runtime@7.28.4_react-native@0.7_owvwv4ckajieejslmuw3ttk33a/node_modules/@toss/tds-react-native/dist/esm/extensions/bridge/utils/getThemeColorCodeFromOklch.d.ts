import type { Oklch } from '../../../utils/ColorManager';
export type ThemeColorCode = 'red' | 'orange' | 'yellow' | 'green' | 'teal' | 'blue' | 'purple' | 'grey';
/**
 * OKLCH 색상 값에서 ThemeColorCode 추출
 */
export declare const getThemeColorCodeFromOklch: ({ c, h }: Oklch) => ThemeColorCode;
