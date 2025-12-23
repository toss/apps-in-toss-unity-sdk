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
var ItemAnimator_1 = require("../../../core/ItemAnimator");
/**
 * Default implementation of RLV layout animations for web. We simply hook in transform transitions to beautifully animate all
 * shift events.
 */
var DefaultWebItemAnimator = /** @class */ (function (_super) {
    __extends(DefaultWebItemAnimator, _super);
    function DefaultWebItemAnimator() {
        var _this = _super !== null && _super.apply(this, arguments) || this;
        _this.shouldAnimateOnce = true;
        _this._hasAnimatedOnce = false;
        _this._isTimerOn = false;
        return _this;
    }
    DefaultWebItemAnimator.prototype.animateWillMount = function (atX, atY, itemIndex) {
        return undefined;
    };
    DefaultWebItemAnimator.prototype.animateDidMount = function (atX, atY, itemRef, itemIndex) {
        //no need
    };
    DefaultWebItemAnimator.prototype.animateWillUpdate = function (fromX, fromY, toX, toY, itemRef, itemIndex) {
        this._hasAnimatedOnce = true;
    };
    DefaultWebItemAnimator.prototype.animateShift = function (fromX, fromY, toX, toY, itemRef, itemIndex) {
        var _this = this;
        if (fromX !== toX || fromY !== toY) {
            var element_1 = itemRef;
            if (!this.shouldAnimateOnce || this.shouldAnimateOnce && !this._hasAnimatedOnce) {
                var transitionEndCallback_1 = function (event) {
                    element_1.style.transition = "";
                    element_1.removeEventListener("transitionend", transitionEndCallback_1);
                    _this._hasAnimatedOnce = true;
                };
                element_1.style.transition = "transform 0.15s ease-out";
                element_1.addEventListener("transitionend", transitionEndCallback_1, false);
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
    DefaultWebItemAnimator.prototype.animateWillUnmount = function (atX, atY, itemRef, itemIndex) {
        //no need
    };
    return DefaultWebItemAnimator;
}(ItemAnimator_1.BaseItemAnimator));
exports.DefaultWebItemAnimator = DefaultWebItemAnimator;
//# sourceMappingURL=DefaultWebItemAnimator.js.map