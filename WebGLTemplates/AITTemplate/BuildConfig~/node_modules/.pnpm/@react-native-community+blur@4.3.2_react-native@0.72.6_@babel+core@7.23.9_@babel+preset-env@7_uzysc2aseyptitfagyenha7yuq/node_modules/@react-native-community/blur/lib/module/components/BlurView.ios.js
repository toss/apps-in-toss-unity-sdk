function _extends() { _extends = Object.assign ? Object.assign.bind() : function (target) { for (var i = 1; i < arguments.length; i++) { var source = arguments[i]; for (var key in source) { if (Object.prototype.hasOwnProperty.call(source, key)) { target[key] = source[key]; } } } return target; }; return _extends.apply(this, arguments); }

import React, { forwardRef } from 'react';
import { StyleSheet } from 'react-native';
import NativeBlurView from '../fabric/BlurViewNativeComponent';
const BlurView = /*#__PURE__*/forwardRef((_ref, ref) => {
  let {
    blurType = 'dark',
    blurAmount = 10,
    style,
    ...rest
  } = _ref;
  return /*#__PURE__*/React.createElement(NativeBlurView, _extends({
    ref: ref,
    style: StyleSheet.compose(styles.transparent, style),
    blurType: blurType,
    blurAmount: blurAmount
  }, rest));
});
const styles = StyleSheet.create({
  transparent: {
    backgroundColor: 'transparent'
  }
});
export default BlurView;
//# sourceMappingURL=BlurView.ios.js.map