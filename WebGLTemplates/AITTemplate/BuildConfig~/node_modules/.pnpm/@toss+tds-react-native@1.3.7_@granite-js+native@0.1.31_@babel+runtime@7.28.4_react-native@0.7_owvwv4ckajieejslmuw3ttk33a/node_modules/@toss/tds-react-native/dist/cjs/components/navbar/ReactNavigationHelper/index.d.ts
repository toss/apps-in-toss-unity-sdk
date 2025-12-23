import { BackButton } from './BackButton';
import { RightTextButton } from './Right';
import { SubtitleTxt, Title, TitleTxt } from './Title';
import { HeaderLeft } from './HeaderLeft';
import { HeaderRight } from './HeaderRight';
import { HeaderTitle } from './HeaderTitle';
import { CloseButton } from './CloseButton';
export declare const ReactNavigationNavbar: {
    HeaderLeft: typeof HeaderLeft;
    HeaderRight: typeof HeaderRight;
    BackButton: typeof BackButton;
    CloseButton: typeof CloseButton;
    RightIconButton: import("react").ForwardRefExoticComponent<Pick<import("../../icon").IconProps & import("react").RefAttributes<import("react-native").View>, "type" | "name"> & import("react-native").AccessibilityProps & import("react-native").TouchableOpacityProps & import("react").RefAttributes<import("react-native").TouchableOpacity>>;
    RightTextButton: typeof RightTextButton;
    HeaderTitle: typeof HeaderTitle;
    TitleTxt: typeof TitleTxt;
    /**
     * @description headerTitleAlign: 'center' 과 함께 사용해주세요
     */
    DoubleLineTitle: typeof Title;
    SubtitleTxt: typeof SubtitleTxt;
};
