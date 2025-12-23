import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import { View } from 'react-native';
import type { FrameAccPositionType, FrameShapeType } from '../types';
interface Props extends ViewProps {
    content: ReactNode;
    shape: FrameShapeType;
    backgroundColor?: string;
    acc?: ReactNode;
    accPosition?: FrameAccPositionType;
    /**
     * @TODO Android 에서 boxShadow 구현 필요
     */
    overlap?: {
        color: string;
    } | null;
    color?: string;
}
export declare const Frame: import("react").ForwardRefExoticComponent<Props & import("react").RefAttributes<View>>;
export {};
