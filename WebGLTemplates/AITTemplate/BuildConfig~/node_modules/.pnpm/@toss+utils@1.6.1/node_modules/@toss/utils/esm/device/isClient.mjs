import { isServer } from './isServer.mjs';

/** @tossdocs-ignore */
function isClient() {
  return !isServer();
}

export { isClient };
