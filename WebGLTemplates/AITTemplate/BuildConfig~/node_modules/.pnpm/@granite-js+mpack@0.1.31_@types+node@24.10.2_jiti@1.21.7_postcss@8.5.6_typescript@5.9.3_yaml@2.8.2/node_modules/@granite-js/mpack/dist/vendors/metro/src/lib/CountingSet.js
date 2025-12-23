"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var CountingSet_exports = {};
__export(CountingSet_exports, {
  default: () => CountingSet
});
module.exports = __toCommonJS(CountingSet_exports);
class CountingSet {
  #map = /* @__PURE__ */ new Map();
  constructor(items) {
    if (items) {
      if (items instanceof CountingSet) {
        this.#map = new Map(items.#map);
      } else {
        for (const item of items) {
          this.add(item);
        }
      }
    }
  }
  has(item) {
    return this.#map.has(item);
  }
  add(item) {
    const newCount = this.count(item) + 1;
    this.#map.set(item, newCount);
  }
  delete(item) {
    const newCount = this.count(item) - 1;
    if (newCount <= 0) {
      this.#map.delete(item);
    } else {
      this.#map.set(item, newCount);
    }
  }
  keys() {
    return this.#map.keys();
  }
  values() {
    return this.#map.keys();
  }
  *entries() {
    for (const item of this) {
      yield [item, item];
    }
  }
  // Iterate over unique entries
  // $FlowIssue[unsupported-syntax]
  [Symbol.iterator]() {
    return this.values();
  }
  /*::
  // For Flow's benefit
  @@iterator(): Iterator<T> {
    return this.values();
  }
  */
  // Number of unique entries
  // $FlowIssue[unsafe-getters-setters]
  get size() {
    return this.#map.size;
  }
  count(item) {
    return this.#map.get(item) ?? 0;
  }
  clear() {
    this.#map.clear();
  }
  forEach(callbackFn, thisArg) {
    for (const item of this) {
      callbackFn.call(thisArg, item, item, this);
    }
  }
  // For Jest purposes. Ideally a custom serializer would be enough, but in
  // practice there is hardcoded magic for Set in toEqual (etc) that we cannot
  // extend to custom collection classes. Instead let's assume values are
  // sortable ( = strings) and make this look like an array with some stable
  // order.
  toJSON() {
    return [...this].sort();
  }
}
