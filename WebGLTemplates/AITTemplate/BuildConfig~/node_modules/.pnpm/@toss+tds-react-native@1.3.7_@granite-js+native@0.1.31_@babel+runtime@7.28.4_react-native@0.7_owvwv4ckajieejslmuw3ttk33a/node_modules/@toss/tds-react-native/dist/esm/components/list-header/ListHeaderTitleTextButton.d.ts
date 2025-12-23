import type { TextButtonProps } from '../text-button';
export interface ListHeaderTitleTextButtonProps extends TextButtonProps {
    /**
     * `ListHeader.TitleTextButton` 컴포넌트의 텍스트 스타일을 설정해요.
     */
    typography: 't4' | 't5' | 't7';
}
export declare function ListHeaderTitleTextButton({ ...props }: ListHeaderTitleTextButtonProps): import("react/jsx-runtime").JSX.Element;
