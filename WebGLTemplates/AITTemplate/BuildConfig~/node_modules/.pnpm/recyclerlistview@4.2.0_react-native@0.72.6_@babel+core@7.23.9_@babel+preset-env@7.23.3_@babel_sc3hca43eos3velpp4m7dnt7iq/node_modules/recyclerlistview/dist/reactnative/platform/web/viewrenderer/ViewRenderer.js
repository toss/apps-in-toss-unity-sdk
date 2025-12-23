"use strict";
var __extends = (this && this.__extends) || (function () {
    var extendStatics = function (d, b) {
        extendStatics = Object.setPrototypeOf ||
            ({ __proto__: [] } instanceof Array && function (d, b) { d.__proto__ = b; }) ||
            function (d, b) { for (var p in b) if (b.hasOwnProperty(p)) d[p] = b[p]; };
        return extendStatics(d, b);
    };
    return function (d, b) {
        extendStatics(d, b);
        function __() { this.constructor = d; }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
})();
var __assign = (this && this.__assign) || function () {
    __assign = Object.assign || function(t) {
        for (var s, i = 1, n = arguments.length; i < n; i++) {
            s = arguments[i];
            for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
                t[p] = s[p];
        }
        return t;
    };
    return __assign.apply(this, arguments);
};
Object.defineProperty(exports, "__esModule", { value: true });
var React = require("react");
var BaseViewRenderer_1 = require("../../../core/viewrenderer/BaseViewRenderer");
/***
 * View renderer is responsible for creating a container of size provided by LayoutProvider and render content inside it.
 * Also enforces a logic to prevent re renders. RecyclerListView keeps moving these ViewRendereres around using transforms to enable recycling.
 * View renderer will only update if its position, dimensions or given data changes. Make sure to have a relevant shouldComponentUpdate as well.
 * This is second of the two things recycler works on. Implemented both for web and react native.
 */
var ViewRenderer = /** @class */ (function (_super) {
    __extends(ViewRenderer, _super);
    function ViewRenderer() {
        var _this = _super !== null && _super.apply(this, arguments) || this;
        _this._dim = { width: 0, height: 0 };
        _this._mainDiv = null;
        _this._isPendingSizeUpdate = false;
        _this._setRef = function (div) {
            _this._mainDiv = div;
        };
        return _this;
    }
    ViewRenderer.prototype.componentDidMount = function () {
        var _this = this;
        _super.prototype.componentDidMount.call(this);
        this._checkSizeChange();
        if (!this._sizeObserver && ResizeObserver) {
            this._sizeObserver = new ResizeObserver(function () {
                _this._checkSizeChange(true);
            });
            if (this._mainDiv) {
                this._sizeObserver.observe(this._mainDiv);
            }
        }
    };
    ViewRenderer.prototype.componentDidUpdate = function () {
        this._isPendingSizeUpdate = false;
        this._checkSizeChange();
    };
    ViewRenderer.prototype.componentWillUnmount = function () {
        _super.prototype.componentWillUnmount.call(this);
        if (this._sizeObserver) {
            this._sizeObserver.disconnect();
            this._sizeObserver = undefined;
        }
    };
    ViewRenderer.prototype.renderCompat = function () {
        var style = this.props.forceNonDeterministicRendering
            ? __assign({ transform: this._getTransform(), WebkitTransform: this._getTransform() }, styles.baseViewStyle, this.props.styleOverrides, this.animatorStyleOverrides) : __assign({ height: this.props.height, overflow: "hidden", width: this.props.width, transform: this._getTransform(), WebkitTransform: this._getTransform() }, styles.baseViewStyle, this.props.styleOverrides, this.animatorStyleOverrides);
        var props = {
            style: style,
            ref: this._setRef,
        };
        return this._renderItemContainer(props, this.props, this.renderChild());
    };
    ViewRenderer.prototype.getRef = function () {
        return this._mainDiv;
    };
    ViewRenderer.prototype._renderItemContainer = function (props, parentProps, children) {
        return (this.props.renderItemContainer && this.props.renderItemContainer(props, parentProps, children)) || (React.createElement("div", __assign({}, props), children));
    };
    ViewRenderer.prototype._getTransform = function () {
        return "translate(" + this.props.x + "px," + this.props.y + "px)";
    };
    ViewRenderer.prototype._checkSizeChange = function (fromObserver) {
        if (fromObserver === void 0) { fromObserver = false; }
        if (this.props.forceNonDeterministicRendering && this.props.onSizeChanged) {
            var mainDiv = this._mainDiv;
            if (mainDiv) {
                this._dim.width = mainDiv.clientWidth;
                this._dim.height = mainDiv.clientHeight;
                if (this.props.width !== this._dim.width || this.props.height !== this._dim.height) {
                    this._isPendingSizeUpdate = true;
                    this.props.onSizeChanged(this._dim, this.props.index);
                }
                else if (fromObserver && this._isPendingSizeUpdate) {
                    this.props.onSizeChanged(this._dim, this.props.index);
                }
            }
        }
        this._onItemRendered();
    };
    ViewRenderer.prototype._onItemRendered = function () {
        if (this.props.onItemLayout) {
            this.props.onItemLayout(this.props.index);
        }
    };
    return ViewRenderer;
}(BaseViewRenderer_1.default));
exports.default = ViewRenderer;
var styles = {
    baseViewStyle: {
        alignItems: "stretch",
        borderWidth: 0,
        borderStyle: "solid",
        boxSizing: "border-box",
        display: "flex",
        flexDirection: "column",
        margin: 0,
        padding: 0,
        position: "absolute",
        minHeight: 0,
        minWidth: 0,
        left: 0,
        top: 0,
    },
};
//# sourceMappingURL=ViewRenderer.js.map