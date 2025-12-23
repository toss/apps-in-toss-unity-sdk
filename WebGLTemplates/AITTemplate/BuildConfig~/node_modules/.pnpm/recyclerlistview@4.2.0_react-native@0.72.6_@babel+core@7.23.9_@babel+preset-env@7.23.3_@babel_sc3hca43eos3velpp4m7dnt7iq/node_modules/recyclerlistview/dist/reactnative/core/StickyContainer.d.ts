/**
 * Created by ananya.chandra on 14/09/18.
 */
import * as React from "react";
import { StyleProp, ViewStyle } from "react-native";
import { RecyclerListViewProps } from "./RecyclerListView";
import { ComponentCompat } from "../utils/ComponentCompat";
import { WindowCorrection } from "./ViewabilityTracker";
export interface StickyContainerProps {
    children: RecyclerChild;
    stickyHeaderIndices?: number[];
    stickyFooterIndices?: number[];
    overrideRowRenderer?: (type: string | number | undefined, data: any, index: number, extendedState?: object) => JSX.Element | JSX.Element[] | null;
    applyWindowCorrection?: (offsetX: number, offsetY: number, winowCorrection: WindowCorrection) => void;
    renderStickyContainer?: (stickyContent: JSX.Element, index: number, extendedState?: object) => JSX.Element | null;
    style?: StyleProp<ViewStyle>;
    alwaysStickyFooter?: boolean;
}
export interface RecyclerChild extends React.ReactElement<RecyclerListViewProps> {
    ref: (recyclerRef: any) => {};
    props: RecyclerListViewProps;
}
export default class StickyContainer<P extends StickyContainerProps> extends ComponentCompat<P> {
    static propTypes: {};
    private _recyclerRef;
    private _dataProvider;
    private _layoutProvider;
    private _extendedState;
    private _rowRenderer;
    private _stickyHeaderRef;
    private _stickyFooterRef;
    private _visibleIndicesAll;
    private _windowCorrection;
    constructor(props: P, context?: any);
    componentWillReceivePropsCompat(newProps: P): void;
    renderCompat(): JSX.Element;
    private _rlvRowRenderer;
    private _getRecyclerRef;
    private _getCurrentWindowCorrection;
    private _getStickyHeaderRef;
    private _getStickyFooterRef;
    private _onVisibleIndicesChanged;
    private _callStickyObjectsOnVisibleIndicesChanged;
    private _onScroll;
    private _getWindowCorrection;
    private _assertChildType;
    private _isChildRecyclerInstance;
    private _getLayoutForIndex;
    private _getDataForIndex;
    private _getLayoutTypeForIndex;
    private _getExtendedState;
    private _getRowRenderer;
    private _getRLVRenderedSize;
    private _getContentDimension;
    private _applyWindowCorrection;
    private _initParams;
}
