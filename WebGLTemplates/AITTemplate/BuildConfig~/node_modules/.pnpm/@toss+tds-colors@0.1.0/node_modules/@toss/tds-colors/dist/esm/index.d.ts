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
export declare const adaptive: Record<"opacity50" | "opacity100" | "opacity200" | "opacity300" | "opacity400" | "opacity500" | "opacity600" | "opacity700" | "opacity800" | "opacity900" | "disabledBlue500" | "background" | "layeredBackground" | "floatBackground" | "dimmedBackground" | "hairlineBorder" | "greyBackground" | "pcScreenBg" | "grey50" | "grey100" | "grey200" | "grey300" | "grey400" | "grey500" | "grey600" | "grey700" | "grey800" | "grey900" | "greyOpacity50" | "greyOpacity100" | "greyOpacity200" | "greyOpacity300" | "greyOpacity400" | "greyOpacity500" | "greyOpacity600" | "greyOpacity700" | "greyOpacity800" | "greyOpacity900" | "blue50" | "blue100" | "blue200" | "blue300" | "blue400" | "blue500" | "blue600" | "blue700" | "blue800" | "blue900" | "red50" | "red100" | "red200" | "red300" | "red400" | "red500" | "red600" | "red700" | "red800" | "red900" | "orange50" | "orange100" | "orange200" | "orange300" | "orange400" | "orange500" | "orange600" | "orange700" | "orange800" | "orange900" | "yellow50" | "yellow100" | "yellow200" | "yellow300" | "yellow400" | "yellow500" | "yellow600" | "yellow700" | "yellow800" | "yellow900" | "green50" | "green100" | "green200" | "green300" | "green400" | "green500" | "green600" | "green700" | "green800" | "green900" | "teal50" | "teal100" | "teal200" | "teal300" | "teal400" | "teal500" | "teal600" | "teal700" | "teal800" | "teal900" | "purple50" | "purple100" | "purple200" | "purple300" | "purple400" | "purple500" | "purple600" | "purple700" | "purple800" | "purple900" | "backgroundDimmed" | "backgroundLevelB01" | "backgroundLevel01" | "backgroundLevel02", string>;

export declare const adaptiveDictionary: Record<"opacity50" | "opacity100" | "opacity200" | "opacity300" | "opacity400" | "opacity500" | "opacity600" | "opacity700" | "opacity800" | "opacity900" | "disabledBlue500" | "background" | "layeredBackground" | "floatBackground" | "dimmedBackground" | "hairlineBorder" | "greyBackground" | "pcScreenBg" | "grey50" | "grey100" | "grey200" | "grey300" | "grey400" | "grey500" | "grey600" | "grey700" | "grey800" | "grey900" | "greyOpacity50" | "greyOpacity100" | "greyOpacity200" | "greyOpacity300" | "greyOpacity400" | "greyOpacity500" | "greyOpacity600" | "greyOpacity700" | "greyOpacity800" | "greyOpacity900" | "blue50" | "blue100" | "blue200" | "blue300" | "blue400" | "blue500" | "blue600" | "blue700" | "blue800" | "blue900" | "red50" | "red100" | "red200" | "red300" | "red400" | "red500" | "red600" | "red700" | "red800" | "red900" | "orange50" | "orange100" | "orange200" | "orange300" | "orange400" | "orange500" | "orange600" | "orange700" | "orange800" | "orange900" | "yellow50" | "yellow100" | "yellow200" | "yellow300" | "yellow400" | "yellow500" | "yellow600" | "yellow700" | "yellow800" | "yellow900" | "green50" | "green100" | "green200" | "green300" | "green400" | "green500" | "green600" | "green700" | "green800" | "green900" | "teal50" | "teal100" | "teal200" | "teal300" | "teal400" | "teal500" | "teal600" | "teal700" | "teal800" | "teal900" | "purple50" | "purple100" | "purple200" | "purple300" | "purple400" | "purple500" | "purple600" | "purple700" | "purple800" | "purple900" | "backgroundDimmed" | "backgroundLevelB01" | "backgroundLevel01" | "backgroundLevel02", [string, string]>;

declare namespace adaptiveDictionary_2 {
    export {
        opacity50,
        opacity100,
        opacity200,
        opacity300,
        opacity400,
        opacity500,
        opacity600,
        opacity700,
        opacity800,
        opacity900,
        disabledBlue500,
        background,
        layeredBackground,
        floatBackground,
        dimmedBackground,
        hairlineBorder,
        greyBackground,
        pcScreenBg,
        grey50,
        grey100,
        grey200,
        grey300,
        grey400,
        grey500,
        grey600,
        grey700,
        grey800,
        grey900,
        greyOpacity50,
        greyOpacity100,
        greyOpacity200,
        greyOpacity300,
        greyOpacity400,
        greyOpacity500,
        greyOpacity600,
        greyOpacity700,
        greyOpacity800,
        greyOpacity900,
        blue50,
        blue100,
        blue200,
        blue300,
        blue400,
        blue500,
        blue600,
        blue700,
        blue800,
        blue900,
        red50,
        red100,
        red200,
        red300,
        red400,
        red500,
        red600,
        red700,
        red800,
        red900,
        orange50,
        orange100,
        orange200,
        orange300,
        orange400,
        orange500,
        orange600,
        orange700,
        orange800,
        orange900,
        yellow50,
        yellow100,
        yellow200,
        yellow300,
        yellow400,
        yellow500,
        yellow600,
        yellow700,
        yellow800,
        yellow900,
        green50,
        green100,
        green200,
        green300,
        green400,
        green500,
        green600,
        green700,
        green800,
        green900,
        teal50,
        teal100,
        teal200,
        teal300,
        teal400,
        teal500,
        teal600,
        teal700,
        teal800,
        teal900,
        purple50,
        purple100,
        purple200,
        purple300,
        purple400,
        purple500,
        purple600,
        purple700,
        purple800,
        purple900,
        backgroundDimmed,
        backgroundLevelB01,
        backgroundLevel01,
        backgroundLevel02
    }
}

declare const background: readonly ["#ffffff", "#17171c"];

declare const backgroundDimmed: readonly ["rgba(0,0,0,0.2)", "rgba(0,0,0,0.56)"];

declare const backgroundLevel01: readonly ["#ffffff", "#202027"];

declare const backgroundLevel02: readonly ["#ffffff", "#2c2c35"];

declare const backgroundLevelB01: readonly ["#f2f4f6", "#101013"];

declare const blue100: readonly ["#c9e2ff", "#23386a"];

declare const blue200: readonly ["#90c2ff", "#25478c"];

declare const blue300: readonly ["#64a8ff", "#265ab3"];

declare const blue400: readonly ["#4593fc", "#2970d9"];

declare const blue50: readonly ["#e8f3ff", "#202c4d"];

declare const blue500: readonly ["#3182f6", "#3485fa"];

declare const blue600: readonly ["#2272eb", "#449bff"];

declare const blue700: readonly ["#1b64da", "#61b0ff"];

declare const blue800: readonly ["#1957c2", "#8fcdff"];

declare const blue900: readonly ["#194aa6", "#c8e7ff"];

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
export declare const colors: Record<"darkThemeGrey50" | "darkThemeGrey100" | "darkThemeGrey200" | "darkThemeGrey300" | "darkThemeGrey400" | "darkThemeGrey500" | "darkThemeGrey600" | "darkThemeGrey700" | "darkThemeGrey800" | "darkThemeGrey900" | "lightThemeBackground" | "darkThemeBackground" | "lightThemeBackgroundLevelB01" | "darkThemeBackgroundLevelB01" | "lightThemeBackgroundLevel01" | "darkThemeBackgroundLevel01" | "lightThemeBackgroundLevel02" | "darkThemeBackgroundLevel02" | "background" | "layeredBackground" | "floatBackground" | "greyBackground" | "grey50" | "grey100" | "grey200" | "grey300" | "grey400" | "grey500" | "grey600" | "grey700" | "grey800" | "grey900" | "greyOpacity50" | "greyOpacity100" | "greyOpacity200" | "greyOpacity300" | "greyOpacity400" | "greyOpacity500" | "greyOpacity600" | "greyOpacity700" | "greyOpacity800" | "greyOpacity900" | "blue50" | "blue100" | "blue200" | "blue300" | "blue400" | "blue500" | "blue600" | "blue700" | "blue800" | "blue900" | "red50" | "red100" | "red200" | "red300" | "red400" | "red500" | "red600" | "red700" | "red800" | "red900" | "orange50" | "orange100" | "orange200" | "orange300" | "orange400" | "orange500" | "orange600" | "orange700" | "orange800" | "orange900" | "yellow50" | "yellow100" | "yellow200" | "yellow300" | "yellow400" | "yellow500" | "yellow600" | "yellow700" | "yellow800" | "yellow900" | "green50" | "green100" | "green200" | "green300" | "green400" | "green500" | "green600" | "green700" | "green800" | "green900" | "teal50" | "teal100" | "teal200" | "teal300" | "teal400" | "teal500" | "teal600" | "teal700" | "teal800" | "teal900" | "purple50" | "purple100" | "purple200" | "purple300" | "purple400" | "purple500" | "purple600" | "purple700" | "purple800" | "purple900" | "inverseGrey50" | "inverseGrey100" | "inverseGrey200" | "inverseGrey300" | "inverseGrey400" | "inverseGrey500" | "inverseGrey600" | "inverseGrey700" | "inverseGrey800" | "inverseGrey900" | "darkBackground" | "darkGreyBackground" | "darkLayeredBackground" | "darkFloatBackground" | "black" | "white" | "whiteOpacity50" | "whiteOpacity100" | "whiteOpacity200" | "whiteOpacity300" | "whiteOpacity400" | "whiteOpacity500" | "whiteOpacity600" | "whiteOpacity700" | "whiteOpacity800" | "whiteOpacity900" | "lightThemeGrey50" | "lightThemeGrey100" | "lightThemeGrey200" | "lightThemeGrey300" | "lightThemeGrey400" | "lightThemeGrey500" | "lightThemeGrey600" | "lightThemeGrey700" | "lightThemeGrey800" | "lightThemeGrey900" | "lightThemeBlue50" | "lightThemeBlue100" | "lightThemeBlue200" | "lightThemeBlue300" | "lightThemeBlue400" | "lightThemeBlue500" | "lightThemeBlue600" | "lightThemeBlue700" | "lightThemeBlue800" | "lightThemeBlue900" | "darkThemeBlue50" | "darkThemeBlue100" | "darkThemeBlue200" | "darkThemeBlue300" | "darkThemeBlue400" | "darkThemeBlue500" | "darkThemeBlue600" | "darkThemeBlue700" | "darkThemeBlue800" | "darkThemeBlue900" | "lightThemeRed50" | "lightThemeRed100" | "lightThemeRed200" | "lightThemeRed300" | "lightThemeRed400" | "lightThemeRed500" | "lightThemeRed600" | "lightThemeRed700" | "lightThemeRed800" | "lightThemeRed900" | "darkThemeRed50" | "darkThemeRed100" | "darkThemeRed200" | "darkThemeRed300" | "darkThemeRed400" | "darkThemeRed500" | "darkThemeRed600" | "darkThemeRed700" | "darkThemeRed800" | "darkThemeRed900" | "lightThemeOrange50" | "lightThemeOrange100" | "lightThemeOrange200" | "lightThemeOrange300" | "lightThemeOrange400" | "lightThemeOrange500" | "lightThemeOrange600" | "lightThemeOrange700" | "lightThemeOrange800" | "lightThemeOrange900" | "darkThemeOrange50" | "darkThemeOrange100" | "darkThemeOrange200" | "darkThemeOrange300" | "darkThemeOrange400" | "darkThemeOrange500" | "darkThemeOrange600" | "darkThemeOrange700" | "darkThemeOrange800" | "darkThemeOrange900" | "lightThemeYellow50" | "lightThemeYellow100" | "lightThemeYellow200" | "lightThemeYellow300" | "lightThemeYellow400" | "lightThemeYellow500" | "lightThemeYellow600" | "lightThemeYellow700" | "lightThemeYellow800" | "lightThemeYellow900" | "darkThemeYellow50" | "darkThemeYellow100" | "darkThemeYellow200" | "darkThemeYellow300" | "darkThemeYellow400" | "darkThemeYellow500" | "darkThemeYellow600" | "darkThemeYellow700" | "darkThemeYellow800" | "darkThemeYellow900" | "lightThemeGreen50" | "lightThemeGreen100" | "lightThemeGreen200" | "lightThemeGreen300" | "lightThemeGreen400" | "lightThemeGreen500" | "lightThemeGreen600" | "lightThemeGreen700" | "lightThemeGreen800" | "lightThemeGreen900" | "darkThemeGreen50" | "darkThemeGreen100" | "darkThemeGreen200" | "darkThemeGreen300" | "darkThemeGreen400" | "darkThemeGreen500" | "darkThemeGreen600" | "darkThemeGreen700" | "darkThemeGreen800" | "darkThemeGreen900" | "lightThemeTeal50" | "lightThemeTeal100" | "lightThemeTeal200" | "lightThemeTeal300" | "lightThemeTeal400" | "lightThemeTeal500" | "lightThemeTeal600" | "lightThemeTeal700" | "lightThemeTeal800" | "lightThemeTeal900" | "darkThemeTeal50" | "darkThemeTeal100" | "darkThemeTeal200" | "darkThemeTeal300" | "darkThemeTeal400" | "darkThemeTeal500" | "darkThemeTeal600" | "darkThemeTeal700" | "darkThemeTeal800" | "darkThemeTeal900" | "lightThemePurple50" | "lightThemePurple100" | "lightThemePurple200" | "lightThemePurple300" | "lightThemePurple400" | "lightThemePurple500" | "lightThemePurple600" | "lightThemePurple700" | "lightThemePurple800" | "lightThemePurple900" | "darkThemePurple50" | "darkThemePurple100" | "darkThemePurple200" | "darkThemePurple300" | "darkThemePurple400" | "darkThemePurple500" | "darkThemePurple600" | "darkThemePurple700" | "darkThemePurple800" | "darkThemePurple900" | "lightThemeBackgroundDimmed" | "darkThemeBackgroundDimmed" | "lightThemeHairlineBorder" | "darkThemeHairlineBorder" | "lightThemePcScreenBg" | "darkThemePcScreenBg", string>;

/**
 * @description
 * 다크모드에 따라 colors를 가져오기 위한 유틸리티입니다.
 *
 * @example
 * const colors = colorPreference === 'light' ? colorsByPreference.light : colorsByPreference.dark;
 * <Txt color={colors.grey50} />
 */
export declare const colorsByPreference: {
    light: LightColorSet;
    dark: DarkColorSet;
};

export declare const colorsCSS: string;

declare type DarkColorSet = {
    [ColorName in keyof typeof adaptiveDictionary_2]: (typeof adaptiveDictionary_2)[ColorName][1];
};

export declare const darkCSS: string;

export declare const darkTDSVariablesCSS: string;

declare const dimmedBackground: readonly ["rgba(0,0,0,0.2)", "rgba(0,0,0,0.56)"];

/** @deprecated */
declare const disabledBlue500: readonly ["#c9e2ff", string];

declare const floatBackground: readonly ["#ffffff", "#2c2c35"];

export declare function getDarkColor(color: keyof typeof adaptiveDictionary_2): "#f9fafb" | "#f2f4f6" | "#e5e8eb" | "#d1d6db" | "#b0b8c1" | "#8b95a1" | "#6b7684" | "#4e5968" | "#333d4b" | "#191f28" | "rgba(0,23,51,0.02)" | "rgba(2,32,71,0.05)" | "rgba(0,27,55,0.1)" | "rgba(0,29,58,0.18)" | "rgba(0,25,54,0.31)" | "rgba(3,24,50,0.46)" | "rgba(0,19,43,0.58)" | "rgba(3,18,40,0.7)" | "rgba(0,12,30,0.8)" | "rgba(2,9,19,0.91)" | "#ffffff" | "#e8f3ff" | "#c9e2ff" | "#90c2ff" | "#64a8ff" | "#4593fc" | "#3182f6" | "#2272eb" | "#1b64da" | "#1957c2" | "#194aa6" | "#ffeeee" | "#ffd4d6" | "#feafb4" | "#fb8890" | "#f66570" | "#f04452" | "#e42939" | "#d22030" | "#bc1b2a" | "#a51926" | "#fff3e0" | "#ffe0b0" | "#ffcd80" | "#ffbd51" | "#ffa927" | "#fe9800" | "#fb8800" | "#f57800" | "#ed6700" | "#e45600" | "#fff9e7" | "#ffefbf" | "#ffe69b" | "#ffdd78" | "#ffd158" | "#ffc342" | "#ffb331" | "#faa131" | "#ee8f11" | "#dd7d02" | "#f0faf6" | "#aeefd5" | "#76e4b8" | "#3fd599" | "#15c47e" | "#03b26c" | "#02a262" | "#029359" | "#028450" | "#027648" | "#edf8f8" | "#bce9e9" | "#89d8d8" | "#58c7c7" | "#30b6b6" | "#18a5a5" | "#109595" | "#0c8585" | "#097575" | "#076565" | "#f9f0fc" | "#edccf8" | "#da9bef" | "#c770e4" | "#b44bd7" | "#a234c7" | "#9128b4" | "#8222a2" | "#73228e" | "#65237b" | "rgba(209,209,253,0.05)" | "rgba(217,217,255,0.11)" | "rgba(222,222,255,0.19)" | "rgba(224,224,255,0.27)" | "rgba(232,232,253,0.36)" | "rgba(242,242,255,0.47)" | "rgba(248,248,255,0.6)" | "rgba(253,253,255,0.75)" | "rgba(253,253,254,0.89)" | "rgba(255,255,255,1)" | "#202027" | "#2c2c35" | "#3c3c47" | "#4d4d59" | "#62626d" | "#7e7e87" | "#9e9ea4" | "#c3c3c6" | "#e4e4e5" | "#202c4d" | "#23386a" | "#25478c" | "#265ab3" | "#2970d9" | "#3485fa" | "#449bff" | "#61b0ff" | "#8fcdff" | "#c8e7ff" | "#3c2020" | "#562025" | "#7a242d" | "#9e2733" | "#ca2f3d" | "#f04251" | "#fa616d" | "#fe818b" | "#ffa8ad" | "#ffd1d3" | "#3d2500" | "#563200" | "#804600" | "#a85f00" | "#cf7200" | "#f18600" | "#fd9528" | "#ffa861" | "#ffc39e" | "#ffe4d6" | "#3d2d1a" | "#724c1e" | "#b56f1d" | "#eb8b1e" | "#ffa126" | "#ffb134" | "#ffc259" | "#ffd68a" | "#ffe5b2" | "#fff1d4" | "#153729" | "#135338" | "#136d47" | "#138a59" | "#13a065" | "#16bb76" | "#26cf88" | "#4ee4a6" | "#82f6c5" | "#ccffea" | "#203537" | "#224e51" | "#226368" | "#247e85" | "#26939a" | "#2eaab2" | "#43bec7" | "#65d4dc" | "#9be8ee" | "#d6fcff" | "#3f2447" | "#522361" | "#66247b" | "#7b2595" | "#962fb5" | "#ae3dd1" | "#c353e5" | "#d77cf2" | "#eaacfc" | "#f6d9ff" | "rgba(0,0,0,0.2)" | "rgba(0,0,0,0.56)" | "#101013" | "#17171c" | "#f6f7f9" | "#000000";

export declare function getLightColor(color: keyof typeof adaptiveDictionary_2): "#f9fafb" | "#f2f4f6" | "#e5e8eb" | "#d1d6db" | "#b0b8c1" | "#8b95a1" | "#6b7684" | "#4e5968" | "#333d4b" | "#191f28" | "rgba(0,23,51,0.02)" | "rgba(2,32,71,0.05)" | "rgba(0,27,55,0.1)" | "rgba(0,29,58,0.18)" | "rgba(0,25,54,0.31)" | "rgba(3,24,50,0.46)" | "rgba(0,19,43,0.58)" | "rgba(3,18,40,0.7)" | "rgba(0,12,30,0.8)" | "rgba(2,9,19,0.91)" | "#ffffff" | "#e8f3ff" | "#c9e2ff" | "#90c2ff" | "#64a8ff" | "#4593fc" | "#3182f6" | "#2272eb" | "#1b64da" | "#1957c2" | "#194aa6" | "#ffeeee" | "#ffd4d6" | "#feafb4" | "#fb8890" | "#f66570" | "#f04452" | "#e42939" | "#d22030" | "#bc1b2a" | "#a51926" | "#fff3e0" | "#ffe0b0" | "#ffcd80" | "#ffbd51" | "#ffa927" | "#fe9800" | "#fb8800" | "#f57800" | "#ed6700" | "#e45600" | "#fff9e7" | "#ffefbf" | "#ffe69b" | "#ffdd78" | "#ffd158" | "#ffc342" | "#ffb331" | "#faa131" | "#ee8f11" | "#dd7d02" | "#f0faf6" | "#aeefd5" | "#76e4b8" | "#3fd599" | "#15c47e" | "#03b26c" | "#02a262" | "#029359" | "#028450" | "#027648" | "#edf8f8" | "#bce9e9" | "#89d8d8" | "#58c7c7" | "#30b6b6" | "#18a5a5" | "#109595" | "#0c8585" | "#097575" | "#076565" | "#f9f0fc" | "#edccf8" | "#da9bef" | "#c770e4" | "#b44bd7" | "#a234c7" | "#9128b4" | "#8222a2" | "#73228e" | "#65237b" | "rgba(209,209,253,0.05)" | "rgba(217,217,255,0.11)" | "rgba(222,222,255,0.19)" | "rgba(224,224,255,0.27)" | "rgba(232,232,253,0.36)" | "rgba(242,242,255,0.47)" | "rgba(248,248,255,0.6)" | "rgba(253,253,255,0.75)" | "rgba(253,253,254,0.89)" | "rgba(255,255,255,1)" | "#202027" | "#2c2c35" | "#3c3c47" | "#4d4d59" | "#62626d" | "#7e7e87" | "#9e9ea4" | "#c3c3c6" | "#e4e4e5" | "#202c4d" | "#23386a" | "#25478c" | "#265ab3" | "#2970d9" | "#3485fa" | "#449bff" | "#61b0ff" | "#8fcdff" | "#c8e7ff" | "#3c2020" | "#562025" | "#7a242d" | "#9e2733" | "#ca2f3d" | "#f04251" | "#fa616d" | "#fe818b" | "#ffa8ad" | "#ffd1d3" | "#3d2500" | "#563200" | "#804600" | "#a85f00" | "#cf7200" | "#f18600" | "#fd9528" | "#ffa861" | "#ffc39e" | "#ffe4d6" | "#3d2d1a" | "#724c1e" | "#b56f1d" | "#eb8b1e" | "#ffa126" | "#ffb134" | "#ffc259" | "#ffd68a" | "#ffe5b2" | "#fff1d4" | "#153729" | "#135338" | "#136d47" | "#138a59" | "#13a065" | "#16bb76" | "#26cf88" | "#4ee4a6" | "#82f6c5" | "#ccffea" | "#203537" | "#224e51" | "#226368" | "#247e85" | "#26939a" | "#2eaab2" | "#43bec7" | "#65d4dc" | "#9be8ee" | "#d6fcff" | "#3f2447" | "#522361" | "#66247b" | "#7b2595" | "#962fb5" | "#ae3dd1" | "#c353e5" | "#d77cf2" | "#eaacfc" | "#f6d9ff" | "rgba(0,0,0,0.2)" | "rgba(0,0,0,0.56)" | "#101013" | "#17171c" | "#f6f7f9" | "#000000";

declare const green100: readonly ["#aeefd5", "#135338"];

declare const green200: readonly ["#76e4b8", "#136d47"];

declare const green300: readonly ["#3fd599", "#138a59"];

declare const green400: readonly ["#15c47e", "#13a065"];

declare const green50: readonly ["#f0faf6", "#153729"];

declare const green500: readonly ["#03b26c", "#16bb76"];

declare const green600: readonly ["#02a262", "#26cf88"];

declare const green700: readonly ["#029359", "#4ee4a6"];

declare const green800: readonly ["#028450", "#82f6c5"];

declare const green900: readonly ["#027648", "#ccffea"];

declare const grey100: readonly ["#f2f4f6", "#2c2c35"];

declare const grey200: readonly ["#e5e8eb", "#3c3c47"];

declare const grey300: readonly ["#d1d6db", "#4d4d59"];

declare const grey400: readonly ["#b0b8c1", "#62626d"];

declare const grey50: readonly ["#f9fafb", "#202027"];

declare const grey500: readonly ["#8b95a1", "#7e7e87"];

declare const grey600: readonly ["#6b7684", "#9e9ea4"];

declare const grey700: readonly ["#4e5968", "#c3c3c6"];

declare const grey800: readonly ["#333d4b", "#e4e4e5"];

declare const grey900: readonly ["#191f28", "#ffffff"];

declare const greyBackground: readonly ["#f2f4f6", "#101013"];

declare const greyOpacity100: readonly ["rgba(2,32,71,0.05)", "rgba(217,217,255,0.11)"];

declare const greyOpacity200: readonly ["rgba(0,27,55,0.1)", "rgba(222,222,255,0.19)"];

declare const greyOpacity300: readonly ["rgba(0,29,58,0.18)", "rgba(224,224,255,0.27)"];

declare const greyOpacity400: readonly ["rgba(0,25,54,0.31)", "rgba(232,232,253,0.36)"];

declare const greyOpacity50: readonly ["rgba(0,23,51,0.02)", "rgba(209,209,253,0.05)"];

declare const greyOpacity500: readonly ["rgba(3,24,50,0.46)", "rgba(242,242,255,0.47)"];

declare const greyOpacity600: readonly ["rgba(0,19,43,0.58)", "rgba(248,248,255,0.6)"];

declare const greyOpacity700: readonly ["rgba(3,18,40,0.7)", "rgba(253,253,255,0.75)"];

declare const greyOpacity800: readonly ["rgba(0,12,30,0.8)", "rgba(253,253,254,0.89)"];

declare const greyOpacity900: readonly ["rgba(2,9,19,0.91)", "#ffffff"];

declare const hairlineBorder: readonly ["#e5e8eb", "#3c3c47"];

declare const layeredBackground: readonly ["#ffffff", "#202027"];

declare type LightColorSet = {
    [ColorName in keyof typeof adaptiveDictionary_2]: (typeof adaptiveDictionary_2)[ColorName][0];
};

export declare const lightCSS: string;

export declare const lightTDSVariablesCSS: string;

/** @deprecated 토큰 이름이 변경되었습니다. opacity100 대신 greyOpacity100를 사용해주세요.*/
declare const opacity100: readonly ["rgba(2,32,71,0.05)", "rgba(217,217,255,0.11)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity200 대신 greyOpacity200를 사용해주세요.*/
declare const opacity200: readonly ["rgba(0,27,55,0.1)", "rgba(222,222,255,0.19)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity300 대신 greyOpacity300를 사용해주세요.*/
declare const opacity300: readonly ["rgba(0,29,58,0.18)", "rgba(224,224,255,0.27)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity400 대신 greyOpacity400를 사용해주세요.*/
declare const opacity400: readonly ["rgba(0,25,54,0.31)", "rgba(232,232,253,0.36)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity50 대신 greyOpacity50를 사용해주세요.*/
declare const opacity50: readonly ["rgba(0,23,51,0.02)", "rgba(209,209,253,0.05)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity500 대신 greyOpacity500를 사용해주세요.*/
declare const opacity500: readonly ["rgba(3,24,50,0.46)", "rgba(242,242,255,0.47)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity600 대신 greyOpacity600를 사용해주세요.*/
declare const opacity600: readonly ["rgba(0,19,43,0.58)", "rgba(248,248,255,0.6)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity700 대신 greyOpacity700를 사용해주세요.*/
declare const opacity700: readonly ["rgba(3,18,40,0.7)", "rgba(253,253,255,0.75)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity800 대신 greyOpacity800를 사용해주세요.*/
declare const opacity800: readonly ["rgba(0,12,30,0.8)", "rgba(253,253,254,0.89)"];

/** @deprecated 토큰 이름이 변경되었습니다. opacity900 대신 greyOpacity900를 사용해주세요.*/
declare const opacity900: readonly ["rgba(2,9,19,0.91)", "rgba(255,255,255,1)"];

declare const orange100: readonly ["#ffe0b0", "#563200"];

declare const orange200: readonly ["#ffcd80", "#804600"];

declare const orange300: readonly ["#ffbd51", "#a85f00"];

declare const orange400: readonly ["#ffa927", "#cf7200"];

declare const orange50: readonly ["#fff3e0", "#3d2500"];

declare const orange500: readonly ["#fe9800", "#f18600"];

declare const orange600: readonly ["#fb8800", "#fd9528"];

declare const orange700: readonly ["#f57800", "#ffa861"];

declare const orange800: readonly ["#ed6700", "#ffc39e"];

declare const orange900: readonly ["#e45600", "#ffe4d6"];

declare const pcScreenBg: readonly ["#f6f7f9", "#202027"];

declare const purple100: readonly ["#edccf8", "#522361"];

declare const purple200: readonly ["#da9bef", "#66247b"];

declare const purple300: readonly ["#c770e4", "#7b2595"];

declare const purple400: readonly ["#b44bd7", "#962fb5"];

declare const purple50: readonly ["#f9f0fc", "#3f2447"];

declare const purple500: readonly ["#a234c7", "#ae3dd1"];

declare const purple600: readonly ["#9128b4", "#c353e5"];

declare const purple700: readonly ["#8222a2", "#d77cf2"];

declare const purple800: readonly ["#73228e", "#eaacfc"];

declare const purple900: readonly ["#65237b", "#f6d9ff"];

declare const red100: readonly ["#ffd4d6", "#562025"];

declare const red200: readonly ["#feafb4", "#7a242d"];

declare const red300: readonly ["#fb8890", "#9e2733"];

declare const red400: readonly ["#f66570", "#ca2f3d"];

declare const red50: readonly ["#ffeeee", "#3c2020"];

declare const red500: readonly ["#f04452", "#f04251"];

declare const red600: readonly ["#e42939", "#fa616d"];

declare const red700: readonly ["#d22030", "#fe818b"];

declare const red800: readonly ["#bc1b2a", "#ffa8ad"];

declare const red900: readonly ["#a51926", "#ffd1d3"];

export declare const tds: {
    tHairlineBackground: string;
    tGradientToTop: string;
    tGradientToRight: string;
    tGradientToLeft: string;
    tGradientToBottom: string;
    tLayeredGradientToTop: string;
    tLayeredGradientToBottom: string;
    tRadioIndicatorBackgroundColor: string;
    tToastBackground: string;
    tDarkFillButtonBackground: string;
    tPrimaryWeakButtonBackground: string;
    tDangerWeakButtonBackground: string;
    tDarkWeakButtonBackground: string;
    tLightWeakButtonBackground: string;
    tButtonPressedAfterBackground: string;
    tPrimaryWeakButtonPressedBackground: string;
    tDangerWeakButtonPressedBackground: string;
    tDarkWeakButtonPressedBackground: string;
    tLightWeakButtonPressedBackground: string;
    tBlueBadgeColor: string;
    tBlueBadgeBackground: string;
    tTealBadgeColor: string;
    tTealBadgeBackground: string;
    tGreenBadgeColor: string;
    tGreenBadgeBackground: string;
    tRedBadgeColor: string;
    tRedBadgeBackground: string;
    tYellowBadgeColor: string;
    tYellowBadgeBackground: string;
    tElephantBadgeColor: string;
    tElephantBadgeBackground: string;
    tSwiperBulletBackground: string;
    tListRowDisabledBackground: string;
};

export declare const tdsDictionary: Record<keyof typeof tds, [string, string]>;

declare const teal100: readonly ["#bce9e9", "#224e51"];

declare const teal200: readonly ["#89d8d8", "#226368"];

declare const teal300: readonly ["#58c7c7", "#247e85"];

declare const teal400: readonly ["#30b6b6", "#26939a"];

declare const teal50: readonly ["#edf8f8", "#203537"];

declare const teal500: readonly ["#18a5a5", "#2eaab2"];

declare const teal600: readonly ["#109595", "#43bec7"];

declare const teal700: readonly ["#0c8585", "#65d4dc"];

declare const teal800: readonly ["#097575", "#9be8ee"];

declare const teal900: readonly ["#076565", "#d6fcff"];

declare const yellow100: readonly ["#ffefbf", "#724c1e"];

declare const yellow200: readonly ["#ffe69b", "#b56f1d"];

declare const yellow300: readonly ["#ffdd78", "#eb8b1e"];

declare const yellow400: readonly ["#ffd158", "#ffa126"];

declare const yellow50: readonly ["#fff9e7", "#3d2d1a"];

declare const yellow500: readonly ["#ffc342", "#ffb134"];

declare const yellow600: readonly ["#ffb331", "#ffc259"];

declare const yellow700: readonly ["#faa131", "#ffd68a"];

declare const yellow800: readonly ["#ee8f11", "#ffe5b2"];

declare const yellow900: readonly ["#dd7d02", "#fff1d4"];

export { }
