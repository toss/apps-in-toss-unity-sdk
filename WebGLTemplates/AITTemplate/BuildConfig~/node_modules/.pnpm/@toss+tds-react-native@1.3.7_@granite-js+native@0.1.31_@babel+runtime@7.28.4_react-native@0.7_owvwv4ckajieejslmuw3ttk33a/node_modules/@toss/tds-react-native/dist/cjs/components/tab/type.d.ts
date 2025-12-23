import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import type { TabValue } from './TabContext';
export interface BaseTabProps extends ViewProps {
    /**
     * 사이즈에 따라 `Tab` 컴포넌트의 높이와 텍스트 크기가 변경돼요.
     * @default 'large'
     */
    size?: 'large' | 'small';
    /**
     * `Tab` 컴포넌트에서 초기 선택된 탭의 값을 설정해요. 탭의 상태를 컴포넌트 내부에서 관리할 때 사용해요.
     */
    defaultValue?: TabValue;
    /**
     * `Tab` 컴포넌트에서 선택된 탭을 설정해요. `Tab` 컴포넌트의 상태를 컴포넌트 외부에서 관리할 때, `onChange` 속성과 함께 사용해요.
     */
    value?: TabValue;
    /**
     * `Tab` 컴포넌트를 구성하는 하나 이상의 `Tab.Item` 컴포넌트를 받아요. `Tab.Item`은 각각의 탭을 나타내요.
     */
    children?: ReactNode;
    /**
     * 선택된 `Tab.Item` 컴포넌트가 변경될 때 실행되는 함수예요.
     */
    onChange?: (value: TabValue) => void;
}
