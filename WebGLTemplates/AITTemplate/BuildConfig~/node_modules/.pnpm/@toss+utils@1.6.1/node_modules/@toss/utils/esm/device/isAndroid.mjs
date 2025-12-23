import { isServer } from './isServer.mjs';

/** @tossdocs-ignore */
function isAndroid() {
  if (isServer()) {
    return false;
  }

  return navigator.userAgent.match(/Android/i) !== null;
}

export { isAndroid };
