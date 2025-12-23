import RecyclerListView, { RecyclerListViewProps, RecyclerListViewState } from "./RecyclerListView";
export interface ProgressiveListViewProps extends RecyclerListViewProps {
    maxRenderAhead?: number;
    renderAheadStep?: number;
    /**
     * A smaller final value can help in building up recycler pool in advance. This is only used if there is a valid updated cycle.
     * e.g, if maxRenderAhead is 0 then there will be no cycle and final value will be unused
     */
    finalRenderAheadOffset?: number;
}
/**
 * This will incrementally update renderAhead distance and render the page progressively.
 * renderAheadOffset = initial value which will be incremented
 * renderAheadStep = amount of increment made on each frame
 * maxRenderAhead = maximum value for render ahead at the end of update cycle
 * finalRenderAheadOffset = value to set after whole update cycle is completed. If undefined, final offset value will be equal to maxRenderAhead
 */
export default class ProgressiveListView extends RecyclerListView<ProgressiveListViewProps, RecyclerListViewState> {
    static defaultProps: {
        maxRenderAhead: number;
        renderAheadStep: number;
        renderAheadOffset: number;
        canChangeSize: boolean;
        disableRecycling: boolean;
        initialOffset: number;
        initialRenderIndex: number;
        isHorizontal: boolean;
        onEndReachedThreshold: number;
        onEndReachedThresholdRelative: number;
    };
    private renderAheadUpdateCallbackId?;
    private isFirstLayoutComplete;
    componentDidMount(): void;
    componentWillUnmount(): void;
    protected onItemLayout(index: number): void;
    private updateRenderAheadProgressively;
    private incrementRenderAhead;
    private performFinalUpdate;
    private cancelRenderAheadUpdate;
}
