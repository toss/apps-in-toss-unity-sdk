"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.getFooterContainer = exports.getItemAnimator = exports.getCellContainerPlatformStyles = exports.PlatformConfig = void 0;
var PlatformConfig = {
    defaultDrawDistance: 250,
    // Using rotate instead of scaleY on Android to avoid performance issues. Issue: https://github.com/Shopify/flash-list/issues/751
    invertedTransformStyle: { transform: [{ rotate: "180deg" }] },
    invertedTransformStyleHorizontal: { transform: [{ rotate: "180deg" }] },
};
exports.PlatformConfig = PlatformConfig;
var getCellContainerPlatformStyles = function (inverted, parentProps) {
    return undefined;
};
exports.getCellContainerPlatformStyles = getCellContainerPlatformStyles;
var getItemAnimator = function () {
    return undefined;
};
exports.getItemAnimator = getItemAnimator;
var getFooterContainer = function () {
    return undefined;
};
exports.getFooterContainer = getFooterContainer;
//# sourceMappingURL=PlatformHelper.android.js.map