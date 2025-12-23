import type { ButtonProps } from '../button';
declare function LowerButton({ viewStyle, ...props }: ButtonProps): import("react/jsx-runtime").JSX.Element;
declare const RightButton: import("react").ForwardRefExoticComponent<ButtonProps & {
    pointerEvents?: "box-none" | "none" | "box-only" | "auto" | undefined | undefined;
} & import("react").RefAttributes<import("react-native").View>>;
export { LowerButton, RightButton };
