import { LayoutProvider } from "./../dependencies/LayoutProvider";
import { WrapGridLayoutManager, Layout } from "./LayoutManager";
import { Dimension } from "../dependencies/LayoutProvider";
export declare class GridLayoutManager extends WrapGridLayoutManager {
    private _maxSpan;
    private _getSpan;
    private _isGridHorizontal;
    private _renderWindowSize;
    private _acceptableRelayoutDelta;
    constructor(layoutProvider: LayoutProvider, renderWindowSize: Dimension, getSpan: (index: number) => number, maxSpan: number, acceptableRelayoutDelta: number, isHorizontal?: boolean, cachedLayouts?: Layout[]);
    overrideLayout(index: number, dim: Dimension): boolean;
    getStyleOverridesForIndex(index: number): object | undefined;
}
