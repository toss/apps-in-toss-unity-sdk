import { Icon } from '../icon';
import type { TxtProps } from '../txt';
import type { ComponentProps, ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import type { Indent } from './types';
export interface BaseAgreementFieldProps {
    left?: ReactNode;
    content: ReactNode;
    right?: ReactNode;
    indent: Indent;
    withBorder: boolean;
    style?: StyleProp<ViewStyle>;
}
declare function BaseAgreementField({ left, content, right, indent, withBorder, style }: BaseAgreementFieldProps): import("react/jsx-runtime").JSX.Element;
declare namespace BaseAgreementField {
    var ContentText: typeof BaseAgreementFieldContentText;
    var Arrow: typeof BaseAgreementFieldArrow;
    var AddonText: typeof BaseAgreementFieldAddonText;
}
type BaseAgreementFieldContentTextSize = 'big' | 'medium';
declare function BaseAgreementFieldContentText({ size, color, style, ...props }: Omit<TxtProps, 'typography'> & {
    size: BaseAgreementFieldContentTextSize;
}): import("react/jsx-runtime").JSX.Element;
declare function BaseAgreementFieldAddonText({ color, style, ...props }: Omit<TxtProps, 'typography'>): import("react/jsx-runtime").JSX.Element;
type IconProps = ComponentProps<typeof Icon>;
declare function BaseAgreementFieldArrow({ color, type, open, style, }: {
    color?: IconProps['color'];
    style?: StyleProp<ViewStyle>;
    open?: boolean;
    type?: 'link' | 'collapsible';
}): import("react/jsx-runtime").JSX.Element;
export default BaseAgreementField;
