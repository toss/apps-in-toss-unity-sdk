export declare const androidTypographyRuleMaxCap: number[];

export declare const BADGE_PROPERTY_KEYS: readonly ["fontSize", "padding", "borderRadius"];

export declare type BadgeProperties = Record<BadgePropertyKey, PropertyValue>;

export declare type BadgePropertyKey = (typeof BADGE_PROPERTY_KEYS)[number];

export declare const badgeSizeMap: Record<FixedTypography, BadgeProperties>;

export declare const defaultTypographyRule: MobileTypographyRule;

export declare const FIXED_TYPOGRAPHY_COMPONENTS: FixedTypographyComponent[];

export declare const FIXED_TYPOGRAPHY_SIZES: FixedTypography[];

export declare type FixedTypography = `${FixedTypographyAsNumber}`;

export declare type FixedTypographyAsNumber = 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 24 | 25 | 26 | 27 | 28 | 29 | 30 | 31 | 32 | 33 | 34 | 35 | 36 | 37 | 38 | 39 | 40 | 41 | 42;

export declare type FixedTypographyComponent = 'text' | 'icon' | 'badge' | 'link';

export declare type FixedTypographyComponentPropertyKey = {
    text: TextPropertyKey;
    icon: IconPropertyKey;
    badge: BadgePropertyKey;
    link: LinkPropertyKey;
};

declare type FixedTypographySizeMap = Record<FixedTypography, {
    text: TextProperties;
    icon: IconProperties;
    badge: BadgeProperties;
    link: LinkProperties;
}>;

/**
 * {
 *   11: { text: { fontSize: ... }, icon: { ... }, ... }
 *   12: { text: { fontSize: ... }, icon: { ... }, ... }
 * }
 * 형태로 리그룹합니다.
 */
export declare const fixedTypographySizeMap: FixedTypographySizeMap;

export declare function getAndroidTypographyRule(fontScale: number): MobileTypographyRule;

export declare const getFixedTypographyMinSize: () => number;

export declare function getIosTypographyRule(fontA11y: IOSFontA11y): MobileTypographyRule;

export declare const ICON_PROPERTY_KEYS: readonly ["height"];

export declare type IconProperties = Record<IconPropertyKey, PropertyValue>;

export declare type IconPropertyKey = (typeof ICON_PROPERTY_KEYS)[number];

export declare const iconSizeMap: Record<FixedTypography, IconProperties>;

export declare type IOSFontA11y = 'Large' | 'xLarge' | 'xxLarge' | 'xxxLarge' | 'A11y_Medium' | 'A11y_Large' | 'A11y_xLarge' | 'A11y_xxLarge' | 'A11y_xxxLarge';

export declare enum IOSFontA11yStyle {
    Large = "Large",
    xLarge = "xLarge",
    xxLarge = "xxLarge",
    xxxLarge = "xxxLarge",
    A11y_Medium = "A11y_Medium",
    A11y_Large = "A11y_Large",
    A11y_xLarge = "A11y_xLarge",
    A11y_xxLarge = "A11y_xxLarge",
    A11y_xxxLarge = "A11y_xxxLarge"
}

export declare const iosFontScales: Record<IOSFontA11y, number>;

export declare const iosTypographyRules: Record<IOSFontA11y, MobileTypographyRule>;

export declare const LINK_PROPERTY_KEYS: readonly ["verticalPadding", "horizontalPadding", "borderRadius", "lightThickness", "boldThickness"];

export declare type LinkProperties = Record<LinkPropertyKey, PropertyValue>;

export declare type LinkPropertyKey = (typeof LINK_PROPERTY_KEYS)[number];

export declare const linkSizeMap: Record<FixedTypography, LinkProperties>;

export declare function makeFixedTypographyVariables(): Record<string, string>;

export declare function makeMobileTypographyVariables(rule: MobileTypographyRule): Record<string, string>;

export declare type MobileTypography = 't1' | 'st1' | 'st2' | 'st3' | 't2' | 'st4' | 'st5' | 'st6' | 't3' | 'st7' | 't4' | 'st8' | 'st9' | 't5' | 'st10' | 't6' | 'st11' | 't7' | 'st12' | 'st13';

export declare type MobileTypographyRule = [
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number,
number
];

/**
 * 컴포넌트를 표현하는데에 필요한 property 이름들
 */
export declare const PROPERTY_NAMES: {
    text: readonly TextPropertyKey[];
    icon: readonly IconPropertyKey[];
    badge: readonly BadgePropertyKey[];
    link: readonly LinkPropertyKey[];
};

export declare type PropertyShorthandValue = [number, number, number, number] | [number, number, number] | [number, number];

export declare type PropertyValue = SingleValue | PropertyShorthandValue;

export declare type SingleValue = number | `${string}%`;

/**
 * For TypeSafe Properties
 */
export declare const TEXT_PROPERTY_KEYS: readonly ["fontSize", "lineHeight"];

export declare type TextProperties = Record<TextPropertyKey, PropertyValue>;

export declare type TextPropertyKey = (typeof TEXT_PROPERTY_KEYS)[number];

export declare const textSizeMap: Record<FixedTypography, TextProperties>;

export declare const TYPOGRAPHY_RULE_ORDER: readonly MobileTypography[];

export declare const typographyBadgeStyles: Record<MobileTypography, Record<BadgePropertyKey, string>>;

export declare const typographyIconStyles: Record<MobileTypography, Record<IconPropertyKey, string>>;

export declare const typographyLinkStyles: Record<MobileTypography, Record<LinkPropertyKey, string>>;

export declare const typographyTextStyles: Record<MobileTypography, Record<TextPropertyKey, string>>;

/**
 * @deprecated use TYPOGRAPHY_RULE_ORDER instead
 */
export declare const TYPOGRAPY_RULE_ORDER: readonly MobileTypography[];

/**
 * Fixed Typography 의 css variable template
 */
export declare function varFixedTypography<T extends FixedTypographyComponent, P extends string = FixedTypographyComponentPropertyKey[T]>(size: FixedTypography, componentName: T, property: P): VariableType;

declare type VariableType = `--${string}`;

export declare function varMobileTypography<T extends FixedTypographyComponent, P extends string = FixedTypographyComponentPropertyKey[T]>(typography: MobileTypography, componentName: T, property: P): VariableType;

export { }
