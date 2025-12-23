function _extends() { _extends = Object.assign ? Object.assign.bind() : function (target) { for (var i = 1; i < arguments.length; i++) { var source = arguments[i]; for (var key in source) { if (Object.prototype.hasOwnProperty.call(source, key)) { target[key] = source[key]; } } } return target; }; return _extends.apply(this, arguments); }

import React, { forwardRef } from 'react';
import { StyleSheet } from 'react-native';
import NativeVibrancyView from '../fabric/VibrancyViewNativeComponent';
const VibrancyView = /*#__PURE__*/forwardRef((_ref, ref) => {
  let {
    style,
    ...rest
  } = _ref;
  return /*#__PURE__*/React.createElement(NativeVibrancyView, _extends({}, rest, {
    ref: ref,
    style: StyleSheet.compose(styles.transparent, style)
  }));
});
const styles = StyleSheet.create({
  transparent: {
    backgroundColor: 'transparent'
  }
});
export default VibrancyView;
//# sourceMappingURL=VibrancyView.ios.js.map