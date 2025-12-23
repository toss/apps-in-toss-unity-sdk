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
var React = require("react");
//Interim solve given we want to be active on old react as well for now.
var ComponentCompat = /** @class */ (function (_super) {
    __extends(ComponentCompat, _super);
    function ComponentCompat(props, context) {
        var _this = _super.call(this, props, context) || this;
        _this._hasRenderedOnce = false;
        _this._didPropsChange = false;
        return _this;
    }
    ComponentCompat.prototype.shouldComponentUpdate = function (newProps, newState) {
        if (this.props !== newProps) {
            this.componentWillReceivePropsCompat(newProps);
        }
        return true;
    };
    //setState inside will not update the existing cycle, not a true replacement for componentWillReceiveProps
    ComponentCompat.prototype.componentWillReceivePropsCompat = function (newProps) {
        //no op
    };
    ComponentCompat.prototype.componentWillMountCompat = function () {
        //no op
    };
    ComponentCompat.prototype.componentWillUpdateCompat = function () {
        //no op
    };
    ComponentCompat.prototype.render = function () {
        if (!this._hasRenderedOnce) {
            this._hasRenderedOnce = true;
            this.componentWillMountCompat();
        }
        else {
            this.componentWillUpdateCompat();
        }
        return this.renderCompat();
    };
    return ComponentCompat;
}(React.Component));
exports.ComponentCompat = ComponentCompat;
//# sourceMappingURL=ComponentCompat.js.map