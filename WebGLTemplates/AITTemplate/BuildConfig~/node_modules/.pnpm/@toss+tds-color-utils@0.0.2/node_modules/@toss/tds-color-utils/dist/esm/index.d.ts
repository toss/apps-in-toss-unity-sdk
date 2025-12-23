import { default as BezierEasing_2 } from 'bezier-easing';

/**
 * @name adaptive
 * @description
 * 토스에서 사용하는 Adaptive 색상들에 대한 CSS Variable 값을 반환합니다.
 * 예를 들어서, `adaptive.grey900` 은 라이트 모드에서는 검정색, 다크 모드에서는 흰색을 의미합니다.
 * 사용할 수 있는 값은 `grey50`, `grey100`, ... `grey900` 과 `opacity50`, `opacity100`, ... `opacity900`, `background`, `greyBackground`, `layeredBackground`, `floatBackground`, `disabledBlue500` 입니다.
 * @example
 * import { adaptive } from '@toss/tds-colors';
 *
 * const Container = styled.div`
 *   color: ${adaptive.grey900};
 * `;
 */
declare const adaptive: Record<"opacity50" | "opacity100" | "opacity200" | "opacity300" | "opacity400" | "opacity500" | "opacity600" | "opacity700" | "opacity800" | "opacity900" | "disabledBlue500" | "background" | "layeredBackground" | "floatBackground" | "dimmedBackground" | "hairlineBorder" | "greyBackground" | "pcScreenBg" | "grey50" | "grey100" | "grey200" | "grey300" | "grey400" | "grey500" | "grey600" | "grey700" | "grey800" | "grey900" | "greyOpacity50" | "greyOpacity100" | "greyOpacity200" | "greyOpacity300" | "greyOpacity400" | "greyOpacity500" | "greyOpacity600" | "greyOpacity700" | "greyOpacity800" | "greyOpacity900" | "blue50" | "blue100" | "blue200" | "blue300" | "blue400" | "blue500" | "blue600" | "blue700" | "blue800" | "blue900" | "red50" | "red100" | "red200" | "red300" | "red400" | "red500" | "red600" | "red700" | "red800" | "red900" | "orange50" | "orange100" | "orange200" | "orange300" | "orange400" | "orange500" | "orange600" | "orange700" | "orange800" | "orange900" | "yellow50" | "yellow100" | "yellow200" | "yellow300" | "yellow400" | "yellow500" | "yellow600" | "yellow700" | "yellow800" | "yellow900" | "green50" | "green100" | "green200" | "green300" | "green400" | "green500" | "green600" | "green700" | "green800" | "green900" | "teal50" | "teal100" | "teal200" | "teal300" | "teal400" | "teal500" | "teal600" | "teal700" | "teal800" | "teal900" | "purple50" | "purple100" | "purple200" | "purple300" | "purple400" | "purple500" | "purple600" | "purple700" | "purple800" | "purple900" | "backgroundDimmed" | "backgroundLevelB01" | "backgroundLevel01" | "backgroundLevel02", string>;

export declare type AdaptiveColor = AdaptiveColors[AdaptiveColorCode];

export declare type AdaptiveColorCode = keyof AdaptiveColors;

export declare type AdaptiveColors = typeof adaptive;

export declare type BezierArray = [number, number, number, number];

declare type BezierOption = {
    easing?: BezierArray | 'easeOut' | 'out';
    colors: [ColorWithOpacity, ColorWithOpacity] | [string, string];
    positions?: [number, number];
    /**
     * @description color-stop의 개수. 기본값은 1
     */
    colorStopCount?: number;
};

export declare type Color = AdaptiveColor | SingleColor;

export declare type ColorCode = AdaptiveColorCode | SingleColorCode;

export declare type Colors = AdaptiveColors | SingleColors;

/**
 * @name colors
 * @description
 * 토스에서 사용하는 다양한 색상 값입니다.
 * 예를 들어서, `colors.blue500` 은 토스에서 가장 자주 사용하는 파란색을 나타냅니다.
 * 구체적으로 어떤 색상을 사용해야 하는지는 Framer 등 디자인 시안을 참고해주세요.
 *
 * 이 색상값은 라이트 모드와 다크 모드에 따라서 값이 변경되지 않습니다. 라이트 모드와 다크 모드에 따라 바뀌는 색상을 쓰고 싶으시다면 `adaptive` 를 사용하세요.
 *
 * @example
 * import { colors } from '@toss/tds-colors';
 *
 * const Button = styled.button`
 *   background-color: ${colors.blue500};
 * `;
 */
declare const colors: Record<"darkThemeGrey50" | "darkThemeGrey100" | "darkThemeGrey200" | "darkThemeGrey300" | "darkThemeGrey400" | "darkThemeGrey500" | "darkThemeGrey600" | "darkThemeGrey700" | "darkThemeGrey800" | "darkThemeGrey900" | "lightThemeBackground" | "darkThemeBackground" | "lightThemeBackgroundLevelB01" | "darkThemeBackgroundLevelB01" | "lightThemeBackgroundLevel01" | "darkThemeBackgroundLevel01" | "lightThemeBackgroundLevel02" | "darkThemeBackgroundLevel02" | "background" | "layeredBackground" | "floatBackground" | "greyBackground" | "grey50" | "grey100" | "grey200" | "grey300" | "grey400" | "grey500" | "grey600" | "grey700" | "grey800" | "grey900" | "greyOpacity50" | "greyOpacity100" | "greyOpacity200" | "greyOpacity300" | "greyOpacity400" | "greyOpacity500" | "greyOpacity600" | "greyOpacity700" | "greyOpacity800" | "greyOpacity900" | "blue50" | "blue100" | "blue200" | "blue300" | "blue400" | "blue500" | "blue600" | "blue700" | "blue800" | "blue900" | "red50" | "red100" | "red200" | "red300" | "red400" | "red500" | "red600" | "red700" | "red800" | "red900" | "orange50" | "orange100" | "orange200" | "orange300" | "orange400" | "orange500" | "orange600" | "orange700" | "orange800" | "orange900" | "yellow50" | "yellow100" | "yellow200" | "yellow300" | "yellow400" | "yellow500" | "yellow600" | "yellow700" | "yellow800" | "yellow900" | "green50" | "green100" | "green200" | "green300" | "green400" | "green500" | "green600" | "green700" | "green800" | "green900" | "teal50" | "teal100" | "teal200" | "teal300" | "teal400" | "teal500" | "teal600" | "teal700" | "teal800" | "teal900" | "purple50" | "purple100" | "purple200" | "purple300" | "purple400" | "purple500" | "purple600" | "purple700" | "purple800" | "purple900" | "inverseGrey50" | "inverseGrey100" | "inverseGrey200" | "inverseGrey300" | "inverseGrey400" | "inverseGrey500" | "inverseGrey600" | "inverseGrey700" | "inverseGrey800" | "inverseGrey900" | "darkBackground" | "darkGreyBackground" | "darkLayeredBackground" | "darkFloatBackground" | "black" | "white" | "whiteOpacity50" | "whiteOpacity100" | "whiteOpacity200" | "whiteOpacity300" | "whiteOpacity400" | "whiteOpacity500" | "whiteOpacity600" | "whiteOpacity700" | "whiteOpacity800" | "whiteOpacity900" | "lightThemeGrey50" | "lightThemeGrey100" | "lightThemeGrey200" | "lightThemeGrey300" | "lightThemeGrey400" | "lightThemeGrey500" | "lightThemeGrey600" | "lightThemeGrey700" | "lightThemeGrey800" | "lightThemeGrey900" | "lightThemeBlue50" | "lightThemeBlue100" | "lightThemeBlue200" | "lightThemeBlue300" | "lightThemeBlue400" | "lightThemeBlue500" | "lightThemeBlue600" | "lightThemeBlue700" | "lightThemeBlue800" | "lightThemeBlue900" | "darkThemeBlue50" | "darkThemeBlue100" | "darkThemeBlue200" | "darkThemeBlue300" | "darkThemeBlue400" | "darkThemeBlue500" | "darkThemeBlue600" | "darkThemeBlue700" | "darkThemeBlue800" | "darkThemeBlue900" | "lightThemeRed50" | "lightThemeRed100" | "lightThemeRed200" | "lightThemeRed300" | "lightThemeRed400" | "lightThemeRed500" | "lightThemeRed600" | "lightThemeRed700" | "lightThemeRed800" | "lightThemeRed900" | "darkThemeRed50" | "darkThemeRed100" | "darkThemeRed200" | "darkThemeRed300" | "darkThemeRed400" | "darkThemeRed500" | "darkThemeRed600" | "darkThemeRed700" | "darkThemeRed800" | "darkThemeRed900" | "lightThemeOrange50" | "lightThemeOrange100" | "lightThemeOrange200" | "lightThemeOrange300" | "lightThemeOrange400" | "lightThemeOrange500" | "lightThemeOrange600" | "lightThemeOrange700" | "lightThemeOrange800" | "lightThemeOrange900" | "darkThemeOrange50" | "darkThemeOrange100" | "darkThemeOrange200" | "darkThemeOrange300" | "darkThemeOrange400" | "darkThemeOrange500" | "darkThemeOrange600" | "darkThemeOrange700" | "darkThemeOrange800" | "darkThemeOrange900" | "lightThemeYellow50" | "lightThemeYellow100" | "lightThemeYellow200" | "lightThemeYellow300" | "lightThemeYellow400" | "lightThemeYellow500" | "lightThemeYellow600" | "lightThemeYellow700" | "lightThemeYellow800" | "lightThemeYellow900" | "darkThemeYellow50" | "darkThemeYellow100" | "darkThemeYellow200" | "darkThemeYellow300" | "darkThemeYellow400" | "darkThemeYellow500" | "darkThemeYellow600" | "darkThemeYellow700" | "darkThemeYellow800" | "darkThemeYellow900" | "lightThemeGreen50" | "lightThemeGreen100" | "lightThemeGreen200" | "lightThemeGreen300" | "lightThemeGreen400" | "lightThemeGreen500" | "lightThemeGreen600" | "lightThemeGreen700" | "lightThemeGreen800" | "lightThemeGreen900" | "darkThemeGreen50" | "darkThemeGreen100" | "darkThemeGreen200" | "darkThemeGreen300" | "darkThemeGreen400" | "darkThemeGreen500" | "darkThemeGreen600" | "darkThemeGreen700" | "darkThemeGreen800" | "darkThemeGreen900" | "lightThemeTeal50" | "lightThemeTeal100" | "lightThemeTeal200" | "lightThemeTeal300" | "lightThemeTeal400" | "lightThemeTeal500" | "lightThemeTeal600" | "lightThemeTeal700" | "lightThemeTeal800" | "lightThemeTeal900" | "darkThemeTeal50" | "darkThemeTeal100" | "darkThemeTeal200" | "darkThemeTeal300" | "darkThemeTeal400" | "darkThemeTeal500" | "darkThemeTeal600" | "darkThemeTeal700" | "darkThemeTeal800" | "darkThemeTeal900" | "lightThemePurple50" | "lightThemePurple100" | "lightThemePurple200" | "lightThemePurple300" | "lightThemePurple400" | "lightThemePurple500" | "lightThemePurple600" | "lightThemePurple700" | "lightThemePurple800" | "lightThemePurple900" | "darkThemePurple50" | "darkThemePurple100" | "darkThemePurple200" | "darkThemePurple300" | "darkThemePurple400" | "darkThemePurple500" | "darkThemePurple600" | "darkThemePurple700" | "darkThemePurple800" | "darkThemePurple900" | "lightThemeBackgroundDimmed" | "darkThemeBackgroundDimmed" | "lightThemeHairlineBorder" | "darkThemeHairlineBorder" | "lightThemePcScreenBg" | "darkThemePcScreenBg", string>;

export declare type ColorScheme = 'light' | 'dark';

export declare type ColorValue = string;

declare type ColorWithOpacity = {
    value: string;
    opacity?: number;
};

/**
 *
 * @description hex, rgb, rgba 색상을 rgba로 변환하는 함수.
 * @description hex와 rgb에서 alpha는 값을 전달하지 않은 경우 기본값인 1로 지정됩니다.
 * @description rgba에서 alpha는 값을 전달하지 않은 경우 기존 alpha값을 유지합니다.
 */
export declare function convertColorToRgba(color: string, alpha?: number): string;

export declare function convertToFullHex(hex: string): string;

export declare type EaseFunction = (t: number) => number;

/**
 * @description color가 문자열이라면 value로 변환한다.
 */
export declare function ensureColorValue(colors: GetLinearGradientOption['colors']): ColorWithOpacity[];

export declare function extractNonNumeric(str: string): string | null;

export declare function extractNumber(str: string): number | null;

/**
 *
 * @description rgba 문자열에서 숫자만 array로 추출한다.
 */
export declare function extractNumbersFromRGBA(rgbaString: string): RGBA;

/**
 *
 * @description 특정 소수점 자릿수에서 반올림한 값을 반환한다.
 */
export declare function fixedDecimalRound(num: number, decimalPlaces: number): number;

export declare function getColorFormat(color: string): "HEX" | "RGB" | "RGBA" | undefined;

/**
 * '@toss/tds-colors'의 colors 혹은 adaptive을 받아 해당하는 색상값을 반환합니다(hex 혹은 rgba).
 */
export declare function getColorValue(option: {
    color: Color;
    colorScheme?: ColorScheme;
}): ColorValue;

/**
 * @deprecated color객체를 사용해주세요.
 * ex) color: ColorValue;  colorScheme?: ColorScheme
 */
export declare function getColorValue(option: Color): ColorValue;

/**
 *
 * @description 기본 position을 반환한다.
 */
export declare function getDefaultPosition(colors: GetLinearGradientOption['colors']): number[];

/**
 *
 * @description 숫자의 소수점 자릿수를 반환한다.
 */
export declare function getDigitCount(num: number): number;

/**
 *
 * @description rgba array를 easeFunction에 따라 easing한 rgba array를 반환한다.
 */
export declare function getEasedRGBAArray({ rgbas, easeFunction, positions, colorStopCount, }: {
    rgbas: [string, string];
    easeFunction: EaseFunction;
    positions: number[];
    colorStopCount: number;
}): {
    rgba: RGBA;
    position: number;
}[];

/**
 *
 * @description cubic bezier array를 easing 함수 형식으로 변환하는 함수
 */
export declare function getEaseFunction(bezierArray: BezierArray): BezierEasing_2.EasingFunction;

/**
 *
 * @description getLinearGradientFromArray와 여러 util을 조합해서 css linear-gradient를 반환하는 함수
 * @description colors의 opacity가 없을 경우, color의 opacity는 value의 opacity로 계산됩니다.
 * @description colors의 opacity가 있을 경우, color의 opacity는 value의 opacity에 opacity를 곱한 값으로 계산됩니다.
 */
export declare function getLinearGradient<Colors extends Array<ColorWithOpacity | string>>(option: GetLinearGradientOption<Colors>): string;

/**
 *
 * @description rgba array를 css linear-gradient 형식으로 변환하는 함수
 */
export declare function getLinearGradientFromArray({ direction, degree: _degree, rgbaArray, }: {
    /**
     * @deprecated degree를 대신 사용해주세요
     */
    direction?: string | number;
    degree?: string | number;
    rgbaArray: {
        rgba: RGBA;
        position: number;
    }[];
}): string;

export declare type GetLinearGradientOption<Colors extends Array<ColorWithOpacity | string> = Array<ColorWithOpacity | string>> = {
    /**
     * @deprecated degree를 대신 사용해주세요
     */
    direction?: string | number;
    degree?: string | number;
} & (LinearOption<Colors> | BezierOption);

export declare function getLinearGradientRgbaArray<Colors extends Array<ColorWithOpacity | string>>(option: Omit<GetLinearGradientOption<Colors>, 'degree'>): {
    rgba: RGBA;
    position: number;
}[];

export declare function getMultipliedAlphaInRgba(color: string, alpha: number): string;

/**
 * @deprecated use getNextColorValue
 */
export declare const getNextColorInPalette: typeof getNextColorValue;

export declare function getNextColorValue({ color, level, colorScheme, }: {
    color: Color;
    level?: number;
    colorScheme?: ColorScheme;
}): ColorValue;

/**
 *
 * @description colors를 받아서 rgba array를 반환하는 함수
 */
export declare function getRGBAFromColors(colors: {
    value: string;
    opacity?: number;
}[]): string[];

export declare function getUpscaleColor<T extends Colors>({ code, colors, level, }: {
    code: ColorCode;
    colors: T;
    level: number;
}): T[keyof T];

export declare const hexPattern: RegExp;

export declare function hexToRgba(color: string, alpha?: number): string;

export declare function isHexColor(color: string): boolean;

export declare function isRgbaColor(color: string): boolean;

export declare function isRgbColor(color: string): boolean;

export declare const LinearGradientBezier: {
    readonly easeOut: BezierArray;
    readonly out: BezierArray;
    readonly linear: BezierArray;
};

declare type LinearOption<Colors extends Array<ColorWithOpacity | string>> = {
    easing: 'linear';
    colors: [...Colors];
    /**
     * @description 색깔 분포. 색상 배열과 동일한 길이.
     */
    positions?: [...{
        [I in keyof Colors]: number;
    }];
    colorStopCount?: never;
};

export declare type Point = {
    x: number;
    y: number;
};

declare type ReversedObject<T extends Record<string, string>> = {
    [K in T[keyof T]]: Extract<keyof T, string>;
};

export declare function reverseKeyValue<T extends Record<string, string>>(obj: T): ReversedObject<T>;

export declare type RGBA = [number, number, number, number];

export declare const rgbaPattern: RegExp;

export declare function rgbaToRgba(color: string, alpha?: number): string;

export declare const rgbPattern: RegExp;

export declare function rgbToRgba(color: string, alpha?: number): string;

export declare type SingleColor = SingleColors[SingleColorCode];

export declare type SingleColorCode = keyof SingleColors;

export declare type SingleColors = typeof colors;

export declare function zipRgbasAndPosition<T extends RGBA[]>({ rgbas, positions, }: {
    rgbas: [...T];
    positions: {
        [k in keyof T]: number;
    };
}): {
    rgba: RGBA;
    position: number;
}[];

export { }
