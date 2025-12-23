import type { CheckboxProps } from '../checkbox';
import type { ComponentProps, ReactNode } from 'react';
import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
import type { BaseAgreementFieldProps } from './BaseAgreementField';
import BaseAgreementField from './BaseAgreementField';
type AgreementFieldType = 'big-bold' | 'medium-bold' | 'medium-regular';
interface AgreementFieldProps extends Pick<BaseAgreementFieldProps, 'indent' | 'withBorder'> {
    children?: ReactNode;
    type: AgreementFieldType;
    addonText?: ReactNode;
    arrow?: ReactNode;
    style?: StyleProp<ViewStyle>;
    /**
     * @description 모든 터치영역 클릭 이벤트
     */
    onPress?: PressableProps['onPress'];
    /**
     * @description Link, Collapse Area 터치영역 클릭 이벤트
     */
    onActionPress?: PressableProps['onPress'];
}
declare function AgreementField({ children, type, addonText, arrow, indent, withBorder, style, onPress, onActionPress, }: AgreementFieldProps): import("react/jsx-runtime").JSX.Element;
declare namespace AgreementField {
    var AddonText: ({ color, style, ...props }: Omit<import("../txt").TxtProps, "typography">) => import("react/jsx-runtime").JSX.Element;
    var Arrow: ({ color, type, open, style, }: {
        color?: string | undefined;
        style?: StyleProp<ViewStyle>;
        open?: boolean;
        type?: "link" | "collapsible";
    }) => import("react/jsx-runtime").JSX.Element;
}
export interface CheckboxAgreementFieldProps extends AgreementFieldProps, Pick<CheckboxProps, 'checked' | 'defaultChecked' | 'onCheckedChange'> {
}
declare function CheckboxAgreementField({ children, checked: _checked, defaultChecked, type, addonText, arrow, indent, withBorder, style, onCheckedChange, onPress, onActionPress, }: CheckboxAgreementFieldProps): import("react/jsx-runtime").JSX.Element;
export declare const marginTopMap: Record<AgreementFieldType, number>;
export declare const contentTextPropsMap: Record<AgreementFieldType, Pick<ComponentProps<typeof BaseAgreementField.ContentText>, 'size' | 'fontWeight'>>;
export { CheckboxAgreementField, AgreementField };
