import type { ReactNode } from 'react';
import type { Text, ViewProps } from 'react-native';
import type { TxtProps, TypographyKeys } from '../txt';
export interface HeaderPublicProps extends TxtProps {
    /**
     * 텍스트의 타이포그래피 스타일을 지정해요.
     * 각 스타일은 텍스트의 크기, 굵기, 간격 등을 다르게 설정해서 다양한 화면 환경과 디자인 요구를 만족해요.
     *
     * `t1` ~ `t7`: 주로 본문 텍스트에 사용하는 크기와 굵기 스타일이에요.
     * `st1` ~ `st13`: 서브 타이틀 또는 부가적인 텍스트에 적합한 스타일이에요. 예를 들어, `st1`은 작은 본문이나 설명 텍스트에 적합하고, `st10`은 강조 텍스트에 사용하기 좋아요.
     * @default "t2"
     */
    typography?: TypographyKeys;
    /**
     * 컴포넌트 하단의 여백을 결정해요.
     */
    paddingBottom?: number;
    /**
     * 컴포넌트 내부에 표시될 내용을 지정해요.
     */
    children: ReactNode;
    /**
     * 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: ViewProps['style'];
}
export type PostH1Props = HeaderPublicProps;
export declare const PostH1: import("react").MemoExoticComponent<import("react").ForwardRefExoticComponent<HeaderPublicProps & import("react").RefAttributes<Text>>>;
export type PostH2Props = HeaderPublicProps;
export declare const PostH2: import("react").MemoExoticComponent<import("react").ForwardRefExoticComponent<HeaderPublicProps & import("react").RefAttributes<Text>>>;
export type PostH3Props = HeaderPublicProps;
export declare const PostH3: import("react").MemoExoticComponent<import("react").ForwardRefExoticComponent<HeaderPublicProps & import("react").RefAttributes<Text>>>;
export type PostH4Props = HeaderPublicProps;
export declare const PostH4: import("react").MemoExoticComponent<import("react").ForwardRefExoticComponent<HeaderPublicProps & import("react").RefAttributes<Text>>>;
