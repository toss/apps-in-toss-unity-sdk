import * as React from "react";
import ContextProvider from "./dependencies/ContextProvider";
import { BaseDataProvider } from "./dependencies/DataProvider";
import { Dimension, BaseLayoutProvider } from "./dependencies/LayoutProvider";
import { Layout } from "./layoutmanager/LayoutManager";
import BaseScrollView, { ScrollEvent, ScrollViewDefaultProps } from "./scrollcomponent/BaseScrollView";
import { TOnItemStatusChanged, WindowCorrection } from "./ViewabilityTracker";
import VirtualRenderer, { RenderStack } from "./VirtualRenderer";
import ItemAnimator from "./ItemAnimator";
import { DebugHandlers } from "..";
import { ComponentCompat } from "../utils/ComponentCompat";
/***
 * This is the main component, please refer to samples to understand how to use.
 * For advanced usage check out prop descriptions below.
 * You also get common methods such as: scrollToIndex, scrollToItem, scrollToTop, scrollToEnd, scrollToOffset, getCurrentScrollOffset,
 * findApproxFirstVisibleIndex.
 * You'll need a ref to Recycler in order to call these
 * Needs to have bounded size in all cases other than window scrolling (web).
 *
 * NOTE: React Native implementation uses ScrollView internally which means you get all ScrollView features as well such as Pull To Refresh, paging enabled
 *       You can easily create a recycling image flip view using one paging enabled flag. Read about ScrollView features in official
 *       react native documentation.
 * NOTE: If you see blank space look at the renderAheadOffset prop and make sure your data provider has a good enough rowHasChanged method.
 *       Blanks are totally avoidable with this listview.
 * NOTE: Also works on web (experimental)
 * NOTE: For reflowability set canChangeSize to true (experimental)
 */
export interface OnRecreateParams {
    lastOffset?: number;
}
export interface RecyclerListViewProps {
    layoutProvider: BaseLayoutProvider;
    dataProvider: BaseDataProvider;
    rowRenderer: (type: string | number, data: any, index: number, extendedState?: object) => JSX.Element | JSX.Element[] | null;
    contextProvider?: ContextProvider;
    renderAheadOffset?: number;
    isHorizontal?: boolean;
    onScroll?: (rawEvent: ScrollEvent, offsetX: number, offsetY: number) => void;
    onRecreate?: (params: OnRecreateParams) => void;
    onEndReached?: () => void;
    onEndReachedThreshold?: number;
    onEndReachedThresholdRelative?: number;
    onVisibleIndexesChanged?: TOnItemStatusChanged;
    onVisibleIndicesChanged?: TOnItemStatusChanged;
    renderFooter?: () => JSX.Element | JSX.Element[] | null;
    externalScrollView?: {
        new (props: ScrollViewDefaultProps): BaseScrollView;
    };
    layoutSize?: Dimension;
    initialOffset?: number;
    initialRenderIndex?: number;
    scrollThrottle?: number;
    canChangeSize?: boolean;
    useWindowScroll?: boolean;
    disableRecycling?: boolean;
    forceNonDeterministicRendering?: boolean;
    extendedState?: object;
    itemAnimator?: ItemAnimator;
    optimizeForInsertDeleteAnimations?: boolean;
    style?: object | number;
    debugHandlers?: DebugHandlers;
    renderContentContainer?: (props?: object, children?: React.ReactNode) => React.ReactNode | null;
    renderItemContainer?: (props: object, parentProps: object, children?: React.ReactNode) => React.ReactNode;
    scrollViewProps?: object;
    applyWindowCorrection?: (offsetX: number, offsetY: number, windowCorrection: WindowCorrection) => void;
    onItemLayout?: (index: number) => void;
    windowCorrectionConfig?: {
        value?: WindowCorrection;
        applyToInitialOffset?: boolean;
        applyToItemScroll?: boolean;
    };
    suppressBoundedSizeException?: boolean;
}
export interface RecyclerListViewState {
    renderStack: RenderStack;
    internalSnapshot: Record<string, object>;
}
export interface WindowCorrectionConfig {
    value: WindowCorrection;
    applyToInitialOffset: boolean;
    applyToItemScroll: boolean;
}
export default class RecyclerListView<P extends RecyclerListViewProps, S extends RecyclerListViewState> extends ComponentCompat<P, S> {
    static defaultProps: {
        canChangeSize: boolean;
        disableRecycling: boolean;
        initialOffset: number;
        initialRenderIndex: number;
        isHorizontal: boolean;
        onEndReachedThreshold: number;
        onEndReachedThresholdRelative: number;
        renderAheadOffset: number;
    };
    static propTypes: {};
    private refreshRequestDebouncer;
    private _virtualRenderer;
    private _onEndReachedCalled;
    private _initComplete;
    private _isMounted;
    private _relayoutReqIndex;
    private _params;
    private _layout;
    private _pendingScrollToOffset;
    private _pendingRenderStack?;
    private _tempDim;
    private _initialOffset;
    private _cachedLayouts?;
    private _scrollComponent;
    private _windowCorrectionConfig;
    private _defaultItemAnimator;
    constructor(props: P, context?: any);
    componentWillReceivePropsCompat(newProps: RecyclerListViewProps): void;
    componentDidUpdate(): void;
    componentDidMount(): void;
    componentWillUnmount(): void;
    scrollToIndex(index: number, animate?: boolean): void;
    /**
     * This API is almost similar to scrollToIndex, but differs when the view is already in viewport.
     * Instead of bringing the view to the top of the viewport, it will calculate the overflow of the @param index
     * and scroll to just bring the entire view to viewport.
     */
    bringToFocus(index: number, animate?: boolean): void;
    scrollToItem(data: any, animate?: boolean): void;
    getLayout(index: number): Layout | undefined;
    scrollToTop(animate?: boolean): void;
    scrollToEnd(animate?: boolean): void;
    scrollToOffset: (x: number, y: number, animate?: boolean, useWindowCorrection?: boolean) => void;
    updateRenderAheadOffset(renderAheadOffset: number): boolean;
    getCurrentRenderAheadOffset(): number;
    getCurrentScrollOffset(): number;
    findApproxFirstVisibleIndex(): number;
    getRenderedSize(): Dimension;
    getContentDimension(): Dimension;
    forceRerender(): void;
    getScrollableNode(): number | null;
    renderCompat(): JSX.Element;
    prepareForLayoutAnimationRender(): void;
    protected getVirtualRenderer(): VirtualRenderer;
    protected onItemLayout(index: number): void;
    private _onItemLayout;
    private _processInitialOffset;
    private _getContextFromContextProvider;
    private _checkAndChangeLayouts;
    private _refreshViewability;
    private _queueStateRefresh;
    private _onSizeChanged;
    private _initStateIfRequired;
    private _renderStackWhenReady;
    private _initTrackers;
    private _getWindowCorrection;
    private _assertDependencyPresence;
    private _assertType;
    private _dataHasChanged;
    private _renderRowUsingMeta;
    private _onViewContainerSizeChange;
    private _checkExpectedDimensionDiscrepancy;
    private _generateRenderStack;
    private _onScroll;
    private _processOnEndReached;
}
