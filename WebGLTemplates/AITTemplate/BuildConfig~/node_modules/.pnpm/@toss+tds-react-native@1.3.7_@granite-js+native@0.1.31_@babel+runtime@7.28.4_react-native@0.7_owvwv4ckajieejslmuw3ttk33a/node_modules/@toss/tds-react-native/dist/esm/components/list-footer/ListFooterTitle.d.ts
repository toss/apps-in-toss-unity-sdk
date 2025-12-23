import type { TxtProps } from '../txt';
export interface ListFooterTitleProps extends TxtProps {
    /**
     * `ListFooter.Title` 컴포넌트의 텍스트 스타일을 설정해요.
     * @default 't5'
     */
    typography?: TxtProps['typography'];
    /**
     * `ListFooter.Title` 컴포넌트의 텍스트 색상을 지정해요
     * @default colors.blue500
     */
    color?: string;
    /**
     * `ListFooter.Title` 컴포넌트의 텍스트 굵기를 설정해요.
     * @default 'medium'
     */
    fontWeight?: TxtProps['fontWeight'];
    /**
     * `ListFooter.Title` 컴포넌트 내부에 표시될 내용을 지정해요.
     */
    children: string;
}
export declare function ListFooterTitle({ typography, color, fontWeight, children, ...restProps }: ListFooterTitleProps): import("react/jsx-runtime").JSX.Element;
