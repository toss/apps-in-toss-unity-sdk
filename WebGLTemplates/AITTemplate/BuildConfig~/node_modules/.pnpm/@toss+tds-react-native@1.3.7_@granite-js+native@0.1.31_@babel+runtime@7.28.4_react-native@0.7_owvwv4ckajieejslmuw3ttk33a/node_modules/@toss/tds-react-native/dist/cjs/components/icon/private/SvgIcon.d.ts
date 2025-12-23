import type { SvgProps } from '@granite-js/native/react-native-svg';
import { Component } from 'react';
export interface SvgIconProps extends SvgProps {
    size?: number;
    color?: string;
    /** @default default */
    type?: 'default' | 'circle';
    name: string;
}
/**
 * @remarks react-native의 createAnimatedComponent가 functional component를
 * animating 하지 못하는 이슈가 있어서 class로 wrapping 합니다
 */
declare class SvgIconClass extends Component<SvgIconProps> {
    render(): import("react/jsx-runtime").JSX.Element;
}
export declare const SvgIcon: typeof SvgIconClass & {
    Root: typeof SvgIconClass;
    prefetchIcon: (name: string) => Promise<void>;
};
export {};
