import { getScrollYOffset } from './getScrollYOffset.mjs';
import { isServer } from './device/isServer.mjs';

/** @tossdocs-ignore */
function getScrollDiffFromBottom() {
  if (isServer() || !document.documentElement) {
    return 0;
  }

  var doc = document.documentElement;
  var body = document.body;
  var scrollTop = getScrollYOffset();
  var scrollBottom = scrollTop + doc.clientHeight;
  var scrollHeight = doc.scrollHeight || body.scrollHeight;
  return scrollHeight - scrollBottom;
}

export { getScrollDiffFromBottom };
