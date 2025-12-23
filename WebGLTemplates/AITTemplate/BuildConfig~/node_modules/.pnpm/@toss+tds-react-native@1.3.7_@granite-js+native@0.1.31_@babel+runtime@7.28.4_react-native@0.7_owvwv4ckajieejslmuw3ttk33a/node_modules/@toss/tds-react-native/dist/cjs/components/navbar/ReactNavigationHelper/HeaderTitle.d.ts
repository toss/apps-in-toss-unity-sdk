import type { PropsWithChildren } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
type HeaderTitleProps = PropsWithChildren<{
    withHeaderLeft: boolean;
    style?: StyleProp<ViewStyle>;
}>;
declare function HeaderTitle(props: HeaderTitleProps): JSX.Element;
export { HeaderTitle };
