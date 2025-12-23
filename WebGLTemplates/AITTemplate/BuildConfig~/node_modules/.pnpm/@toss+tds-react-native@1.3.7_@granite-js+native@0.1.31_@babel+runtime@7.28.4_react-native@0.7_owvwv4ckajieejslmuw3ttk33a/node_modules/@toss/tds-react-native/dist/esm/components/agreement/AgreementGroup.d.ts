import type { ViewProps } from 'react-native';
import type { CheckboxAgreementFieldProps } from './AgreementField';
import type { Indent } from './types';
type AgreementGroupProps = ViewProps & {
    indent: Indent;
};
type AgreementGroupItemProps = Pick<CheckboxAgreementFieldProps, 'children' | 'type' | 'style' | 'checked' | 'defaultChecked' | 'onCheckedChange' | 'onPress' | 'onActionPress'>;
declare function AgreementGroup({ style, indent, ...props }: AgreementGroupProps): import("react/jsx-runtime").JSX.Element;
declare function AgreementGroupItem({ children, type, style, checked: _checked, defaultChecked, onCheckedChange, onPress, onActionPress, }: AgreementGroupItemProps): import("react/jsx-runtime").JSX.Element;
export { AgreementGroup, AgreementGroupItem };
