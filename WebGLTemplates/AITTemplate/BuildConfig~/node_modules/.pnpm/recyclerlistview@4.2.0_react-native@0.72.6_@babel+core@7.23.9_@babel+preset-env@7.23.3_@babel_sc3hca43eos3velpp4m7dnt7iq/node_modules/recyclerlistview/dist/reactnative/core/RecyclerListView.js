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
var __assign = (this && this.__assign) || function () {
    __assign = Object.assign || function(t) {
        for (var s, i = 1, n = arguments.length; i < n; i++) {
            s = arguments[i];
            for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
                t[p] = s[p];
        }
        return t;
    };
    return __assign.apply(this, arguments);
};
Object.defineProperty(exports, "__esModule", { value: true });
/***
 * DONE: Reduce layout processing on data insert
 * DONE: Add notify data set changed and notify data insert option in data source
 * DONE: Add on end reached callback
 * DONE: Make another class for render stack generator
 * DONE: Simplify rendering a loading footer
 * DONE: Anchor first visible index on any insert/delete data wise
 * DONE: Build Scroll to index
 * DONE: Give viewability callbacks
 * DONE: Add full render logic in cases like change of dimensions
 * DONE: Fix all proptypes
 * DONE: Add Initial render Index support
 * DONE: Add animated scroll to web scrollviewer
 * DONE: Animate list view transition, including add/remove
 * DONE: Implement sticky headers and footers
 * TODO: Destroy less frequently used items in recycle pool, this will help in case of too many types.
 * TODO: Make viewability callbacks configurable
 * TODO: Observe size changes on web to optimize for reflowability
 * TODO: Solve //TSI
 */
var debounce = require("lodash.debounce");
var PropTypes = require("prop-types");
var React = require("react");
var ts_object_utils_1 = require("ts-object-utils");
var ContextProvider_1 = require("./dependencies/ContextProvider");
var DataProvider_1 = require("./dependencies/DataProvider");
var LayoutProvider_1 = require("./dependencies/LayoutProvider");
var CustomError_1 = require("./exceptions/CustomError");
var RecyclerListViewExceptions_1 = require("./exceptions/RecyclerListViewExceptions");
var Constants_1 = require("./constants/Constants");
var Messages_1 = require("./constants/Messages");
var VirtualRenderer_1 = require("./VirtualRenderer");
var ItemAnimator_1 = require("./ItemAnimator");
var ComponentCompat_1 = require("../utils/ComponentCompat");
//#if [REACT-NATIVE]
var ScrollComponent_1 = require("../platform/reactnative/scrollcomponent/ScrollComponent");
var ViewRenderer_1 = require("../platform/reactnative/viewrenderer/ViewRenderer");
var react_native_1 = require("react-native");
var IS_WEB = !react_native_1.Platform || react_native_1.Platform.OS === "web";
var RecyclerListView = /** @class */ (function (_super) {
    __extends(RecyclerListView, _super);
    function RecyclerListView(props, context) {
        var _this = _super.call(this, props, context) || this;
        _this.refreshRequestDebouncer = debounce(function (executable) {
            executable();
        });
        _this._onEndReachedCalled = false;
        _this._initComplete = false;
        _this._isMounted = true;
        _this._relayoutReqIndex = -1;
        _this._params = {
            initialOffset: 0,
            initialRenderIndex: 0,
            isHorizontal: false,
            itemCount: 0,
            renderAheadOffset: 250,
        };
        _this._layout = { height: 0, width: 0 };
        _this._pendingScrollToOffset = null;
        _this._tempDim = { height: 0, width: 0 };
        _this._initialOffset = 0;
        _this._scrollComponent = null;
        //If the native content container is used, then positions of the list items are changed on the native side. The animated library used
        //by the default item animator also changes the same positions which could lead to inconsistency. Hence, the base item animator which
        //does not perform any such animations will be used.
        _this._defaultItemAnimator = new ItemAnimator_1.BaseItemAnimator();
        // useWindowCorrection specifies if correction should be applied to these offsets in case you implement
        // `applyWindowCorrection` method
        _this.scrollToOffset = function (x, y, animate, useWindowCorrection) {
            if (animate === void 0) { animate = false; }
            if (useWindowCorrection === void 0) { useWindowCorrection = false; }
            if (_this._scrollComponent) {
                if (_this.props.isHorizontal) {
                    y = 0;
                    x = useWindowCorrection ? x - _this._windowCorrectionConfig.value.windowShift : x;
                }
                else {
                    x = 0;
                    y = useWindowCorrection ? y - _this._windowCorrectionConfig.value.windowShift : y;
                }
                _this._scrollComponent.scrollTo(x, y, animate);
            }
        };
        _this._onItemLayout = function (index) {
            _this.onItemLayout(index);
        };
        _this._onSizeChanged = function (layout) {
            if (layout.height === 0 || layout.width === 0) {
                if (!_this.props.suppressBoundedSizeException) {
                    throw new CustomError_1.default(RecyclerListViewExceptions_1.default.layoutException);
                }
                else {
                    return;
                }
            }
            if (!_this.props.canChangeSize && _this.props.layoutSize) {
                return;
            }
            var hasHeightChanged = _this._layout.height !== layout.height;
            var hasWidthChanged = _this._layout.width !== layout.width;
            _this._layout.height = layout.height;
            _this._layout.width = layout.width;
            if (!_this._initComplete) {
                _this._initComplete = true;
                _this._initTrackers(_this.props);
                _this._processOnEndReached();
            }
            else {
                if ((hasHeightChanged && hasWidthChanged) ||
                    (hasHeightChanged && _this.props.isHorizontal) ||
                    (hasWidthChanged && !_this.props.isHorizontal)) {
                    _this._checkAndChangeLayouts(_this.props, true);
                }
                else {
                    _this._refreshViewability();
                }
            }
        };
        _this._renderStackWhenReady = function (stack) {
            // TODO: Flickers can further be reduced by setting _pendingScrollToOffset in constructor
            // rather than in _onSizeChanged -> _initTrackers
            if (_this._pendingScrollToOffset) {
                _this._pendingRenderStack = stack;
                return;
            }
            if (!_this._initStateIfRequired(stack)) {
                _this.setState(function () {
                    return { renderStack: stack };
                });
            }
        };
        _this._dataHasChanged = function (row1, row2) {
            return _this.props.dataProvider.rowHasChanged(row1, row2);
        };
        _this._onViewContainerSizeChange = function (dim, index) {
            //Cannot be null here
            var layoutManager = _this._virtualRenderer.getLayoutManager();
            if (_this.props.debugHandlers && _this.props.debugHandlers.resizeDebugHandler) {
                var itemRect = layoutManager.getLayouts()[index];
                _this.props.debugHandlers.resizeDebugHandler.resizeDebug({
                    width: itemRect.width,
                    height: itemRect.height,
                }, dim, index);
            }
            // Add extra protection for overrideLayout as it can only be called when non-deterministic rendering is used.
            if (_this.props.forceNonDeterministicRendering && layoutManager.overrideLayout(index, dim)) {
                if (_this._relayoutReqIndex === -1) {
                    _this._relayoutReqIndex = index;
                }
                else {
                    _this._relayoutReqIndex = Math.min(_this._relayoutReqIndex, index);
                }
                _this._queueStateRefresh();
            }
        };
        _this._onScroll = function (offsetX, offsetY, rawEvent) {
            // correction to be positive to shift offset upwards; negative to push offset downwards.
            // extracting the correction value from logical offset and updating offset of virtual renderer.
            _this._virtualRenderer.updateOffset(offsetX, offsetY, true, _this._getWindowCorrection(offsetX, offsetY, _this.props));
            if (_this.props.onScroll) {
                _this.props.onScroll(rawEvent, offsetX, offsetY);
            }
            _this._processOnEndReached();
        };
        _this._virtualRenderer = new VirtualRenderer_1.default(_this._renderStackWhenReady, function (offset) {
            _this._pendingScrollToOffset = offset;
        }, function (index) {
            return _this.props.dataProvider.getStableId(index);
        }, !props.disableRecycling);
        if (_this.props.windowCorrectionConfig) {
            var windowCorrection = void 0;
            if (_this.props.windowCorrectionConfig.value) {
                windowCorrection = _this.props.windowCorrectionConfig.value;
            }
            else {
                windowCorrection = { startCorrection: 0, endCorrection: 0, windowShift: 0 };
            }
            _this._windowCorrectionConfig = {
                applyToItemScroll: !!_this.props.windowCorrectionConfig.applyToItemScroll,
                applyToInitialOffset: !!_this.props.windowCorrectionConfig.applyToInitialOffset,
                value: windowCorrection,
            };
        }
        else {
            _this._windowCorrectionConfig = {
                applyToItemScroll: false,
                applyToInitialOffset: false,
                value: { startCorrection: 0, endCorrection: 0, windowShift: 0 },
            };
        }
        _this._getContextFromContextProvider(props);
        if (props.layoutSize) {
            _this._layout.height = props.layoutSize.height;
            _this._layout.width = props.layoutSize.width;
            _this._initComplete = true;
            _this._initTrackers(props);
        }
        else {
            _this.state = {
                internalSnapshot: {},
                renderStack: {},
            };
        }
        return _this;
    }
    RecyclerListView.prototype.componentWillReceivePropsCompat = function (newProps) {
        this._assertDependencyPresence(newProps);
        this._checkAndChangeLayouts(newProps);
        if (!newProps.onVisibleIndicesChanged) {
            this._virtualRenderer.removeVisibleItemsListener();
        }
        if (newProps.onVisibleIndexesChanged) {
            throw new CustomError_1.default(RecyclerListViewExceptions_1.default.usingOldVisibleIndexesChangedParam);
        }
        if (newProps.onVisibleIndicesChanged) {
            this._virtualRenderer.attachVisibleItemsListener(newProps.onVisibleIndicesChanged);
        }
    };
    RecyclerListView.prototype.componentDidUpdate = function () {
        this._processInitialOffset();
        this._processOnEndReached();
        this._checkAndChangeLayouts(this.props);
        this._virtualRenderer.setOptimizeForAnimations(false);
    };
    RecyclerListView.prototype.componentDidMount = function () {
        if (this._initComplete) {
            this._processInitialOffset();
            this._processOnEndReached();
        }
    };
    RecyclerListView.prototype.componentWillUnmount = function () {
        this._isMounted = false;
        if (this.props.contextProvider) {
            var uniqueKey = this.props.contextProvider.getUniqueKey();
            if (uniqueKey) {
                this.props.contextProvider.save(uniqueKey + Constants_1.Constants.CONTEXT_PROVIDER_OFFSET_KEY_SUFFIX, this.getCurrentScrollOffset());
                if (this.props.forceNonDeterministicRendering) {
                    if (this._virtualRenderer) {
                        var layoutManager = this._virtualRenderer.getLayoutManager();
                        if (layoutManager) {
                            var layoutsToCache = layoutManager.getLayouts();
                            this.props.contextProvider.save(uniqueKey + Constants_1.Constants.CONTEXT_PROVIDER_LAYOUT_KEY_SUFFIX, JSON.stringify({ layoutArray: layoutsToCache }));
                        }
                    }
                }
            }
        }
    };
    RecyclerListView.prototype.scrollToIndex = function (index, animate) {
        var layoutManager = this._virtualRenderer.getLayoutManager();
        if (layoutManager) {
            var offsets = layoutManager.getOffsetForIndex(index);
            this.scrollToOffset(offsets.x, offsets.y, animate, this._windowCorrectionConfig.applyToItemScroll);
        }
        else {
            console.warn(Messages_1.Messages.WARN_SCROLL_TO_INDEX); //tslint:disable-line
        }
    };
    /**
     * This API is almost similar to scrollToIndex, but differs when the view is already in viewport.
     * Instead of bringing the view to the top of the viewport, it will calculate the overflow of the @param index
     * and scroll to just bring the entire view to viewport.
     */
    RecyclerListView.prototype.bringToFocus = function (index, animate) {
        var listSize = this.getRenderedSize();
        var itemLayout = this.getLayout(index);
        var currentScrollOffset = this.getCurrentScrollOffset() + this._windowCorrectionConfig.value.windowShift;
        var isHorizontal = this.props.isHorizontal;
        if (itemLayout) {
            var mainAxisLayoutDimen = isHorizontal ? itemLayout.width : itemLayout.height;
            var mainAxisLayoutPos = isHorizontal ? itemLayout.x : itemLayout.y;
            var mainAxisListDimen = isHorizontal ? listSize.width : listSize.height;
            var screenEndPos = mainAxisListDimen + currentScrollOffset;
            if (mainAxisLayoutDimen > mainAxisListDimen || mainAxisLayoutPos < currentScrollOffset || mainAxisLayoutPos > screenEndPos) {
                this.scrollToIndex(index);
            }
            else {
                var viewEndPos = mainAxisLayoutPos + mainAxisLayoutDimen;
                if (viewEndPos > screenEndPos) {
                    var offset = viewEndPos - screenEndPos;
                    this.scrollToOffset(offset + currentScrollOffset, offset + currentScrollOffset, animate, true);
                }
            }
        }
    };
    RecyclerListView.prototype.scrollToItem = function (data, animate) {
        var count = this.props.dataProvider.getSize();
        for (var i = 0; i < count; i++) {
            if (this.props.dataProvider.getDataForIndex(i) === data) {
                this.scrollToIndex(i, animate);
                break;
            }
        }
    };
    RecyclerListView.prototype.getLayout = function (index) {
        var layoutManager = this._virtualRenderer.getLayoutManager();
        return layoutManager ? layoutManager.getLayouts()[index] : undefined;
    };
    RecyclerListView.prototype.scrollToTop = function (animate) {
        this.scrollToOffset(0, 0, animate);
    };
    RecyclerListView.prototype.scrollToEnd = function (animate) {
        var lastIndex = this.props.dataProvider.getSize() - 1;
        this.scrollToIndex(lastIndex, animate);
    };
    // You can use requestAnimationFrame callback to change renderAhead in multiple frames to enable advanced progressive
    // rendering when view types are very complex. This method returns a boolean saying if the update was committed. Retry in
    // the next frame if you get a failure (if mount wasn't complete). Value should be greater than or equal to 0;
    // Very useful when you have a page where you need a large renderAheadOffset. Setting it at once will slow down the load and
    // this will help mitigate that.
    RecyclerListView.prototype.updateRenderAheadOffset = function (renderAheadOffset) {
        var viewabilityTracker = this._virtualRenderer.getViewabilityTracker();
        if (viewabilityTracker) {
            viewabilityTracker.updateRenderAheadOffset(renderAheadOffset);
            return true;
        }
        return false;
    };
    RecyclerListView.prototype.getCurrentRenderAheadOffset = function () {
        var viewabilityTracker = this._virtualRenderer.getViewabilityTracker();
        if (viewabilityTracker) {
            return viewabilityTracker.getCurrentRenderAheadOffset();
        }
        return this.props.renderAheadOffset;
    };
    RecyclerListView.prototype.getCurrentScrollOffset = function () {
        var viewabilityTracker = this._virtualRenderer.getViewabilityTracker();
        return viewabilityTracker ? viewabilityTracker.getLastActualOffset() : 0;
    };
    RecyclerListView.prototype.findApproxFirstVisibleIndex = function () {
        var viewabilityTracker = this._virtualRenderer.getViewabilityTracker();
        return viewabilityTracker ? viewabilityTracker.findFirstLogicallyVisibleIndex() : 0;
    };
    RecyclerListView.prototype.getRenderedSize = function () {
        return this._layout;
    };
    RecyclerListView.prototype.getContentDimension = function () {
        return this._virtualRenderer.getLayoutDimension();
    };
    // Force Rerender forcefully to update view renderer. Use this in rare circumstances
    RecyclerListView.prototype.forceRerender = function () {
        this.setState({
            internalSnapshot: {},
        });
    };
    RecyclerListView.prototype.getScrollableNode = function () {
        if (this._scrollComponent && this._scrollComponent.getScrollableNode) {
            return this._scrollComponent.getScrollableNode();
        }
        return null;
    };
    RecyclerListView.prototype.renderCompat = function () {
        //TODO:Talha
        // const {
        //     layoutProvider,
        //     dataProvider,
        //     contextProvider,
        //     renderAheadOffset,
        //     onEndReached,
        //     onEndReachedThreshold,
        //     onVisibleIndicesChanged,
        //     initialOffset,
        //     initialRenderIndex,
        //     disableRecycling,
        //     forceNonDeterministicRendering,
        //     extendedState,
        //     itemAnimator,
        //     rowRenderer,
        //     ...props,
        // } = this.props;
        var _this = this;
        return (React.createElement(ScrollComponent_1.default, __assign({ ref: function (scrollComponent) { return _this._scrollComponent = scrollComponent; } }, this.props, this.props.scrollViewProps, { onScroll: this._onScroll, onSizeChanged: this._onSizeChanged, contentHeight: this._initComplete ? this._virtualRenderer.getLayoutDimension().height : 0, contentWidth: this._initComplete ? this._virtualRenderer.getLayoutDimension().width : 0, renderAheadOffset: this.getCurrentRenderAheadOffset() }), this._generateRenderStack()));
    };
    // Disables recycling for the next frame so that layout animations run well.
    // WARNING: Avoid this when making large changes to the data as the list might draw too much to run animations. Single item insertions/deletions
    // should be good. With recycling paused the list cannot do much optimization.
    // The next render will run as normal and reuse items.
    RecyclerListView.prototype.prepareForLayoutAnimationRender = function () {
        this._virtualRenderer.setOptimizeForAnimations(true);
    };
    RecyclerListView.prototype.getVirtualRenderer = function () {
        return this._virtualRenderer;
    };
    RecyclerListView.prototype.onItemLayout = function (index) {
        if (this.props.onItemLayout) {
            this.props.onItemLayout(index);
        }
    };
    RecyclerListView.prototype._processInitialOffset = function () {
        var _this = this;
        if (this._pendingScrollToOffset) {
            setTimeout(function () {
                if (_this._pendingScrollToOffset) {
                    var offset = _this._pendingScrollToOffset;
                    _this._pendingScrollToOffset = null;
                    if (_this.props.isHorizontal) {
                        offset.y = 0;
                    }
                    else {
                        offset.x = 0;
                    }
                    _this.scrollToOffset(offset.x, offset.y, false, _this._windowCorrectionConfig.applyToInitialOffset);
                    if (_this._pendingRenderStack) {
                        _this._renderStackWhenReady(_this._pendingRenderStack);
                        _this._pendingRenderStack = undefined;
                    }
                }
            }, 0);
        }
    };
    RecyclerListView.prototype._getContextFromContextProvider = function (props) {
        if (props.contextProvider) {
            var uniqueKey = props.contextProvider.getUniqueKey();
            if (uniqueKey) {
                var offset = props.contextProvider.get(uniqueKey + Constants_1.Constants.CONTEXT_PROVIDER_OFFSET_KEY_SUFFIX);
                if (typeof offset === "number" && offset > 0) {
                    this._initialOffset = offset;
                    if (props.onRecreate) {
                        props.onRecreate({ lastOffset: this._initialOffset });
                    }
                    props.contextProvider.remove(uniqueKey + Constants_1.Constants.CONTEXT_PROVIDER_OFFSET_KEY_SUFFIX);
                }
                if (props.forceNonDeterministicRendering) {
                    var cachedLayouts = props.contextProvider.get(uniqueKey + Constants_1.Constants.CONTEXT_PROVIDER_LAYOUT_KEY_SUFFIX);
                    if (cachedLayouts && typeof cachedLayouts === "string") {
                        this._cachedLayouts = JSON.parse(cachedLayouts).layoutArray;
                        props.contextProvider.remove(uniqueKey + Constants_1.Constants.CONTEXT_PROVIDER_LAYOUT_KEY_SUFFIX);
                    }
                }
            }
        }
    };
    RecyclerListView.prototype._checkAndChangeLayouts = function (newProps, forceFullRender) {
        this._params.isHorizontal = newProps.isHorizontal;
        this._params.itemCount = newProps.dataProvider.getSize();
        this._virtualRenderer.setParamsAndDimensions(this._params, this._layout);
        this._virtualRenderer.setLayoutProvider(newProps.layoutProvider);
        if (newProps.dataProvider.hasStableIds() && this.props.dataProvider !== newProps.dataProvider) {
            if (newProps.dataProvider.requiresDataChangeHandling()) {
                this._virtualRenderer.handleDataSetChange(newProps.dataProvider);
            }
            else if (this._virtualRenderer.hasPendingAnimationOptimization()) {
                console.warn(Messages_1.Messages.ANIMATION_ON_PAGINATION); //tslint:disable-line
            }
        }
        if (this.props.layoutProvider !== newProps.layoutProvider || this.props.isHorizontal !== newProps.isHorizontal) {
            //TODO:Talha use old layout manager
            this._virtualRenderer.setLayoutManager(newProps.layoutProvider.createLayoutManager(this._layout, newProps.isHorizontal));
            if (newProps.layoutProvider.shouldRefreshWithAnchoring) {
                this._virtualRenderer.refreshWithAnchor();
            }
            else {
                this._virtualRenderer.refresh();
            }
            this._refreshViewability();
        }
        else if (this.props.dataProvider !== newProps.dataProvider) {
            if (newProps.dataProvider.getSize() > this.props.dataProvider.getSize()) {
                this._onEndReachedCalled = false;
            }
            var layoutManager = this._virtualRenderer.getLayoutManager();
            if (layoutManager) {
                layoutManager.relayoutFromIndex(newProps.dataProvider.getFirstIndexToProcessInternal(), newProps.dataProvider.getSize());
                this._virtualRenderer.refresh();
            }
        }
        else if (forceFullRender) {
            var layoutManager = this._virtualRenderer.getLayoutManager();
            if (layoutManager) {
                var cachedLayouts = layoutManager.getLayouts();
                this._virtualRenderer.setLayoutManager(newProps.layoutProvider.createLayoutManager(this._layout, newProps.isHorizontal, cachedLayouts));
                this._refreshViewability();
            }
        }
        else if (this._relayoutReqIndex >= 0) {
            var layoutManager = this._virtualRenderer.getLayoutManager();
            if (layoutManager) {
                var dataProviderSize = newProps.dataProvider.getSize();
                layoutManager.relayoutFromIndex(Math.min(Math.max(dataProviderSize - 1, 0), this._relayoutReqIndex), dataProviderSize);
                this._relayoutReqIndex = -1;
                this._refreshViewability();
            }
        }
    };
    RecyclerListView.prototype._refreshViewability = function () {
        this._virtualRenderer.refresh();
        this._queueStateRefresh();
    };
    RecyclerListView.prototype._queueStateRefresh = function () {
        var _this = this;
        this.refreshRequestDebouncer(function () {
            if (_this._isMounted) {
                _this.setState(function (prevState) {
                    return prevState;
                });
            }
        });
    };
    RecyclerListView.prototype._initStateIfRequired = function (stack) {
        if (!this.state) {
            this.state = {
                internalSnapshot: {},
                renderStack: stack,
            };
            return true;
        }
        return false;
    };
    RecyclerListView.prototype._initTrackers = function (props) {
        this._assertDependencyPresence(props);
        if (props.onVisibleIndexesChanged) {
            throw new CustomError_1.default(RecyclerListViewExceptions_1.default.usingOldVisibleIndexesChangedParam);
        }
        if (props.onVisibleIndicesChanged) {
            this._virtualRenderer.attachVisibleItemsListener(props.onVisibleIndicesChanged);
        }
        this._params = {
            initialOffset: this._initialOffset ? this._initialOffset : props.initialOffset,
            initialRenderIndex: props.initialRenderIndex,
            isHorizontal: props.isHorizontal,
            itemCount: props.dataProvider.getSize(),
            renderAheadOffset: props.renderAheadOffset,
        };
        this._virtualRenderer.setParamsAndDimensions(this._params, this._layout);
        var layoutManager = props.layoutProvider.createLayoutManager(this._layout, props.isHorizontal, this._cachedLayouts);
        this._virtualRenderer.setLayoutManager(layoutManager);
        this._virtualRenderer.setLayoutProvider(props.layoutProvider);
        this._virtualRenderer.init();
        var offset = this._virtualRenderer.getInitialOffset();
        var contentDimension = layoutManager.getContentDimension();
        if ((offset.y > 0 && contentDimension.height > this._layout.height) ||
            (offset.x > 0 && contentDimension.width > this._layout.width)) {
            this._pendingScrollToOffset = offset;
            if (!this._initStateIfRequired()) {
                this.setState({});
            }
        }
        else {
            this._virtualRenderer.startViewabilityTracker(this._getWindowCorrection(offset.x, offset.y, props));
        }
    };
    RecyclerListView.prototype._getWindowCorrection = function (offsetX, offsetY, props) {
        return (props.applyWindowCorrection && props.applyWindowCorrection(offsetX, offsetY, this._windowCorrectionConfig.value))
            || this._windowCorrectionConfig.value;
    };
    RecyclerListView.prototype._assertDependencyPresence = function (props) {
        if (!props.dataProvider || !props.layoutProvider) {
            throw new CustomError_1.default(RecyclerListViewExceptions_1.default.unresolvedDependenciesException);
        }
    };
    RecyclerListView.prototype._assertType = function (type) {
        if (!type && type !== 0) {
            throw new CustomError_1.default(RecyclerListViewExceptions_1.default.itemTypeNullException);
        }
    };
    RecyclerListView.prototype._renderRowUsingMeta = function (itemMeta) {
        var dataSize = this.props.dataProvider.getSize();
        var dataIndex = itemMeta.dataIndex;
        if (!ts_object_utils_1.ObjectUtil.isNullOrUndefined(dataIndex) && dataIndex < dataSize) {
            var itemRect = this._virtualRenderer.getLayoutManager().getLayouts()[dataIndex];
            var data = this.props.dataProvider.getDataForIndex(dataIndex);
            var type = this.props.layoutProvider.getLayoutTypeForIndex(dataIndex);
            var key = this._virtualRenderer.syncAndGetKey(dataIndex);
            var styleOverrides = this._virtualRenderer.getLayoutManager().getStyleOverridesForIndex(dataIndex);
            this._assertType(type);
            if (!this.props.forceNonDeterministicRendering) {
                this._checkExpectedDimensionDiscrepancy(itemRect, type, dataIndex);
            }
            return (React.createElement(ViewRenderer_1.default, { key: key, data: data, dataHasChanged: this._dataHasChanged, x: itemRect.x, y: itemRect.y, layoutType: type, index: dataIndex, styleOverrides: styleOverrides, layoutProvider: this.props.layoutProvider, forceNonDeterministicRendering: this.props.forceNonDeterministicRendering, isHorizontal: this.props.isHorizontal, onSizeChanged: this._onViewContainerSizeChange, childRenderer: this.props.rowRenderer, height: itemRect.height, width: itemRect.width, itemAnimator: ts_object_utils_1.Default.value(this.props.itemAnimator, this._defaultItemAnimator), extendedState: this.props.extendedState, internalSnapshot: this.state.internalSnapshot, renderItemContainer: this.props.renderItemContainer, onItemLayout: this._onItemLayout }));
        }
        return null;
    };
    RecyclerListView.prototype._checkExpectedDimensionDiscrepancy = function (itemRect, type, index) {
        if (this.props.layoutProvider.checkDimensionDiscrepancy(itemRect, type, index)) {
            if (this._relayoutReqIndex === -1) {
                this._relayoutReqIndex = index;
            }
            else {
                this._relayoutReqIndex = Math.min(this._relayoutReqIndex, index);
            }
        }
    };
    RecyclerListView.prototype._generateRenderStack = function () {
        var renderedItems = [];
        if (this.state) {
            for (var key in this.state.renderStack) {
                if (this.state.renderStack.hasOwnProperty(key)) {
                    renderedItems.push(this._renderRowUsingMeta(this.state.renderStack[key]));
                }
            }
        }
        return renderedItems;
    };
    RecyclerListView.prototype._processOnEndReached = function () {
        if (this.props.onEndReached && this._virtualRenderer) {
            var layout = this._virtualRenderer.getLayoutDimension();
            var viewabilityTracker = this._virtualRenderer.getViewabilityTracker();
            if (viewabilityTracker) {
                var windowBound = this.props.isHorizontal ? layout.width - this._layout.width : layout.height - this._layout.height;
                var lastOffset = viewabilityTracker ? viewabilityTracker.getLastOffset() : 0;
                var threshold = windowBound - lastOffset;
                var listLength = this.props.isHorizontal ? this._layout.width : this._layout.height;
                var triggerOnEndThresholdRelative = listLength * ts_object_utils_1.Default.value(this.props.onEndReachedThresholdRelative, 0);
                var triggerOnEndThreshold = ts_object_utils_1.Default.value(this.props.onEndReachedThreshold, 0);
                if (threshold <= triggerOnEndThresholdRelative || threshold <= triggerOnEndThreshold) {
                    if (this.props.onEndReached && !this._onEndReachedCalled) {
                        this._onEndReachedCalled = true;
                        this.props.onEndReached();
                    }
                }
                else {
                    this._onEndReachedCalled = false;
                }
            }
        }
    };
    RecyclerListView.defaultProps = {
        canChangeSize: false,
        disableRecycling: false,
        initialOffset: 0,
        initialRenderIndex: 0,
        isHorizontal: false,
        onEndReachedThreshold: 0,
        onEndReachedThresholdRelative: 0,
        renderAheadOffset: IS_WEB ? 1000 : 250,
    };
    RecyclerListView.propTypes = {};
    return RecyclerListView;
}(ComponentCompat_1.ComponentCompat));
exports.default = RecyclerListView;
RecyclerListView.propTypes = {
    //Refer the sample
    layoutProvider: PropTypes.instanceOf(LayoutProvider_1.BaseLayoutProvider).isRequired,
    //Refer the sample
    dataProvider: PropTypes.instanceOf(DataProvider_1.BaseDataProvider).isRequired,
    //Used to maintain scroll position in case view gets destroyed e.g, cases of back navigation
    contextProvider: PropTypes.instanceOf(ContextProvider_1.default),
    //Methods which returns react component to be rendered. You get type of view and data in the callback.
    rowRenderer: PropTypes.func.isRequired,
    //Initial offset you want to start rendering from, very useful if you want to maintain scroll context across pages.
    initialOffset: PropTypes.number,
    //Specify how many pixels in advance do you want views to be rendered. Increasing this value can help reduce blanks (if any). However keeping this as low
    //as possible should be the intent. Higher values also increase re-render compute
    renderAheadOffset: PropTypes.number,
    //Whether the listview is horizontally scrollable. Both use staggeredGrid implementation
    isHorizontal: PropTypes.bool,
    //On scroll callback onScroll(rawEvent, offsetX, offsetY), note you get offsets no need to read scrollTop/scrollLeft
    onScroll: PropTypes.func,
    //callback onRecreate(params), when recreating recycler view from context provider. Gives you the initial params in the first
    //frame itself to allow you to render content accordingly
    onRecreate: PropTypes.func,
    //Provide your own ScrollView Component. The contract for the scroll event should match the native scroll event contract, i.e.
    // scrollEvent = { nativeEvent: { contentOffset: { x: offset, y: offset } } }
    //Note: Please extend BaseScrollView to achieve expected behaviour
    externalScrollView: PropTypes.oneOfType([PropTypes.func, PropTypes.object]),
    //Callback given when user scrolls to the end of the list or footer just becomes visible, useful in incremental loading scenarios
    onEndReached: PropTypes.func,
    //Specify how many pixels in advance you onEndReached callback
    onEndReachedThreshold: PropTypes.number,
    //Specify how far from the end (in units of visible length of the list)
    //the bottom edge of the list must be from the end of the content to trigger the onEndReached callback
    onEndReachedThresholdRelative: PropTypes.number,
    //Deprecated. Please use onVisibleIndicesChanged instead.
    onVisibleIndexesChanged: PropTypes.func,
    //Provides visible index, helpful in sending impression events etc, onVisibleIndicesChanged(all, now, notNow)
    onVisibleIndicesChanged: PropTypes.func,
    //Provide this method if you want to render a footer. Helpful in showing a loader while doing incremental loads.
    renderFooter: PropTypes.func,
    //Specify the initial item index you want rendering to start from. Preferred over initialOffset if both are specified.
    initialRenderIndex: PropTypes.number,
    //Specify the estimated size of the recyclerlistview to render the list items in the first pass. If provided, recyclerlistview will
    //use these dimensions to fill in the items in the first render. If not provided, recyclerlistview will first render with no items
    //and then fill in the items based on the size given by its onLayout event. canChangeSize can be set to true to relayout items when
    //the size changes.
    layoutSize: PropTypes.object,
    //iOS only. Scroll throttle duration.
    scrollThrottle: PropTypes.number,
    //Specify if size can change, listview will automatically relayout items. For web, works only with useWindowScroll = true
    canChangeSize: PropTypes.bool,
    //Web only. Layout elements in window instead of a scrollable div.
    useWindowScroll: PropTypes.bool,
    //Turns off recycling. You still get progressive rendering and all other features. Good for lazy rendering. This should not be used in most cases.
    disableRecycling: PropTypes.bool,
    //Default is false, if enabled dimensions provided in layout provider will not be strictly enforced.
    //Rendered dimensions will be used to relayout items. Slower if enabled.
    forceNonDeterministicRendering: PropTypes.bool,
    //In some cases the data passed at row level may not contain all the info that the item depends upon, you can keep all other info
    //outside and pass it down via this prop. Changing this object will cause everything to re-render. Make sure you don't change
    //it often to ensure performance. Re-renders are heavy.
    extendedState: PropTypes.object,
    //Enables animating RecyclerListView item cells e.g, shift, add, remove etc. This prop can be used to pass an external item animation implementation.
    //Look into BaseItemAnimator/DefaultJSItemAnimator/DefaultNativeItemAnimator/DefaultWebItemAnimator for more info.
    //By default there are few animations, to disable completely simply pass blank new BaseItemAnimator() object. Remember, create
    //one object and keep it do not create multiple object of type BaseItemAnimator.
    //Note: You might want to look into DefaultNativeItemAnimator to check an implementation based on LayoutAnimation. By default,
    //animations are JS driven to avoid workflow interference. Also, please note LayoutAnimation is buggy on Android.
    itemAnimator: PropTypes.instanceOf(ItemAnimator_1.BaseItemAnimator),
    //All of the Recyclerlistview item cells are enclosed inside this item container. The idea is pass a native UI component which implements a
    //view shifting algorithm to remove the overlaps between the neighbouring views. This is achieved by shifting them by the appropriate
    //amount in the correct direction if the estimated sizes of the item cells are not accurate. If this props is passed, it will be used to
    //enclose the list items and otherwise a default react native View will be used for the same.
    renderContentContainer: PropTypes.func,
    //This container is for wrapping individual cells that are being rendered by recyclerlistview unlike contentContainer which wraps all of them.
    renderItemContainer: PropTypes.func,
    //Deprecated in favour of `prepareForLayoutAnimationRender` method
    optimizeForInsertDeleteAnimations: PropTypes.bool,
    //To pass down style to inner ScrollView
    style: PropTypes.oneOfType([
        PropTypes.object,
        PropTypes.number,
    ]),
    //For TS use case, not necessary with JS use.
    //For all props that need to be proxied to inner/external scrollview. Put them in an object and they'll be spread
    //and passed down.
    scrollViewProps: PropTypes.object,
    // Used when the logical offsetY differs from actual offsetY of recyclerlistview, could be because some other component is overlaying the recyclerlistview.
    // For e.x. toolbar within CoordinatorLayout are overlapping the recyclerlistview.
    // This method exposes the windowCorrection object of RecyclerListView, user can modify the values in realtime.
    applyWindowCorrection: PropTypes.func,
    // This can be used to hook an itemLayoutListener to listen to which item at what index is layout.
    // To get the layout params of the item, you can use the ref to call method getLayout(index), e.x. : `this._recyclerRef.getLayout(index)`
    // but there is a catch here, since there might be a pending relayout due to which the queried layout might not be precise.
    // Caution: RLV only listens to layout changes if forceNonDeterministicRendering is true
    onItemLayout: PropTypes.func,
    //Used to specify is window correction config and whether it should be applied to some scroll events
    windowCorrectionConfig: PropTypes.object,
};
//# sourceMappingURL=RecyclerListView.js.map