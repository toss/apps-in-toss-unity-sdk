import type { ReactNode } from 'react';
import { TabsList } from './TabsList';
import { TabsViewList } from './TabsViews';
import { TabView } from './TabView';
interface Props {
    fluid: boolean;
    defaultValue: string;
    value?: string;
    children: ReactNode;
    onChange?: (value: string, tabIndex: number) => void;
}
declare function Tabs({ fluid, defaultValue, value: _value, children, onChange }: Props): import("react/jsx-runtime").JSX.Element;
declare namespace Tabs {
    var TabList: typeof TabsList;
    var TabItem: typeof import("./TabItem").TabItem;
    var ViewList: typeof TabsViewList;
    var View: typeof TabView;
}
export { Tabs };
