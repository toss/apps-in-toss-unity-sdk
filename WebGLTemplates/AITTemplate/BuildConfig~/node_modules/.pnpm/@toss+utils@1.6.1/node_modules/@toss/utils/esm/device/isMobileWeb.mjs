import { getOSByUserAgent } from './getOSByUserAgent.mjs';

/** @tossdocs-ignore */
function isMobileWeb() {
  var userAgent = getOSByUserAgent();

  if (userAgent === 'ios' || userAgent === 'android') {
    return true;
  }

  return false;
}

export { isMobileWeb };
