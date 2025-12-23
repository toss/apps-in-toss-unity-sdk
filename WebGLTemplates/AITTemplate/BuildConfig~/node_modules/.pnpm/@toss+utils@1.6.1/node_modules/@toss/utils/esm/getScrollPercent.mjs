import { getScrollYOffset } from './getScrollYOffset.mjs';
import { isServer } from './device/isServer.mjs';

/** @tossdocs-ignore */
function getScrollPercent() {
  if (isServer() || !document.documentElement) {
    return 0;
  }

  var doc = document.documentElement;
  var body = document.body;
  var scrollTop = getScrollYOffset();
  var scrollHeight = (doc.scrollHeight || body.scrollHeight) - doc.clientHeight;
  return scrollTop / scrollHeight * 100;
}

export { getScrollPercent };
