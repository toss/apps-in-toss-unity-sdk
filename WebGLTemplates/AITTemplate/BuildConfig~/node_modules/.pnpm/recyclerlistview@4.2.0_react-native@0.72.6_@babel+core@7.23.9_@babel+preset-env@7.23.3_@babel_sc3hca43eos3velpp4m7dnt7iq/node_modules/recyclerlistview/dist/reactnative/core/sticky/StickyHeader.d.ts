/**
 * Created by ananya.chandra on 20/09/18.
 */
import StickyObject, { StickyObjectProps } from "./StickyObject";
export default class StickyHeader<P extends StickyObjectProps> extends StickyObject<P> {
    constructor(props: P, context?: any);
    onScroll(offsetY: number): void;
    protected initStickyParams(): void;
    protected calculateVisibleStickyIndex(stickyIndices: number[] | undefined, smallestVisibleIndex: number, largestVisibleIndex: number, offsetY: number, windowBound?: number): void;
    protected getNextYd(nextY: number, nextHeight: number): number;
    protected getCurrentYd(currentY: number, currentHeight: number): number;
    protected getScrollY(offsetY: number, scrollableHeight: number): number | undefined;
    protected hasReachedBoundary(offsetY: number, _windowBound?: number): boolean;
}
