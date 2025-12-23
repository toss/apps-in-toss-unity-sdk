import { isServer } from './isServer.mjs';

/** @tossdocs-ignore */
function isIE() {
  if (isServer()) {
    return false;
  }

  return /MSIE|Trident/i.test(window.navigator.userAgent);
}

export { isIE };
