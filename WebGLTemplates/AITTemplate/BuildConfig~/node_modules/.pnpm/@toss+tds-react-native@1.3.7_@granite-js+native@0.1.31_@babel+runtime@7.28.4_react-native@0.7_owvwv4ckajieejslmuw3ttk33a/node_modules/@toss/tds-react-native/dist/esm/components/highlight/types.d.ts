import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
export type Layout = Pick<HighlightUIProps, 'x' | 'y' | 'width' | 'height'>;
export type XAlignmentValue = 'left' | 'center' | 'right';
export type YAlignmentValue = 'top' | 'bottom';
export interface HighlightUIProps extends ViewProps {
    /**
     * @description 하이라이트 open 여부
     */
    open: boolean;
    /**
     * @description 하이라이팅 대상의 x 위치
     */
    x: number;
    /**
     * @description 하이라이팅 대상의 y 위치
     */
    y: number;
    /**
     * @description 하이라이팅 대상의 너비
     */
    width: number;
    /**
     * @description 하이라이팅 대상의 높이
     */
    height: number;
    /**
     * @description 하이라이트 내부 padding
     * @default 0
     */
    padding?: number;
    /**
     * @description 하이라이트 내부 left padding
     * @default padding
     */
    paddingLeft?: number;
    /**
     * @description 하이라이트 내부 right padding
     * @default padding
     */
    paddingRight?: number;
    /**
     * @description 하이라이트 내부 top padding
     * @default padding
     */
    paddingTop?: number;
    /**
     * @description 하이라이트 내부 bottom padding
     * @default padding
     */
    paddingBottom?: number;
    /**
     * @description 하이라이트 외부 영역에 표시할 메시지
     */
    message?: ReactNode;
    /**
     * @default colors.white
     * @description 하이라이트 외부 영역에 표시할 메시지 색상
     */
    messageColor?: string;
    /**
     * @description 하이라이트 외부 영역에 표시할 메시지의 x축 정렬
     */
    messageXAlignment?: XAlignmentValue;
    /**
     * @description 하이라이트 외부 영역에 표시할 메시지의 y축 정렬
     */
    messageYAlignment?: YAlignmentValue;
    /**
     * @description 하이라이트 외부 영역을 터치했을 때 호출되는 콜백
     */
    onPress?: () => void;
    /**
     * @description 하이라이트가 닫히는 애니메이션이 종료되었을 때 호출되는 콜백
     */
    onExited?: () => void;
    /**
     * @description 하이라이트 내부 영역을 터치했을 때 호출되는 콜백
     */
    onTargetPress?: () => void;
}
export interface HighlightProps extends Pick<HighlightUIProps, 'open' | 'padding' | 'paddingLeft' | 'paddingTop' | 'paddingRight' | 'paddingBottom' | 'message' | 'messageColor' | 'messageXAlignment' | 'messageYAlignment' | 'onPress' | 'onExited' | 'onTargetPress'>, ViewProps {
    /**
     * @description 하이라이트 시작 지연 시간 (단위: s)
     * @default 0
     */
    delay?: number;
    /**
     * @description 하이라이트의 닫힘 지연 시간 (단위: s)
     */
    closeLockDuration?: number;
}
