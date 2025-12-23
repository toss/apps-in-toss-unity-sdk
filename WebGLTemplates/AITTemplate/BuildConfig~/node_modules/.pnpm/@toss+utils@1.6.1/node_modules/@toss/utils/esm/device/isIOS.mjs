import { isServer } from './isServer.mjs';

/** @tossdocs-ignore */
function isIOS() {
  if (isServer()) {
    return false;
  }

  return navigator.userAgent.match(/ipad|iphone/i) !== null;
}

export { isIOS };
