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
var react_native_1 = require("react-native");
var ItemAnimator_1 = require("../../../core/ItemAnimator");
var DefaultNativeItemAnimator = /** @class */ (function (_super) {
    __extends(DefaultNativeItemAnimator, _super);
    function DefaultNativeItemAnimator() {
        var _this = _super.call(this) || this;
        _this.shouldAnimateOnce = true;
        _this._hasAnimatedOnce = false;
        _this._isTimerOn = false;
        if (react_native_1.Platform.OS === "android" && react_native_1.UIManager.setLayoutAnimationEnabledExperimental) {
            react_native_1.UIManager.setLayoutAnimationEnabledExperimental(true);
        }
        return _this;
    }
    DefaultNativeItemAnimator.prototype.animateWillMount = function (atX, atY, itemIndex) {
        return undefined;
    };
    DefaultNativeItemAnimator.prototype.animateDidMount = function (atX, atY, itemRef, itemIndex) {
        //no need
    };
    DefaultNativeItemAnimator.prototype.animateWillUpdate = function (fromX, fromY, toX, toY, itemRef, itemIndex) {
        this._hasAnimatedOnce = true;
    };
    DefaultNativeItemAnimator.prototype.animateShift = function (fromX, fromY, toX, toY, itemRef, itemIndex) {
        var _this = this;
        if (fromX !== toX || fromY !== toY) {
            if (!this.shouldAnimateOnce || this.shouldAnimateOnce && !this._hasAnimatedOnce) {
                react_native_1.LayoutAnimation.configureNext(react_native_1.LayoutAnimation.Presets.easeInEaseOut);
                this._hasAnimatedOnce = true;
            }
        }
        else {
            if (!this._isTimerOn) {
                this._isTimerOn = true;
                if (!this._hasAnimatedOnce) {
                    setTimeout(function () {
                        _this._hasAnimatedOnce = true;
                    }, 1000);
                }
            }
        }
        return false;
    };
    DefaultNativeItemAnimator.prototype.animateWillUnmount = function (atX, atY, itemRef, itemIndex) {
        //no need
    };
    return DefaultNativeItemAnimator;
}(ItemAnimator_1.BaseItemAnimator));
exports.DefaultNativeItemAnimator = DefaultNativeItemAnimator;
//# sourceMappingURL=DefaultNativeItemAnimator.js.map