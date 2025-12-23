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
var RecyclerListView_1 = require("./RecyclerListView");
/**
 * This will incrementally update renderAhead distance and render the page progressively.
 * renderAheadOffset = initial value which will be incremented
 * renderAheadStep = amount of increment made on each frame
 * maxRenderAhead = maximum value for render ahead at the end of update cycle
 * finalRenderAheadOffset = value to set after whole update cycle is completed. If undefined, final offset value will be equal to maxRenderAhead
 */
var ProgressiveListView = /** @class */ (function (_super) {
    __extends(ProgressiveListView, _super);
    function ProgressiveListView() {
        var _this = _super !== null && _super.apply(this, arguments) || this;
        _this.isFirstLayoutComplete = false;
        return _this;
    }
    ProgressiveListView.prototype.componentDidMount = function () {
        _super.prototype.componentDidMount.call(this);
        if (!this.props.forceNonDeterministicRendering) {
            this.updateRenderAheadProgressively(this.getCurrentRenderAheadOffset());
        }
    };
    ProgressiveListView.prototype.componentWillUnmount = function () {
        this.cancelRenderAheadUpdate();
        _super.prototype.componentWillUnmount.call(this);
    };
    ProgressiveListView.prototype.onItemLayout = function (index) {
        if (!this.isFirstLayoutComplete) {
            this.isFirstLayoutComplete = true;
            if (this.props.forceNonDeterministicRendering) {
                this.updateRenderAheadProgressively(this.getCurrentRenderAheadOffset());
            }
        }
        _super.prototype.onItemLayout.call(this, index);
    };
    ProgressiveListView.prototype.updateRenderAheadProgressively = function (newVal) {
        var _this = this;
        this.cancelRenderAheadUpdate(); // Cancel any pending callback.
        this.renderAheadUpdateCallbackId = requestAnimationFrame(function () {
            if (!_this.updateRenderAheadOffset(newVal)) {
                _this.updateRenderAheadProgressively(newVal);
            }
            else {
                _this.incrementRenderAhead();
            }
        });
    };
    ProgressiveListView.prototype.incrementRenderAhead = function () {
        if (this.props.maxRenderAhead && this.props.renderAheadStep) {
            var layoutManager = this.getVirtualRenderer().getLayoutManager();
            var currentRenderAheadOffset = this.getCurrentRenderAheadOffset();
            if (layoutManager) {
                var contentDimension = layoutManager.getContentDimension();
                var maxContentSize = this.props.isHorizontal ? contentDimension.width : contentDimension.height;
                if (currentRenderAheadOffset < maxContentSize && currentRenderAheadOffset < this.props.maxRenderAhead) {
                    var newRenderAheadOffset = currentRenderAheadOffset + this.props.renderAheadStep;
                    this.updateRenderAheadProgressively(newRenderAheadOffset);
                }
                else {
                    this.performFinalUpdate();
                }
            }
        }
    };
    ProgressiveListView.prototype.performFinalUpdate = function () {
        var _this = this;
        this.cancelRenderAheadUpdate(); // Cancel any pending callback.
        this.renderAheadUpdateCallbackId = requestAnimationFrame(function () {
            if (_this.props.finalRenderAheadOffset !== undefined) {
                _this.updateRenderAheadOffset(_this.props.finalRenderAheadOffset);
            }
        });
    };
    ProgressiveListView.prototype.cancelRenderAheadUpdate = function () {
        if (this.renderAheadUpdateCallbackId !== undefined) {
            cancelAnimationFrame(this.renderAheadUpdateCallbackId);
        }
    };
    ProgressiveListView.defaultProps = __assign({}, RecyclerListView_1.default.defaultProps, { maxRenderAhead: Number.MAX_VALUE, renderAheadStep: 300, renderAheadOffset: 0 });
    return ProgressiveListView;
}(RecyclerListView_1.default));
exports.default = ProgressiveListView;
//# sourceMappingURL=ProgressiveListView.js.map