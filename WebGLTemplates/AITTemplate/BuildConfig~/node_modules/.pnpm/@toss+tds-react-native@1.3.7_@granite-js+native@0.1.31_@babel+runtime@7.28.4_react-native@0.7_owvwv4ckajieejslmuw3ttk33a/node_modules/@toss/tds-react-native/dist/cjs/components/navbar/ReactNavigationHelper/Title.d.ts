import type { TxtProps } from '../../txt';
import type { ReactNode } from 'react';
interface BaseProps extends Pick<TxtProps, 'children' | 'color'> {
    /**
     * 더큰텍스트를 사용할지 여부를 결정합니다.
     * @default false
     */
    preventFontScalingA11y?: boolean;
}
interface TitleTxtProps extends BaseProps, Pick<TxtProps, 'fontWeight'> {
}
interface SubtitleTxtProps extends BaseProps {
}
interface TitleProps {
    upper: ReactNode;
    lower: ReactNode;
}
declare function TitleTxt({ color, fontWeight, preventFontScalingA11y, ...props }: TitleTxtProps): import("react/jsx-runtime").JSX.Element;
declare function SubtitleTxt({ color, preventFontScalingA11y, ...props }: SubtitleTxtProps): import("react/jsx-runtime").JSX.Element;
declare function Title({ upper, lower }: TitleProps): import("react/jsx-runtime").JSX.Element;
export { Title, TitleTxt, SubtitleTxt };
