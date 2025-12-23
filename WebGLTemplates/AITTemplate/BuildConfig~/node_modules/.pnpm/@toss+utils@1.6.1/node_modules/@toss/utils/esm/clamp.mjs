/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function clamp(value, bound1, bound2) {
  if (bound2 == null) {
    return Math.min(value, bound1);
  }

  if (bound2 < bound1) {
    throw new Error('The value of bound2 must be a number greater than bound1.');
  }

  return Math.min(Math.max(value, bound1), bound2);
}

export { clamp };
