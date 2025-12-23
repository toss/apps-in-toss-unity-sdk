import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
import type { TabValue } from './TabContext';
export interface TabItemProps {
    /**
     * 이 값이 `true`일 때 `Tab.Item`의 우측 상단에 빨간 동그라미가 표시돼요. 중요한 알림이나 새로운 업데이트가 있음을 사용자에게 시각적으로 전달할 수 있어요.
     *
     * @default false
     */
    redBean?: boolean;
    /**
     * 탭의 값을 설정해요. `Tab` 컴포넌트에서 탭을 구분하는 데 사용돼요.
     */
    value: TabValue;
    /**
     * `Tab.Item` 컴포넌트 내부에 표시될 내용을 설정해요.
     */
    children: ReactNode;
    /**
     * `Tab.Item` 컴포넌트의 스타일을 설정해요.
     */
    style?: StyleProp<ViewStyle>;
    /**
     * `Tab.Item` 컴포넌트를 클릭했을 때 실행되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
}
export declare function TabItem({ redBean, value, children, style, onPress }: TabItemProps): import("react/jsx-runtime").JSX.Element;
