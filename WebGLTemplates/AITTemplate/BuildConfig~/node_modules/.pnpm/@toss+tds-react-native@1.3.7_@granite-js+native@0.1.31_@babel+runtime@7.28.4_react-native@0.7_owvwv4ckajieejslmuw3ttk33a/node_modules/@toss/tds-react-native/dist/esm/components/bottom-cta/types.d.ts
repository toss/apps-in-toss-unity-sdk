import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
export type CommonProps = {
    topAccessory?: ReactNode;
    bottomAccessory?: ReactNode;
    /**
     * @description BottomCTA 의 gradient 를 지정합니다. null 을 주면 gradient 를 없앱니다.
     * @default `<BottomCTAGradient stopColor={adaptive.layeredBackground} />`
     */
    gradient?: ReactNode;
    containerStyle?: StyleProp<ViewStyle>;
    /**
     * @default false;
     * @description 키보드가 올라왔을 때 스타일을 변경할 것인지 여부
     */
    keyboardStyleEnabled?: boolean;
};
