import { isServer } from './device/isServer.mjs';

/** @tossdocs-ignore */
function getViewportSize() {
  if (isServer()) {
    return {
      width: 0,
      height: 0
    };
  }

  return {
    width: window.innerWidth,
    height: window.innerHeight
  };
}

export { getViewportSize };
