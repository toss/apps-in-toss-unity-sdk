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
Object.defineProperty(exports, "__esModule", { value: true });
var LayoutManager_1 = require("./LayoutManager");
var GridLayoutManager = /** @class */ (function (_super) {
    __extends(GridLayoutManager, _super);
    function GridLayoutManager(layoutProvider, renderWindowSize, getSpan, maxSpan, acceptableRelayoutDelta, isHorizontal, cachedLayouts) {
        var _this = _super.call(this, layoutProvider, renderWindowSize, isHorizontal, cachedLayouts) || this;
        _this._getSpan = getSpan;
        _this._isGridHorizontal = isHorizontal;
        _this._renderWindowSize = renderWindowSize;
        if (acceptableRelayoutDelta < 0) {
            throw new Error("acceptableRelayoutDelta cannot be less than 0");
        }
        else {
            _this._acceptableRelayoutDelta = acceptableRelayoutDelta;
        }
        if (maxSpan <= 0) {
            throw new Error("Max Column Span cannot be less than or equal to 0");
        }
        else {
            _this._maxSpan = maxSpan;
        }
        return _this;
    }
    GridLayoutManager.prototype.overrideLayout = function (index, dim) {
        // we are doing this because - when we provide decimal dimensions for a
        // certain cell - the onlayout returns a different dimension in certain high end devices.
        // This causes the layouting to behave weirdly as the new dimension might not adhere to the spans and the cells arrange themselves differently
        // So, whenever we have layouts for a certain index, we explicitly override the dimension to those very layout values
        // and call super so as to set the overridden flag as true
        var layout = this.getLayouts()[index];
        var heightDiff = Math.abs(dim.height - layout.height);
        var widthDiff = Math.abs(dim.width - layout.width);
        if (layout) {
            if (this._isGridHorizontal) {
                if (heightDiff < this._acceptableRelayoutDelta) {
                    if (widthDiff === 0) {
                        return false;
                    }
                    dim.height = layout.height;
                }
            }
            else {
                if (widthDiff < this._acceptableRelayoutDelta) {
                    if (heightDiff === 0) {
                        return false;
                    }
                    dim.width = layout.width;
                }
            }
        }
        return _super.prototype.overrideLayout.call(this, index, dim);
    };
    GridLayoutManager.prototype.getStyleOverridesForIndex = function (index) {
        var columnSpanForIndex = this._getSpan(index);
        return this._isGridHorizontal
            ? {
                height: (this._renderWindowSize.height / this._maxSpan) * columnSpanForIndex,
            }
            : {
                width: (this._renderWindowSize.width / this._maxSpan) * columnSpanForIndex,
            };
    };
    return GridLayoutManager;
}(LayoutManager_1.WrapGridLayoutManager));
exports.GridLayoutManager = GridLayoutManager;
//# sourceMappingURL=GridLayoutManager.js.map