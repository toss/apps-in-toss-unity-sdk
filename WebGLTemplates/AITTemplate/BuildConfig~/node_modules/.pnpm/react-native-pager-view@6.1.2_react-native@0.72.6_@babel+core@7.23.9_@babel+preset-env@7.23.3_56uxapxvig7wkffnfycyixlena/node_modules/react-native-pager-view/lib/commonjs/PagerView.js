"use strict";

Object.defineProperty(exports, "__esModule", {
  value: true
});
exports.PagerView = void 0;
var _react = _interopRequireDefault(require("react"));
var _reactNative = require("react-native");
var _utils = require("./utils");
var _PagerViewNativeComponent = _interopRequireWildcard(require("./PagerViewNativeComponent"));
function _getRequireWildcardCache(nodeInterop) { if (typeof WeakMap !== "function") return null; var cacheBabelInterop = new WeakMap(); var cacheNodeInterop = new WeakMap(); return (_getRequireWildcardCache = function (nodeInterop) { return nodeInterop ? cacheNodeInterop : cacheBabelInterop; })(nodeInterop); }
function _interopRequireWildcard(obj, nodeInterop) { if (!nodeInterop && obj && obj.__esModule) { return obj; } if (obj === null || typeof obj !== "object" && typeof obj !== "function") { return { default: obj }; } var cache = _getRequireWildcardCache(nodeInterop); if (cache && cache.has(obj)) { return cache.get(obj); } var newObj = {}; var hasPropertyDescriptor = Object.defineProperty && Object.getOwnPropertyDescriptor; for (var key in obj) { if (key !== "default" && Object.prototype.hasOwnProperty.call(obj, key)) { var desc = hasPropertyDescriptor ? Object.getOwnPropertyDescriptor(obj, key) : null; if (desc && (desc.get || desc.set)) { Object.defineProperty(newObj, key, desc); } else { newObj[key] = obj[key]; } } } newObj.default = obj; if (cache) { cache.set(obj, newObj); } return newObj; }
function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }
function _extends() { _extends = Object.assign ? Object.assign.bind() : function (target) { for (var i = 1; i < arguments.length; i++) { var source = arguments[i]; for (var key in source) { if (Object.prototype.hasOwnProperty.call(source, key)) { target[key] = source[key]; } } } return target; }; return _extends.apply(this, arguments); }
function _defineProperty(obj, key, value) { if (key in obj) { Object.defineProperty(obj, key, { value: value, enumerable: true, configurable: true, writable: true }); } else { obj[key] = value; } return obj; }
/**
 * Container that allows to flip left and right between child views. Each
 * child view of the `PagerView` will be treated as a separate page
 * and will be stretched to fill the `PagerView`.
 *
 * It is important all children are `<View>`s and not composite components.
 * You can set style properties like `padding` or `backgroundColor` for each
 * child. It is also important that each child have a `key` prop.
 *
 * Example:
 *
 * ```
 * render: function() {
 *   return (
 *     <PagerView
 *       style={styles.PagerView}
 *       initialPage={0}>
 *       <View style={styles.pageStyle} key="1">
 *         <Text>First page</Text>
 *       </View>
 *       <View style={styles.pageStyle} key="2">
 *         <Text>Second page</Text>
 *       </View>
 *     </PagerView>
 *   );
 * }
 *
 * ...
 *
 * var styles = {
 *   ...
 *   PagerView: {
 *     flex: 1
 *   },
 *   pageStyle: {
 *     alignItems: 'center',
 *     padding: 20,
 *   }
 * }
 * ```
 */

class PagerView extends _react.default.Component {
  constructor() {
    super(...arguments);
    _defineProperty(this, "isScrolling", false);
    _defineProperty(this, "pagerView", null);
    _defineProperty(this, "_onPageScroll", e => {
      if (this.props.onPageScroll) {
        this.props.onPageScroll(e);
      }

      // Not implemented on iOS yet
      if (_reactNative.Platform.OS === 'android') {
        if (this.props.keyboardDismissMode === 'on-drag') {
          _reactNative.Keyboard.dismiss();
        }
      }
    });
    _defineProperty(this, "_onPageScrollStateChanged", e => {
      if (this.props.onPageScrollStateChanged) {
        this.props.onPageScrollStateChanged(e);
      }
      this.isScrolling = e.nativeEvent.pageScrollState === 'dragging';
    });
    _defineProperty(this, "_onPageSelected", e => {
      if (this.props.onPageSelected) {
        this.props.onPageSelected(e);
      }
    });
    _defineProperty(this, "setPage", selectedPage => {
      if (this.pagerView) {
        _PagerViewNativeComponent.Commands.setPage(this.pagerView, selectedPage);
      }
    });
    _defineProperty(this, "setPageWithoutAnimation", selectedPage => {
      if (this.pagerView) {
        _PagerViewNativeComponent.Commands.setPageWithoutAnimation(this.pagerView, selectedPage);
      }
    });
    _defineProperty(this, "setScrollEnabled", scrollEnabled => {
      if (this.pagerView) {
        _PagerViewNativeComponent.Commands.setScrollEnabledImperatively(this.pagerView, scrollEnabled);
      }
    });
    _defineProperty(this, "_onMoveShouldSetResponderCapture", () => {
      return this.isScrolling;
    });
  }
  get deducedLayoutDirection() {
    if (!this.props.layoutDirection ||
    //@ts-ignore fix it
    this.props.layoutDirection === 'locale') {
      return _reactNative.I18nManager.isRTL ? 'rtl' : 'ltr';
    } else {
      return this.props.layoutDirection;
    }
  }
  render() {
    return /*#__PURE__*/_react.default.createElement(_PagerViewNativeComponent.default, _extends({}, this.props, {
      ref: ref => {
        this.pagerView = ref;
      },
      style: this.props.style,
      layoutDirection: this.deducedLayoutDirection,
      onPageScroll: this._onPageScroll,
      onPageScrollStateChanged: this._onPageScrollStateChanged,
      onPageSelected: this._onPageSelected,
      onMoveShouldSetResponderCapture: this._onMoveShouldSetResponderCapture,
      children: (0, _utils.childrenWithOverriddenStyle)(this.props.children)
    }));
  }
}
exports.PagerView = PagerView;
//# sourceMappingURL=PagerView.js.map