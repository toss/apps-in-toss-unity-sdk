function _extends() { _extends = Object.assign ? Object.assign.bind() : function (target) { for (var i = 1; i < arguments.length; i++) { var source = arguments[i]; for (var key in source) { if (Object.prototype.hasOwnProperty.call(source, key)) { target[key] = source[key]; } } } return target; }; return _extends.apply(this, arguments); }

import React, { forwardRef, useEffect } from 'react';
import { DeviceEventEmitter, StyleSheet } from 'react-native';
import NativeBlurView from '../fabric/BlurViewNativeComponentAndroid';
const OVERLAY_COLORS = {
  light: 'rgba(255, 255, 255, 0.2)',
  xlight: 'rgba(255, 255, 255, 0.75)',
  dark: 'rgba(16, 12, 12, 0.64)'
};
const BlurView = /*#__PURE__*/forwardRef((_ref, ref) => {
  let {
    downsampleFactor,
    blurRadius,
    blurAmount = 10,
    blurType = 'dark',
    overlayColor,
    enabled,
    autoUpdate,
    children,
    style,
    ...rest
  } = _ref;
  useEffect(() => {
    DeviceEventEmitter.addListener('ReactNativeBlurError', message => {
      throw new Error(`[ReactNativeBlur]: ${message}`);
    });
    return () => {
      DeviceEventEmitter.removeAllListeners('ReactNativeBlurError');
    };
  }, []);

  const getOverlayColor = () => {
    if (overlayColor != null) {
      return overlayColor;
    }

    return OVERLAY_COLORS[blurType] || OVERLAY_COLORS.dark;
  };

  const getBlurRadius = () => {
    if (blurRadius != null) {
      if (blurRadius > 25) {
        throw new Error(`[ReactNativeBlur]: blurRadius cannot be greater than 25! (was: ${blurRadius})`);
      }

      return blurRadius;
    } // iOS seems to use a slightly different blurring algorithm (or scale?).
    // Android blurRadius + downsampleFactor is approximately 80% of blurAmount.


    const equivalentBlurRadius = Math.round(blurAmount * 0.8);

    if (equivalentBlurRadius > 25) {
      return 25;
    }

    return equivalentBlurRadius;
  };

  const getDownsampleFactor = () => {
    if (downsampleFactor != null) {
      return downsampleFactor;
    }

    return blurRadius;
  };

  return /*#__PURE__*/React.createElement(NativeBlurView, _extends({}, rest, {
    ref: ref,
    blurRadius: getBlurRadius(),
    downsampleFactor: getDownsampleFactor(),
    overlayColor: getOverlayColor(),
    blurAmount: blurAmount,
    blurType: blurType,
    enabled: enabled,
    autoUpdate: autoUpdate,
    pointerEvents: "none",
    style: StyleSheet.compose(styles.transparent, style)
  }), children);
});
const styles = StyleSheet.create({
  transparent: {
    backgroundColor: 'transparent'
  }
});
export default BlurView;
//# sourceMappingURL=BlurView.android.js.map