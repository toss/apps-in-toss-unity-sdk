import type { BadgeProperties, IconProperties, LinkProperties, MobileTypography, TextProperties } from '@toss/tds-typography';
import type { TypographyLevel } from './typographyLevel';
export declare const Typography: Record<string, MobileTypography>;
export type TypographyKeys = (typeof Typography)[keyof typeof Typography];
export type TypographyMap = Record<TypographyKeys, {
    fontSize: number;
    lineHeight: number;
}>;
export type ComponentTypographyMap = Record<MobileTypography, {
    text: TextProperties;
    icon: IconProperties;
    badge: BadgeProperties;
    link: LinkProperties;
}>;
export declare function getTypographyMap(fontScale: TypographyLevel): TypographyMap;
export declare const defaultTypographyMap: TypographyMap;
export declare function getLinkTypography(fontScale: TypographyLevel): Record<TypographyKeys, LinkProperties>;
/**
 * Icon Typography Map
 */
export declare function getIconTypography(fontScale: TypographyLevel): Record<TypographyKeys, IconProperties>;
/**
 * Badge Typography Map
 */
export declare function getBadgeTypography(fontScale: TypographyLevel): Record<TypographyKeys, BadgeProperties>;
