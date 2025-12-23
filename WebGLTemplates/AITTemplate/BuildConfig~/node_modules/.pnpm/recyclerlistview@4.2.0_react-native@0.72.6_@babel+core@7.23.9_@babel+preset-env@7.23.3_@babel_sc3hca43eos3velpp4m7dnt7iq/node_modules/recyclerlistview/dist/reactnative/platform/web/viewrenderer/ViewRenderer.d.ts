/// <reference types="react" />
import BaseViewRenderer from "../../../core/viewrenderer/BaseViewRenderer";
/***
 * View renderer is responsible for creating a container of size provided by LayoutProvider and render content inside it.
 * Also enforces a logic to prevent re renders. RecyclerListView keeps moving these ViewRendereres around using transforms to enable recycling.
 * View renderer will only update if its position, dimensions or given data changes. Make sure to have a relevant shouldComponentUpdate as well.
 * This is second of the two things recycler works on. Implemented both for web and react native.
 */
export default class ViewRenderer extends BaseViewRenderer<any> {
    private _dim;
    private _mainDiv;
    private _sizeObserver?;
    private _isPendingSizeUpdate;
    componentDidMount(): void;
    componentDidUpdate(): void;
    componentWillUnmount(): void;
    renderCompat(): JSX.Element;
    protected getRef(): object | null;
    private _renderItemContainer;
    private _setRef;
    private _getTransform;
    private _checkSizeChange;
    private _onItemRendered;
}
