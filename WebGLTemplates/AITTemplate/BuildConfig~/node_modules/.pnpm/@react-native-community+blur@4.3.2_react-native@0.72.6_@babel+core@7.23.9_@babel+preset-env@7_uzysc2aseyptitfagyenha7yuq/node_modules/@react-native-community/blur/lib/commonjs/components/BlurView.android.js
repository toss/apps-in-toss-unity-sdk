"use strict";

Object.defineProperty(exports, "__esModule", {
  value: true
});
exports.default = void 0;

var _react = _interopRequireWildcard(require("react"));

var _reactNative = require("react-native");

var _BlurViewNativeComponentAndroid = _interopRequireDefault(require("../fabric/BlurViewNativeComponentAndroid"));

function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }

function _getRequireWildcardCache(nodeInterop) { if (typeof WeakMap !== "function") return null; var cacheBabelInterop = new WeakMap(); var cacheNodeInterop = new WeakMap(); return (_getRequireWildcardCache = function (nodeInterop) { return nodeInterop ? cacheNodeInterop : cacheBabelInterop; })(nodeInterop); }

function _interopRequireWildcard(obj, nodeInterop) { if (!nodeInterop && obj && obj.__esModule) { return obj; } if (obj === null || typeof obj !== "object" && typeof obj !== "function") { return { default: obj }; } var cache = _getRequireWildcardCache(nodeInterop); if (cache && cache.has(obj)) { return cache.get(obj); } var newObj = {}; var hasPropertyDescriptor = Object.defineProperty && Object.getOwnPropertyDescriptor; for (var key in obj) { if (key !== "default" && Object.prototype.hasOwnProperty.call(obj, key)) { var desc = hasPropertyDescriptor ? Object.getOwnPropertyDescriptor(obj, key) : null; if (desc && (desc.get || desc.set)) { Object.defineProperty(newObj, key, desc); } else { newObj[key] = obj[key]; } } } newObj.default = obj; if (cache) { cache.set(obj, newObj); } return newObj; }

function _extends() { _extends = Object.assign ? Object.assign.bind() : function (target) { for (var i = 1; i < arguments.length; i++) { var source = arguments[i]; for (var key in source) { if (Object.prototype.hasOwnProperty.call(source, key)) { target[key] = source[key]; } } } return target; }; return _extends.apply(this, arguments); }

const OVERLAY_COLORS = {
  light: 'rgba(255, 255, 255, 0.2)',
  xlight: 'rgba(255, 255, 255, 0.75)',
  dark: 'rgba(16, 12, 12, 0.64)'
};
const BlurView = /*#__PURE__*/(0, _react.forwardRef)((_ref, ref) => {
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
  (0, _react.useEffect)(() => {
    _reactNative.DeviceEventEmitter.addListener('ReactNativeBlurError', message => {
      throw new Error(`[ReactNativeBlur]: ${message}`);
    });

    return () => {
      _reactNative.DeviceEventEmitter.removeAllListeners('ReactNativeBlurError');
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

  return /*#__PURE__*/_react.default.createElement(_BlurViewNativeComponentAndroid.default, _extends({}, rest, {
    ref: ref,
    blurRadius: getBlurRadius(),
    downsampleFactor: getDownsampleFactor(),
    overlayColor: getOverlayColor(),
    blurAmount: blurAmount,
    blurType: blurType,
    enabled: enabled,
    autoUpdate: autoUpdate,
    pointerEvents: "none",
    style: _reactNative.StyleSheet.compose(styles.transparent, style)
  }), children);
});

const styles = _reactNative.StyleSheet.create({
  transparent: {
    backgroundColor: 'transparent'
  }
});

var _default = BlurView;
exports.default = _default;
//# sourceMappingURL=BlurView.android.js.map