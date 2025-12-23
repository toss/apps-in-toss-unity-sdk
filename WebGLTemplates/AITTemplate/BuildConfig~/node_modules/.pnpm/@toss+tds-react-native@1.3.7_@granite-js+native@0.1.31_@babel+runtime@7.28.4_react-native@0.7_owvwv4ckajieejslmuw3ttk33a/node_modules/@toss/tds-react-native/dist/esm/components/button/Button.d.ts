import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, TextStyle, ViewProps, ViewStyle } from 'react-native';
import { View } from 'react-native';
export interface ButtonProps extends Omit<PressableProps, 'style'> {
    /**
     * `Button` 컴포넌트 내부에 표시될 내용을 지정해요.
     */
    children: ReactNode;
    /**
     * `Button` 컴포넌트가 눌렸을 때 호출되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
    /**
     * `Button` 컴포넌트의 색상 타입을 지정해요.
     *
     * @default 'primary'
     */
    type?: 'primary' | 'danger' | 'light' | 'dark';
    /**
     * `Button` 컴포넌트의 스타일을 지정해요. 'fill'은 채워진 스타일, 'weak'은 투명한 스타일이에요.
     *
     * @default 'fill'
     */
    style?: 'fill' | 'weak';
    /**
     * `Button` 컴포넌트의 표시 방식을 지정해요. 'block'은 전체 너비를 차지하고, 'full'은 부모 컨테이너에 맞춰요.
     *
     * @default 'inline'
     */
    display?: 'block' | 'full' | 'inline';
    /**
     * `Button` 컴포넌트의 크기를 지정해요.
     *
     * @default 'big'
     */
    size?: 'big' | 'large' | 'medium' | 'tiny';
    /**
     * true일 경우 버튼에 로딩 스피너가 표시되고 `Button` 컴포넌트가 클릭되지 않아요.
     *
     * @default false
     */
    loading?: boolean;
    /**
     * `Button` 컴포넌트를 비활성화할지 여부를 지정해요. true일 경우 `Button` 컴포넌트가 클릭되지 않아요.
     *
     * @default false
     */
    disabled?: boolean;
    /**
     * `Button` 컴포넌트의 외부 스타일을 변경할 때 사용해요. 가장 바깥쪽 `View`에 적용돼요.
     */
    viewStyle?: StyleProp<ViewStyle>;
    /**
     * `Button` 컴포넌트의 텍스트 색상을 지정해요. 기본으로 설정되는 색상을 덮어씌울 때 사용해요.
     */
    color?: string;
    /**
     * `Button` 컴포넌트의 컨테이너 스타일을 변경할 때 사용해요. `Button` 컴포넌트 내부의 `View`에 적용돼요.
     */
    containerStyle?: StyleProp<ViewStyle>;
    /**
     * `Button` 컴포넌트의 텍스트 스타일을 변경할 때 사용해요.
     */
    textStyle?: StyleProp<TextStyle>;
    /**
     * `Button` 컴포넌트의 왼쪽에 추가할 아이콘이나 텍스트를 지정해요.
     */
    leftAccessory?: ReactNode;
}
type PointerEvents = Pick<ViewProps, 'pointerEvents'>;
export declare const containerStylesBySize: {
    base: {
        alignItems: "center";
        justifyContent: "center";
        overflow: "hidden";
        position: "relative";
    };
    tiny: {
        paddingHorizontal: number;
        paddingVertical: number;
        minHeight: number;
        minWidth: number;
        borderRadius: number;
    };
    medium: {
        paddingHorizontal: number;
        paddingVertical: number;
        minHeight: number;
        minWidth: number;
        borderRadius: number;
    };
    large: {
        paddingHorizontal: number;
        paddingVertical: number;
        minHeight: number;
        minWidth: number;
        borderRadius: number;
    };
    big: {
        paddingHorizontal: number;
        paddingVertical: number;
        minHeight: number;
        minWidth: number;
        borderRadius: number;
    };
};
export declare const Button: import("react").ForwardRefExoticComponent<ButtonProps & PointerEvents & import("react").RefAttributes<View>>;
export {};
