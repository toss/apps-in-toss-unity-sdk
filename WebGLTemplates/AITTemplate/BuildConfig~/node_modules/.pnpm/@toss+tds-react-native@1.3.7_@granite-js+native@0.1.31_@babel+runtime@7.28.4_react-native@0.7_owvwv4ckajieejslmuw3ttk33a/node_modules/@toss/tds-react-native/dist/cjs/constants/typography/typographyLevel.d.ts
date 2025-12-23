import type { IOSFontA11y } from '@toss/tds-typography';
import { iosFontScales } from '@toss/tds-typography';
import type { ValueOf } from '../types';
/** useWindowDimensions().fontScale 로 나온 fontScale */
export type IOSWindowFontScale = 3.571 | 3.143 | 2.643 | 2.143 | 1.786 | 1.353 | 1.235 | 1.118 | 1.0 | 0.941 | 0.882 | 0.823;
/**
 * useWindowDimensions().fontScale 을 TypographyLevel 값으로 맵핑
 */
export declare const iosWindowFontScaleToTypographyLevel: Record<IOSWindowFontScale, IOSFontA11y>;
/**
 * useWindowDimensions().fontScale 을 fontScale 로 맵핑
 */
export declare const iosWindowFontScaleToNormalizeFontScale: Record<IOSWindowFontScale, ValueOf<typeof iosFontScales>>;
export declare const iosNormalizeFontScaleToTypographyLevel: Record<ValueOf<typeof iosFontScales>, IOSFontA11y>;
export declare const iosTypographyLevelToNormalizeFontScale: Record<IOSFontA11y, ValueOf<typeof iosFontScales>>;
export { iosFontScales };
export type AndroidTypographyLevel = number;
/**
 * - iOS 는 typographyLevel 을 IOSFontA11y 로 받습니다.
 * - Android 는 typographyLevel 을 number 로 받습니다.
 */
export type TypographyLevel = IOSFontA11y | AndroidTypographyLevel;
