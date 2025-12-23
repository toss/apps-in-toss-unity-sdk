import { isAndroid } from './isAndroid.mjs';
import { isIOS } from './isIOS.mjs';
import { isServer } from './isServer.mjs';

/** @tossdocs-ignore */
function getOSByUserAgent() {
  if (isServer()) {
    return false;
  }

  if (isIOS()) {
    return 'ios';
  }

  if (isAndroid()) {
    return 'android';
  }

  return 'web';
}

export { getOSByUserAgent };
