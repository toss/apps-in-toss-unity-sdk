import { isServer } from './isServer.mjs';

/** @tossdocs-ignore */
function isMacOS() {
  if (isServer()) {
    return false;
  }

  return navigator.platform.match(/Macintosh|MacIntel|MacPPC|Mac68K/) !== null;
}

export { isMacOS };
