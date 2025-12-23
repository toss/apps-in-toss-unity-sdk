import { AgreementButton } from './AgreementButton';
import { AgreementDescription } from './AgreementDescription';
import { AgreementField, CheckboxAgreementField } from './AgreementField';
import { AgreementGroup, AgreementGroupItem } from './AgreementGroup';
import { default as BaseAgreementField } from './BaseAgreementField';
import { AgreementCollapsibleTrigger, CollapsibleContent } from './Collapsible';
export type { CollapsibleRef } from './Collapsible';
export declare const Agreement: {
    BaseField: typeof BaseAgreementField;
    Field: typeof AgreementField;
    CheckboxField: typeof CheckboxAgreementField;
    Description: typeof AgreementDescription;
    Button: typeof AgreementButton;
    Arrow: ({ color, type, open, style, }: {
        color?: string | undefined;
        style?: import("react-native").StyleProp<import("react-native").ViewStyle>;
        open?: boolean;
        type?: "link" | "collapsible";
    }) => import("react/jsx-runtime").JSX.Element;
    AddonText: ({ color, style, ...props }: Omit<import("../txt").TxtProps, "typography">) => import("react/jsx-runtime").JSX.Element;
    Collapsible: import("react").ForwardRefExoticComponent<import("./Collapsible").CollapsibleProps & {
        children?: import("react").ReactNode | undefined;
    } & import("react").RefAttributes<import("./Collapsible").CollapsibleRef>>;
    CollapsibleTrigger: typeof AgreementCollapsibleTrigger;
    CollapsibleContent: typeof CollapsibleContent;
    Group: typeof AgreementGroup;
    GroupItem: typeof AgreementGroupItem;
};
