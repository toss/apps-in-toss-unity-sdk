/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function minBy(collection, iteratee) {
  if (collection.length === 0) {
    return undefined;
  }

  return collection.reduce(function (a, b) {
    return iteratee(a) <= iteratee(b) ? a : b;
  }, {});
}

export { minBy };
