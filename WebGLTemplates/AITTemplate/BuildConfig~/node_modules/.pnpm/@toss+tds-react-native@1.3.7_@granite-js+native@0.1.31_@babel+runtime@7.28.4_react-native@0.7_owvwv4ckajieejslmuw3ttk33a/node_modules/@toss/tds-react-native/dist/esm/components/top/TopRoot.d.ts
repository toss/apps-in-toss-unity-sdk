import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import { View } from 'react-native';
type RightVerticalAlign = 'top' | 'center' | 'end';
type Props = {
    upperGap?: number;
    lowerGap?: number;
    rightVerticalAlign?: RightVerticalAlign;
    /**
     * @example
     * import { Asset, assetFrame } from '../../asset';
     * <Asset
     *   frame={{ ...assetFrame.CleanW60, color: 'transparent' }}
     *   resource={
     *     <Asset.ResourceImage
     *       source={{ uri: 'https://static.toss.im/3d-emojis/u1F31E.png' }}
     *       scale={1}
     *       scaleType="fit"
     *     />
     *   }
     * />
     */
    upper?: ReactNode;
    subtitle1?: ReactNode;
    title: ReactNode;
    subtitle2?: ReactNode;
    lower?: ReactNode;
    right?: ReactNode;
    style?: StyleProp<ViewStyle>;
};
declare function TopRoot({ upperGap, lowerGap, rightVerticalAlign, upper, subtitle1, title, subtitle2, lower, right, style, }: Props): import("react/jsx-runtime").JSX.Element;
declare namespace TopRoot {
    var Root: typeof TopRoot;
    var TitleParagraph: typeof import("./TopTitleParagraph").TitleParagraph;
    var TitleTextButton: typeof import("./TopTitleTextButton").TitleTextButton;
    var TitleSelector: typeof import("./TopTitleSelector").TitleSelector;
    var UpperBadges: ({ items }: import("./TopBadges").BadgesProps) => import("react/jsx-runtime").JSX.Element;
    var UpperAssetContent: typeof import("./TopUpperAssetContent").UpperAssetContent;
    var LowerButton: typeof import("./TopButton").LowerButton;
    var LowerCTA: typeof import("./TopCTA").LowerCTA;
    var SubtitleParagraph: typeof import("./TopSubtitleParagraph").SubtitleParagraph;
    var SubtitleTextButton: typeof import("./TopSubtitleTextButton").SubtitleTextButton;
    var SubtitleSelector: typeof import("./TopSubtitleSelector").SubtitleSelector;
    var SubtitleBadges: ({ items }: import("./TopBadges").BadgesProps) => import("react/jsx-runtime").JSX.Element;
    var RightButton: import("react").ForwardRefExoticComponent<import("../button").ButtonProps & {
        pointerEvents?: "box-none" | "none" | "box-only" | "auto" | undefined | undefined;
    } & import("react").RefAttributes<View>>;
    var RightArrow: typeof import("./TopArrow").RightArrow;
}
export { TopRoot };
