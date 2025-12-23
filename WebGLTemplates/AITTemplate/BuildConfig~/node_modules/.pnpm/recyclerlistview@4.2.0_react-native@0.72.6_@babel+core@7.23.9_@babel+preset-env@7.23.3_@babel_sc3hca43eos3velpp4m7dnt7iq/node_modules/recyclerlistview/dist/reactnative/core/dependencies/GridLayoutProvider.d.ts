import { LayoutProvider, Dimension } from "./LayoutProvider";
import { Layout, LayoutManager } from "../layoutmanager/LayoutManager";
export declare class GridLayoutProvider extends LayoutProvider {
    private _getHeightOrWidth;
    private _getSpan;
    private _maxSpan;
    private _renderWindowSize?;
    private _isHorizontal?;
    private _acceptableRelayoutDelta;
    constructor(maxSpan: number, getLayoutType: (index: number) => string | number, getSpan: (index: number) => number, getHeightOrWidth: (index: number) => number, acceptableRelayoutDelta?: number);
    newLayoutManager(renderWindowSize: Dimension, isHorizontal?: boolean, cachedLayouts?: Layout[]): LayoutManager;
    private setLayout;
}
