import { noop } from './noop.mjs';

/** @tossdocs-ignore */
var scrollRestoration = {
  set: function set(value) {
    if (!('scrollRestoration' in window.history)) {
      return noop;
    }

    var originalScrollRestoration = window.history.scrollRestoration;
    window.history.scrollRestoration = value;
    return function () {
      window.history.scrollRestoration = originalScrollRestoration;
    };
  }
};

export { scrollRestoration };
