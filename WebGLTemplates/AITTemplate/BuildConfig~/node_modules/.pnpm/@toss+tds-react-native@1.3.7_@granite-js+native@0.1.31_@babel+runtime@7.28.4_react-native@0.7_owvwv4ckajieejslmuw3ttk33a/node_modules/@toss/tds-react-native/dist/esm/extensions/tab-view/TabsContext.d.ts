import type { PagerView } from '@granite-js/native/react-native-pager-view';
import type { Dispatch, RefObject, SetStateAction } from 'react';
import type { LayoutRectangle } from 'react-native';
export type TabValue = string;
export type TabIndex = number;
export type TabWidthMap = Record<TabIndex, LayoutRectangle>;
export interface TabsContext {
    fluid: boolean;
    value?: TabValue;
    /**
     * tabWidth 를 tabIndex 로 관리합니다.
     * 탭들의 각각 width 를 저장하고, 이를 이용해 indicator 에서 활용합니다.
     */
    tabLayoutMap?: TabWidthMap;
    tabContainerWidth?: number;
    setTabWidthMap?: Dispatch<SetStateAction<TabWidthMap>>;
    onChange?: (value: TabValue, tabIndex: TabIndex) => void;
    pagerViewRef?: RefObject<PagerView> | null;
    initialTabIndex?: number;
    tabIndex?: number;
    onPageSelected?: (event: any) => void;
}
export declare const TabsContext: import("react").Context<TabsContext>;
export declare function useTabs(): TabsContext;
