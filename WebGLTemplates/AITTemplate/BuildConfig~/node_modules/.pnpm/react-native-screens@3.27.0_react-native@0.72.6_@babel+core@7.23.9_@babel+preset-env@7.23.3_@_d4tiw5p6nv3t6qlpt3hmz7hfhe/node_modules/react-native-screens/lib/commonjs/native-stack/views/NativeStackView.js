"use strict";

Object.defineProperty(exports, "__esModule", {
  value: true
});
exports.default = NativeStackView;
var React = _interopRequireWildcard(require("react"));
var _reactNative = require("react-native");
var _AppContainer = _interopRequireDefault(require("react-native/Libraries/ReactNative/AppContainer"));
var _warnOnce = _interopRequireDefault(require("warn-once"));
var _reactNativeScreens = require("react-native-screens");
var _native = require("@react-navigation/native");
var _reactNativeSafeAreaContext = require("react-native-safe-area-context");
var _HeaderConfig = _interopRequireDefault(require("./HeaderConfig"));
var _SafeAreaProviderCompat = _interopRequireDefault(require("../utils/SafeAreaProviderCompat"));
var _getDefaultHeaderHeight = _interopRequireDefault(require("../utils/getDefaultHeaderHeight"));
var _getStatusBarHeight = _interopRequireDefault(require("../utils/getStatusBarHeight"));
var _HeaderHeightContext = _interopRequireDefault(require("../utils/HeaderHeightContext"));
var _AnimatedHeaderHeightContext = _interopRequireDefault(require("../utils/AnimatedHeaderHeightContext"));
function _interopRequireDefault(obj) { return obj && obj.__esModule ? obj : { default: obj }; }
function _getRequireWildcardCache(nodeInterop) { if (typeof WeakMap !== "function") return null; var cacheBabelInterop = new WeakMap(); var cacheNodeInterop = new WeakMap(); return (_getRequireWildcardCache = function (nodeInterop) { return nodeInterop ? cacheNodeInterop : cacheBabelInterop; })(nodeInterop); }
function _interopRequireWildcard(obj, nodeInterop) { if (!nodeInterop && obj && obj.__esModule) { return obj; } if (obj === null || typeof obj !== "object" && typeof obj !== "function") { return { default: obj }; } var cache = _getRequireWildcardCache(nodeInterop); if (cache && cache.has(obj)) { return cache.get(obj); } var newObj = {}; var hasPropertyDescriptor = Object.defineProperty && Object.getOwnPropertyDescriptor; for (var key in obj) { if (key !== "default" && Object.prototype.hasOwnProperty.call(obj, key)) { var desc = hasPropertyDescriptor ? Object.getOwnPropertyDescriptor(obj, key) : null; if (desc && (desc.get || desc.set)) { Object.defineProperty(newObj, key, desc); } else { newObj[key] = obj[key]; } } } newObj.default = obj; if (cache) { cache.set(obj, newObj); } return newObj; }
function _extends() { _extends = Object.assign ? Object.assign.bind() : function (target) { for (var i = 1; i < arguments.length; i++) { var source = arguments[i]; for (var key in source) { if (Object.prototype.hasOwnProperty.call(source, key)) { target[key] = source[key]; } } } return target; }; return _extends.apply(this, arguments); } // @ts-ignore Getting private component
// eslint-disable-next-line import/no-named-as-default, import/default, import/no-named-as-default-member, import/namespace
const isAndroid = _reactNative.Platform.OS === 'android';
let Container = _reactNative.View;
if (__DEV__) {
  const DebugContainer = props => {
    const {
      stackPresentation,
      ...rest
    } = props;
    if (_reactNative.Platform.OS === 'ios' && stackPresentation !== 'push') {
      return /*#__PURE__*/React.createElement(_AppContainer.default, null, /*#__PURE__*/React.createElement(_reactNative.View, rest));
    }
    return /*#__PURE__*/React.createElement(_reactNative.View, rest);
  };
  // @ts-ignore Wrong props
  Container = DebugContainer;
}
const MaybeNestedStack = _ref => {
  var _options$statusBarTra, _options$headerLargeT;
  let {
    options,
    route,
    stackPresentation,
    children
  } = _ref;
  const {
    colors
  } = (0, _native.useTheme)();
  const {
    headerShown = true,
    contentStyle
  } = options;
  const Screen = React.useContext(_reactNativeScreens.ScreenContext);
  const isHeaderInModal = isAndroid ? false : stackPresentation !== 'push' && headerShown === true;
  const headerShownPreviousRef = React.useRef(headerShown);
  React.useEffect(() => {
    (0, _warnOnce.default)(!isAndroid && stackPresentation !== 'push' && headerShownPreviousRef.current !== headerShown, `Dynamically changing 'headerShown' in modals will result in remounting the screen and losing all local state. See options for the screen '${route.name}'.`);
    headerShownPreviousRef.current = headerShown;
  }, [headerShown, stackPresentation, route.name]);
  const content = /*#__PURE__*/React.createElement(Container, {
    style: [styles.container, stackPresentation !== 'transparentModal' && stackPresentation !== 'containedTransparentModal' && {
      backgroundColor: colors.background
    }, contentStyle]
    // @ts-ignore Wrong props passed to View
    ,
    stackPresentation: stackPresentation
    // This view must *not* be flattened.
    // See https://github.com/software-mansion/react-native-screens/pull/1825
    // for detailed explanation.
    ,
    collapsable: false
  }, children);
  const dimensions = (0, _reactNativeSafeAreaContext.useSafeAreaFrame)();
  const topInset = (0, _reactNativeSafeAreaContext.useSafeAreaInsets)().top;
  const isStatusBarTranslucent = (_options$statusBarTra = options.statusBarTranslucent) !== null && _options$statusBarTra !== void 0 ? _options$statusBarTra : false;
  const statusBarHeight = (0, _getStatusBarHeight.default)(topInset, dimensions, isStatusBarTranslucent);
  const hasLargeHeader = (_options$headerLargeT = options.headerLargeTitle) !== null && _options$headerLargeT !== void 0 ? _options$headerLargeT : false;
  const headerHeight = (0, _getDefaultHeaderHeight.default)(dimensions, statusBarHeight, stackPresentation, hasLargeHeader);
  if (isHeaderInModal) {
    return /*#__PURE__*/React.createElement(_reactNativeScreens.ScreenStack, {
      style: styles.container
    }, /*#__PURE__*/React.createElement(Screen, {
      enabled: true,
      isNativeStack: true,
      hasLargeHeader: hasLargeHeader,
      style: _reactNative.StyleSheet.absoluteFill
    }, /*#__PURE__*/React.createElement(_HeaderHeightContext.default.Provider, {
      value: headerHeight
    }, /*#__PURE__*/React.createElement(_HeaderConfig.default, _extends({}, options, {
      route: route
    })), content)));
  }
  return content;
};
const RouteView = _ref2 => {
  var _options$statusBarTra2, _options$headerLargeT2;
  let {
    descriptors,
    route,
    index,
    navigation,
    stateKey
  } = _ref2;
  const {
    options,
    render: renderScene
  } = descriptors[route.key];
  const {
    gestureEnabled,
    headerShown,
    hideKeyboardOnSwipe,
    homeIndicatorHidden,
    sheetAllowedDetents = 'large',
    sheetLargestUndimmedDetent = 'all',
    sheetGrabberVisible = false,
    sheetCornerRadius = -1.0,
    sheetExpandsWhenScrolledToEdge = true,
    nativeBackButtonDismissalEnabled = false,
    navigationBarColor,
    navigationBarHidden,
    replaceAnimation = 'pop',
    screenOrientation,
    statusBarAnimation,
    statusBarColor,
    statusBarHidden,
    statusBarStyle,
    statusBarTranslucent,
    swipeDirection = 'horizontal',
    transitionDuration,
    freezeOnBlur
  } = options;
  let {
    customAnimationOnSwipe,
    fullScreenSwipeEnabled,
    gestureResponseDistance,
    stackAnimation,
    stackPresentation = 'push'
  } = options;
  if (swipeDirection === 'vertical') {
    // for `vertical` direction to work, we need to set `fullScreenSwipeEnabled` to `true`
    // so the screen can be dismissed from any point on screen.
    // `customAnimationOnSwipe` needs to be set to `true` so the `stackAnimation` set by user can be used,
    // otherwise `simple_push` will be used.
    // Also, the default animation for this direction seems to be `slide_from_bottom`.
    if (fullScreenSwipeEnabled === undefined) {
      fullScreenSwipeEnabled = true;
    }
    if (customAnimationOnSwipe === undefined) {
      customAnimationOnSwipe = true;
    }
    if (stackAnimation === undefined) {
      stackAnimation = 'slide_from_bottom';
    }
  }
  if (index === 0) {
    // first screen should always be treated as `push`, it resolves problems with no header animation
    // for navigator with first screen as `modal` and the next as `push`
    stackPresentation = 'push';
  }
  const dimensions = (0, _reactNativeSafeAreaContext.useSafeAreaFrame)();
  const topInset = (0, _reactNativeSafeAreaContext.useSafeAreaInsets)().top;
  const isStatusBarTranslucent = (_options$statusBarTra2 = options.statusBarTranslucent) !== null && _options$statusBarTra2 !== void 0 ? _options$statusBarTra2 : false;
  const statusBarHeight = (0, _getStatusBarHeight.default)(topInset, dimensions, isStatusBarTranslucent);
  const hasLargeHeader = (_options$headerLargeT2 = options.headerLargeTitle) !== null && _options$headerLargeT2 !== void 0 ? _options$headerLargeT2 : false;
  const defaultHeaderHeight = (0, _getDefaultHeaderHeight.default)(dimensions, statusBarHeight, stackPresentation, hasLargeHeader);
  const parentHeaderHeight = React.useContext(_HeaderHeightContext.default);
  const isHeaderInPush = isAndroid ? headerShown : stackPresentation === 'push' && headerShown !== false;
  const staticHeaderHeight = isHeaderInPush !== false ? defaultHeaderHeight : parentHeaderHeight !== null && parentHeaderHeight !== void 0 ? parentHeaderHeight : 0;

  // We need to ensure the first retrieved header height will be cached and set in animatedHeaderHeight.
  // We're caching the header height here, as on iOS native side events are not always coming to the JS on first notify.
  // TODO: Check why first event is not being received once it is cached on the native side.
  const cachedAnimatedHeaderHeight = React.useRef(defaultHeaderHeight);
  const animatedHeaderHeight = React.useRef(new _reactNative.Animated.Value(staticHeaderHeight, {
    useNativeDriver: true
  })).current;
  const Screen = React.useContext(_reactNativeScreens.ScreenContext);
  const {
    dark
  } = (0, _native.useTheme)();
  return /*#__PURE__*/React.createElement(Screen, {
    key: route.key,
    enabled: true,
    isNativeStack: true,
    hasLargeHeader: hasLargeHeader,
    style: _reactNative.StyleSheet.absoluteFill,
    sheetAllowedDetents: sheetAllowedDetents,
    sheetLargestUndimmedDetent: sheetLargestUndimmedDetent,
    sheetGrabberVisible: sheetGrabberVisible,
    sheetCornerRadius: sheetCornerRadius,
    sheetExpandsWhenScrolledToEdge: sheetExpandsWhenScrolledToEdge,
    customAnimationOnSwipe: customAnimationOnSwipe,
    freezeOnBlur: freezeOnBlur,
    fullScreenSwipeEnabled: fullScreenSwipeEnabled,
    hideKeyboardOnSwipe: hideKeyboardOnSwipe,
    homeIndicatorHidden: homeIndicatorHidden,
    gestureEnabled: isAndroid ? false : gestureEnabled,
    gestureResponseDistance: gestureResponseDistance,
    nativeBackButtonDismissalEnabled: nativeBackButtonDismissalEnabled,
    navigationBarColor: navigationBarColor,
    navigationBarHidden: navigationBarHidden,
    replaceAnimation: replaceAnimation,
    screenOrientation: screenOrientation,
    stackAnimation: stackAnimation,
    stackPresentation: stackPresentation,
    statusBarAnimation: statusBarAnimation,
    statusBarColor: statusBarColor,
    statusBarHidden: statusBarHidden,
    statusBarStyle: statusBarStyle !== null && statusBarStyle !== void 0 ? statusBarStyle : dark ? 'light' : 'dark',
    statusBarTranslucent: statusBarTranslucent,
    swipeDirection: swipeDirection,
    transitionDuration: transitionDuration,
    onHeaderBackButtonClicked: () => {
      navigation.dispatch({
        ..._native.StackActions.pop(),
        source: route.key,
        target: stateKey
      });
    },
    onWillAppear: () => {
      navigation.emit({
        type: 'transitionStart',
        data: {
          closing: false
        },
        target: route.key
      });
    },
    onWillDisappear: () => {
      navigation.emit({
        type: 'transitionStart',
        data: {
          closing: true
        },
        target: route.key
      });
    },
    onAppear: () => {
      navigation.emit({
        type: 'appear',
        target: route.key
      });
      navigation.emit({
        type: 'transitionEnd',
        data: {
          closing: false
        },
        target: route.key
      });
    },
    onDisappear: () => {
      navigation.emit({
        type: 'transitionEnd',
        data: {
          closing: true
        },
        target: route.key
      });
    },
    onHeaderHeightChange: e => {
      const headerHeight = e.nativeEvent.headerHeight;
      if (cachedAnimatedHeaderHeight.current !== headerHeight) {
        // Currently, we're setting value by Animated#setValue, because we want to cache animated value.
        // Also, in React Native 0.72 there was a bug on Fabric causing a large delay between the screen transition,
        // which should not occur.
        // TODO: Check if it's possible to replace animated#setValue to Animated#event.
        animatedHeaderHeight.setValue(headerHeight);
        cachedAnimatedHeaderHeight.current = headerHeight;
      }
    },
    onDismissed: e => {
      navigation.emit({
        type: 'dismiss',
        target: route.key
      });
      const dismissCount = e.nativeEvent.dismissCount > 0 ? e.nativeEvent.dismissCount : 1;
      navigation.dispatch({
        ..._native.StackActions.pop(dismissCount),
        source: route.key,
        target: stateKey
      });
    },
    onGestureCancel: () => {
      navigation.emit({
        type: 'gestureCancel',
        target: route.key
      });
    }
  }, /*#__PURE__*/React.createElement(_AnimatedHeaderHeightContext.default.Provider, {
    value: animatedHeaderHeight
  }, /*#__PURE__*/React.createElement(_HeaderHeightContext.default.Provider, {
    value: staticHeaderHeight
  }, /*#__PURE__*/React.createElement(MaybeNestedStack, {
    options: options,
    route: route,
    stackPresentation: stackPresentation
  }, renderScene()), /*#__PURE__*/React.createElement(_HeaderConfig.default, _extends({}, options, {
    route: route,
    headerShown: isHeaderInPush
  })))));
};
function NativeStackViewInner(_ref3) {
  let {
    state,
    navigation,
    descriptors
  } = _ref3;
  const {
    key,
    routes
  } = state;
  return /*#__PURE__*/React.createElement(_reactNativeScreens.ScreenStack, {
    style: styles.container
  }, routes.map((route, index) => /*#__PURE__*/React.createElement(RouteView, {
    key: route.key,
    descriptors: descriptors,
    route: route,
    index: index,
    navigation: navigation,
    stateKey: key
  })));
}
function NativeStackView(props) {
  return /*#__PURE__*/React.createElement(_SafeAreaProviderCompat.default, null, /*#__PURE__*/React.createElement(NativeStackViewInner, props));
}
const styles = _reactNative.StyleSheet.create({
  container: {
    flex: 1
  }
});
//# sourceMappingURL=NativeStackView.js.map