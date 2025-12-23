"use strict";
/**
 * Created by ananya.chandra on 20/09/18.
 */
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
Object.defineProperty(exports, "__esModule", { value: true });
var React = require("react");
var react_native_1 = require("react-native");
var RecyclerListViewExceptions_1 = require("../exceptions/RecyclerListViewExceptions");
var CustomError_1 = require("../exceptions/CustomError");
var ComponentCompat_1 = require("../../utils/ComponentCompat");
var StickyType;
(function (StickyType) {
    StickyType[StickyType["HEADER"] = 0] = "HEADER";
    StickyType[StickyType["FOOTER"] = 1] = "FOOTER";
})(StickyType = exports.StickyType || (exports.StickyType = {}));
var StickyObject = /** @class */ (function (_super) {
    __extends(StickyObject, _super);
    function StickyObject(props, context) {
        var _this = _super.call(this, props, context) || this;
        _this.stickyType = StickyType.HEADER;
        _this.stickyTypeMultiplier = 1;
        _this.stickyVisiblity = false;
        _this.currentIndex = 0;
        _this.currentStickyIndex = 0;
        _this.visibleIndices = [];
        _this.bounceScrolling = false;
        _this._stickyViewOffset = new react_native_1.Animated.Value(0);
        _this._previousStickyIndex = 0;
        _this._nextStickyIndex = 0;
        _this._firstCompute = true;
        _this._smallestVisibleIndex = 0;
        _this._largestVisibleIndex = 0;
        _this._offsetY = 0;
        _this._windowCorrection = {
            startCorrection: 0, endCorrection: 0, windowShift: 0,
        };
        return _this;
    }
    StickyObject.prototype.componentWillReceivePropsCompat = function (newProps) {
        this._updateDimensionParams();
        this.calculateVisibleStickyIndex(newProps.stickyIndices, this._smallestVisibleIndex, this._largestVisibleIndex, this._offsetY, this._windowBound);
        this._computeLayouts(newProps.stickyIndices);
        this.stickyViewVisible(this.stickyVisiblity, false);
    };
    StickyObject.prototype.renderCompat = function () {
        // Add the container style if renderContainer is undefined
        var containerStyle = [{ transform: [{ translateY: this._stickyViewOffset }] },
            (!this.props.renderContainer && [{ position: "absolute", width: this._scrollableWidth }, this.containerPosition])];
        var content = (React.createElement(react_native_1.Animated.View, { style: containerStyle }, this.stickyVisiblity ? this._renderSticky() : null));
        if (this.props.renderContainer) {
            var _extendedState = this.props.getExtendedState();
            return this.props.renderContainer(content, this.currentStickyIndex, _extendedState);
        }
        else {
            return (content);
        }
    };
    StickyObject.prototype.onVisibleIndicesChanged = function (all) {
        if (this._firstCompute) {
            this.initStickyParams();
            this._offsetY = this._getAdjustedOffsetY(this._offsetY);
            this._firstCompute = false;
        }
        this._updateDimensionParams();
        this._setSmallestAndLargestVisibleIndices(all);
        this.calculateVisibleStickyIndex(this.props.stickyIndices, this._smallestVisibleIndex, this._largestVisibleIndex, this._offsetY, this._windowBound);
        this._computeLayouts();
        this.stickyViewVisible(this.stickyVisiblity);
    };
    StickyObject.prototype.onScroll = function (offsetY) {
        offsetY = this._getAdjustedOffsetY(offsetY);
        this._offsetY = offsetY;
        this._updateDimensionParams();
        this.boundaryProcessing(offsetY, this._windowBound);
        if (this._previousStickyIndex !== undefined) {
            if (this._previousStickyIndex * this.stickyTypeMultiplier >= this.currentStickyIndex * this.stickyTypeMultiplier) {
                throw new CustomError_1.default(RecyclerListViewExceptions_1.default.stickyIndicesArraySortError);
            }
            var scrollY_1 = this.getScrollY(offsetY, this._scrollableHeight);
            if (this._previousHeight && this._currentYd && scrollY_1 && scrollY_1 < this._currentYd) {
                if (scrollY_1 > this._currentYd - this._previousHeight) {
                    this.currentIndex -= this.stickyTypeMultiplier;
                    var translate = (scrollY_1 - this._currentYd + this._previousHeight) * (-1 * this.stickyTypeMultiplier);
                    this._stickyViewOffset.setValue(translate);
                    this._computeLayouts();
                    this.stickyViewVisible(true);
                }
            }
            else {
                this._stickyViewOffset.setValue(0);
            }
        }
        if (this._nextStickyIndex !== undefined) {
            if (this._nextStickyIndex * this.stickyTypeMultiplier <= this.currentStickyIndex * this.stickyTypeMultiplier) {
                throw new CustomError_1.default(RecyclerListViewExceptions_1.default.stickyIndicesArraySortError);
            }
            var scrollY_2 = this.getScrollY(offsetY, this._scrollableHeight);
            if (this._currentHeight && this._nextYd && scrollY_2 && scrollY_2 + this._currentHeight > this._nextYd) {
                if (scrollY_2 <= this._nextYd) {
                    var translate = (scrollY_2 - this._nextYd + this._currentHeight) * (-1 * this.stickyTypeMultiplier);
                    this._stickyViewOffset.setValue(translate);
                }
                else if (scrollY_2 > this._nextYd) {
                    this.currentIndex += this.stickyTypeMultiplier;
                    this._stickyViewOffset.setValue(0);
                    this._computeLayouts();
                    this.stickyViewVisible(true);
                }
            }
            else {
                this._stickyViewOffset.setValue(0);
            }
        }
    };
    StickyObject.prototype.stickyViewVisible = function (_visible, shouldTriggerRender) {
        if (shouldTriggerRender === void 0) { shouldTriggerRender = true; }
        this.stickyVisiblity = _visible;
        if (shouldTriggerRender) {
            this.setState({});
        }
    };
    StickyObject.prototype.getWindowCorrection = function (props) {
        return (props.getWindowCorrection && props.getWindowCorrection()) || this._windowCorrection;
    };
    StickyObject.prototype.boundaryProcessing = function (offsetY, windowBound) {
        var hasReachedBoundary = this.hasReachedBoundary(offsetY, windowBound);
        if (this.bounceScrolling !== hasReachedBoundary) {
            this.bounceScrolling = hasReachedBoundary;
            if (this.bounceScrolling) {
                this.stickyViewVisible(false);
            }
            else {
                this.onVisibleIndicesChanged(this.visibleIndices);
            }
        }
    };
    StickyObject.prototype._updateDimensionParams = function () {
        var rlvDimension = this.props.getRLVRenderedSize();
        if (rlvDimension) {
            this._scrollableHeight = rlvDimension.height;
            this._scrollableWidth = rlvDimension.width;
        }
        var contentDimension = this.props.getContentDimension();
        if (contentDimension && this._scrollableHeight) {
            this._windowBound = contentDimension.height - this._scrollableHeight;
        }
    };
    StickyObject.prototype._computeLayouts = function (newStickyIndices) {
        var stickyIndices = newStickyIndices ? newStickyIndices : this.props.stickyIndices;
        if (stickyIndices) {
            this.currentStickyIndex = stickyIndices[this.currentIndex];
            this._previousStickyIndex = stickyIndices[this.currentIndex - this.stickyTypeMultiplier];
            this._nextStickyIndex = stickyIndices[this.currentIndex + this.stickyTypeMultiplier];
            if (this.currentStickyIndex !== undefined) {
                this._currentLayout = this.props.getLayoutForIndex(this.currentStickyIndex);
                this._currentY = this._currentLayout ? this._currentLayout.y : undefined;
                this._currentHeight = this._currentLayout ? this._currentLayout.height : undefined;
                this._currentYd = this._currentY && this._currentHeight ? this.getCurrentYd(this._currentY, this._currentHeight) : undefined;
            }
            if (this._previousStickyIndex !== undefined) {
                this._previousLayout = this.props.getLayoutForIndex(this._previousStickyIndex);
                this._previousHeight = this._previousLayout ? this._previousLayout.height : undefined;
            }
            if (this._nextStickyIndex !== undefined) {
                this._nextLayout = this.props.getLayoutForIndex(this._nextStickyIndex);
                this._nextY = this._nextLayout ? this._nextLayout.y : undefined;
                this._nextHeight = this._nextLayout ? this._nextLayout.height : undefined;
                this._nextYd = this._nextY && this._nextHeight ? this.getNextYd(this._nextY, this._nextHeight) : undefined;
            }
        }
    };
    StickyObject.prototype._setSmallestAndLargestVisibleIndices = function (indicesArray) {
        this.visibleIndices = indicesArray;
        this._smallestVisibleIndex = indicesArray[0];
        this._largestVisibleIndex = indicesArray[indicesArray.length - 1];
    };
    StickyObject.prototype._renderSticky = function () {
        if (this.currentStickyIndex !== undefined) {
            var _stickyData = this.props.getDataForIndex(this.currentStickyIndex);
            var _stickyLayoutType = this.props.getLayoutTypeForIndex(this.currentStickyIndex);
            var _extendedState = this.props.getExtendedState();
            var _rowRenderer = this.props.getRowRenderer();
            if (this.props.overrideRowRenderer) {
                return this.props.overrideRowRenderer(_stickyLayoutType, _stickyData, this.currentStickyIndex, _extendedState);
            }
            else {
                return _rowRenderer(_stickyLayoutType, _stickyData, this.currentStickyIndex, _extendedState);
            }
        }
        return null;
    };
    StickyObject.prototype._getAdjustedOffsetY = function (offsetY) {
        return offsetY + this.getWindowCorrection(this.props).windowShift;
    };
    return StickyObject;
}(ComponentCompat_1.ComponentCompat));
exports.default = StickyObject;
//# sourceMappingURL=StickyObject.js.map